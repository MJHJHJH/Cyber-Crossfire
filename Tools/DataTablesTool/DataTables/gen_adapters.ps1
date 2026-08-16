# Auto-generate Core/Adapters from Core/Json Tables + beans.
# Optional adapter_manifest.json: { "namespace": "GamePlay", "overrides": { "Shop": [ fields... ] } }
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir "..\..\..")).Path
$JsonCodeDir = Join-Path $ProjectRoot "Assets\DataTable\Core\Json"
$OutDir = Join-Path $ProjectRoot "Assets\DataTable\Core\Adapters"
$ManifestPath = Join-Path $ScriptDir "adapter_manifest.json"
$TablesCs = Join-Path $JsonCodeDir "Tables.cs"

if (-not (Test-Path $TablesCs)) {
    throw "Tables.cs not found. Run gen_json first: $TablesCs"
}

$ns = "GamePlay"
$overrides = @{}
if (Test-Path $ManifestPath) {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($manifest.namespace)) {
        $ns = $manifest.namespace
    }
    if ($manifest.overrides) {
        foreach ($prop in $manifest.overrides.PSObject.Properties) {
            $overrides[$prop.Name] = @($prop.Value)
        }
    }
    # Legacy: full tables array still supported as override source
    if ($manifest.tables) {
        foreach ($t in @($manifest.tables)) {
            if ($t.fields) { $overrides[$t.name] = @($t.fields) }
        }
    }
}

if (Test-Path $OutDir) {
    Get-ChildItem -LiteralPath $OutDir -Force | Where-Object { $_.Extension -in ".cs", ".meta" -or $_.PSIsContainer } | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $content, $utf8)
}

function Get-Camel([string]$name) {
    if ([string]::IsNullOrEmpty($name)) { return $name }
    return $name.Substring(0, 1).ToLowerInvariant() + $name.Substring(1)
}

function Test-PrimitiveOrCollection([string]$type) {
    $t = $type.Trim()
    $primitives = @(
        "int", "long", "float", "double", "bool", "string", "short", "byte",
        "uint", "ulong", "ushort", "sbyte", "decimal", "char"
    )
    if ($primitives -contains $t) { return $true }
    if ($t.StartsWith("System.Collections.Generic.")) { return $true }
    return $false
}

function Convert-RecordType([string]$type) {
    $t = $type.Trim()
    if ($t -match '^System\.Collections\.Generic\.List<(.+)>$') {
        return "System.Collections.Generic.IReadOnlyList<$($Matches[1])>"
    }
    return $t
}

function Find-BeanFile([string]$typeName) {
    # ShopDef.MyItem -> ShopDef\MyItem.cs ; Activity -> Activity.cs
    $rel = ($typeName -replace '\.', '\') + ".cs"
    $direct = Join-Path $JsonCodeDir $rel
    if (Test-Path $direct) { return $direct }

    $simple = ($typeName.Split('.') | Select-Object -Last 1) + ".cs"
    $found = Get-ChildItem -LiteralPath $JsonCodeDir -Recurse -Filter $simple -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $simple } |
        Select-Object -First 1
    if ($found) { return $found.FullName }
    return $null
}

function Get-BeanFields([string]$beanType) {
    $file = Find-BeanFile $beanType
    if (-not $file) {
        throw "Bean file not found for type '$beanType' under $JsonCodeDir"
    }

    $text = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $fields = New-Object System.Collections.Generic.List[object]
    $rxMatches = [regex]::Matches($text, 'public\s+readonly\s+(?<type>\S+)\s+(?<name>\w+)\s*;')
    foreach ($m in $rxMatches) {
        $type = $m.Groups["type"].Value.Trim()
        $name = $m.Groups["name"].Value
        if ($name -eq "__ID__") { continue }
        [void]$fields.Add([pscustomobject]@{ type = $type; name = $name })
    }
    return $fields
}

