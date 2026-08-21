## ADDED Requirements

### Requirement: 武器价格配表

系统 SHALL 通过 Luban 提供武器配置表，至少包含武器 Id 与 Price，并经由现有 DataTable 注册方式以接口（如 `IWeapon`）查询。

#### Scenario: 按 Id 取价格

- **WHEN** 配表已 Load 且查询某武器 Id
- **THEN** 返回该武器的 Price

#### Scenario: 列表可用

- **WHEN** 配表已 Load
- **THEN** 可枚举全部武器记录且数量与表定义一致

### Requirement: 购买使用配表价格

购买武器时系统 SHALL 使用 Luban 武器表中的 Price，不得从玩家存档读取价格。

#### Scenario: 价格来源

- **WHEN** 玩家在军械库购买未解锁武器
- **THEN** 扣除金额等于 `IWeapon`（或等价接口）中该 Id 的 Price
