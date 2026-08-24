## Why

战斗中的子弹（玩家武器 `WeaponBase.CreateProjectile`、敌方 `EnemyAI_Tank_A` / `EnemyAI_RingShooter` / `EnemyAI_Boss_A`，以及 `ProjectileBase` 命中/超距自毁）全部走 `Instantiate` + `Destroy`：玩家 SMG 约 5 发/秒（强化后 15 发/秒），Boss 环射一轮最多 9 发且命中即销毁，每场战斗产生大量瞬时 GameObject 创建/销毁，带来 Instantiate/Destroy 与 GC 双重开销。

框架自带 `ObjectPoolComponent`（经 `GameFrameWork.ObjectPool` 访问），但它是纯 C# 的 `ObjectBase` 对象池，项目业务代码从未使用。且存在场景分离约束：框架组件挂在常驻 Home 场景（`GameFrameWork.unity` 中的 `GameFrameWork.prefab`，Home 场景被 `SceneComponent` 保护不可卸载），而战斗场景（Level 1-5）经 YooAsset 附加加载、切换时整体卸载——池化实例的生命周期必须跨场景设计，不能直接照搬 `Instantiate/Destroy` 到池上。

## What Changes

- 新增 AOT 侧 `GameObjectObject : ObjectBase`（GameObject 包装）：`OnSpawn` 激活、`OnUnspawn` 隐藏并回收到常驻池根节点、`Release` 时销毁实例。放 AOT 程序集（`GamePlay.AOT` 或 `GameFrameWork.RunTime`）以保持 `IObjectPool<GameObjectObject>` 泛型实例化全程 AOT，避免 hybridCLR 解释执行出现在弹道热点上。
- 新增热更侧 `BulletPool` 门面（`CommandoRobot` 命名空间）：
  - 按 prefab 惰性创建命名 SingleSpawn 池：`GameFrameWork.ObjectPool.CreateSingleSpawnObjectPool<GameObjectObject>(prefabName, capacity, expireTime)`，实例以 prefab 名注册，`Spawn(prefabName)` / `Unspawn(target)` 进出池；
  - 提供 `SpawnBullet(prefab, pos, fwd)` / `RecycleBullet(GameObject)`，并管理 `DontDestroyOnLoad` 的常驻池根节点——**池实例不挂在战斗场景下**，随场景卸载被销毁是本次改造的核心风险点。
- `ProjectileBase` 改造：
  - 超距/命中自毁点由 `Destroy(gameObject)` 改为回收；发射侧的 `Destroy(obj, 5/10)` 安全网改为延迟回收计时（回收时取消，防止重复回收）；
  - 新增"出池重置"契约：`Start()` 对池化对象只执行一次，`m_StartPosition` 等运行态字段必须在每次出池时重新初始化，且需恢复 prefab 初始默认值后再由调用方配置（`m_Speed` / `m_Damage` / `m_Range` / `m_Creator` / `m_IsEnemyTeam` / `m_TurnSpeed` / `m_DetachObject`）；
  - 入池清理：清空 `m_Creator`，`m_DetachObject` 先解除挂载再回收，避免跨场景引用残留。
- 改造 4 处发射点：`WeaponBase.CreateProjectile`、`EnemyAI_Tank_A.ShootBullets`、`EnemyAI_RingShooter.ShootRingBullet`、`EnemyAI_Boss_A.ShootBullet` / `ShootRingBullet`。
- 战斗退出兜底：`BulletPool.ClearBattleBullets()` 在战斗结束/场景切换时把仍在在途的子弹强制回收；否则场景卸载会销毁实例、池中残留 Spawned 幽灵条目（框架池不感知 Target 被场景销毁）。出池侧再做自愈检查：取出的包装对象 `Target == null` 时视为未命中并补建新实例。

## Capabilities

### New Capabilities

- `bullet-object-pool`: 基于框架 `ObjectPoolComponent` 的 GameObject 对象池化封装；跨场景（常驻 Home 场景 vs 战斗场景）池实例生命周期管理；子弹出池/入池重置契约与延迟回收语义。

### Modified Capabilities

- （无）现有 `openspec/specs/` 中无相关需求需改写。

## Impact

- 运行时（热更）：`WeaponBase`、`ProjectileBase`、`EnemyAI_Tank_A`、`EnemyAI_RingShooter`、`EnemyAI_Boss_A`；新增 `BulletPool`。
- 运行时（AOT）：新增 `GameObjectObject`（已定稿放置于 `GamePlay.AOT` 程序集，其引用 `GameFrameWork.RunTime`，热更侧可正常调用）。
- 框架复用：`GameFrameWork.ObjectPool`（`ObjectPoolComponent`）、`ObjectBase` / `IObjectPool<T>` 的命名池、`Spawn(name)`、`Unspawn(target)`、`ReleaseAllUnused`。
- 资源/场景：不改 prefab 与场景资产；不改变子弹速度/伤害/射程等既有配置与手感。
- 明确不做：命中特效/枪口特效等粒子的池化（同为高频 `Instantiate`，列为后续变更候选）；手雷 `PlayerGrenade`；敌人死亡特效与掉落物。
