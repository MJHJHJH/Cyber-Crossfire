---
change: init-hybridclr-flow
schema: spec-driven
---

# Design: Procedure 流程适配：加载 YooAsset 与初始化 HybridCLR

## Background

现有启动链路（全部 AOT）：

```
GameEntry (GameFramework)
   │
   ▼
ProcedureLaunch       打开 Resources/YooAssetInitPanel + 注册 Provider
   │
   ▼
ProcedureAssetInit    YooAsset 补丁 FSM（多包串行），成功注入主包到 UI/Scene/Sound
   │
   ▼
ProcedureDataTableInit  Luban 配表加载 → SoundGroup 初始化
   │
   ▼
ProcedureMain         （AOT 版）加载场景组
```

HybridCLR 侧现状：
- `HotUpdate.asmdef`（`autoReferenced=false`）已存在，60+ 游戏脚本。
- `HybridCLRDllCopyTool` 产出 `Bundles/HybridCLR/HotUpdate/HotUpdate.dll.bytes` 与 `Bundles/HybridCLR/AotMetadata/*.bytes`。
- YooAsset `DefaultRawPackage` 已配置收集上述路径（`HotUpdate.dll.bytes` 单独收集，`AotMetadata` 目录按目录收集），采用 Addressable 模式（`EnableAddressable: 1`）。
- 运行时无任何 HybridCLR 初始化代码。

## Approach

### 1. 程序集结构调整（方案 A）

```
┌─────────────────────────────────────────────────────────┐
│  GamePlay.AOT (asmdef)                                   │
│  Assets/Project/Scripts/Runtime/AOT/**                   │
│  Procedure* / AssetPatch / UI / Sound / DI               │
│  ├─ 引用 GameFrameWork.RunTime, YooAsset, UniTask        │
│  └─ 引用 GamePlay.DataTable                              │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│  GamePlay.DataTable (asmdef)                             │
│  Assets/Project/DataTable/**  (Luban 表类, AOT)          │
│  └─ 引用 Luban                                            │
└──────────────────────────────────────────────────────────┘
                       ▲
┌──────────────────────┴──────────────────────────────────┐
│  HotUpdate (asmdef, autoReferenced=false)                │
│  热更游戏脚本 + 热更 ProcedureMain                        │
│  └─ 引用 GamePlay.AOT, GamePlay.DataTable                │
│     (由此可访问 ProcedureSceneSwitch 等 AOT 类型)         │
└──────────────────────────────────────────────────────────┘
```

要点：
- `GamePlay.AOT` 不能引用 HotUpdate（HybridCLR 硬规则，避免主包链接热更程序集）。
- `HotUpdate.asmdef` 现有引用列表为 GUID 形式，新增引用以 GUID 追加，保持同一形式；并补充了缺失的 `UnityEngine.UI` 引用。
- `ProcedureMain` 已手动迁移至热更程序集（`Runtime/HotUpdate/Procedure/ProcedureMain.cs`），AOT 侧无同名文件，后续逻辑直接在该文件上修改。

### 2. 新增 ProcedureHotUpdateInit（AOT）

插入位置：`ProcedureAssetInit.OnPatchSucceeded` 之后 → `ChangeState<ProcedureHotUpdateInit>` → 完成后再进入 `ProcedureDataTableInit`。

职责：
1. 获取 HotUpdate 程序集：
   - `#if !UNITY_EDITOR`：从 `DefaultRawPackage` 用 `LoadAssetAsync<TextAsset>("HotUpdate.dll.bytes")` 加载（YooAsset 3.x 中 `GetRawFileData()` 已废弃），`TextAsset.bytes` → `Assembly.Load(byte[])`。
   - `#else`：`AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate")`。
2. 加载 AOT 补充元数据（仅真机）：
   - `package.GetAllAssetInfos()` 遍历，按 `AssetInfo.AssetPath` 前缀 `Assets/Project/Bundles/HybridCLR/AotMetadata` 过滤，逐个 `LoadAssetAsync<TextAsset>` 读取 bytes，执行 `RuntimeApi.LoadMetadataForAOTAssembly(byte[], HomologousImageMode.SuperSet)`，按 `LoadImageErrorCode` 校验结果。
   - Editor 模式跳过（AOT 程序集已全量在域内）。
