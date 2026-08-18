# 流程导航能力

## Purpose

热更侧为场景 UI / 玩法提供流程切换门面，在无 `procedureOwner` 时安全写入 FSM 黑板并切换菜单与战斗流程。

## Requirements

### Requirement: Navigator facade for UI and gameplay

系统 MUST 在热更侧提供流程导航门面（如 `ProcedureNavigator`），供场景 UI / 玩法在无 `procedureOwner` 时切换流程。开战入口 MUST 接受战斗场景 location 字符串，写入 Procedure FSM 黑板后切换到 `ProcedureBattle`。回菜单入口 MUST 切换到 `ProcedureMain`，并在切换前将 `Time.timeScale` 恢复为 `1`（若曾暂停）。

#### Scenario: Main menu starts battle

- **WHEN** 主菜单选择某一关卡对应 location（如 `"Level 1"`）
- **THEN** 门面将 location 写入黑板并切换到 `ProcedureBattle`

#### Scenario: Exit to menu restores time scale

- **WHEN** 暂停或游戏中请求退出到主菜单
- **THEN** `Time.timeScale` 为 `1`，且流程切换到 `ProcedureMain`

### Requirement: No Unity SceneManager for menu and battle transitions

`MainMenuUI`、`WinUI`、`LoseUI`、以及 `GameControl` 的退出主菜单路径 MUST NOT 使用 `SceneManager.LoadScene` 完成菜单↔战斗切换；MUST 经导航门面或等价的流程切换完成。

#### Scenario: Win continues to menu

- **WHEN** 胜利界面选择继续/回菜单
- **THEN** 系统经导航门面进入 `ProcedureMain`，不调用 `SceneManager.LoadScene`

### Requirement: Level location mapping

系统 MUST 将关卡选择 index 映射为 YooAsset 场景 location 字符串（集中定义），UI MUST NOT 内联散落多个硬编码 `LoadScene` 分支作为唯一真相来源。关卡号等玩法数据可继续写入既有 `GameplayData`，但进战流程契约 MUST 以黑板 location 为准。

#### Scenario: Level index maps to location

- **WHEN** 玩家选择关卡 index `0`
- **THEN** 门面收到对应 location（如 `"Level 1"`）并用于进战