function Expand-RecordFields([string]$rowType) {
    $result = New-Object System.Collections.Generic.List[object]
    foreach ($f in (Get-BeanFields $rowType)) {
        if (Test-PrimitiveOrCollection $f.type) {
            [void]$result.Add([pscustomobject]@{
                    name = $f.name
                    type = (Convert-RecordType $f.type)
                    expr = "row.$($f.name)"
                })
            continue
        }

        # Nested bean: flatten one level
        $nestedFields = Get-BeanFields $f.type
        if ($nestedFields.Count -eq 0) {
            throw "Nested bean '$($f.type)' has no fields (parent field $($f.name))"
        }
        foreach ($nf in $nestedFields) {
            if (-not (Test-PrimitiveOrCollection $nf.type)) {
                throw "Only one-level nested beans supported: $($f.type).$($nf.name) is complex"
            }
            $recordName = if ($nf.name.StartsWith($f.name)) { $nf.name } else { $f.name + $nf.name }
            [void]$result.Add([pscustomobject]@{
                    name = $recordName
                    type = (Convert-RecordType $nf.type)
                    expr = "row.$($f.name).$($nf.name)"
                })
        }
    }
    return $result
}

function Get-TablesFromGenerated {
    $text = Get-Content -LiteralPath $TablesCs -Raw -Encoding UTF8
    $list = New-Object System.Collections.Generic.List[object]
    $rxMatches = [regex]::Matches($text, '(?<prop>\w+)\s*=\s*new\s+(?<tb>Tb\w+)\s*\(\s*loader\s*\(\s*"(?<loc>[^"]+)"\s*\)\s*\)')
    foreach ($m in $rxMatches) {
        $cfgTable = $m.Groups["tb"].Value
        $location = $m.Groups["loc"].Value
        $name = if ($cfgTable.StartsWith("Tb")) { $cfgTable.Substring(2) } else { $cfgTable }

        $tbFile = Join-Path $JsonCodeDir ($cfgTable + ".cs")
        if (-not (Test-Path $tbFile)) {
            throw "Missing table class file: $tbFile"
        }
        $tbText = Get-Content -LiteralPath $tbFile -Raw -Encoding UTF8
        $rowMatch = [regex]::Match($tbText, 'Dictionary\s*<\s*\w+\s*,\s*(?<row>\w+)\s*>')
        if (-not $rowMatch.Success) {
            $rowMatch = [regex]::Match($tbText, 'List\s*<\s*(?<row>\w+)\s*>')
        }
        if (-not $rowMatch.Success) {
            throw "Cannot detect row type in $tbFile"
        }
        $cfgRow = $rowMatch.Groups["row"].Value

        if ($overrides.ContainsKey($name)) {
            $fields = New-Object System.Collections.Generic.List[object]
            foreach ($of in @($overrides[$name])) { [void]$fields.Add($of) }
        } else {
            $fields = Expand-RecordFields $cfgRow
        }

        if ($null -eq $fields -or $fields.Count -eq 0) {
            throw "No fields for table $name ($cfgRow)"
        }

        [void]$list.Add([pscustomobject]@{
                name     = $name
                location = $location
                cfgTable = $cfgTable
                cfgRow   = $cfgRow
                fields   = $fields
            })
    }

    if ($list.Count -eq 0) {
        throw "No tables parsed from $TablesCs"
    }
    return $list
}

function New-Contract([object]$table) {
    $recordName = "$($table.name)_Record"
    $interfaceName = "I$($table.name)"
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("// <auto-generated by gen_adapters.ps1>")
    [void]$sb.AppendLine("using System.Collections.Generic;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    public readonly struct $recordName")
    [void]$sb.AppendLine("    {")
    $params = (@($table.fields) | ForEach-Object { "$($_.type) $(Get-Camel $_.name)" }) -join ", "
    [void]$sb.AppendLine("        public $recordName($params)")
    [void]$sb.AppendLine("        {")
    foreach ($f in @($table.fields)) {
        [void]$sb.AppendLine("            $($f.name) = $(Get-Camel $f.name);")
    }
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    foreach ($f in @($table.fields)) {
        [void]$sb.AppendLine("        public $($f.type) $($f.name) { get; }")
    }
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("    public interface $interfaceName")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        IReadOnlyList<$recordName> All { get; }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        int Count { get; }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        $recordName Get(int id);")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        bool TryGet(int id, out $recordName record);")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")
    Write-Utf8NoBom (Join-Path $OutDir "$interfaceName.cs") $sb.ToString()
    Write-Host "  wrote $interfaceName.cs"
}

