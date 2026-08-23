## 1. 框架层（AOT：ThirdParty/GameFrameWork/RunTime/UIManager）

- [x] 1.1 `IUIManager` 新增：`PreloadAsync(int panelId, float keepAliveSeconds, CancellationToken)`、`PreloadAsync(string location, string uiGroupName, float keepAliveSeconds, CancellationToken)`、`PreloadAsync(IEnumerable<int>, float keepAliveSeconds, CancellationToken)`、`UnloadPreload(string location)`、`UnloadAllPreloads()`、`IsPreloaded(string location)`（`keepAliveSeconds <= 0` 永久 / `> 0` 定时）
- [x] 1.2 `UIManager` 新增 `m_UIFormsBeingPreloaded`（HashSet\<location\>）与预载主流程：配置解析 → `ResolveLoader` → `LoadAsync` → 实例化（Awake 提前跑）→ `GetOrAddComponent<UIForm>`（**不执行 OnInit**）→ 挂入所属 UIGroup 物理根（按配置 CanvasMode 经 `DualUIGroupHelper.GetParent`）→ `SetActive(false)` → `UIFormInstanceObject.Create` + `Register(spawned:false)` + `SetLocked(true)`，并记入 `m_PreloadedInstances`（含存活参数）；含失败告警、取消清理、已打开/已预载/在途打开跳过逻辑
- [x] 1.3 `UIManager.OpenAsync` 两处小改：单例等待循环同步等待在途预载；池复用路径命中预载停放实例时 `SetLocked(false)` + 移出 `m_PreloadedInstances`（停止存活计时）+ 以 `isNewInstance:true` 进入 `InternalOpenUIForm`（首次打开执行 `OnInit(userData)` + `OnOpen(userData)`）
- [x] 1.4 `UIManager.UnloadPreload / UnloadAllPreloads`：解锁 → `ReleaseObject`（Destroy 实例 + 释放 YooAsset 句柄；停放实例 SpawnCount=0，不可 Unspawn）
- [x] 1.5 `UIManager.Update` 驱动存活计时：Timed 条目按 `realElapseSeconds` 倒计时，先收集到期 location 再统一自动 `UnloadPreload`；永久条目跳过
- [x] 1.6 `UIComponent`：转发 panelId/location/批量预载（含 keepAliveSeconds，所属组不存在时先 `AddGroup`，与 `OpenByPanelIdAsync` 一致）、卸载、查询接口（联动 `m_DestroyCts` 链接取消）

## 2. 流程接入（AOT/HotUpdate）

- [x] 2.1 `ProcedureSceneSwitch.SwitchAsync` 新增可选 `IReadOnlyList<int> preloadPanelIds`：LoadingPanel 打开后并行启动预载；SwitchEnd 前 `WhenAny(预载任务, 超时上限 3s)` 软等待；超时/异常/取消路径 finally 取消预载 + `UnloadAllPreloads`（同步修正 `ProcedureMain` 调用点）
- [x] 2.2 `ProcedureBattle`：新增 `BattlePreloadPanelIds`（1002/1003/1005/1006/1007/1008/1009）以 `keepAliveSeconds = 0`（永久保留）传入 `SwitchAsync`；`OnLeave` 调 `UnloadAllPreloads`

## 3. 验证

- [x] 3.1 进战斗：HUD/摇杆秒开；Profiler 对比改动前后打开耗时（资源加载/实例化归零）
- [x] 3.2 战斗中首次开 Pause / Win / Lose / GemReward / Message 无卡顿
- [x] 3.3 预载超时 / 场景切换取消 / 失败路径：无停放实例泄漏（对象池信息核对）
- [x] 3.4 回归：非预载面板（主页 MainHUD、ShopPanel）打开行为与改动前一致
- [x] 3.5 二次进战斗：未使用停放实例出战斗已卸载、已使用实例回池复用正常
- [x] 3.6 存活参数：`keepAliveSeconds = 3` 预载后不打开 → 3 秒后自动销毁（对象池信息核对）；打开后不被计时销毁；`keepAliveSeconds = 0` 永久保留直到手动卸载

## 4. 收尾说明

- [x] 4.1 在代码或 tasks 备注中标明：UIPanel 表 `PreloadGroup` 列与离屏布局预热为后续变更
