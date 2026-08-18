## 1. 黑板与导航门面

- [x] 1.1 新增热更侧 `Variable<string>` 包装（如 `VarString`）及进战黑板 key 常量
- [x] 1.2 新增 `ProcedureNavigator`：`EnterBattle(location)`（SetData + ChangeProcedure Battle）、`EnterMainMenu()`（恢复 timeScale + ChangeProcedure Main）
- [x] 1.3 新增关卡 index → YooAsset location 映射表（集中一处）

## 2. 战斗流程

- [x] 2.1 新增 `ProcedureBattle`：OnEnter 读黑板 location、RemoveData、缓存当前 location、SwitchAsync；OnLeave 取消 CTS
- [x] 2.2 确认 `ProcedureMain` 仅加载激活 `"MainMenu"`（与现实现对齐，必要时修正 SceneGroup）

## 3. 热更流程注册

- [x] 3.1 扩展 `ProcedureHotUpdateInit`：反射创建并 `AddProcedure` `ProcedureBattle`（与 Main 同等约定类型名常量）
- [x] 3.2 确认启动仍只 ChangeState 到 `ProcedureMain`

## 4. 替换遗留 SceneManager 调用

- [x] 4.1 `MainMenuUI.BtnLevel`：写 `GameplayData.LevelNumber`，映射 location 后调 `EnterBattle`
- [x] 4.2 `WinUI` / `LoseUI.BtnExit` / `GameControl.ExitGame`（含 Pause 退出）：改 `EnterMainMenu`，禁止 `LoadScene("MainMenu")`
- [x] 4.3 `LoseUI.BtnRestart`：用当前战斗 location 调 `EnterBattle`，禁止 `LoadScene(当前场景名)`

## 5. 验证

- [ ] 5.1 Editor：启动 → 菜单 → 选关 → 战斗场景出现且 Home 仍在
- [ ] 5.2 胜/负回菜单、暂停退出回菜单；`timeScale` 正常；负侧重开同关
- [x] 5.3 确认相关脚本无新增 `SceneManager.LoadScene` 用于菜单↔战斗
