# DataComponent 能力

## Purpose

在 GameFrameWork 提供全局通用数据读写组件，以 JSON 文件持久化到 persistentDataPath，供热更业务存档使用。

## Requirements

### Requirement: DataComponent 全局可用

系统 SHALL 在 GameFrameWork 中提供 `DataComponent`，并通过 `GameFrameWork.Data` 暴露，供运行时全局访问。

#### Scenario: 组件已初始化

- **WHEN** 游戏框架内置组件完成初始化
- **THEN** `GameFrameWork.Data` 非空且可调用 Load/Save 与读写 API

### Requirement: JSON 文件持久化

系统 SHALL 将数据持久化到 `Application.persistentDataPath` 下的 JSON 文件，不得使用 PlayerPrefs 作为玩家存档后端。

#### Scenario: 首次保存创建文件

- **WHEN** 内存中存在已修改数据且调用 Save
- **THEN** 在 persistentDataPath 约定路径写出合法 JSON 文件

#### Scenario: 再次启动加载

- **WHEN** 上次已成功 Save 且再次启动后调用 Load
- **THEN** 内存中可通过约定 key 读回此前写入的对象内容

### Requirement: 按 key 读写对象

系统 SHALL 支持按 string key 对可序列化对象执行 Get/Set，以及 Has/Remove。

#### Scenario: Set 后 Get

- **WHEN** 对 key `"player"` 调用 SetObject 写入对象后立即 GetObject
- **THEN** 返回的对象字段与写入值一致

#### Scenario: 不存在的 key

- **WHEN** 对从未写入的 key 调用 GetObject
- **THEN** 返回该类型的默认值或明确的空结果，且不抛未处理异常

### Requirement: 加载与重置

系统 SHALL 提供 Load、Save、Reset。Reset MUST 清空内存中的存档数据并删除或清空对应持久化文件。

#### Scenario: Reset 后无旧数据

- **WHEN** 在已有存档文件时调用 Reset，再 Load
- **THEN** 业务默认对象恢复为初始默认值，不得读回 Reset 前的 Coin 等字段

### Requirement: 应用退出时刷盘

系统 SHALL 在应用暂停到后台或退出时，将脏数据刷入磁盘。

#### Scenario: 暂停时保存

- **WHEN** 内存数据已标脏且应用进入 Pause
- **THEN** 持久化文件被更新为当前内存状态

### Requirement: 流程关键节点刷盘

系统 SHALL 在离开战斗流程时将脏数据刷入磁盘；进战前 SHALL 刷盘以保存主菜单期间的修改。

#### Scenario: 离开战斗落盘

- **WHEN** `ProcedureBattle` 离开且存档已标脏
- **THEN** 持久化文件被更新

#### Scenario: 进战前落盘

- **WHEN** 从主菜单进入战斗
- **THEN** 进战前执行一次存档落盘
