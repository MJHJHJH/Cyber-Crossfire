# ZString 2.6.0 使用指南与技术原理梳理

> 适用项目：CommandoRobot（Unity 2022.3.37f1c1，netstandard2.1 API 兼容级别）
> 库来源：[Cysharp/ZString](https://github.com/Cysharp/ZString)（Zero Allocation StringBuilder for .NET and Unity）
> 安装方式：NuGet for Unity 4.5.0（见 `Assets/packages.config`，包体位于 `Assets/Packages/ZString.2.6.0/`，目标框架 `lib/netstandard2.1`）
> 参考：[官方 README](https://github.com/Cysharp/ZString/blob/master/README.md)、[DeepWiki 文档](https://deepwiki.com/Cysharp/ZString/1-overview)

---

## 一、ZString 是什么

ZString 是 Cysharp（UniTask、R3、ZLogger 同一作者）出品的**零分配字符串构建/格式化库**。它提供：

- `ZString.Format / Concat / Join` —— 替代 `string.Format` / `+` 拼接 / `string.Join`，**零装箱、零中间分配**；
- `Utf16ValueStringBuilder` —— 替代 `System.Text.StringBuilder` 的**值类型（struct）构建器**；
- `Utf8ValueStringBuilder` —— 直接产出 **UTF-8 字节**的构建器，适合网络协议、日志、序列化；
- `ZStringWriter` —— 基于 Utf16ValueStringBuilder 的 `TextWriter` 实现。

一句话定位：**凡是高频执行（每帧/每次事件/热路径）的字符串拼接与格式化，用它替代原生方案，可消除 GC 分配，避免移动端 GC spike 卡顿。**

---

## 二、项目中的引用方式（已就绪）

本项目通过 NuGet for Unity 安装，DLL 位于：

```
Assets/Packages/ZString.2.6.0/lib/netstandard2.1/ZString.dll
```

- 它是 **precompiled assembly**（Assets 下的工程 DLL），任何程序集（AOT 与 HybridCLR 热更程序集）的 C# 脚本中直接 `using Cysharp.Text;` 即可使用，**无需手动添加程序集引用**。
- 依赖 `System.Runtime.CompilerServices.Unsafe 6.0.0`（netstandard2.1 目标需要），已随 NuGet 自动安装于 `Assets/Packages/`。
- Unity 2021.2+ 支持 `Span`/`ReadOnlySpan`，本项目的 netstandard2.1 目标下 **ZString 全部 API（含 ReadOnlySpan 重载）均可用**。
- 与项目已有的 UniTask、R3（同为 Cysharp 出品）风格一致，无命名冲突。

---

## 三、快速上手

```csharp
using Cysharp.Text;

// 1) 单行拼接（替代 string.Concat / "+" 拼接）
string s1 = ZString.Concat("HP: ", 100, "/", 100);      // "HP: 100/100"

// 2) 格式化（替代 string.Format，参数不装箱）
string s2 = ZString.Format("player={0} level={1} exp={2:0.0}", "Bob", 12, 345.678f);

// 3) 连接集合（替代 string.Join）
string s3 = ZString.Join(", ", new[] { 1, 2, 3, 4 });   // "1, 2, 3, 4"

// 4) StringBuilder 模式（热路径首选，零分配）
using (var sb = ZString.CreateStringBuilder())          // 等价 new Utf16ValueStringBuilder()
{
    sb.Append("击杀 ");
    sb.Append(10);
    sb.Append(" 只怪物，获得 ");
    sb.Append(2500);
    sb.Append(" 金币");
    string result = sb.ToString();                       // 唯一一次分配：最终字符串
}
```

运行演示：挂载 `Assets/ZStringStart.cs` 到任意 GameObject，运行后在 Console 查看全部 API 的输出。

---

## 四、核心 API 详解

### 4.1 `ZString` 静态类（单行 API，免去 builder 管理）

| API | 说明 |
| --- | --- |
| `ZString.Format<T1..T16>(string 或 ReadOnlySpan<char>, args...)` | 复合格式串格式化，最多 16 个泛型参数，零装箱 |
| `ZString.Concat<T1..T16>(args...)` | 直接拼接 1~16 个任意类型值 |
| `ZString.Concat<T>(T[] / List<T> / ICollection<T> / IList<T> / IReadOnlyList<T> / IReadOnlyCollection<T> / IEnumerable<T> / ReadOnlySpan<T>)` | 集合拼接 |
| `ZString.Join<T>(char 或 string 分隔符, T[] / List<T> / ReadOnlySpan<T> / IEnumerable<T>)` | 集合连接，另有 `string[]` / `ReadOnlySpan<string>` 重载 |
| `ZString.CreateStringBuilder()` / `CreateStringBuilder(bool notNested)` | 创建 `Utf16ValueStringBuilder`（ArrayPool 模式，需 Dispose） |
| `ZString.CreateUtf8StringBuilder()` / `CreateUtf8StringBuilder(bool notNested)` | 创建 `Utf8ValueStringBuilder` |
| `ZString.PrepareUtf16<T1..T16>(string template)` | 预解析 UTF-16 格式模板，返回 `PreparedFormat<T...>` |
| `ZString.PrepareUtf8<T1..T16>(string template)` | 预解析 UTF-8 格式模板 |
| `ZString.Utf8Format<T1..T16>(IBufferWriter<byte>, string template, args...)` | 直接把格式化结果写入 `IBufferWriter<byte>`（如 PipeWriter / ArrayBufferWriter） |

### 4.2 `Utf16ValueStringBuilder`（值类型，替代 StringBuilder）

构造方式：

```csharp
var sb = new Utf16ValueStringBuilder();                 // ArrayPool 模式（默认）：池化租借，可嵌套、可持有，Dispose 归还
var sb2 = new Utf16ValueStringBuilder(disposeImmediately: true); // ThreadStatic 模式：零分配极速，禁止嵌套，用完立即 Dispose
var sb3 = ZString.CreateStringBuilder();                // 等价于默认构造（ArrayPool 模式）
var sb4 = ZString.CreateStringBuilder(notNested: true); // 等价于 disposeImmediately: true（ThreadStatic 模式）
```

> 注意语义与直觉相反：`disposeImmediately: true` / `notNested: true` 走 **thread-static**（更快但禁止嵌套、必须立即释放）；默认构造走 **ArrayPool** 租借（安全可嵌套）。该结论来自对 DLL 的反编译，见"五、技术原理"。

常用成员（与 `StringBuilder` 对齐，可直接迁移）：

- `Append(...)`：所有数字类型（int/long/float/double/decimal/uint...）、`DateTime`/`DateTimeOffset`/`TimeSpan`/`Guid`/`bool`/`char`/`string`/`ReadOnlySpan<char>`，以及带格式串的重载 `Append(int, "X2")` 等；`Append<T>(T)` 泛型版本（自定义类型也可零装箱接入）
- `AppendLine()` / `AppendLine(值)`：换行追加
- `AppendFormat<T1..T16>(模板, args...)`：追加格式化内容
- `AppendJoin(char/string, T[] / IEnumerable<T>)`：追加连接集合
- `Insert` / `Remove` / `Replace(char,char)` / `Replace(string,string)` / `ReplaceAt`
- `Length`（可读写）、`AsSpan()` / `AsMemory()` / `AsArraySegment()`（零拷贝视图，`AsArraySegment().Array` 可取底层数组）、`ToString()`、`Dispose()`、`Clear()`

### 4.3 `Utf8ValueStringBuilder`（直接产出 UTF-8 字节）

```csharp
using (var sb = ZString.CreateUtf8StringBuilder())
{
    sb.Append("msg:");
    sb.Append(42);                       // 数字直接编码为 UTF-8 字节，无中间 string
    ReadOnlySpan<byte> bytes = sb.AsSpan(); // 零拷贝读取
    // 发送/写入：socket、Pipe、ArrayBufferWriter 等
}
```

- 格式化重载使用 `System.Buffers.StandardFormat`（如 `sb.Append(3.14f, StandardFormat.Parse("0.00"))`）；
- 适合**网络协议拼包、日志输出、二进制序列化**场景——省去「字符串 → UTF-16 → UTF-8 编码」的中间分配与编码开销；
- `ToString()` 会把 UTF-8 解码为 string（仅最终一次分配）。

### 4.4 `PreparedFormat<T...>`（模板预解析）

格式串只解析一次，之后重复使用：

```csharp
var template = ZString.PrepareUtf16<int, string>("id={0} name={1}");
string a = template.Format(7, "robot");   // "id=7 name=robot"
string b = template.Format(8, "turret");  // "id=8 name=turret"
```

适合**日志模板、协议头、排行榜文本**等高频、同模板不同参数的场景（也常用于 ZLogger 的日志模板缓存）。

### 4.5 `ZStringWriter`（TextWriter 适配）

需要 `TextWriter` 参数的 API（JSON 序列化、旧式日志组件等）可用它承接：

```csharp
using (var writer = new ZStringWriter())   // 内部基于 Utf16ValueStringBuilder
{
    writer.Write("level=");
    writer.Write(99);
    string text = writer.ToString();
}
```

### 4.6 与 TextMeshPro 配合：完全 0 GC 更新（推荐做法）

TMP 的 `text = ...` 走 string（至少 1 次分配），`SetText(string, float...)` 只支持 float 参数且格式串需为字面量。要完全 0 GC，走「ZString builder → `AsArraySegment()` 零拷贝视图 → TMP `SetCharArray` 逐字符拷入内部缓冲」链路（完整示例见 `Assets/ZStringStart.cs` 第 9 步）：

```csharp
void UpdateTmpLabelZeroGc(TMP_Text label, int hp, int maxHp)
{
    using (var sb = ZString.CreateStringBuilder(notNested: true)) // thread-static：0 分配，禁止嵌套
    {
        sb.Append("HP: ");
        sb.Append(hp);
        sb.Append('/');
        sb.Append(maxHp);

        var seg = sb.AsArraySegment();               // 零拷贝：Array=底层缓冲, Count=已写长度
        label.SetCharArray(seg.Array, 0, seg.Count); // TMP 逐字符拷贝进内部缓冲，无 string 分配
    } // Dispose 释放线程占用标记（缓冲保留在线程内复用）
}
```

- 原理（TMP 3.x 源码实证）：`SetCharArray(char[], start, length)` → `PopulateTextBackingArray(char[])` **逐字符拷贝**到 `m_TextBackingArray`，不产生 string；生成 Inspector 显示字符串的代码在 `#if UNITY_EDITOR` 内，**正式构建无此开销**；
- 成本：thread-static 缓冲线程内首次分配一次（约 62KB char[]，之后复用）；TMP 内部缓冲首次按文本长度 `Resize` 一次（之后复用）—— **稳定运行后每帧 0 分配**，适合 HUD、飘字、计分等每帧更新；
- 注意：`AsArraySegment()` 视图在 `Dispose()` 后失效，务必在 `SetCharArray` 同步拷贝完成后（即 using 块内）再释放；
- 注意：TMP 会把 `<...>` 解析为富文本标签，纯文本内容无影响；builder 内不能使用 thread-static 嵌套（见第八节）。

---

## 五、技术原理（基于 ZString.dll 反编译实证）

> 本节所有结论均来自对项目内 `Assets/Packages/ZString.2.6.0/lib/netstandard2.1/ZString.dll` 的 IL 反编译（ildasm）验证，非道听途说。

### 1. 核心数据结构：一个只有 3 个字段的 struct

`Utf16ValueStringBuilder` 实例字段（反编译确认）：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `buffer` | `char[]` | 字符缓冲（thread-static 共享数组 或 从 ArrayPool 租借的数组） |
| `index` | `int` | 已写入长度（`Length` 直接读它） |
| `disposeImmediately` | `bool` | 模式标记 |

静态字段：

- `[ThreadStatic] char[] scratchBuffer` —— 线程私有缓冲，长度常量 `ThreadStaticBufferSize = 0x7987`（**31111 字符**）
- `[ThreadStatic] bool scratchBufferUsed` —— 线程占用标记（嵌套检测的依据）
- `DefaultBufferSize = 0x8000`（**32768 字符**，ArrayPool 默认租借大小）
- `newLine1 / newLine2 / crlf` —— 换行符配置（静态，不进实例）

struct 整体约 16~24 字节，在栈上/寄存器中使用，**无对象头、无 GC 追踪**。对比 `System.Text.StringBuilder` 是 class：有对象头+方法表，`new` 一次分配一个对象，内部还有自己的 `char[]`。

### 2. 两种缓冲模式（构造函数 IL 实证）

`ctor(bool disposeImmediately)` 反编译后的逻辑（伪码）：

```csharp
if (disposeImmediately)                      // = true → thread-static 模式
{
    if (scratchBufferUsed)                   // 该线程已有 thread-static builder 在用 → 嵌套！
        ThrowNestedException();              // 抛 NestedStringBuilderCreationException（2.6.0 新增）
    buffer = scratchBuffer ??= new char[31111];  // 懒创建线程私有缓冲（仅首次有一次性分配）
    scratchBufferUsed = true;                // 占用标记
}
else                                         // = false（默认）→ ArrayPool 模式
{
    buffer = ArrayPool<char>.Shared.Rent(32768);
}
index = 0; disposeImmediately = 参数;
```

两种模式对比：

| | ThreadStatic（`disposeImmediately: true` / `notNested: true`） | ArrayPool（默认构造 / `CreateStringBuilder()`） |
| --- | --- | --- |
| 缓冲来源 | 线程私有静态数组（31111） | `ArrayPool<char>.Shared.Rent(32768)` |
| 分配 | 零分配（首用后线程内复用） | 池化复用（首次租借后基本无 GC 分配） |
| 嵌套使用 | **禁止**，嵌套抛 `NestedStringBuilderCreationException` | 允许（每次租独立数组） |
| 释放要求 | 用完立即 `Dispose()`（释放线程占用标记） | `Dispose()` 归还池数组 |
| 跨线程 | 绝对禁止（缓冲线程私有） | 不建议（builder 应局部使用） |

**两个关键事实（与常见直觉相反）**：
- `ZString.Format / Concat / Join` 内部使用 **thread-static** 模式（IL：`new Utf16ValueStringBuilder(disposeImmediately: true)`）—— 所以在这些调用期间再开 thread-static builder 会触发嵌套异常；
- `ZString.CreateStringBuilder()` 无参版本 = **ArrayPool** 模式；`CreateStringBuilder(notNested: true)` 才等于 thread-static 模式（参数直接透传给构造函数）。

### 3. Dispose：靠数组长度识别缓冲来源

`Dispose()` 反编译逻辑：

```csharp
if (buffer == null) return;                       // 幂等
if (buffer.Length != 31111)                       // 不是 thread-static 缓冲
    ArrayPool<char>.Shared.Return(buffer);        // → 归还池
buffer = null; index = 0;
if (disposeImmediately) scratchBufferUsed = false; // thread-static 模式释放线程占用
```

巧妙之处：**用数组长度判断缓冲归属**（thread-static 固定 31111，池租借默认 32768），因此对两种模式调用 `Dispose()` 都是安全的——thread-static 缓冲不会误归还 ArrayPool。

### 4. 扩容：翻倍 + 旧数组归还池

`TryGrow(sizeHint)`：`buffer.Length < index + sizeHint` 时调用 `Grow`。
`Grow(sizeHint)`：新大小 = `max(旧长 × 2, index + sizeHint)` → `ArrayPool.Rent(新大小)` → 拷贝旧内容 → **旧数组若不是 thread-static 则归还池** → 替换 `buffer`。

对比 StringBuilder：容量不足时分配新数组并丢弃旧的（旧数组成为 GC 垃圾）；ZString 把旧数组还回池，**GC 零负担**。

### 5. 泛型零装箱：`FormatterCache<T>` + `TryFormat<T>` 委托

`Append<T>(T value)` 反编译逻辑：

```csharp
var formatter = FormatterCache<T>.TryFormatDelegate;   // 泛型静态字段：每种 T 一份，惰性初始化
if (formatter(value, buffer.AsSpan(index), out int written, default))  // 直写 Span
{
    index += written;
}
else
{
    Grow(written);
    // 用新缓冲重试一次；仍失败 → ArgumentException
}
```

`FormatterCache<T>` 静态构造（cctor）的选择逻辑：
1. `CreateFormatter(typeof(T))` —— **内置注册表**，Utf16 版覆盖 17 种类型：`sbyte/short/int/long/byte/ushort/uint/ulong/float/double/TimeSpan/DateTime/DateTimeOffset/decimal/Guid/nint/nuint`，每个都返回一个**手写 TryFormat 委托**；
2. 不在表中：枚举 → `EnumUtil<T>.TryFormatUtf16`（枚举名/数字，零装箱）；`string` → 专用拷贝；其余 → `TryFormatDefault`（fallback，走 `IFormattable`/`ToString`）。

扩展点：`Utf16ValueStringBuilder.RegisterTryFormat<T>(TryFormat<T>)` / Utf8 同款 —— 可为自己定义的 struct 注册直写 formatter，完全绕开 `ToString()`。

**为什么零装箱**：`T` 是值类型时，整个 `Append<T>` 调用链不产生 `box` 指令；值直接以泛型实参形式传递。`Concat<T1>` 里还用了 `Unsafe.As<T, int>(ref arg)` 做零开销类型转换，把 `T1` 无成本地当作 `int` 处理。IL 层的 `typeof(T) == typeof(int)` 判断在 JIT/IL2CPP 泛型特化后折叠为常量比较，分支被消除。

### 6. 单行 API 内部：thread-static builder + try/finally

`Concat<T1>(T1 arg1)` 反编译逻辑：

```csharp
if (typeof(T1) == typeof(string))              // string 快速路径：连 builder 都不建
    return (string)(object)arg1;
var sb = new Utf16ValueStringBuilder(true);    // thread-static 模式！
try
{
    // 类型分派：string → Append(string)；int → Unsafe.As 后 Append(int)；...
    return sb.ToString();                      // 唯一一次分配
}
finally
{
    sb.Dispose();                              // 释放线程占用标记
}
```

**推论**：`Format/Concat/Join` 调用期间（尤其参数求值阶段）再创建 thread-static 模式 builder 会抛 `NestedStringBuilderCreationException`。这就是 2.6.0 嵌套检测的设计目的——以前是静默数据覆盖，现在是显式报错。

### 7. 手写格式化直写 Span，绕过 ToString()

- 内置 formatter 是**手写 TryFormat 实现**（逐位提取数字字符写入目标 `Span<char>`），支持格式说明符（`"D4"`、`"X"`、`"0.00"` 等），与 `string.Format` 语义一致，但**从不产生中间 string**；
- Utf8 版内置 18 种类型（比 Utf16 多 `bool`、少 `string`——string 走 UTF-8 编码器写入池缓冲），数字直接写 **ASCII 字节**（数字字符即 ASCII），连 UTF-16 中间态都省掉；
- `DateTime/TimeSpan/Guid/decimal` 均有手写路径，避免 `DateTime.ToString()` 等中间分配。

### 8. 最终产物：唯一一次分配

`ToString()` 反编译逻辑：

```csharp
if (index == 0) return string.Empty;           // 空结果零分配
return new string(buffer, 0, index);           // 唯一一次托管分配：精确长度拷贝
```

`AsSpan()` / `AsMemory()` / `AsArraySegment()` 返回零拷贝视图（`buffer.AsSpan(0, index)`），适用于不转 string 直接消费的场景。

**分配全景对比**（构建 `"HP: 100/100"` 这类字符串）：

| 方案 | 分配 |
| --- | --- |
| `"HP: " + hp + "/" + max` | 2 个中间 string + 1 个最终 string（`hp.ToString()` 另算） |
| `string.Format("HP: {0}/{1}", hp, max)` | 1 个 `object[]` + 2 次装箱 + 最终 string |
| `System.Text.StringBuilder` | 1 个对象 + 内部 `char[]`（多次扩容则多个）+ 每段 `int.ToString()` |
| ZString（thread-static） | **0 次**（仅最终 string） |

### 9. 性能来源汇总

1. struct 值类型：无对象分配；
2. 泛型 + FormatterCache：无装箱；
3. 手写 TryFormat 直写 Span：无中间 string / 无 `ToString()`；
4. ArrayPool / ThreadStatic 缓冲：无反复 `new char[]`，扩容旧数组归还池；
5. 类型分派 JIT 常量折叠：无运行时反射开销；
6. 单次拷贝原则：内容只在 `new string(buffer,0,index)` 时拷贝一次。

---

## 六、核心优势特性总结

1. **零分配（Zero Allocation）**：除最终结果 string 外无任何 GC 分配 —— Unity 移动端消除 GC spike/卡顿的利器；
2. **高性能**：比 `StringBuilder` / `string.Format` 快数倍（无装箱、无中间 string、手写格式化），官方 Benchmark 见 [DeepWiki Performance](https://deepwiki.com/Cysharp/ZString/6-performance)；
3. **API 高度兼容 StringBuilder**：`Append/AppendLine/AppendFormat/AppendJoin/Insert/Remove/Replace`，**迁移成本极低**（替换类型 + 加 `using` + 包 using 块即可）；
4. **UTF-16 / UTF-8 双通道**：网络协议、日志可直接产出 UTF-8 字节，无需二次编码；
5. **泛型 Format/Concat/Join 支持 1~16 参数**，覆盖绝大多数业务场景；
6. **模板预解析（PrepareUtf8/Utf16）**：日志/协议模板只解析一次；
7. **线程模型清晰**：ThreadStatic 池线程内复用 + ArrayPool 显式归还，无锁；
8. **netstandard2.1，Unity 2021.2+ 全功能支持**（含 Span 重载），与 UniTask/R3/ZLogger 同生态；
9. **体积小、零运行时依赖负担**（仅依赖 System.Runtime.CompilerServices.Unsafe）。

---

## 七、适用场景与反模式

### 推荐场景
- 每帧执行的 UI 文本更新（HUD、伤害飘字、FPS 显示）
- 日志/调试输出（热路径）；推荐配合 ZLogger（基于 ZString 的零分配日志库）
- 网络协议拼包/解析（Utf8ValueStringBuilder 直接产出字节）
- 排行榜、邮件、聊天等高频模板化文本
- 大量 `string.Format` / `+` 拼接的现有代码改造

### 反模式（不要用 ZString）
- 编译期常量拼接（`"a" + "b"` 编译器会合并，无运行时分配）
- 单次低频的字符串组装（收益可忽略，徒增代码复杂度）
- 需要在字段/数组/闭包中长期保存 builder 的场景（见下节坑点）

---

## 八、注意事项与坑（务必阅读）

1. **struct 不可逃逸**：`Utf16ValueStringBuilder` 是值类型。**不要**存入类字段、放进数组/List、被 lambda / async / iterator 捕获——会发生拷贝或装箱，破坏零分配且行为诡异。只在局部变量中使用。
2. **必须 Dispose**：ArrayPool 模式（`ZString.CreateStringBuilder()` / 默认 `new Utf16ValueStringBuilder()`）用完必须 `Dispose()`（建议 `using`），否则池数组不归还，长期运行会内存膨胀。thread-static 模式（`disposeImmediately: true` / `notNested: true`）同样必须 Dispose —— 用于释放线程占用标记，否则同线程后续的 thread-static builder 会误判为嵌套而抛异常。
3. **不要跨线程**：thread-static 模式的缓冲是线程私有静态数组，绝对禁止跨线程使用（Async 边界、线程池任务中需重建）；ArrayPool 模式虽来自全局池，builder 也应遵循"局部使用、用完即弃"原则，勿跨线程共享。
4. **thread-static 模式禁止嵌套（2.6.0 起显式报错）**：`ZString.Format/Concat/Join` 内部使用 thread-static 缓冲（IL 实证：内部 `new Utf16ValueStringBuilder(true)`）。在这些调用链中（尤其参数求值期间）再创建 thread-static 模式的 builder，会抛 `NestedStringBuilderCreationException`。需要嵌套时请使用默认构造（ArrayPool 模式）或 `ZString.CreateStringBuilder()`。
5. **ToString() 后语义**：`ToString()` 返回的是新 string（唯一分配点），builder 仍可继续复用；不要再持有 ToString 前的 `AsSpan()` 引用（ToSpan 视图在后续 Append/扩容后失效）。
6. **IL2CPP / HybridCLR 泛型裁剪（本项目重点）**：ZString 的核心是泛型方法。IL2CPP 构建（尤其本项目 HybridCLR 热更程序集使用 ZString 时），未被 AOT 侧实例化的泛型调用可能被裁剪/无 AOT 代码，导致运行期 `ExecutionEngineException`。**建议**：
   - 在 link.xml 中保留 ZString 程序集（`<assembly fullname="ZString" preserve="all"/>`）；
   - 或在 AOT 主程序集中显式调用一遍要用的泛型形式（如 `ZString.Format<int,string>(...)`），让 IL2CPP 生成对应 AOT 代码（HybridCLR 的 AOT 泛型补充同理）。
7. **别过度使用**：字符串对象本身仍是托管分配，ZString 只是消灭「过程中的」垃圾。最终结果 string 的分配不可避免。
8. **版本一致性**：包内 `ZString.xml` 文档注释与 2.6.0 一致；升级/降级 ZString 需重新走 NuGet for Unity 安装流程，勿手改 `Assets/Packages` 内文件。

---

## 九、参考链接

- 官方仓库 / README：https://github.com/Cysharp/ZString
- 2.6.0 Release：https://github.com/Cysharp/ZString/releases/tag/2.6.0
- DeepWiki 文档（Getting Started / Core Components / Performance / Unity Integration）：https://deepwiki.com/Cysharp/ZString/1-overview
- NuGet 包页：https://www.nuget.org/packages/ZString/2.6.0
