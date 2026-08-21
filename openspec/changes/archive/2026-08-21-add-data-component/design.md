## Context

当前玩家进度由热更侧 `DataStorage`（ScriptableObject）+ `PlayerPrefs` 承担，配置（武器价格）与运行时状态混在同一 SO；DI 虽已把 SO 注入 `GameLifetimeScope`，但旧 MonoBehaviour 仍序列化引用，Save 调用点不一致。

项目已有 GameFrameWork 组件模式（`UIComponent`、`LubanConfigComponent`）与 HybridCLR 边界：框架/配表在 AOT，业务可热更。存档字段必须能热更扩展，因此框架组件不能直接暴露 Coin 等业务属性。

## Goals / Non-Goals

**Goals:**

- 提供与现有组件一致的 `DataComponent`，全局 `GameFrameWork.Data` 访问。
- 通用 KV/对象 API + JSON 文件持久化（`persistentDataPath`）。
- 热更 `PlayerSave` 对齐旧 `DataStorage` 存档字段与业务，并收口全部调用点。
- 武器价格迁 Luban；删除 `DataStorage` 及 SO。

**Non-Goals:**

- 不兼容 / 不迁移旧 PlayerPrefs。
- 不做云存档、加密、多存档槽。
- 不把 `Contents` 武器预制体引用迁入 Luban（本 change 只迁价格）。
- 不改 `GameplayData` 本局临时状态模型（仍可 DI 单例）。
- 不实现完整关卡解锁 UI 玩法扩展（仅保证 `LevelUnlocked` 可读写持久化；主菜单选关可按该字段做基础锁定）。

## Decisions

### 1. DataComponent 为通用存储，非玩家专用 API

- **选择**：`GetObject<T>` / `SetObject<T>` / `Has` / `Remove` / `Load` / `Save` / `Reset`，按 string key。
- **理由**：与 `LubanConfigComponent` 一样，框架不随业务字段增减；玩家字段可热更。
- **备选**：`GameFrameWork.Data.Coin` 式专用 API → 否决（锁死 AOT）。

### 2. 模块分层对齐 UI

- **选择**：`IDataManager` + `DataManager`（`GameFrameworkModule`）+ `DataComponent` 壳；默认 `JsonFileDataHelper` 读写单文件。
- **理由**：与 `UIManager`/`UIComponent` 一致，便于扩展序列化器。
- **文件**：例如 `{persistentDataPath}/GameData/player_data.json`，内含 `version` + 按 key 的 JSON 片段（或可序列化字典结构）。首版可用「单 key 单对象」简化：文件即包装 `PlayerSaveData` 也可，但推荐 **多 entry 容器**，避免第二次业务数据再改格式。

### 3. 序列化使用 JsonUtility（可换）

- **选择**：默认 `JsonUtility`；抽象 `IDataSerializer` 便于以后替换。
- **理由**：无新依赖；存档字段为 int/bool[]，满足需求。
- **注意**：`JsonUtility` 不支持 Dictionary；多 key 可用显式 `List<Entry>` 或「一文件一 key」；推荐 **单文件 + Entry 列表**（key + jsonPayload 字符串）或根对象字段固定为已知 wrapper。实现时选一种并在代码注释写清。

### 4. 热更 PlayerSave 门面

- **选择**：`HotUpdate/Data/PlayerSaveData.cs` + `PlayerSave.cs`（静态或 Singleton 门面），内部用 `GameFrameWork.Data`，key 如 `"player"`。
- **字段**：`Coin`、`LevelUnlocked`、`SelectedWeapon`、`WeaponsUnlocked[]`。
- **API**：`Load`/`Save`/`Reset`、`AddCoin`、`TryUnlockWeapon`、`SelectWeapon`、`SetLevelUnlocked`（或等价）。
- **默认值**：无文件时 Coin=0、LevelUnlocked=0、SelectedWeapon=0、WeaponsUnlocked[0]=true，其余 false；数组长度与武器配表数量对齐（或常量上限，以表 Count 为准）。
- **Presenter**：可继续构造注入 `PlayerSave` 包装实例，或直接调门面；旧 MonoBehaviour 用门面。

### 5. Save 策略

- **选择**：业务修改只 `SetObject` 标脏；**不在** `AddCoin` 等高频路径写文件。
- **落盘节点**：`ProcedureBattle.OnLeave`、`ProcedureNavigator.EnterBattle`（进战前）、`DataComponent` 的 Pause/Quit。
- **理由**：战斗内频繁捡币若每次 IO 有卡顿风险；关键节点统一刷盘即可。

### 6. 加载时机

- **选择**：`DataComponent` 在可用后尽早 `Load()`（如 `ProcedureDataTableInit` 之后或 `ProcedureMain` / `GameDiInstaller` 之前）；`PlayerSave.EnsureLoaded()` 在首次业务访问前保证默认对象存在。
- **理由**：主菜单打开前存档已就绪，不依赖某个 UI OnAttach。

### 7. Luban 武器表

- **选择**：新增 `TbWeapon` / `IWeapon`（至少 `Id`、`Price`；可选 `Name`）；接入现有 Adapter 生成管线与 `LubanTablesFactory`。
- **价格来源**：购买与 Armory UI 只读配表，不写进存档。
- **预制体**：继续 `Contents.m_PlayerWeapons`（本次不迁）。

### 8. DI / 删除 DataStorage

- **选择**：从 `AppLifetimeScope` 移除 `_dataStorage`；`GameDiInstaller` 不再注册；删除 `DataStorage.cs` 与 `.asset`；所有引用改为 `PlayerSave` + `IWeapon`。
- **GameplayData**：保留注入方式不变。

### 9. LevelUnlocked

- **选择**：字段持久化；主菜单选关时若 `index > LevelUnlocked` 则拒绝进入（基础行为）。通关后如何增长可在 Win 流程中 `SetLevelUnlocked(max(...))`（若现有 Win 无解锁逻辑则补最小实现：胜利时至少解锁下一关）。

## Risks / Trade-offs

- [JsonUtility 表达力弱] → 用 Entry 列表或固定 wrapper；禁止把 Dictionary 直接塞进存档根。
- [立即全量替换调用点范围大] → tasks 按框架 → 热更门面 → Luban → 调用点 → 删除旧 SO 排序，降低半迁移状态。
- [无 PlayerPrefs 迁移，老玩家进度清零] → 已接受；发布说明可提及。
- [DataComponent 在 AOT，业务类型在热更] → 框架只存 string/JSON；反序列化在热更 `PlayerSave` 内完成，或框架 `GetObject<T>` 仅在 T 对调用方可见时使用（热更调用）。
- [武器数量变化导致数组长度不一致] → Load 后按表 Count 扩缩并保旧槽位数据。

## Migration Plan

1. 落地 DataComponent 并挂场景，暴露 `GameFrameWork.Data`。
2. 落地 PlayerSave + Luban 武器表。
3. 批量替换调用点并验证主菜单/购买/通关/广告加币。
4. 删除 DataStorage 与 DI 引用；清理 PlayerPrefs 相关代码（无需迁移工具）。
5. 回滚：恢复旧脚本/资源（git）；新 JSON 文件可忽略。

## Open Questions

- 无。探索阶段未决项已在 Decisions 中拍板（通用 KV、脏标记+关键立即存、价格进表、预制体暂留 Contents）。
