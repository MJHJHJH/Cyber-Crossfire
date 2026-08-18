---
change: init-hybridclr-flow
schema: spec-driven
---

# Change Proposal: Procedure 流程适配：加载 YooAsset 与初始化 HybridCLR

## Why

项目已具备完整的运行基础：GameFramework Procedure 启动链路（Launch → AssetInit → DataTableInit → Main）、YooAsset 补丁流程（AssetPatch FSM）、HybridCLR 工具链（HybridCLRDllCopyTool 已产出热更 DLL 与 AOT 补充元数据 .bytes），但存在两个断点：

1. **HybridCLR 运行时初始化缺失**：热更 DLL（`HotUpdate.dll.bytes`）与 AOT 元数据虽已就位（`Bundles/HybridCLR/`），但从未在运行时执行 `LoadImage` / `LoadMetadataForAOTAssembly`。热更程序集里的 60+ 游戏脚本（Player/AI/Weapon/UI）实际从未被加载执行。
2. **热更程序集无法引用 AOT 公共代码**：热更版 `ProcedureMain` 直接引用了 AOT 的 `ProcedureSceneSwitch`，但 Unity 规则禁止 asmdef 程序集引用默认程序集 `Assembly-CSharp`，当前会编译失败。

因此需要：在启动链路的 AssetInit（YooAsset 就绪）之后插入 HybridCLR 初始化流程，并调整程序集结构，让热更程序集能够引用 AOT 公共代码，最终以热更程序集内的 `ProcedureMain` 作为游戏主流程。

## What Changes

### 流程链

```
Launch → AssetInit(YooAsset补丁) → HotUpdateInit(HybridCLR初始化·动态注册热更Main) → DataTableInit(Luban) → Main(热更ProcedureMain·ChangeState)
```

- 新增 `ProcedureHotUpdateInit`（AOT），插入在 `ProcedureAssetInit` 之后。
- 框架新增动态注册能力（`Fsm.AddState` / `IProcedureManager.AddProcedure` / `ProcedureComponent.AddProcedure`）：热更程序集加载完成后反射创建 `ProcedureMain` 实例并注册进 Procedure Fsm。
- `ProcedureDataTableInit` 结尾用 `ChangeState(procedureOwner, Type)` 常规切换到热更主流程，热更 `ProcedureMain : ProcedureBase` 与普通流程一样享受 Fsm 状态机生命周期。

### 程序集结构调整

- 新建 `GamePlay.AOT.asmdef` 包裹 `Assets/Project/Scripts/Runtime/AOT/` 下的 AOT 运行时代码。
- 新建 `GamePlay.DataTable.asmdef` 包裹 `Assets/Project/DataTable/`（Luban 表类，仍属 AOT）。
- `HotUpdate.asmdef` 增加对 `GamePlay.AOT`、`GamePlay.DataTable` 的引用（从而可引用 `ProcedureSceneSwitch` 等 AOT 类型），并补齐缺失的 `UnityEngine.UI` 引用。
- 主流程 `ProcedureMain` 已手动迁移至热更程序集（`Runtime/HotUpdate/Procedure/ProcedureMain.cs`），改造为 `ProcedureBase` 子类（`namespace GamePlay`），由 AOT 侧动态注册进 Procedure Fsm 后常规切换，后续逻辑直接在该文件上修改。

### HybridCLR 初始化

- Editor 模式：直接 `AppDomain.CurrentDomain.GetAssemblies()` 查找 `HotUpdate` 程序集，跳过加载。
- 真机模式：从 `DefaultRawPackage` 以 `RawFileObject` 加载寻址名 `HotUpdate.dll` → `GetBytes()` → `Assembly.Load`；遍历 `AotMetadata` 目录资源逐一 `LoadMetadataForAOTAssembly`。

## Impact

- 启动链路新增一个流程节点，`AssetInit` 完成后先初始化热更再加载配表。
- 程序集引用关系调整，AOT 公共代码与 DataTable 各自独立成 asmdef，热更程序集可引用它们；AOT 侧不允许反向引用热更。
- 热更程序集作为最终主流程载体，后续游戏逻辑改动只热更 DLL，无需重打主包。
- 影响范围：AOT 流程代码（新增 `ProcedureHotUpdateInit`、修改 `ProcedureDataTableInit`/`ProcedureAssetInit`）+ asmdef 配置（新增 2 个、修改 1 个）。`ProcedureMain` 已迁至热更程序集，不在 AOT 侧改动。不涉及现有 YooAsset 补丁状态机改动。
