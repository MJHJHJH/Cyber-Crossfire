## Why

现有 `DataStorage` 把运行时存档与配置混在 ScriptableObject 上，经 `PlayerPrefs` 散 key 读写，调用点分散且 Save 时机不可靠。需要在 GameFrameWork 提供全局数据读写能力，并在热更侧用可扩展的 JSON 存档替换旧方案。

## What Changes

- 在 GameFrameWork 新增 **DataComponent**（通用 KV / 对象读写），持久化到 `Application.persistentDataPath` 的 JSON 文件。
- 在 `HotUpdate/Data` 新增 **PlayerSaveData / PlayerSave**，承载原 `DataStorage` 业务字段（Coin、LevelUnlocked、武器解锁、选中武器）与业务 API。
- 武器价格等配置迁入 **Luban 配表**（如 `IWeapon`）；存档不再保存价格。
- **BREAKING**：删除 `DataStorage` 及所有相关 SO 引用 / DI 注入；不再兼容旧 PlayerPrefs key。
- 所有原 `DataStorage` 调用点（Presenter、广告、战斗、旧 UI、App/Game Scope）改为走新存储系统。
- `GameplayData`（本局临时状态）保持独立，不进入 DataComponent。

## Capabilities

### New Capabilities

- `data-component`: GameFrameWork 全局数据读写组件（Load/Save、Get/Set 对象、文件路径与生命周期）。
- `player-save`: 热更侧玩家存档模型与门面（字段、默认值、经济/武器/关卡解锁业务读写）。
- `weapon-config`: Luban 武器配置表（至少 Id、Price），供购买与 UI 展示。

### Modified Capabilities

- `assembly-structure`: 明确 DataComponent 归属框架程序集、玩家存档归属 HotUpdate、配表归属 DataTable，禁止 AOT 反向依赖热更存档类型。

## Impact

- **框架**：`GameFrameWork` 新增 Data 模块/组件，并在 `GameFrameWork.Builtin` 暴露 `GameFrameWork.Data`；启动场景需挂载组件。
- **热更**：`HotUpdate/Data` 新目录；Presenter / `PlayerCharacter` / `GameControl` / Admob / OldUI 等去掉 `DataStorage`。
- **DI**：`AppLifetimeScope` 去掉 DataStorage 引用袋；`GameDiInstaller` 不再注册 `DataStorage`。
- **配表**：Luban 新增武器表并接入 `LubanTablesFactory` / Adapters。
- **资源**：删除 `DataStorage.asset` 及相关序列化引用；旧 PlayerPrefs 数据丢弃（可接受）。
