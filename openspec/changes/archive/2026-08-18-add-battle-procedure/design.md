## Context

启动链路已具备：YooAsset → HybridCLR 动态注册热更 `ProcedureMain` → 主菜单经 `ProcedureSceneSwitch` Additive 加载。玩法侧（`MainMenuUI` / `WinUI` / `LoseUI` / `GameControl`）仍用 `SceneManager.LoadScene`（Single），与框架 Home 场景及流程状态冲突，且 `ChangeState` 无法直接带参。

已定方向：独立 `ProcedureBattle`；菜单保持 `ProcedureMain`；UI 经门面进战；黑板只传场景 location；显式注册 Main + Battle；Win/Lose/Pause 退出均回菜单。

约束：AOT 不得强引用热更类型；仓库仅有 `Variable` / `Variable<T>`，无官方 `VarString`；UI 无 `procedureOwner`。

## Goals / Non-Goals

**Goals:**

- 菜单 ↔ 战斗全部走 Procedure + `ProcedureSceneSwitch`
- 进战参数经 Procedure FSM `SetData`/`GetData`/`RemoveData`
- UI/玩法只依赖导航门面，不直接 `ChangeState` / `LoadScene`
- 注册 `ProcedureBattle`，保证 `ChangeProcedure` 可用
- 暂停退出恢复 `timeScale`；Lose 重开再次 `EnterBattle`

**Non-Goals:**

- 不修改框架 `ChangeState` / `ChangeProcedure` 签名以支持 payload
- 不重做战斗玩法、HUD、胜负判定逻辑
- 不把关卡表全面 Luban 化（本变更只集中 location 映射）
- 不解决 YooAsset 打包本身；假定关卡场景已有正确 location
- 不扫全程序集自动注册所有 `ProcedureBase`（本变更显式两个）

## Decisions

### 1. 流程切分：Main = 菜单，Battle = 战斗

- **选择**：两个热更 `ProcedureBase`，双向 `ChangeProcedure`
- **理由**：生命周期清晰（取消 CTS、场景组边界）；避免 Main 长期占用却混杂战斗态
- **备选**：单流程内只切场景 → 否决（出口/重入难管）

### 2. 传参：FSM 黑板 + location 字符串

- **选择**：key 如 `"BattleEnter"`，值为 `Variable<string>`（或项目内 `VarString`）；`LevelNumber` 仍写 `GameplayData`
- **理由**：符合 GF 惯例；契约最小；无需改框架 API
- **备选**：静态全局 / 改 `ChangeState(args)` → 否决（脏读或破坏约定）

### 3. UI 入口：`ProcedureNavigator` 门面

- **选择**：`EnterBattle(location)` / `EnterMainMenu()`；内部 `GameFrameWork.Fsm.GetFsm<IProcedureManager>()` 写黑板，再 `GameFrameWork.Procedure.ChangeProcedure<T>()`
- **理由**：`ProcedureComponent` 无 `SetData` 封装；UI 不应散落 Fsm API
- **备选**：给 `ProcedureComponent` 加 SetData 转发 → 可后续做，本变更不强制改框架

### 4. 热更注册：显式 Main + Battle

- **选择**：在 `ProcedureHotUpdateInit` 注册两处（反射类型名，与现有 Main 一致）
- **理由**：改动小、可预测；AOT 仍无热更强引用
- **备选**：扫描全部 ProcedureBase → 留待流程增多时再做

### 5. 当前战斗 location 供重开

- **选择**：`ProcedureBattle` 进入成功后缓存 `_currentBattleLocation`（或静态/门面可读的当前战斗 location）；Lose 重开读该值再 `EnterBattle`
- **理由**：黑板进战后已 Remove；Unity 激活场景名不一定等于 YooAsset location
- **备选**：重开前再 SetData 同 location → 也可，但需 UI 知道 location；缓存更稳

### 6. 关卡映射

- **选择**：HotUpdate 内静态数组/只读表 `index → "Level N"`，`MainMenuUI.BtnLevel` 映射后调门面
- **理由**：去掉散落 `switch` + `LoadScene`

## Risks / Trade-offs

- [重复点击进战] → 门面或 UI 在 `ProcedureSceneSwitch.IsSwitching` / 当前已是 Battle 时忽略或禁用按钮
- [暂停 timeScale=0 卡住异步] → `EnterMainMenu` 与 `ExitGame` 路径先 `timeScale = 1`
- [location 与包内 Address 不一致] → 实现前核对 YooAsset 寻址；失败打明确日志
- [AOT 注册类型名写死] → 与现有 `HotUpdateMainTypeName` 同样约定，Battle 增加常量类型名
- [Additive 多场景与旧关卡假设] → 依赖现有 SceneComponent Home + FallbackCamera；不在本变更重做灯光/AudioListener
- [无 VarString] → 热更或 AOT 公共处新增薄 `Variable<string>` 包装；优先放 HotUpdate 若仅战斗用，若 AOT 流程也要用则放 AOT（本变更仅热更用则 HotUpdate 即可）

## Migration Plan

1. 落地 Variable 包装、Navigator、ProcedureBattle、注册扩展
2. 替换四处 `LoadScene` 调用
3. Editor 跑：启动 → 菜单 → 选关 → 战斗 → 胜/负/暂停退出 → 菜单；负侧重开同关
4. 回滚：恢复 `LoadScene` 调用并去掉 Battle 注册（不推荐上线混用）

## Open Questions

- 无阻塞项。可选后续：`ProcedureComponent.SetData` 转发、批量注册热更流程、关卡表进 Luban。
