## MODIFIED Requirements

### Requirement: 热更主流程入口

热更程序集内以 `ProcedureMain : ProcedureBase`（`namespace GamePlay`）作为游戏主菜单流程，`OnEnter` 内调用 AOT `ProcedureSceneSwitch.SwitchAsync` 加载主菜单场景组。

热更程序集内另须提供 `ProcedureBattle : ProcedureBase` 作为战斗流程。

系统在热更程序集加载完成后：
- 反射创建 `ProcedureMain` 与 `ProcedureBattle` 实例；
- 通过 `ProcedureComponent.AddProcedure`（底层 `Fsm.AddState` 动态注册）将二者均注册进 Procedure Fsm；
- 在 `ProcedureDataTableInit` 中通过 `ChangeState(procedureOwner, Type)` 常规切换到 `ProcedureMain`，享受 Fsm 状态机完整生命周期（`OnEnter`/`OnUpdate`/`OnLeave`）。

#### Scenario: Both hot-update procedures registered

- **WHEN** HybridCLR 热更初始化完成并完成流程注册
- **THEN** Procedure Fsm 中同时存在可切换的 `ProcedureMain` 与 `ProcedureBattle`

#### Scenario: Startup still enters main menu procedure

- **WHEN** `ProcedureDataTableInit` 完成并切换热更入口流程
- **THEN** 当前流程为 `ProcedureMain`（而非 `ProcedureBattle`）
