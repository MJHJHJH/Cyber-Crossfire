## Why

启动链路已切到框架 `Procedure` + `ProcedureSceneSwitch`（YooAsset Additive），但主菜单开战、胜负回退、暂停退出仍调用 `UnityEngine.SceneManagement.SceneManager.LoadScene`（Single）。这会冲掉框架 Home 场景与流程状态，也无法向下一流程传入「加载哪个战斗场景」。需要补齐热更侧战斗流程，并用 FSM 黑板传参，让菜单与战斗用同一套场景切换路径。

## What Changes

- 新增热更流程 `ProcedureBattle`：`OnEnter` 读取进战参数，经 `ProcedureSceneSwitch.SwitchAsync` 加载并激活指定战斗场景；`OnLeave` 取消进行中的切换。
- `ProcedureMain` 继续只负责主菜单场景组；从战斗返回时切回 `ProcedureMain`，由其 `OnEnter` 再加载 `MainMenu`。
- 新增热更门面 `ProcedureNavigator`：UI / 玩法只调 `EnterBattle(location)` / `EnterMainMenu()`，内部对 Procedure FSM `SetData` 后 `ChangeProcedure`，不直接碰 `SceneManager`。
- 新增 FSM 黑板用 `Variable` 子类（场景 location 字符串）；进战只传 location，关卡号仍写现有 `GameplayData`。
- `ProcedureHotUpdateInit` 显式动态注册 `ProcedureMain` 与 `ProcedureBattle`（热更流程无法进 AOT Inspector 列表）。
- 替换 `MainMenuUI` / `WinUI` / `LoseUI` / `GameControl.ExitGame` 中的 `SceneManager.LoadScene`；Lose 重开改为再次 `EnterBattle`（当前战斗 location）；退出菜单前恢复 `timeScale`。
- 关卡 location 字符串集中映射（如 index → `"Level 1"`…），避免 UI 内散落 switch。

## Capabilities

### New Capabilities

- `battle-procedure`: 热更战斗流程生命周期、进战参数（FSM 黑板）、与主菜单流程双向切换、经 `ProcedureSceneSwitch` 加载战斗/菜单场景。
- `procedure-navigation`: 热更侧流程导航门面，供场景 UI / 玩法在无 `procedureOwner` 时安全切换流程并写入黑板。

### Modified Capabilities

- `hybridclr-init`: 热更初始化在动态注册主流程时，须同时注册 `ProcedureBattle`，使后续可 `ChangeProcedure` 进入战斗。

## Impact

- 热更：`Procedure/`、`UI/`（MainMenu/Win/Lose）、`Gameplay/GameControl`；新增导航门面与 `Variable` 包装。
- AOT：`ProcedureHotUpdateInit` 注册逻辑扩展（仍不得引用热更类型的强类型，继续反射或已有类型名约定）。
- 依赖：现有 `GameFrameWork.Procedure` / `Fsm` / `ProcedureSceneSwitch` / YooAsset 场景 location；战斗与菜单场景须已进包且 location 与代码一致。
- 不改框架 `ChangeState` 签名；不引入 Unity `SceneManager.LoadScene` 新调用。
