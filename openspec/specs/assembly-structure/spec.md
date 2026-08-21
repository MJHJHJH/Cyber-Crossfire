# 程序集结构能力

## Purpose

打通热更程序集对 AOT 公共代码的引用，使热更代码可编译期访问 `ProcedureSceneSwitch` 等 AOT 服务。

## Requirements

### Requirement: AOT 运行时代码独立程序集

#### Description

`Assets/Project/Scripts/Runtime/AOT/` 下代码（Procedure、AssetPatch、UI、Sound、DI）划入独立的 `GamePlay.AOT` 程序集。

### Requirement: 配表代码独立程序集

#### Description

`Assets/Project/DataTable/`（Luban 表类与 Adapters）划入独立的 `GamePlay.DataTable` 程序集，仍属 AOT，不得被热更化。

### Requirement: 热更程序集引用 AOT

#### Description

`HotUpdate` 程序集引用 `GamePlay.AOT` 与 `GamePlay.DataTable`。

禁止任何 AOT 程序集反向引用 `HotUpdate` 程序集。

### Requirement: 主流程归属

#### Description

游戏主流程 `ProcedureMain` 归属热更程序集；AOT 侧不再保留同名主流程实现。

### Requirement: 存档与配置的程序集归属

`DataComponent` 及框架数据管理实现 SHALL 归属 GameFrameWork（AOT）程序集。玩家存档业务类型（如 `PlayerSaveData`、`PlayerSave`）SHALL 归属 `HotUpdate` 程序集。武器等静态配置 SHALL 归属 `GamePlay.DataTable`（或现有配表程序集），不得放入热更存档类型中。

#### Scenario: AOT 不引用热更存档类型

- **WHEN** 检查 AOT / GameFrameWork 程序集引用
- **THEN** 不存在对 `HotUpdate` 玩家存档类型的编译期引用

#### Scenario: 热更可使用框架 Data 与配表

- **WHEN** 热更代码读写玩家存档或查询武器价格
- **THEN** 可通过 `GameFrameWork.Data` 与 `GameFrameWork.DataTable`（或项目等价入口）完成，且不依赖 `DataStorage` SO
