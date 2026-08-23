## Context

进战斗链路：`ProcedureBattle.SwitchBattleSceneAsync` → `ProcedureSceneSwitch.SwitchAsync`（LoadingPanel 盖屏、场景 suspend 加载、≥1.5s 展示窗口）→ SwitchEnd → `OnSceneReadyAsync` → `OpenAsync(BattleHud=1002)`。

当前 `OpenAsync(panelId)` 打开路径的代价构成：

1. YooAsset `LoadAssetAsync` — bundle IO + 反序列化（冷启动数十 ms 级，主线程收尾）
2. `InstantiateUIForm` — 主线程实例化，复杂 HUD 数 ms
3. `UIForm.OnInit` / `UIFormLogic.OnInit` — 缓存绑定（每实例一次）
4. `OnOpen → Visible=true → SetActive(true)` — 首次 Canvas 全量重建/布局（1~3ms）
5. Presenter 解析挂接（VContainer）

上述全部发生在场景已亮出、LoadingPanel 已关闭之后 → 卡顿感。

对象池现状（`UIManager.SetObjectPoolManager` → `UI Instance Pool`）：

- `InstanceAutoReleaseInterval=10s`、`InstanceExpireTime=10s`、`Capacity=10`（`UIConfig.UIObjectPoolConfig.Default`）
- `GetCanReleaseObjects` 跳过 `IsInUse || Locked || !CustomCanReleaseFlag`，即 **Locked 实例不会被自动过期释放**
- 池 `Spawn(location)` 仅当 `!IsInUse` 时返回对象，且不触碰 GameObject 激活态；激活统一由 `UIFormLogic.OnOpen → SetActive(true)` 触发，关闭路径 `OnClose → SetActive(false) → OnRecycle → Unspawn`
- 打开路径：池命中即 `InternalOpenUIForm(..., isNewInstance:false)`，跳过 OnInit（复用语义：OnInit 每实例一次）；**预载停放实例例外**——首次打开以 `isNewInstance:true` 补执行 OnInit（见 D10）

关键推论：**预载停放实例与"关闭后再开"的实例在池内几乎等价**——复用同一池命中路径，无需新增打开分支；唯一差异是首次打开以 `isNewInstance:true` 补执行 `OnInit`（见 D10）。

已确认：当前全部面板逻辑均未覆写 `Awake/Start/OnInit`（场景依赖绑定集中在 `OnOpen`，如 `GameUI_PCUIFormLogic.OnOpen` 才 Resolve Presenter），预载实例化可安全早于战斗场景存在。

利益相关：战斗进场 HUD/摇杆手感；战斗中首次弹窗手感；主页/商店等后续场景复用同一能力。

## Goals / Non-Goals

**Goals:**

- 提供"加载 + 实例化 + 停放挂池"的通用预载接口，`OpenAsync` 语义不变、非预载面板零影响
- 进战斗对 HUD / Joystick / 战斗弹窗预载，场景亮出后打开零资源加载、零实例化
- 预载失败 / 超时 / 取消有明确兜底，不断场景切换流程
- 停放实例内存可控：锁定防过期 + 出战斗显式卸载未使用停放

**Non-Goals:**

- 不改 `OpenAsync` 的加载主路径（仅等待在途预载与解锁两处小改）
- 不做离屏布局预热（打开时仍保留一次 Canvas 重建，1~2ms 级，后续变更）
- 不改 UIPanel 表结构（`PreloadGroup` 列后续变更）
- 不预载 LoadingPanel / FadeScreen（后续变更）

## Decisions

### D1. 实例级预载，复用现有 UI 实例池

- **选择**：预载 = 解析配置 → loader 加载资源 → `InstantiateUIForm`（Awake 提前执行）→ `GetOrAddComponent<UIForm>`（仅组件附加，保证 `AllowMultiInstance` 可读）→ **挂入所属 UIGroup 物理根**（按配置 `CanvasMode` 经 `DualUIGroupHelper.GetParent` 选 Overlay/Camera 根，见 D3）→ `SetActive(false)` 停放 → `UIFormInstanceObject.Create` + `Register(spawned:false)` + `SetLocked(true)`，并记入 `m_PreloadedInstances`（location → 存活信息，见 D11）。**预载期不执行任何 UIForm 生命周期（`OnInit` / `OnOpen`）**，避免预载期业务数据未就绪导致错误
- **理由**：打开路径零新增；首次打开补执行 `OnInit`（见 D10），与"关后再开"语义一致；Locked 防 10s 过期释放
- **备选**：资源级预载（省 IO 不省实例化，收益有限，拒绝）；打开时机前移（仅隐藏卡顿且 OnOpen 撞场景初始化窗口有竞态，拒绝）

### D2. 预载 API：panelId 为主、location 为辅，均带存活参数

