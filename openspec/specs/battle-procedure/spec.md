# 战斗流程能力

## Purpose

热更侧独立战斗流程：经 FSM 黑板接收进战场景 location，用 `ProcedureSceneSwitch` 加载战斗场景；主菜单由 `ProcedureMain` 负责。

## Requirements

### Requirement: Battle procedure loads scene from enter args

系统 MUST 提供热更流程 `ProcedureBattle`（`ProcedureBase`）。进入该流程时 MUST 从 Procedure FSM 黑板读取进战场景 location（字符串），用完后 MUST 移除该数据；随后 MUST 调用 `ProcedureSceneSwitch.SwitchAsync`，以该 location 作为场景组唯一成员并激活。离开流程时 MUST 取消进行中的场景切换。

#### Scenario: Enter battle with Level 1

- **WHEN** 流程切换到 `ProcedureBattle` 且黑板上进战 location 为 `"Level 1"`
- **THEN** 系统经 `ProcedureSceneSwitch` 加载并激活 `"Level 1"`，并清除该黑板项

#### Scenario: Leave cancels in-flight switch

- **WHEN** `ProcedureBattle` 在场景切换未完成时离开
- **THEN** 进行中的切换 MUST 被取消，不得在离开后继续改场景状态

### Requirement: Main menu procedure owns menu scene

`ProcedureMain` MUST 仅负责主菜单场景组（location `"MainMenu"` 并激活 `"MainMenu"`）。从战斗返回菜单时 MUST 切换到 `ProcedureMain`，由该流程 `OnEnter` 再次执行菜单场景加载。

#### Scenario: Return from battle to menu

- **WHEN** 当前为 `ProcedureBattle` 且请求进入主菜单
- **THEN** 系统切换到 `ProcedureMain`，并加载激活 `"MainMenu"`

### Requirement: Restart battle without Unity SceneManager

失败重开 MUST 再次进入战斗流程并传入当前战斗场景 location，MUST NOT 调用 `UnityEngine.SceneManagement.SceneManager.LoadScene`。

#### Scenario: Lose restart same level

- **WHEN** 玩家在战斗中选择重开，且当前战斗 location 为 `"Level 2"`
- **THEN** 系统再次进入 `ProcedureBattle` 并加载 `"Level 2"`
