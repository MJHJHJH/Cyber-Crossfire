# 玩家存档能力

## Purpose

在热更侧提供玩家进度存档门面，通过 GameFrameWork.Data 持久化，替代旧 DataStorage / PlayerPrefs。

## Requirements

### Requirement: 玩家存档字段

热更侧玩家存档 SHALL 至少包含与旧 DataStorage 对齐的字段：`Coin`、`LevelUnlocked`、`SelectedWeapon`、`WeaponsUnlocked`。

#### Scenario: 默认存档

- **WHEN** 无存档文件且首次 EnsureLoaded/Load
- **THEN** Coin 为 0、LevelUnlocked 为 0、SelectedWeapon 为 0、WeaponsUnlocked 中索引 0 为 true、其余为 false

### Requirement: 玩家存档门面

系统 SHALL 在 `HotUpdate/Data` 提供玩家存档门面（如 `PlayerSave`），通过 `GameFrameWork.Data` 读写，业务代码不得直接操作 PlayerPrefs 或 ScriptableObject 存档。业务修改 SHALL 仅标脏；高频操作（如局内加币）MUST NOT 立即写盘。

#### Scenario: 加币只标脏

- **WHEN** 调用门面增加 Coin（如局内捡币）
- **THEN** 内存 Coin 增加且存档标脏，但不要求立即更新持久化文件

#### Scenario: 购买武器

- **WHEN** 玩家金币足够且目标武器未解锁并请求购买
- **THEN** 扣除金币、对应 WeaponsUnlocked 为 true、SelectedWeapon 更新为该武器，并标脏

#### Scenario: 金币不足

- **WHEN** 玩家金币小于武器价格并请求购买
- **THEN** 不修改 Coin、解锁状态与选中武器

#### Scenario: 关键节点落盘

- **WHEN** 离开战斗、进战前，或应用 Pause/Quit，且存档已标脏
- **THEN** 持久化文件被更新为当前内存状态

### Requirement: 关卡不锁定

系统 SHALL 允许进入所有有效关卡；选关 MUST NOT 以 `LevelUnlocked` 拒绝进入。存档可保留 `LevelUnlocked` 字段以兼容旧数据。

#### Scenario: 任意有效关卡可进

- **WHEN** 玩家选择配置表中存在的关卡 index（含大于 LevelUnlocked 的 index）
- **THEN** 可进入对应战斗流程

### Requirement: 移除 DataStorage

系统 MUST NOT 再依赖 `DataStorage` ScriptableObject 作为存档或配置容器；相关类型与资源 SHALL 被删除，调用点 SHALL 迁移到玩家存档门面与配表。

#### Scenario: 编译期无 DataStorage 引用

- **WHEN** 迁移完成后在 Assets 脚本中检索 `DataStorage`
- **THEN** 无业务代码仍引用该类型