function New-Access([object]$table) {
    $recordName = "$($table.name)_Record"
    $interfaceName = "I$($table.name)"
    $accessName = "$($table.name)TableAccess"
    $cfgTable = $table.cfgTable
    $cfgRow = $table.cfgRow
    $exprs = (@($table.fields) | ForEach-Object { $_.expr }) -join ", "

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("// <auto-generated by gen_adapters.ps1>")
    [void]$sb.AppendLine("using System.Collections.Generic;")
    [void]$sb.AppendLine("using GameFramework;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    public sealed class $accessName : $interfaceName")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        private readonly cfg.json.$cfgTable _json;")
    [void]$sb.AppendLine("        private readonly cfg.bin.$cfgTable _bin;")
    [void]$sb.AppendLine("        private readonly bool _jsonMode;")
    [void]$sb.AppendLine("        private readonly List<$recordName> _all;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        private $accessName(cfg.json.$cfgTable json)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            _jsonMode = true;")
    [void]$sb.AppendLine("            _json = json;")
    [void]$sb.AppendLine("            _all = new List<$recordName>(json.DataList.Count);")
    [void]$sb.AppendLine("            foreach (cfg.json.$cfgRow row in json.DataList)")
    [void]$sb.AppendLine("                _all.Add(ToRecord(row));")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        private $accessName(cfg.bin.$cfgTable bin)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            _jsonMode = false;")
    [void]$sb.AppendLine("            _bin = bin;")
    [void]$sb.AppendLine("            _all = new List<$recordName>(bin.DataList.Count);")
    [void]$sb.AppendLine("            foreach (cfg.bin.$cfgRow row in bin.DataList)")
    [void]$sb.AppendLine("                _all.Add(ToRecord(row));")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public IReadOnlyList<$recordName> All => _all;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public int Count => _all.Count;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public static $accessName From(cfg.json.$cfgTable table)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            if (table == null)")
    [void]$sb.AppendLine("                throw new GameFrameworkException(`"$cfgTable is invalid.`");")
    [void]$sb.AppendLine("            return new $accessName(table);")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public static $accessName From(cfg.bin.$cfgTable table)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            if (table == null)")
    [void]$sb.AppendLine("                throw new GameFrameworkException(`"$cfgTable is invalid.`");")
    [void]$sb.AppendLine("            return new $accessName(table);")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public $recordName Get(int id)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            if (_jsonMode)")
    [void]$sb.AppendLine("                return ToRecord(_json.Get(id));")
    [void]$sb.AppendLine("            return ToRecord(_bin.Get(id));")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public bool TryGet(int id, out $recordName record)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            if (_jsonMode)")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                if (!_json.DataMap.TryGetValue(id, out cfg.json.$cfgRow row) || row == null)")
    [void]$sb.AppendLine("                {")
    [void]$sb.AppendLine("                    record = default;")
    [void]$sb.AppendLine("                    return false;")
    [void]$sb.AppendLine("                }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("                record = ToRecord(row);")
    [void]$sb.AppendLine("                return true;")
    [void]$sb.AppendLine("            }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("            if (!_bin.DataMap.TryGetValue(id, out cfg.bin.$cfgRow rowBin) || rowBin == null)")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                record = default;")
    [void]$sb.AppendLine("                return false;")
    [void]$sb.AppendLine("            }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("            record = ToRecord(rowBin);")
    [void]$sb.AppendLine("            return true;")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        private static $recordName ToRecord(cfg.json.$cfgRow row)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            return new $recordName($exprs);")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        private static $recordName ToRecord(cfg.bin.$cfgRow row)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            return new $recordName($exprs);")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")

    Write-Utf8NoBom (Join-Path $OutDir "$accessName.cs") $sb.ToString()
    Write-Host "  wrote $accessName.cs"
}

