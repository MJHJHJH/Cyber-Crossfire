## Why

进战斗链路中，`ProcedureBattle.OnSceneReadyAsync` 在战斗场景已显示、LoadingPanel 已关闭之后才调用 `OpenAsync(BattleHud)`。打开路径的重头代价——YooAsset 资源加载 → Instantiate 实例化 → 首次 Canvas 布局——全部落在玩家可见阶段（`OnInit`/`OnOpen` 生命周期同样在此阶段执行，但此时战斗数据已就绪），造成"场景亮了、HUD 才蹦出来"的卡顿感。战斗中途首次打开弹窗（暂停/胜负/奖励/消息）同样会卡。

现有框架已具备复用基础：UI 实例池（`UI Instance Pool`）按 location 复用实例，关闭后 `SetActive(false)` 回池，池 `Spawn` 不触碰激活态，激活统一由 `UIFormLogic.OnOpen` 触发。因此"预载停放实例"与"关闭后再打开"是同一代码路径，`OpenAsync` 无需新增打开分支。

## What Changes

- `IUIManager` / `UIManager` 新增预载接口：`PreloadAsync(panelId/location/批量, float keepAliveSeconds, ct)`、`UnloadPreload(location)`、`UnloadAllPreloads()`、`IsPreloaded(location)`
- 预载语义：加载资源 → 实例化（Awake 提前执行）→ 直接挂入所属 UIGroup 的物理根（按配置 `CanvasMode` 选 Overlay/Camera 根，与打开后最终归属一致）→ `SetActive(false)` 停放等待激活 → 注册进现有 UI 实例池（`Register(spawned:false)` + `SetLocked(true)` 防 10s 过期释放）。**预载期不执行任何 UIForm 生命周期（`OnInit`/`OnOpen`）**，避免预载期业务数据缺失导致错误；打开时零根节点切换
- 存活策略（接口传入 `keepAliveSeconds`）：`<= 0` → **永久保留**，直到手动 `UnloadPreload` / `UnloadAllPreloads`；`> 0` → 预载完成起算**存活 N 秒自动销毁**（未被打开的前提下），打开后移出记录停止计时、按普通池回收
- `OpenAsync` 仅两处小改：① 单例等待循环同步等待在途预载完成；② 池复用路径命中预载停放实例时：解锁、移出预载记录、以新实例语义（`isNewInstance:true`）执行完整生命周期 `OnInit(userData)` + `OnOpen(userData)`；关后再开的普通回池实例仍跳过 `OnInit`。非预载面板行为完全不变
- `ProcedureSceneSwitch.SwitchAsync` 新增可选 `preloadPanelIds`：与场景加载并行启动预载，SwitchEnd 前软等待（超时上限），失败/取消时 finally 卸载停放实例
- `ProcedureBattle` 传入战斗预载清单（BattleHud / Joystick / Pause / Win / Lose / GemReward / Message）

## Capabilities

### New Capabilities

- `ui-panel-preload`: UI 面板实例预载与对象池挂靠、在途互斥、按需卸载

### Modified Capabilities

- （无）现有 `openspec/specs/` 中无 UI 相关需求需改写

## Impact

- 框架（AOT）：`IUIManager`、`UIManager`、`UIComponent`（`Assets/Project/ThirdParty/GameFrameWork/RunTime/UIManager`）
- 流程（AOT/HotUpdate）：`ProcedureSceneSwitch`（新增可选参数，默认行为不变）；`ProcedureBattle`（传入预载清单、离开时卸载）
- 资源/配表：无（预载清单为代码常量，本轮不改 UIPanel 表结构）
- 明确不做：离屏布局预热（打开时保留一次 Canvas 重建）；LoadingPanel / FadeScreen 启动预载；UIPanel 配表 `PreloadGroup` 列（均可作后续变更）
