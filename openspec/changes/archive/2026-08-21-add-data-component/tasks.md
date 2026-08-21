## 1. GameFrameWork DataComponent

- [x] 1.1 新增 `IDataManager` / `DataManager`（GameFrameworkModule）与 JSON 文件 Helper（persistentDataPath）
- [x] 1.2 新增 `DataComponent`：Load/Save/Reset、GetObject/SetObject/Has/Remove、脏标记与 Pause/Quit 刷盘
- [x] 1.3 在 `GameFrameWork.Builtin` 暴露 `GameFrameWork.Data`，并在启动场景挂载组件
- [x] 1.4 在合适流程节点（DataTable 之后、主菜单业务之前）调用 `Data.Load()`

## 2. Luban 武器配置

- [x] 2.1 新增武器表定义与数据（至少 Id、Price；按现有 6 把武器填入原 DataStorage 价格）
- [x] 2.2 生成/更新表代码与 `IWeapon` Adapter，并注册到 `LubanTablesFactory`
- [x] 2.3 验证 `GameFrameWork.DataTable` 可按 Id 取 Price、可枚举全部武器

## 3. HotUpdate 玩家存档

- [x] 3.1 新建 `HotUpdate/Data`：`PlayerSaveData`、`PlayerSave`（门面 + 默认值 + 按配表对齐 WeaponsUnlocked 长度）
- [x] 3.2 实现 AddCoin / TryUnlockWeapon / SelectWeapon 等读写（标脏，关键节点落盘）
- [x] 3.3 在热更初始化或 `ProcedureMain` 进入前 `PlayerSave.EnsureLoaded()`

## 4. 调用点迁移

- [x] 4.1 改造 `MainHUDPresenter`：读档展示、买武器走配表价格（关卡不锁定）
- [x] 4.2 改造 `WinUiPresenter`、`GameUI_PCPresenter`、广告奖励加币路径
- [x] 4.3 改造 `PlayerCharacter` / `GameControl` 等运行时引用，去掉 DataStorage 字段
- [x] 4.4 改造或清理 OldUI（MainMenuUI、WinUI、UI_HUD 等）中的 DataStorage 用法
- [x] 4.5 通关胜利加币并在离开战斗时统一落盘（不做 LevelUnlocked 锁定）

## 5. 删除旧 DataStorage 与 DI 清理

- [x] 5.1 从 `AppLifetimeScope` / `GameDiInstaller` 移除 DataStorage 引用与注册
- [x] 5.2 删除 `DataStorage.cs`、`.asset` 及场景/预制体上残留序列化引用
- [x] 5.3 全项目确认无 `DataStorage` / 存档用 PlayerPrefs 业务引用

## 6. 验证

- [x] 6.1 首次启动默认存档；购买武器、广告/通关加币后杀进程重启数据仍在
- [x] 6.2 关卡不锁定；全部有效关卡可进入
- [x] 6.3 Pause/Quit 与离开战斗后脏数据已落盘