- `UniTask PreloadAsync(int panelId, float keepAliveSeconds = 0f, CancellationToken)`：走 `TryGetPanelConfig` + `ResolveLoader(config.LoaderKind)`，与打开同 loader（YooAsset / Resources）
- `UniTask PreloadAsync(string location, float keepAliveSeconds = 0f, CancellationToken)`：走 Resources loader，与 `OpenAsync(location)` 对齐（供无表面板使用）
- `UniTask PreloadAsync(IEnumerable<int> panelIds, float keepAliveSeconds = 0f, CancellationToken)`：批量预载，统一存活参数（差异化存活时间分多次调用）
- `keepAliveSeconds <= 0f` → **永久保留**（手动 `UnloadPreload` / `UnloadAllPreloads`）；`> 0f` → **存活 N 秒自动销毁**（见 D11）
- `bool UnloadPreload(string location)` / `void UnloadAllPreloads()` / `bool IsPreloaded(string location)`

### D3. 停放挂点：直接挂入所属 UIGroup 物理根，等待激活

- 预载即把实例挂入其 UIGroup 对应物理根（`DualUIGroupHelper.GetParent`，根选择按 `UIFormPanelConfig.CanvasMode`，与 `ApplyPanelConfig` 的最终归属一致），`SetActive(false)` 停放等待激活
- 打开时 `CreateUIForm` 的 `SetParent` 幂等（父节点已相同），**省去根节点切换**；组根挂在 UIComponent 持有的持久 Canvas 下，跨场景存活，停放实例不受场景卸载影响
- 停放实例未 `AddUIForm` 进组链表，不参与 `UIGroup.Update` / `Refresh` / 显示排序（inactive 且无 UIFormInfo），仅占物理挂点
- **前置**：预载要求所属 UIGroup 已注册；`UIComponent.PreloadAsync` 与 `OpenByPanelIdAsync` 一致，组不存在时先 `AddGroup`
- **理由**：用户确认——无需独立 PreloadRoot 节点；直接挂最终父节点省去打开时根切换，层级更简洁
- **备选**：独立 `PreloadRoot` 停放节点 → 拒绝（打开时多一次 reparent，层级冗余）

### D4. 首次打开时解锁并执行完整生命周期，回收后走正常过期

- `OpenAsync` 池复用路径命中预载停放实例时：`SetLocked(false)` + 移出 `m_PreloadedInstances`（停止存活计时）+ 以 `isNewInstance:true` 进入 `InternalOpenUIForm`（详见 D10）
- 之后关闭回池按普通实例过期/容量裁剪，避免"永远锁定"占内存；关后再开跳过 `OnInit`（与现有一致）
- 预载记录语义 = **未使用停放实例集合**，卸载仅针对它们

### D10. 预载实例首次打开走完整生命周期

- **选择**：预载期零业务生命周期；`OpenAsync` 首次复用停放实例时按新实例语义执行 `UIForm.OnInit(userData)`（`isNewInstance:true`）与 `OnOpen(userData)`，此时战斗数据已就绪且 `userData` 为真实值
- **理由**：用户确认——预载时业务数据（战斗上下文、场景对象）可能未就绪，`OnInit` 提前跑有出错风险；打开时执行则语义与正常新开完全一致，`UIForm.OnInit` 现有实现即"新实例才初始化逻辑"，仅需在池命中路径传 `isNewInstance:true`
- **备选**：预载时 `OnInit(null)` → 拒绝（数据缺失风险）；预载时存 userData 后置回放 → 复杂度高且无必要

### D11. 存活策略：永久保留 / 存活 N 秒自动销毁

- **选择**：`keepAliveSeconds <= 0f` → 永久保留（仅手动卸载）；`> 0f` → 预载完成起算，`UIManager.Update` 按 `realElapseSeconds` 倒计时，到期且未被打开 → 自动执行 `UnloadPreload`（Unspawn + 解锁 + `ReleaseObject`）
- 记录结构：`m_PreloadedInstances: Dictionary<string, PreloadEntry>`，`PreloadEntry { object InstanceTarget; float RemainingSeconds; }`（永久模式 `RemainingSeconds < 0f`）；`Update` 先收集到期 location 再统一卸载，避免遍历中修改字典
- 首次打开（D4/D10）：移出记录即停止计时；此后走普通池过期回收
- **理由**：用户确认——接口需支持两种存活方式：永久保留由流程手动关闭（如战斗面板随 `ProcedureBattle.OnLeave` 卸载）；定时模式供"可能用不上"的预载（如提示类面板）自动释放内存，避免长期占驻
- **备选**：只做永久 + 手动 → 不满足需求；复用对象池 `ExpireTime` 表达定时 → 池过期语义面向全体实例且与 Locked 冲突，拒绝

### D5. 在途互斥：预载中打开 / 打开中预载