3. 通过反射获取热更入口类型 `ProcedureMain`（热更程序集顶层类，无命名空间），存入静态字段 `MainProcedureType` 供后续流程使用。
4. `ChangeState<ProcedureDataTableInit>`。

Editor / 真机差异收敛点：统一产出"热更入口类型"，后续流程只依赖该类型，不区分平台。

### 3. 热更入口约定

- 热更程序集内 `ProcedureMain : ProcedureBase`（`Runtime/HotUpdate/Procedure/ProcedureMain.cs`，`namespace GamePlay`），作为游戏主流程。
- 热更版 `ProcedureMain` 引用 AOT 的 `ProcedureSceneSwitch`（同在 `GamePlay` 命名空间），asmdef 改造后编译通过。
- **动态注册机制（新能力）**：GameFrameWork 的 `Fsm` 原本只在 `CreateFsm` 时一次性注册状态、运行期不可扩展，而热更程序集在启动时尚未加载、无法预注册。为此在框架内新增动态注册链路：
  - `Fsm.AddState(FsmState<T>)`：运行时追加状态并触发 `OnInit`。
  - `IProcedureManager.AddProcedure(ProcedureBase)` / `ProcedureManager.AddProcedure` / `ProcedureComponent.AddProcedure`：对外暴露流程动态注册。
- `ProcedureHotUpdateInit` 加载热更程序集后反射创建 `ProcedureMain` 实例，经 `ProcedureComponent.AddProcedure` 注册进 Procedure Fsm，与常规流程一样受 Fsm 状态机生命周期管理。
- `ProcedureDataTableInit` 配表完成后调用受保护非泛型重载 `ChangeState(procedureOwner, Type)` 切换到热更流程（`FsmState` 已提供该重载，无需反射泛型）。

### 4. 启动链路

```
Launch ──▶ AssetInit ──▶ HotUpdateInit ──▶ DataTableInit ──▶ Main(热更·ChangeState)
   │           │              │                 │
   │           │       反射取热更程序集           │
   │           │       加载元数据(真机)           │
   │           │       反射创建 ProcedureMain     │
   │           │       AddProcedure 注册进 Fsm ──┘  ChangeState(owner, hotMainType)
   │           │                                        │
   │           │                                        ▼
   │           │                           热更 ProcedureMain.OnEnter
   │           │                                SceneGroup 加载
   └───────────┴─────────────────────────────────────────┘
```

## Trade-offs

| 方案 | 优点 | 缺点 |
|---|---|---|
| A：AOT/DataTable 独立 asmdef | 热更可编译期引用 AOT 公共类型，符合 HybridCLR 标准结构，代码形态最自然 | 新增 2 个 asmdef、改动引用关系，需在 Unity 中重编译验证 |
| B：接口/委托桥接（未选） | 不动程序集 | 热更调用点全部改造，侵入大、样板代码多 |
| C：AOT 注入回调（未选） | 热更不引用 AOT | 主流程能力被钳制，无法自由访问场景切换等服务 |

## Risks

1. **Addressable location 匹配**：`HotUpdate.dll.bytes` 与 `AotMetadata` 在 `DefaultRawPackage` 下按 `AddressByFileName` 收集，运行时用 `AssetInfo.Address` / 收集路径常量定位；若收集配置调整需同步常量。
2. **`LoadMetadataForAOTAssembly` 版本匹配**：元数据 DLL 必须与构建时的 IL2CPP 剥离产物一致（工具链已保证来源为 `AssembliesPostIl2CppStripDir`）。
3. **动态注册时序**：`ProcedureHotUpdateInit` 必须在热更程序集加载完成后、`ProcedureDataTableInit` 切到热更流程前完成 `AddProcedure`（当前实现已保证顺序）；重复注册会抛异常（`Fsm.AddState` 校验重复类型）。
4. **Editor 模式下热更程序集已内嵌**：编辑器直接取域内程序集，与真机 `Assembly.Load` 路径的入口类型解析需保持一致（热更 `ProcedureMain` 完整类型名为 `GamePlay.ProcedureMain`，两路径解析一致）。