function New-Factory([object[]]$tables) {
    $locations = ($tables | ForEach-Object { "`"$($_.location)`"" }) -join ", "
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("// <auto-generated by gen_adapters.ps1>")
    [void]$sb.AppendLine("using System.Collections.Generic;")
    [void]$sb.AppendLine("using System.Threading;")
    [void]$sb.AppendLine("using Cysharp.Threading.Tasks;")
    [void]$sb.AppendLine("using GameFramework;")
    [void]$sb.AppendLine("using Luban;")
    [void]$sb.AppendLine("using Luban.SimpleJSON;")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("namespace $ns")
    [void]$sb.AppendLine("{")
    [void]$sb.AppendLine("    public sealed class LubanTablesFactory : ILubanTablesFactory")
    [void]$sb.AppendLine("    {")
    [void]$sb.AppendLine("        private static readonly string[] TABLE_LOCATIONS = { $locations };")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("        public async UniTask<LubanTableRegistry> CreateAsync(")
    [void]$sb.AppendLine("            LubanDataMode mode,")
    [void]$sb.AppendLine("            ILubanDataLoader loader,")
    [void]$sb.AppendLine("            CancellationToken cancellationToken = default)")
    [void]$sb.AppendLine("        {")
    [void]$sb.AppendLine("            if (loader == null)")
    [void]$sb.AppendLine("                throw new GameFrameworkException(`"Luban data loader is invalid.`");")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("            var registry = new LubanTableRegistry();")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("            if (mode == LubanDataMode.Json)")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                var cache = new Dictionary<string, string>(TABLE_LOCATIONS.Length);")
    [void]$sb.AppendLine("                foreach (string location in TABLE_LOCATIONS)")
    [void]$sb.AppendLine("                {")
    [void]$sb.AppendLine("                    cancellationToken.ThrowIfCancellationRequested();")
    [void]$sb.AppendLine("                    cache[location] = await loader.LoadTextAsync(location, cancellationToken);")
    [void]$sb.AppendLine("                }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("                var tables = new cfg.json.Tables(name => JSON.Parse(cache[name]));")
    foreach ($t in $tables) {
        [void]$sb.AppendLine("                registry.Register<I$($t.name)>($($t.name)TableAccess.From(tables.$($t.cfgTable)));")
    }
    [void]$sb.AppendLine("                return registry;")
    [void]$sb.AppendLine("            }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("            if (mode == LubanDataMode.Binary)")
    [void]$sb.AppendLine("            {")
    [void]$sb.AppendLine("                var cache = new Dictionary<string, byte[]>(TABLE_LOCATIONS.Length);")
    [void]$sb.AppendLine("                foreach (string location in TABLE_LOCATIONS)")
    [void]$sb.AppendLine("                {")
    [void]$sb.AppendLine("                    cancellationToken.ThrowIfCancellationRequested();")
    [void]$sb.AppendLine("                    cache[location] = await loader.LoadBytesAsync(location, cancellationToken);")
    [void]$sb.AppendLine("                }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("                var tables = new cfg.bin.Tables(name => new ByteBuf(cache[name]));")
    foreach ($t in $tables) {
        [void]$sb.AppendLine("                registry.Register<I$($t.name)>($($t.name)TableAccess.From(tables.$($t.cfgTable)));")
    }
    [void]$sb.AppendLine("                return registry;")
    [void]$sb.AppendLine("            }")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("            throw new GameFrameworkException(Utility.Text.Format(`"Unsupported LubanDataMode: {0}`", mode));")
    [void]$sb.AppendLine("        }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")

    Write-Utf8NoBom (Join-Path $OutDir "LubanTablesFactory.cs") $sb.ToString()
    Write-Host "  wrote LubanTablesFactory.cs"
}

Write-Host "[gen_adapters] scanning $TablesCs"
$tableList = Get-TablesFromGenerated
Write-Host "[gen_adapters] tables=$($tableList.Count) -> $($tableList.name -join ', ')"
foreach ($table in $tableList) {
    New-Contract $table
    New-Access $table
}
New-Factory $tableList
Write-Host "[gen_adapters] done."
