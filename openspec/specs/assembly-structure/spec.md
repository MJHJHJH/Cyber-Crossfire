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