- 新增 `m_UIFormsBeingPreloaded`（`HashSet<string>` location）记录在途预载
- `OpenAsync` 单例等待循环同步等待在途预载完成（对称于现有 `IsLoadingUIForm` 等待）：预载完成即已进池，随即复用
- `PreloadAsync` 前序检查：已打开（`GetUIForm(location) != null`）→ 跳过；已预载 → 跳过；在途打开（`IsLoadingUIForm`）→ 等待后跳过
- 预载失败：仅告警并移出记录，不抛给流程；打开时正常加载兜底

### D6. 预载启动窗口：与场景加载并行 + 软等待

- `SwitchAsync(..., IReadOnlyList<int> preloadPanelIds = null)`：LoadingPanel 打开后立即启动预载，与场景 suspend 加载并行（YooAsset 支持并发句柄）
- SwitchEnd 前 `UniTask.WhenAny(preloadTask, Delay(超时上限))`；超时 → 取消预载 + `UnloadAllPreloads`，打开走兜底
- 场景切换异常 / 取消：finally 取消预载 CT + 卸载
- 预载任务软失败：逐面板 try/catch，单面板失败不影响其余
- 当前面板无 Awake 场景依赖，并行启动安全；若未来面板 Awake 依赖场景对象，可退化为 ScenesReady 后启动（API 不变）

### D7. 预载清单：代码常量（v1）

- `ProcedureBattle.BattlePreloadPanelIds = { 1002 BattleHud, 1003 Joystick, 1005 Pause, 1006 Lose, 1007 Win, 1008 GemReward, 1009 Message }`，统一 `keepAliveSeconds = 0f`（永久保留，随 `ProcedureBattle.OnLeave` 卸载）；后续"可能不开"的面板可改配定时模式（D11）
- **理由**：零表结构变更、可快速验证；后续迁 UIPanel 表 `PreloadGroup` 列只换数据来源
- **备选**：UIPanel 表加列 → 本轮不做（后续变更）

### D8. 卸载语义

- `UnloadPreload(location)`：`Unspawn` → `SetLocked(false)` → `ReleaseObject`（Destroy 实例 + 释放 YooAsset 句柄）
- `UnloadAllPreloads()`：遍历预载记录逐个卸载；已打开实例不在记录中，不受影响
- 定时模式到期自动销毁复用 `UnloadPreload` 路径（D11）
- `ProcedureBattle.OnLeave` 调 `UnloadAllPreloads`：释放本场未使用的停放实例；已使用过的实例回池后按正常过期回收，可在下一场复用

### D9. 多开面板

- `AllowMultiInstance` 面板预载单个实例同样可复用（池 Spawn 语义一致）；并发多开时首个复用停放实例，其余走正常加载

## Risks / Trade-offs

- **[Risk] 停放实例被场景卸载连带销毁** → 挂入 UIComponent 持有 Canvas 下的组根规避（跨场景存活）
- **[Risk] 未锁定实例 10s 过期释放** → 预载即锁定（D1），打开时解锁（D4）
- **[Risk] 预载 Awake 早于场景对象存在**（Unity 机制：实例化 active prefab 时 Awake 必然执行，无法推迟到打开）→ `OnInit`/`OnOpen` 已后置到打开时执行，数据依赖风险仅剩余 Awake；当前面板无 `Awake` 覆写，若未来面板 Awake 依赖业务数据需另行约定，或退化为 ScenesReady 后启动（D6）
- **[Risk] 预载与打开并发导致双加载** → 在途互斥（D5）
- **[Risk] 停放实例内存占用**（最多 7 个）→ 出战斗卸载未使用停放（D8）；已使用回池按容量/过期裁剪
- **[Trade-off] 打开时仍有一次 Canvas 重建（1~2ms）** → v1 接受，后续可做离屏布局预热

## Migration Plan

1. 框架层实现：`IUIManager` / `UIManager` / `UIComponent`（D1–D5、D8、D10）
2. `ProcedureSceneSwitch.SwitchAsync` 可选参数与预载任务编排（D6）
3. `ProcedureBattle` 传清单 + OnLeave 卸载（D7、D8）
4. Play Mode 验证：进战斗 HUD/摇杆秒开（Profiler 对比打开耗时）；战斗中途首次开 Pause/Win/Lose/GemReward/Message 无卡顿；超时/取消/失败路径无停放泄漏；主页/商店等非预载面板行为回归
5. 回滚：还原框架与流程脚本即可；无表结构 / 存档格式变更

## Open Questions

- （无）用户已确认：实例级预载进池、不影响 `OpenAsync` 现有加载逻辑、预载范围 = HUD + Joystick + 战斗弹窗、代码常量清单、与场景加载并行启动、**预载不执行 OnInit、首次打开走完整生命周期（D10）**
