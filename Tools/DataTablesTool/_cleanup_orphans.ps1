$ErrorActionPreference = "Continue"
$base = "E:\Unity\ProjectGroup\AltasTest\Assets\GamePlay\DataTable\Generated"
$out = "E:\Unity\ProjectGroup\AltasTest\Tools\DataTablesTool\_cleanup_report.txt"
$lines = New-Object System.Collections.Generic.List[string]

function Remove-OrphanMetas([string]$root) {
  if (-not (Test-Path $root)) { $lines.Add("skip missing $root"); return }
  Get-ChildItem $root -Recurse -Filter *.meta -Force | ForEach-Object {
    $asset = $_.FullName.Substring(0, $_.FullName.Length - 5)
    if (-not (Test-Path -LiteralPath $asset)) {
      $lines.Add("DELETE orphan: $($_.FullName)")
      Remove-Item -LiteralPath $_.FullName -Force
    }
  }
}

# remove leftover dirs that should not exist
foreach ($rel in @("Json\Shop","Json\demo","Bin\Shop","Bin\demo")) {
  $p = Join-Path $base $rel
  if (Test-Path $p) {
    $lines.Add("REMOVE DIR: $p")
    Remove-Item -LiteralPath $p -Recurse -Force
  }
}

Remove-OrphanMetas (Join-Path $base "Json")
Remove-OrphanMetas (Join-Path $base "Bin")
Remove-OrphanMetas "E:\Unity\ProjectGroup\AltasTest\Assets\DataTable\Data\Json"
Remove-OrphanMetas "E:\Unity\ProjectGroup\AltasTest\Assets\DataTable\Data\Bin"

$lines.Add("--- remaining Generated tree ---")
Get-ChildItem $base -Recurse -Force | ForEach-Object {
  $lines.Add($_.FullName.Substring($base.Length + 1))
}

$lines | Set-Content -Path $out -Encoding UTF8
Write-Output "wrote $out count=$($lines.Count)"
