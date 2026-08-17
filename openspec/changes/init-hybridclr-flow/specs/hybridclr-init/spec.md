# HybridCLR 初始化能力

## Purpose

在 YooAsset 资源包就绪后初始化 HybridCLR 热更运行时，使热更程序集可被加载并作为游戏主流程执行。

## Requirements

### Requirement: 热更程序集加载

#### Description

系统在 YooAsset 补丁完成、主包注入完成后，加载热更程序集 `HotUpdate`。

#### Scenario

真机（非 Editor）：

- 从 YooAsset 主包加载 `HotUpdate.dll.bytes` 资源。
- 使用 `Assembly.Load(byte[])` 将热更程序集载入当前 AppDomain。

Editor：

- 不执行加载，直接通过 `AppDomain.CurrentDomain.GetAssemblies()` 按程序集名 `HotUpdate` 获取已编译的热更程序集。

### Requirement: AOT 补充元数据加载

#### Description

真机模式下，在加载热更程序集前，动态遍历 `AotMetadata` 目录下所有 `.bytes` 文件，逐一执行 `RuntimeApi.LoadMetadataForAOTAssembly`，为 AOT 泛型/补充元数据补全执行信息。

Editor 模式跳过（AOT 程序集已全量在域内，无需补元数据）。

### Requirement: 热更主流程入口

#### Description

热更程序集内以 `ProcedureMain : ProcedureBase`（`namespace GamePlay`）作为游戏主流程，`OnEnter` 内调用 AOT `ProcedureSceneSwitch.SwitchAsync` 加载场景组。

系统在热更程序集加载完成后：
- 反射创建 `ProcedureMain` 实例；
- 通过 `ProcedureComponent.AddProcedure`（底层 `Fsm.AddState` 动态注册）注册进 Procedure Fsm；
- 在 `ProcedureDataTableInit` 中通过 `ChangeState(procedureOwner, Type)` 常规切换到该流程，享受 Fsm 状态机完整生命周期（`OnEnter`/`OnUpdate`/`OnLeave`）。

## Requirement: 流程编排

### Description

启动链路顺序为：

```
Launch → AssetInit(YooAsset) → HotUpdateInit(HybridCLR·注册热更Main) → DataTableInit(Luban·ChangeState到热更Main)
```

HybridCLR 初始化必须发生在 YooAsset 就绪之后、Luban 配表加载之前。
