# CommandoRobot

> Unity 3D 动作射击游戏 × 可热更新商业化框架的工程化实践

一个基于 **Unity 2022.3.37f1c1** 的 TPS射击游戏，核心价值在于以完整游戏为业务载体，自研搭建了一套**支持代码热更、资源热更、数据驱动UI 的架构**
`GameFramework 状态机 + HybridCLR 热更 + YooAsset 资源管理 + Luban 配表 + VContainer DI + R3/UniTask 异步响应式`。

---

## 目录

- [1. 架构总览](#1-架构总览)
- [2. 程序集划分：AOT / 热更边界](#2-程序集划分aot--热更边界)
- [3. 游戏流程管理（Procedure FSM）](#3-游戏流程管理procedure-fsm)
- [4. 热更新设计（HybridCLR）](#4-热更新设计hybridclr)
- [5. 资源管理（YooAsset）](#5-资源管理yooasset)
- [6. 场景管理（ProcedureSceneSwitch）](#6-场景管理proceduresceneswitch)
- [7. UI 框架](#7-ui-框架)
- [8. 配表系统（Luban）](#8-配表系统luban)
- [9. 异步与依赖注入（UniTask + R3 + VContainer）](#9-异步与依赖注入unitask--r3--vcontainer)
- [10. 技术栈清单](#10-技术栈清单)
- [11. 目录结构](#11-目录结构)

---

## 1. 架构总览

系统按 **AOT（不可热更）** 与 **HotUpdate（可热更）** 双层划分，所有业务逻辑位于热更层，AOT 层只保留"启动引导 + 框架服务"。

```
┌───────────────────────────── 热更层 HotUpdate.dll ─────────────────────────────┐
│  ProcedureMain / ProcedureBattle / ProcedureNavigator（流程）                  │
│  UI 层：MainHUD / BattleHUD / Pause / Win / Lose（Presenter + View）           │
│  玩法层：Player / AI / Weapon / Projectile / Pickup / Gameplay / AdMob         │
│  DI：GameDiInstaller（向 Game Scope 注入热更领域类型）                          │
└─────────────────────────────────────┬──────────────────────────────────────────┘
                                      │ 引用（禁止反向引用）
┌───────────────────────────── AOT 层（GamePlay.AOT.dll）────────────────────────┐
│  启动流程：Launch → AssetInit → HotUpdateInit → DataTableInit                  │
│  框架服务：ProcedureSceneSwitch / AssetPatch 状态机 / UIPanelConfigProvider    │
│  UI 框架：MVP（IPanelView / PanelPresenter）+ MVVM（MvvmUIFormLogic）          │
│  DI 容器：App / Game / Ui 三层 LifetimeScope                                   │
│  基础设施：GameFrameWork 框架、YooAsset、HybridCLR RuntimeApi、Luban           │
└─────────────────────────────────────┬──────────────────────────────────────────┘
                                      │ 独立程序集、保持 AOT
                        ┌─────────────▼─────────────┐
                        │  GamePlay.DataTable.dll   │
                        │  Luban 表类 + Adapters     │
                        └───────────────────────────┘
```

**核心设计原则：**

- 热更程序集引用 AOT，**AOT 禁止反向引用热更**，边界由程序集（asmdef）物理强制；
- 主流程（`ProcedureMain`）归属热更层，AOT 只负责"把热更流程加载并注册进状态机"；
- 配表代码独立成程序集（`GamePlay.DataTable`），因 Luban 生成代码涉及 AOT 泛型，**必须留在 AOT 侧**，避免热更元数据缺失。

---

## 2. 程序集划分：AOT / 热更边界

| 程序集 | 内容 | 归属 | 职责 |
| --- | --- | --- | --- |
| `GamePlay.AOT` | 启动流程、场景切换、AssetPatch、UI 框架、Sound、DI Scope | AOT | 提供框架服务与热更入口 |
| `GamePlay.DataTable` | Luban 生成的表类、Adapters、`LubanTablesFactory` | AOT | 配表数据层，禁止热更化 |
| `HotUpdate` | 全部游戏流程与玩法（Procedure/UI/Player/AI/Weapon…） | 热更 | 业务逻辑主体，可随版本迭代 |

> 依赖方向：`HotUpdate → GamePlay.AOT / GamePlay.DataTable`，**禁止反向引用**。
> 该边界由 openspec 规范（`openspec/specs/assembly-structure`）以需求形式固化，防止回归。

---

## 3. 游戏流程管理（Procedure FSM）

基于 GameFramework 的 FSM 状态机实现启动编排，每个流程持有独立 `CancellationTokenSource`，`OnLeave` 时取消未完成异步任务，保证流程切换安全：

**要点：**

- 每个 Procedure 都是 `ProcedureBase` 子类，享受 FSM 完整生命周期（`OnEnter/OnUpdate/OnLeave`）；
- 异步启动任务全部 `UniTask` 化，配合 `CancellationToken` 可在流程离开时即刻取消，杜绝"流程已切换、旧任务还在跑"的竞态；
- 启动链顺序严格：**YooAsset 就绪 → HybridCLR 初始化 → Luban 配表加载 → 游戏流程**。

---

## 4. 热更新设计（HybridCLR）

`ProcedureHotUpdateInit` 负责热更运行时初始化，**Editor 与真机双路径**：

| 阶段 | Editor | 真机 |
| --- | --- | --- |
| 程序集 | 直接 `AppDomain.GetAssemblies()` 取已编译的 `HotUpdate` | 从 YooAsset `DefaultRawPackage`（RawFileBuildPipeline）按寻址名 `HotUpdate.dll` 读取字节流，`Assembly.Load(byte[])` |
| AOT 补充元数据 | 跳过（程序集全量在域内） | 遍历 `AotMetadata` 目录资源，逐个 `RuntimeApi.LoadMetadataForAOTAssembly(..., HomologousImageMode.SuperSet)` |

**热更流程注册（反射 + 动态注入）：**

```
扫描 HotUpdate.dll 全部导出类型
   → 过滤出非抽象 ProcedureBase 子类
   → Activator.CreateInstance 实例化
   → ProcedureComponent.AddProcedure() 动态注册进 Procedure FSM
   → ProcedureDataTableInit 通过常规 ChangeState 切到热更主流程
```

**要点：**

- 新增流程/玩法**无需改动 AOT 代码**，热更层"自发现、自注册"；
- 热更 DLL 与 AOT 元数据都作为 YooAsset RawFile 资源下发，**代码热更复用资源热更通道**，一套补丁管线通吃；
- 泛型等 AOT 缺失元数据由 `SuperSet` 模式补齐，兼顾兼容性与性能；
- 编辑器下走域内程序集、真机走字节码加载，两套路径由 `#if UNITY_EDITOR` 隔离，日常开发零额外成本。

---

## 5. 资源管理（YooAsset）

YooAsset 作为统一资源层，承载场景加载/卸载、UI 面板、热更 DLL 与 AOT 元数据在内的全部资源生命周期。

### 5.1 应用

- **多 Package 分治**：常规游戏资源走主包，**HybridCLR 的 `HotUpdate.dll` 与 AOT 补充元数据走独立 RawFile 包**（`DefaultRawPackage`）——两类资源职责单一、可独立下发，**代码热更复用资源热更通道**，一套补丁管线通吃；
- **统一注入、按 location 寻址**：资源包就绪后统一注入 UI / Scene / Sound 组件，上层所有加载（面板 / 场景 / 音效）只认 location，**不感知资源来源与加载器实现**；
- **加载方式表驱动**：UI 面板的加载来源（`ResourcesLoader / YooAssetLoader`）由 UIPanel 表字段控制，同一套 `OpenAsync` 接口兼容两种加载器，出包 / 调试切换零代码；
- **编辑器 / 真机双模式**（`YooPlayMode`）：编辑器走资源直读（改资源即生效），真机走 Bundle 构建，业务代码零差异；
- **场景生命周期一体化**：场景的挂起加载 / 激活 / 卸载全部由 `SceneHandle` 驱动，卸载自动释放句柄引用计数，与场景字典状态强一致（详见第 6 节）。

### 5.2 与 UniTask 配合：异步加载优势

- **Handle → UniTask 无缝转换**：YooAsset 的 `AssetHandle / SceneHandle / AsyncOperation` 均可 `ToUniTask()`，资源加载彻底异步化，主线程零阻塞；
- **进度实时回传**：`ToUniTask(progress)` 把加载进度经 `IProgress<float>` 实时上报，场景切换据此聚合多场景进度驱动 LoadingUI；
- **取消贯穿到资源层**：`ToUniTask(cancellationToken)` 让加载可被流程级 `CancellationToken` 取消——流程离开即刻中止资源加载，配合每个 Procedure 独立的 CTS（`OnLeave` 统一 Cancel），杜绝"流程已切换、资源还在后台加载"的竞态；
- **热更加载实例**：真机下从 RawFile 包 `LoadAssetAsync<RawFileObject>` 读取 `HotUpdate.dll` 字节流，await 完成后 `Assembly.Load` 注入热更程序集——资源加载、补丁下载、代码热更共用同一套异步通道。

---

## 6. 场景管理（ProcedureSceneSwitch）

自研的场景组切换服务，**统一承载菜单 ↔ 战斗的全部场景加载**，是流程层与场景层的唯一桥梁：流程只需声明"目标场景组 + 激活场景"，加载顺序、进度、激活归属、卸载时机全部收敛。

### 6.1 切换流程

五阶段管线：

```
① 挂起加载（suspend，并行） → ② 回到 Home 场景 → ③ 统一激活
→ ④ 卸载组外旧场景 → ⑤ 激活目标场景 → ScenesReady → SwitchEnd
```

**① 挂起加载**：对目标组内缺失的场景并行发起挂起加载（`allowSceneActivation = false`）——Unity 在此模式下只完成数据搬运、进度停在 0.9，场景对象不实例化、不执行任何脚本；`SceneComponent` 为每个场景先登记句柄与 Loading 状态再 await（保证 `AllowSceneActivation` 随时可取到句柄），并聚合各场景进度实时上报 LoadingUI（平滑追向 89% 目标）；全部就绪（阈值 0.89，120s 超时兜底）后进入下一阶段。

**② 回到 Home**：将激活场景切回框架常驻主场景（GameFrameWork.unity），作为过渡期"安全区"——新场景尚未就绪、旧场景即将退场，此窗口内任何代码都不依赖业务场景的激活状态。

**③ 统一激活**：对全部挂起场景调用 `AllowSceneActivation`，场景随即进入激活流程：下一帧起实例化对象 → 全部 `Awake` → `sceneLoaded` 事件（`Awake` 之后、`Start` 之前）→ 各场景 `Start`。激活归属采用**"只提升一次"策略**：`sceneLoaded` 按完成顺序触发，但只有**目标场景自身完成 Awake** 时才将激活场景置为它，且此后不再变更——任何场景的 `Start` / 根级 `Instantiate` 只可能落在 Home 或目标场景，脚本初始化落点完全确定。

**④ 卸载组外旧场景**：全部加载完成后，对不在目标组内的已加载场景经 YooAsset 逐个卸载（成功即释放句柄引用，引用计数与字典同步）；此时 Unity 场景管线空闲，卸载即时执行。

**⑤ 收尾**：显式激活目标场景（幂等）→ 触发 `ScenesReady` → LoadingUI 动画播完（最短展示 1.5s）→ `SwitchEnd` → 关闭 LoadingUI，切换完成。

### 6.2 设计亮点

- **激活归属确定性收敛**：多场景组下激活场景不随完成顺序漂移，统一收敛至目标场景，保证各场景脚本初始化（`Start` / `Instantiate`）的落点确定；
- **卸载状态强一致**：卸载操作一旦启动必须等待执行到终点（不挂接外部取消），字典状态与真实场景始终同步；操作成败显式校验，失败时恢复场景状态并上抛，杜绝"幽灵场景"与"重复实例"两类状态失真；
- **全程可取消**：`CancellationToken` 贯穿加载 / 激活 / 卸载，流程离开即中止，不残留后台改场景状态；
- **进度与体验**：多场景并行加载 + 聚合进度 + LoadingUI 平滑动画与最短展示时长，避免进度跳变与闪屏；
- **幂等短路**：目标场景组已加载时仅激活目标场景即返回，重开战斗零成本复用场景；
- **状态机守护**：`SceneComponent` 以 Loading / Loaded / Unloading 三态跟踪每个场景，统一拦截非法操作（重复加载、卸载未加载场景、卸载常驻 Home 场景）；
- **事件驱动**：`SwitchBegin / SwitchProgress / ScenesReady / SwitchEnd / SwitchFailed` 全生命周期事件，UI 与埋点可监听。

---

## 7. UI 框架

### 7.1 面板生命周期管理（源于 GameFrameWork）

面板生命周期由 **GameFrameWork 的 UIFormLogic 体系**提供（框架内嵌于 `ThirdParty/GameFrameWork`）：面板实例池化复用，打开 / 关闭 / 回收三态驱动以下回调，业务绑定全部挂在生命周期回调上：

```
Open    →  UIFormLogic.OnOpen    →  业务初始化（Presenter.Attach / ViewModel.Bind）
Update  →  UIFormLogic.OnUpdate  →  业务刷新（Presenter.Tick）
Close   →  UIFormLogic.OnClose   →  业务清理（Presenter.Detach / ViewModel.Unbind + Dispose）
Recycle →  池化回收兜底检查，防"关了没拆"的泄漏
```

- **池化复用**：面板关闭后实例入对象池，再次打开优先复用（UIManager 池化 + 单例语义），Logic 不持有跨面板状态；
- **异步等待打开**：`OpenAsync` 返回 `UniTask<IUIForm>`，流程层可 await 面板就绪后再继续。

### 7.2 MVP & MVVM 实现：VContainer + R3 耦合进生命周期

**MVP（面向命令型面板）**：View 即 `UIFormLogic` 实现 `IPanelView`（如 `MainHUDUIFormLogic`），Presenter 继承 `PanelPresenter<TView>`、构造注入领域数据（`DataStorage / GameplayData`）：

- `OnOpen` → `LifetimeScope.Find<UiLifetimeScope>().Container.Resolve<MainHUDPresenter>()` → `Presenter.Attach(this)`；
- `OnUpdate` → `Presenter.Tick()`；`OnClose` → `Presenter.Detach()`；
- **耦合点**：Attach / Detach 与 UIForm 的 Open / Close 严格对齐——面板开、Presenter 建；面板关、Presenter 拆，生命周期由框架托管而非手动管理。

**MVVM（面向数据密集面板）**：`MvvmUIFormLogic<TViewModel>` + `ViewModelBase`（纯 C#）：

- `OnOpen` → 从 `UiLifetimeScope` Resolve ViewModel → `OnBind` 建立绑定；`OnClose` → `OnUnbind` + 绑定袋 `Dispose` + `ViewModel.Dispose`；`OnRecycle` 兜底清理；
- **R3 的落点**：`ViewModelBase` 以 R3 `ReactiveProperty<T>` 暴露数据、`DisposableBag` 收纳全部订阅（`CreateProperty` / `AddDisposable`），Logic 侧 `AddBinding` 收纳界面绑定——**订阅随面板生命周期自动释放，杜绝 `+=` 泄漏**；
- **VContainer 的落点**：Presenter 经 `GameDiInstaller.InstallPresenters` 注册进 Ui Scope（HybridCLR 之后延迟 Build），ViewModel 由同一容器按需 Resolve，领域依赖从 Game Scope 解析——**容器解析发生在 OnOpen、释放跟随 OnClose**，池化 UIFormLogic 不 Inject、只 Resolve，不被容器强引用。

### 7.3 异步加载安全（YooAsset + UniTask）

- **全链路异步 + 可等待**：`OpenAsync` 返回 `UniTask<IUIForm>`，加载不阻塞主线程，流程层可"等面板就绪再继续"；
- **取消贯穿**：`CancellationToken` 贯穿 UIManager → Loader → YooAsset Handle（`ToUniTask().AttachExternalCancellation(ct)`），流程离开即刻中止在途打开；UIComponent 还会与组件销毁 token 链接（`CreateLinkedTokenSource`），框架销毁时自动取消全部在途加载；
- **句柄引用计数安全**：`YooAssetUIFormLoader` 中取消 / 失败路径 `catch` 里 `handle.Release()` 归还引用计数、成功后显式检查 `Status`；句柄释放进一步封装为 `ReleaseAsset` 闭包，**随对象池回收自动归还**，任何路径都不泄漏；
- **在途去重与单例**：同一 location 在途加载期间，重复请求等待加载结束后复用（`IsLoadingUIForm` 轮询），失败 / 取消路径统一清理在途记录并释放资源。

### 7.4 表驱动与热更界面

- **表驱动打开**：面板配置（prefab location、所属 Group、遮挡暂停、Canvas 模式、排序、加载方式）全部来自 Luban UIPanel 表，由 `UIPanelConfigProvider` 运行时查表转为框架配置：

```csharp
await GameFrameWork.UI.OpenAsync(panelId, userData, cancellationToken); // 按表 id
await GameFrameWork.UI.OpenAsync(location, group, pause, userData, ct);  // 按 location
```

- 加载方式由表字段 `Loader` 驱动（`ResourcesLoader / YooAssetLoader`），**新增面板 = 配一行表 + 出包，零代码**；
- **热更界面**：面板 Prefab 随资源包热更下发，UIFormLogic / Presenter 位于热更程序集，**界面逻辑与表现均可随版本热更**；启动期（配表未加载）的初始化面板走固定 `Resources` 路径，启动链路不依赖任何外部数据。

### 7.5 业务支撑

**层级管理（表驱动 + 双 Canvas + UI 组栈）**

- **调层级 = 改表，零代码**：面板层级完全由 Luban UIPanel 表驱动（`Id / Location / GroupName / CanvasMode / SortOrder / PauseCoveredUIForm / Loader`），`UIPanelConfigProvider` 运行时查表转为 `UIFormPanelConfig`；面板 id 统一收敛在 `UIPanelIds` 常量类，流程层按 id 打开，不散落字符串/魔法数字；
- **物理双 Canvas**：面板挂载由 `CanvasMode`（或 `UIForm.CanvasKind`）决定走 Overlay / Camera 任一物理根，跨 Canvas 显示序由 `UIConfig` 的 `sortingOrder` 与表字段 `SortOrder` 共同控制；
- **逻辑组栈**：`UIComponent.AddGroup` 按组名注册（组深度 + 打开策略 `UIGroupOpenMode`：`ClearToTop` 关顶置顶 / `Normal` 单例 Refocus），同组内按打开顺序入栈；表字段 `PauseCoveredUIForm` 控制上层面板打开时下层是否暂停（覆盖即暂停/恢复），弹窗压住 HUD 时 HUD 自动停更，关闭弹窗自动恢复。

**预加载（PreloadAsync）**

- `UIComponent.PreloadAsync(panelId / location / 批量, keepAliveSeconds)`：提前实例化并停放（`SetActive(false)` 入实例池），**正式打开时零加载延迟**；`keepAliveSeconds` 控制停放驻留时长，超时自动回收；
- 配套管理：`UnloadPreload` / `UnloadAllPreloads` / `IsPreloaded`；预载自动确保面板所属 UI 组已注册（`EnsurePanelGroups`），按表 id 预载无需手动建组；
- 典型场景：Loading 阶段预载战斗 HUD / 弹窗，进战斗或点击瞬间秒开，不打断流程。

**SafeArea（刘海屏 / 挖孔屏适配）**

- **组件 `SafeAreaFitter`**（AOT 程序集）：挂在面板根节点或内容节点，按 `Screen.safeArea` ∪ `Screen.cutouts` 折算到画布坐标系，**锚点保持**（只叠加 `offsetMin/offsetMax` 增量，不破坏美术布局）；`Edges` 逐边控制（内容贴哪条屏幕边勾哪条）、`Expand` 全屏背景铺满、`Padding` 额外留白；转屏/分辨率变化自动重算，UIForm 对象池复用安全；
- **服务 `SafeAreaProvider`**：全局安全区缓存 + 脏检查 + `Changed` 事件，代码动态布局可直接订阅取归一化 insets；
- **调试 `SafeAreaDebug`**：运行时红遮罩标注非安全区（刘海/挖孔/手势条）+ 实时数据，真机验收用（发布前移除）；
- **编辑器验证**：Device Simulator（`com.unity.device-simulator.devices`）真实模拟 `Screen.safeArea/cutouts`，配合 Scene 参考线 `Tools/Safe Area/Scene Overlay` 预览；详见 `Assets/Project/Scripts/Runtime/AOT/UI/SafeArea/README.md`。

---

## 8. 配表系统（Luban）

- 四张业务表：`Sound / SoundGroup / UIPanel / Weapon`，**双数据模式**（JSON 调试 / Binary 发布）由 `LubanDataMode` 切换，同一套 `LubanTablesFactory` 双路径生成；
- **接口化访问**：`ISound / ISoundGroup / IUIPanel / IWeapon` 接口 + Adapter 封装 Luban 生成的 `Tables`，上层组件（UI/Sound/武器）只依赖接口，**换表实现不影响业务代码**；
- 配表加载通过 `LubanConfigComponent.LoadAsync` 统一异步化，加载完成后由 AOT 侧回调驱动：
  - `SoundConfigProvider / SoundGroupConfigProvider` → 声音系统按 ID 播放、声音组/mixer 路由；
  - `UIPanelConfigProvider` → UI 面板打开；
  - 配表就绪后 `InitSoundGroupsFromTable` 补齐启动期无法建立的 SoundGroup；
- 表代码生成独立程序集（`GamePlay.DataTable`），AOT 保持，避免热更化风险。

---

## 9. 异步与依赖注入（UniTask + R3 + VContainer）

### 9.1 异步流（UniTask + R3）

- **全链路 UniTask**：启动流程、补丁、场景切换、UI 打开全部异步化；每个流程持有 `CancellationTokenSource`，生命周期结束统一 Cancel + Dispose；
- **取消贯穿到底**：`CancellationToken` 透传到 YooAsset Handle（`ToUniTask(token)`）、场景加载、配表加载，`OperationCanceledException` 静默收敛，不污染日志；
- **R3 响应式**：MVVM 绑定、跨模块事件订阅统一走 R3 流，绑定对象进 `DisposableBag` 随面板生命周期自动释放，杜绝 `+=` 泄漏。

### 9.2 三层 DI 容器（VContainer）

```
AppLifetimeScope（父）→ GameLifetimeScope（子）→ UiLifetimeScope（孙）
```

- **App Scope**：应用级常驻（ScriptableObject 数据袋 `DataStorage / GameplayData` 的持有者）；
- **Game Scope**：**HybridCLR 之后**由热更侧创建（`GameDiInstaller.EnsureInitialized`），注册热更领域类型 —— 因为 AOT 容器无法 Register 热更类型，**DI 构建时机与热更初始化顺序强绑定**，这是架构联动的关键细节；
- **Ui Scope**：延迟 Build，从 Game Scope 解析领域依赖，为面板 Resolve Presenter/ViewModel；
- 池化 UIFormLogic **不 Inject、只 Resolve**，避免池化对象被容器强引用导致生命周期失控。

---

## 10. 技术栈清单

| 类别 | 技术 | 用途 |
| --- | --- | --- |
| 引擎 | Unity 2022.3.37f1c1 (LTS) | 3D 游戏 |
| 框架 | GameFramework（自研扩展） | FSM、Procedure、UIForm、Event、Data、Sound 模块化 |
| 代码热更 | HybridCLR | 热更程序集 + AOT 补充元数据 |
| 资源热更 | YooAsset | 多 Package 补丁、RawFile 包承载 DLL、场景加载/卸载 |
| 配表 | Luban | 表生成、JSON/Binary 双模式、接口化适配 |
| DI | VContainer | 三层 LifetimeScope、跨 AOT/热更注入 |
| 异步/响应式 | UniTask / R3 | 全链路异步、取消传播、响应式绑定 |
| AI规范 | openspec（spec-driven） | 架构边界以需求场景形式固化，防回归 |

---

## 11. 目录结构

```
Assets/
├─ Project/
│  ├─ Scripts/
│  │  ├─ Runtime/
│  │  │  ├─ AOT/                        # GamePlay.AOT 程序集
│  │  │  │  ├─ Procedure/               # 启动流程 + ProcedureSceneSwitch + AssetPatch
│  │  │  │  ├─ UI/                      # UI 框架：MVP / MVVM / Provider / LoadingUI
│  │  │  │  ├─ Sound/                   # 配表驱动的声音 Provider
│  │  │  │  └─ DI/                      # App / Game / Ui LifetimeScope
│  │  │  └─ HotUpdate/                  # HotUpdate 程序集（全部业务）
│  │  │     ├─ Procedure/               # ProcedureMain / Battle / Navigator / LevelSceneLocations
│  │  │     ├─ UI/                      # MainHUD / BattleHUD / Pause / Win / Lose（MVP）
│  │  │     ├─ Player/  AI/  Weapon/    # 玩法：角色、AI、武器（RPG/Shotgun）
│  │  │     ├─ Projectiles/ Pickup/     # 弹道、拾取物
│  │  │     ├─ Gameplay/                # GameControl / LevelBase / DamageControl ...
│  │  │     ├─ Admob/                   # 激励视频广告
│  │  │     └─ DI/                      # 热更侧 DI 安装器
│  │  └─ Editor/                        # HybridCLR DLL 拷贝、YooAsset 缓存清理工具
│  ├─ Scenes/                           # MainMenu + Level 1~5
│  └─ ThirdParty/                       # GameFrameWork 框架源码（内嵌）
├─ DataTable/                           # GamePlay.DataTable 程序集（Luban 生成 + Adapters）
├─ HybridCLRGenerate/                   # HybridCLR 生成的桥接/补充元数据
├─ StreamingAssets/                     # YooAsset 初始资源
├─ Tools/
│  └─ DataTablesTool/                   # Luban 配表工具链
│     ├─ DataTables/                    # 表工程：Datas（表数据）/ Defines（表定义）/ luban.conf
│     │                                 #   生成脚本：gen_json / gen_bin / gen_adapters
│     └─ Luban/                         # Luban 工具本体（含代码生成模板）
└─ openspec/
   ├─ specs/                            # 生效架构规范（assembly-structure / battle-procedure /
   │                                    #   hybridclr-init / procedure-navigation）
   └─ changes/                          # 变更记录：proposal → design → tasks → specs（含 archive 归档）
```

配表链路：`Tools/DataTablesTool`（Luban 生成 JSON/Binary 表 + Adapters）→ `Assets/DataTable`（表代码程序集）→ 运行时 `LubanTablesFactory` 加载。

openspec 以需求场景（Gherkin 风格）固化架构边界（程序集结构 / 战斗流程 / HybridCLR 初始化 / 流程导航），变更走 `proposal → design → tasks → specs` 流程并归档于 `changes/`。
