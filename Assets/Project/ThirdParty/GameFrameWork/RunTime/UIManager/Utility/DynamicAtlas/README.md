# DynamicAtlas 动态图集框架

> 运行时合图（Runtime Texture Packing）UI 组件框架，位于 `GameFrameWork.RunTime` 程序集（AOT 侧），供热更层 UI 使用。
> 本文档覆盖：框架介绍、使用方式、注意事项。

---

## 目录

- [1. 概述](#1-概述)
- [2. 架构总览](#2-架构总览)
- [3. 核心原理](#3-核心原理)
- [4. 使用方式](#4-使用方式)
- [5. 生命周期](#5-生命周期)
- [6. 注意事项](#6-注意事项)
- [7. 附录：与静态图集 / YooAsset 扩展示例的关系](#7-附录与静态图集--yooasset-扩展示例的关系)

---

## 1. 概述

### 1.1 要解决的问题

UI 中大量 `Image` 使用**各自独立的纹理**时，会产生大量 draw call / 纹理切换（SetTexture），渲染效率低。
传统方案是美术侧人工维护**静态图集**（SpriteAtlas / TexturePacker），但存在：

- 新增/修改图需重打图集，迭代成本高；
- 大图集整包加载，内存浪费；
- 图集与代码/预制体解耦困难。

**动态图集**的思路：运行时把零散 Sprite 的像素**拷贝进若干张固定尺寸的大纹理（图集页）**，
UI 组件改渲染"页纹理上的子区域"（`Sprite.Create` 切片），使大量不同来源的图最终落在同一张纹理上，实现合批。

### 1.2 定位与现状

- 框架代码完整、自洽，位于 `RunTime/UIManager/Utility/DynamicAtlas/`，命名空间 `DynamicAtlas`，属于 `GameFrameWork.RunTime` 程序集（AOT 侧，供热更代码引用）；
- 当前工程**尚未接入**：全 Assets 无预制体/场景挂载 `DynamicAtlasImage`，HotUpdate 代码无调用。本文档"使用方式"即接入指引；
- 核心设计：GPU `CopyTexture` 像素拷贝、固定 RGBA32 图集页、矩形装箱、引用计数、脏缓存零拷贝复活、软释放延迟销毁、空页回收与内存整理。

---

## 2. 架构总览

```
┌────────────────────────────── UI 组件层 ──────────────────────────────┐
│ DynamicAtlasImage（Image 子类）：Location 绑定 / 自动合图 / 引用释放    │
│ DynamicAtlasDebugView（RawImage 调试视图）                            │
├────────────────────────────── 加载层 ─────────────────────────────────┤
│ DynamicAtlasLoader（静态）：YooAsset 加载 + 请求合并 + 句柄释放        │
│ DynamicAtlasLocation（静态）：Sprite → Location 解析                   │
├────────────────────────────── 核心管理层 ─────────────────────────────┤
│ DynamicAtlasManager（静态单例）：Insert/Release/查询/回收/引用计数     │
├────────────────────────────── 图集页层 ───────────────────────────────┤
│ AtlasPage：页纹理 + 装箱器 + CopyTexture 拷贝 + 清除 + 活跃计数       │
│ DynamicAtlasGpuFill：GPU 纯色模板生成/拷贝                            │
├────────────────────────────── 算法层 ─────────────────────────────────┤
│ Packing/RectanglePacker：矩形装箱（自由区分割法）                     │
│ Packing/IntegerRectangle、SortableSize                                │
├────────────────────────────── 定义层 ─────────────────────────────────┤
│ DynamicAtlasTypes：枚举 / AtlasEntry / AtlasInsertResult / Config     │
└───────────────────────────────────────────────────────────────────────┘
```

| 文件 | 职责 | 关键成员 |
|---|---|---|
| `DynamicAtlasManager.cs` | 单例核心：条目字典、分组页表、脏缓存、引用计数、回收 | `Insert / Release / TryGet / IsInUse / GetRefCount / Reset / CollectEmptyPages / TrimMemory / ClearIdlePixels` |
| `AtlasPage.cs` | 单页纹理 + 装箱 + 像素拷贝 | `TryPack / TryCopyFrom / FreeRect / TryReclaim / ClearIdlePixels / ActiveCount` |
| `DynamicAtlasImage.cs` | UI 组件（继承 `Image`） | `sprite` 重定义、`Location`、`Reload / ApplySourceSprite / BindEntry / ReleaseBound` |
| `DynamicAtlasLoader.cs` | YooAsset 加载封装 | `LoadAndInsertAsync / LoadAndInsertSync / IsYooAssetReady` |
| `DynamicAtlasLocation.cs` | Location 解析 | `TryResolveFromSprite / TryResolveFromTexture` |
| `DynamicAtlasTypes.cs` | 类型与配置 | `DynamicAtlasGroup / AtlasEntry / AtlasInsertResult / DynamicAtlasConfig` |
| `DynamicAtlasGpuFill.cs` | GPU 模板 | 透明/灰底 1x1 → Blit 拉伸 → CopyTexture |
| `Packing/RectanglePacker.cs` | 矩形装箱 | 自由区分割 + 对象池（`s_rectPool`） |
| `DynamicAtlasDebugView.cs` | 调试 | 页纹理显示到 RawImage |
| `Editor/.../DynamicAtlasImageEditor.cs` | Inspector | Location/Group 编辑、Reload 按钮 |
| `Editor/.../DynamicAtlasTexturePostprocessor.cs` | 编辑器 | 图源目录导入强制全平台 RGBA32 |
| `Editor/.../DynamicAtlasBuildValidator.cs` | 编辑器 | 构建前校验图源格式，违规中断构建 |

---

## 3. 核心原理

### 3.1 运行时合图：像素拷贝（CopyTexture）

合图不是"引用"，而是**真实像素拷贝**：

1. 图集页是一张 `new Texture2D(size, size, RGBA32, false, false)`（无 mipmap、Bilinear、Clamp）；
2. 插入时把源纹理的 `sourceRect` 通过 `Graphics.CopyTexture` 拷到页纹理的 `destRect`（`AtlasPage.TryCopyFrom`）；
3. 拷贝前先用**透明模板**把 `destRect ± padding` 清成透明（`ClearRectWithPadding`），避免边缘脏像素；
4. 用 `Sprite.Create(pageTexture, destRect, ...)` 生成指向页内区域的 `AtlasEntry.Sprite`，交给 `Image` 渲染。

关键约束（代码已处理）：

- 禁止源 == 目标页纹理（自拷贝污染），`ReferenceEquals` 检查；
- 禁止把图集页纹理当源再插入（`IsAtlasPageTexture` 入口检查）；
- **源纹理格式必须 RGBA32/ARGB32**（`IsSupportedSourceTexture` 校验，RenderTexture 放行），压缩纹理直接返回 `UnsupportedFormat` 并 LogError——CopyTexture 跨格式拷贝不受支持，且压缩纹理拷贝区域须 4×4 块对齐，动态装箱无法保证；
- `SystemInfo.copyTextureSupport == None` 时拷贝失败（返回 `CopyFailed` + LogError，无 CPU 回退）；
- 源纹理 `textureRect` → `RectInt` 截断 + Clamp（`TextureRectToRectInt`）。

### 3.2 矩形装箱（RectanglePacker）

经典**自由区分割法**（Ville Koskela 算法移植）：

- 初始一个整页自由区 `(0,0,W,H)`；
- `TryPack(w,h)` 在自由区列表中找"最左（X 最小）且放得下"的区域，切出目标矩形，剩余空间按"右/上/左/下"四方向切分回填；
- 释放时 `FreeRectangle` 把矩形精确放回自由区列表；`TryReclaimRectangle` 支持精确收回（供脏缓存复活）；
- 矩形对象走静态池 `s_rectPool` 复用，减少 GC。

### 3.3 Sprite 重建与元数据

`Sprite.Create` 时透传源 Sprite 的 `border`（九宫格）、`pivot`（按源 rect 归一化）、`pixelsPerUnit`；
`SpriteMeshType.FullRect` 是项目约定（源图需 FullRect）。首次插入后执行元数据一致性告警（尺寸/PPU/Border）。

### 3.4 引用计数（RefCount）

- `AtlasEntry.RefCount` 表示"持有该图集切片的组件/请求数"；
- `Insert` 命中已有条目时 `RefCount++` 并复用（不重复拷贝）；
- `Release` 递减，归零后：矩形归还 Packer、页面活跃计数 `-1`、**Sprite 进入软释放队列**（延迟 `SoftReleaseFrames` 帧销毁，默认 3 帧）、条目移出字典、写入脏缓存；
- `DynamicAtlasImage` 通过 `_boundKey` 精确记账，配合 `_loadVersion` 防止过期异步回调误绑定。

### 3.5 脏缓存与零拷贝复活（Dirty Cache）

`Release` 后**像素保留在页纹理中**，同时把 `(key, page, rect, 元数据)` 记入 `_dirtyCache`：

- 再次 `Insert` 同一 `Location@GroupSize` 且尺寸一致 → `TryReviveFromDirtyCache` 直接收回该区域、重建 Sprite，**零拷贝复活**；
- 新插入的矩形与脏区相交 → 对应脏条目失效（像素将被覆盖）；
- 脏缓存有界：`DirtyCacheLimit`（默认 512）超限时驱逐最老条目（只放弃复活能力，不影响正确性）。

### 3.6 YooAsset 加载与请求合并（DynamicAtlasLoader）

- 资源约定：图源资源收集进 **DefaultPackage**，Location = **文件名（无扩展名）**；
- `LoadAndInsertAsync`：同一 `Location@GroupSize` 的**并发请求合并为一次加载、一次装箱**（`s_pending` 等待表）；
- 分发采用**两阶段计数**：先补齐所有等待者的引用份额再逐个回调，过期回调只归还自己的份额，避免误释放条目；
- 完成回调统一在 `finally` 中 `handle.Release()`；
- `LoadAndInsertSync`：同步变体（无合并）。

### 3.7 内存回收

| API | 作用 | 典型时机 |
|---|---|---|
| `CollectEmptyPages()` | 回收所有"无活跃条目"的空页（含中间页，自动保持页索引连续并修正引用） | 场景切换后 |
| `ClearIdlePixels()` | 所有页空闲区像素清为透明并失效对应脏缓存（显式放弃复活优化） | 低内存 |
| `TrimMemory(clearIdlePixels)` | 组合入口：回收空页，可选清空闲像素 | 场景切换 / 低内存 |
| `Reset()` | 销毁全部页与条目、清空脏缓存（整体卸载/重建） | 切账号、回滚 |
| 软释放（`SoftReleaseFrames`） | Release 归零的 Sprite 延迟 N 帧销毁，误释放后组件重新绑定可自愈 | 自动生效 |
| 脏缓存驱逐（`DirtyCacheLimit`） | 超限驱逐最老条目 | 自动生效 |

### 3.8 关键约定汇总

| 项 | 约定 |
|---|---|
| Location | YooAsset 地址，= 资源文件名（无扩展名） |
| 资源包 | DefaultPackage |
| 源 Sprite | `Mesh Type = FullRect`（项目约定） |
| 页格式 | `TextureFormat.RGBA32`，无 mipmap |
| 分组 | 1024 / 2048 / 4096（`DynamicAtlasGroup`） |
| Padding | `DEFAULT_PADDING = 2` |
| 入口 key | `Location@GroupSize` |

---

## 4. 使用方式

### 4.1 组件方式（推荐）

把 `Image` 替换为 **DynamicAtlasImage**（`AddComponentMenu("UI/Dynamic Atlas Image")`），两种图源模式：

**A. 填 Location（走 YooAsset 异步加载）**

```csharp
// 序列化字段：_location = "ui_icon_sword"，_atlasGroup = Size1024，_loadOnEnable = true
// OnEnable 自动 Reload：YooAsset 加载 → 合图 → 显示
```

**B. 直接赋 Source Image（同步合图，编辑器/运行时均可）**

```csharp
GetComponent<DynamicAtlasImage>().sprite = someSprite; // 解析 Location → 同步 Insert → 显示
```

两种模式最终同一套 `Location@GroupSize` key，可混用。

### 4.2 代码方式（不依赖组件）

```csharp
// 异步：加载 + 合图 + 回调（合并并发请求）
DynamicAtlasLoader.LoadAndInsertAsync("ui_icon_sword", DynamicAtlasGroup.Size1024, result =>
{
    if (result.Success) { /* result.Entry.Sprite / result.Entry.Key */ }
});

// 同步
AtlasInsertResult r = DynamicAtlasLoader.LoadAndInsertSync("ui_icon_sword", DynamicAtlasGroup.Size1024);

// 直接插入已有 Sprite / Texture
DynamicAtlasManager.Instance.Insert("ui_icon_sword", someSprite, DynamicAtlasGroup.Size1024);

// 释放（引用计数必须与 Insert 一一对应）
DynamicAtlasManager.Instance.Release("ui_icon_sword@1024"); // 或 Release(location, group)

// 释放前自查条目是否仍被持有
bool inUse = DynamicAtlasManager.Instance.IsInUse(key);
int count = DynamicAtlasManager.Instance.GetRefCount(key);
```

> ⚠️ 代码方式要求调用者严格保证"Insert 与 Release 一一对应"。释放前可用 `IsInUse(key)` / `GetRefCount(key)` 自查；即使误释放，`SoftReleaseFrames` 帧内组件重新绑定可自愈。

### 4.3 资源与收集器要求

1. **图源目录约定**：动态图集图源统一放 `Assets/Project/Art/Sprites/DynamicAtlas/`（已加入 DefaultPackage 的 UIGroup 收集，`AddressByFileName` + `PackDirectory`），**大图/背景图不要放入**（RGBA32 是 4B/px，收窄目录控制包体）；
2. **导入格式（自动强制）**：图源目录内纹理由 `DynamicAtlasTexturePostprocessor` 在导入/重导时强制全平台 **RGBA32**（Default/Android/iPhone 平台覆盖一起压制，mipmap 关闭）——CopyTexture 要求源与图集页同格式，压缩纹理（ETC2/ASTC/BC）跨格式拷贝不受支持且区域须 4×4 块对齐；存量资源需手动 Reimport 一次生效；构建前由 `DynamicAtlasBuildValidator` 校验，违规中断构建；
3. **运行时防御**：`Insert` 对非 RGBA32/ARGB32 的 Texture2D 直接返回 `UnsupportedFormat`（RenderTexture 放行）——热更/动态创建的资源无法被导入器约束，由这层兜底；
4. 不要给动态图源开启 SpriteAtlas 打包（会被静态图集合并，导致 textureRect 指向图集纹理）。

### 4.4 调试工具

- **DynamicAtlasDebugView**：挂到 RawImage 上，实时查看指定分组/页的图集纹理（`LateUpdate` 每帧刷新），支持 Next/Prev 翻页；
- **`DynamicAtlasManager.DebugClearFreeAreas()`**：空闲区恢复灰底（调试用）；
- **`TotalPageCount` / `DirtyCacheCount` / `GetPageCount`**：内存与状态监控；
- Inspector 面板提供 `Reload` 按钮。

### 4.5 性能预期

- 同组图源合到一张页 → draw call 从 N 降到 1（同图集页内）；
- 每页内存：1024² ≈ 4MB、2048² ≈ 16MB、4096² ≈ 64MB（RGBA32 × 4 字节）；
- 合图成本：一次 GPU `CopyTexture`（提交轻量）；大量图并发加载完成时回调集中提交，大面板首次打开建议错峰或分批。

---

## 5. 生命周期

### 5.1 条目（AtlasEntry）

```
[Insert] 首次 ──► 装箱 + CopyTexture + Sprite.Create（RefCount=1，页 ActiveCount++）
   │ ▲
   │ │ 复用（RefCount++）            ┌──────────────┐
   ▼ │                              │  _dirtyCache  │
[使用中] ── Release 至 0 ──► 矩形归还 + Sprite 软释放（延迟销毁）
   │                           + 条目移除 + 写入脏缓存 ──► 同 key 再插入 → 零拷贝复活
   ▼
[释放后] 像素保留在页纹理中（供复活），区域可被其它图装箱复用
```

### 5.2 页面（AtlasPage）

```
首次 Insert（该分组无可用页）─► new AtlasPage（页纹理创建，GPU 端写入）
   ─► 页加入 _groups[groupSize] ─► ActiveCount 归零后可由 CollectEmptyPages/TrimMemory 回收
   ─► Reset 销毁全部页
```

### 5.3 组件（DynamicAtlasImage）

```
OnEnable（_loadOnEnable && Location 非空）──► Reload ──► YooAsset 就绪? ──► LoadAndInsertAsync
   │                                                     └─ 未就绪 ──► 静默返回（需手动 Reload）
   │                                             完成回调：过期则 Release；有效则 BindEntry
   ├─ OnEnable（sprite 序列化恢复）──► ApplySourceSprite（同步 Insert）
   ├─ OnValidate（编辑器 Play 模式）──► ApplySourceSprite
   ├─ OnDisable ──► 仅 _loadVersion++（不释放绑定）
   └─ OnDestroy ──► ReleaseBound（Release 绑定 Key + SetAtlasSprite(null)）
```

### 5.4 异步加载（请求合并）

```
组件A/B Reload 同 key ──► LoadAndInsertAsync
   ├─ TryGet 命中？── 是 ──► RefCount++ → 立即回调
   ├─ 有等待者？── 是 ──► 追加回调
   └─ 首个请求：LoadAssetAsync ──► Completed
         ──► 一次装箱/拷贝 ──► 两阶段补齐 RefCount ──► 逐个回调 ──► handle.Release()
```

### 5.5 全局生命周期

- `DynamicAtlasManager.Instance` 是 C# 静态单例（无 MonoBehaviour、无 Unity 生命周期钩子）；
- 页表、条目、脏缓存、软释放队列均为单例持有；当前项目未关闭 Domain Reload，退出 Play 后由域重载清理；
- 场景切换：`ProcedureSceneSwitch` 在旧场景卸载后自动调用 `TrimMemory(true)`；`Application.lowMemory` 订阅兜底；
- 面板级开/关**不**触发清理（依赖复活优化），条目由组件 `OnDestroy` 释放。

---

## 6. 注意事项

1. **图源格式**：图源必须 RGBA32（图源目录自动强制 + 运行时校验双保险）。压缩纹理（ETC2/ASTC/BC）会被 `Insert` 拒绝（`UnsupportedFormat` + LogError），表现为该图不显示——请检查导入设置，不要试图把大图/静态图集图塞进动态图集；
2. **Release 纪律**：`Insert` 与 `Release` 必须一一对应；释放前用 `IsInUse` / `GetRefCount` 自查；即使误释放，`SoftReleaseFrames`（默认 3）帧内组件重新绑定可自愈，但**长期显示不重绑仍会白图**——先摘显示、再 Release；
3. **Reset 纪律**：`Reset()` 销毁全部页面纹理，调用前必须确保无 UI 组件正在显示图集 Sprite（面板全部关闭/卸载）；
4. **OnDisable 不释放绑定**：组件禁用（如列表项 SetActive(false)）只作废在途加载，不释放 RefCount——条目常驻页面，空页回收不触发；如需释放请销毁组件（OnDestroy）或显式 Release；
5. **YooAsset 未就绪**：`Reload` 在资源包未初始化时静默返回且无重试——请确保 UI 在包就绪后打开，或手动调用 `Reload()`；
6. **绕过图集的赋值**：`Image.overrideSprite` 以及通过 `Image` 基类引用赋值 `sprite`（`GetComponent<Image>().sprite = x`）不会走合图逻辑，直接显示原图——请统一使用 `DynamicAtlasImage` 类型引用；
7. **静态图集 Sprite 作输入**：将静态图集（SpriteAtlas）的 Sprite 喂给 `DynamicAtlasImage` 会被 `UnsupportedFormat` 拒绝（图集纹理通常为压缩格式）；旋转打包的 sprite 拷贝内容错乱；运行时 Location 解析回退 `texture.name` 会取到图集纹理名而非子图名——静态图集图不应进动态图集；
8. **页内存**：每页 4/16/64MB（1024/2048/4096），页面只增不减直到回收触发——合理选择分组尺寸，`TrimMemory` 在场景切换时已自动调用；
9. **编辑器**：Play 模式下 Inspector 修改 Location 会被旧 sprite 解析结果回滚，改 Location 请用代码或 Reload 按钮；编辑模式代码调用 `sprite` setter 会创建运行时图集页；
10. **源 Sprite 约定**：保持 `Mesh Type = FullRect`（Tight 会导致镂空区域参与命中测试）；mipmap 建议关闭（图集页无 mipmap）；
11. **运行时改分组**：`AtlasGroup` setter 只改字段，不迁移已绑定条目，需手动 `Reload()`；
12. **Domain Reload**：若为提速关闭 Domain Reload（Enter Play Mode Options），静态单例持有的纹理会在退出 Play 后被销毁而 C# 引用悬空——建议保持开启或自行处理重置。

---

## 7. 附录：与静态图集 / YooAsset 扩展示例的关系

### 7.1 项目中的静态图集

- `Assets/Project/Art/Atlases/Weapon Sprite Atlas.spriteatlasv2`（Sprite Atlas V2，武器图集），被 DefaultPackage `ArtGroup` 按文件名收集；
- 动态图集与静态图集是**互补**关系：静态管"稳定大图/美术资产"，动态管"运行时拼装/迭代快的零散图"；二者图源不要混用（见注意事项 7）。

### 7.2 YooAsset 扩展示例（未启用）

- `ThirdParty/YooAsset/3.0.5/Extension Sample/Runtime/SpriteAtlasLoader`：响应 `SpriteAtlasManager.atlasRequested`，用于 **YooAsset 包内静态 SpriteAtlas 的运行时加载**；当前项目未挂载该组件——若未来用 bundle 内 SpriteAtlas 且按需触发，需要启用它；
- `Extension Sample/Runtime/UIPanelMonitor`（Editor 面板依赖图集清单刷新）：未使用。

### 7.3 接入检查清单

- [ ] UI 图源放入 `Assets/Project/Art/Sprites/DynamicAtlas/`（确认收集器已配置）；
- [ ] 存量图源在 Unity 里右键 Reimport 一次（触发 postprocessor 强制 RGBA32）；
- [ ] 替换目标 UI 的 `Image` 为 `DynamicAtlasImage`，或代码接入 Loader API 并保证 Insert/Release 配对；
- [ ] 大面板首次打开做一次 Profiler 验证（CopyTexture 提交、页内存）；
- [ ] 长时运行观察 `TotalPageCount` / `DirtyCacheCount`（切场景后应回落）。

---

*文档基于当前工程代码生成（GameFrameWork.RunTime / DynamicAtlas）。*
