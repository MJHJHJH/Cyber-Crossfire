## ADDED Requirements

### Requirement: 存档与配置的程序集归属

`DataComponent` 及框架数据管理实现 SHALL 归属 GameFrameWork（AOT）程序集。玩家存档业务类型（如 `PlayerSaveData`、`PlayerSave`）SHALL 归属 `HotUpdate` 程序集。武器等静态配置 SHALL 归属 `GamePlay.DataTable`（或现有配表程序集），不得放入热更存档类型中。

#### Scenario: AOT 不引用热更存档类型

- **WHEN** 检查 AOT / GameFrameWork 程序集引用
- **THEN** 不存在对 `HotUpdate` 玩家存档类型的编译期引用

#### Scenario: 热更可使用框架 Data 与配表

- **WHEN** 热更代码读写玩家存档或查询武器价格
- **THEN** 可通过 `GameFrameWork.Data` 与 `GameFrameWork.DataTable`（或项目等价入口）完成，且不依赖 `DataStorage` SO
