# Design: 子弹对象池化（bullet-object-pool）

> 范围（已确认）：仅子弹（玩家/敌方全部子弹与 `ProjectileBase` 自毁路径），不含手雷与特效。
> 程序集（已确认）：`GameObjectObject` 放 `GamePlay.AOT`；`BulletPool` 门面放热更程序集。
> 兜底（已确认）：出站强制回收 + 出池自愈双保险。

## 1. 总体架构

```
热更（HotUpdate）                     AOT（GamePlay.AOT / GameFrameWork.RunTime）
─────────────                         ────────────────────────────────────────
WeaponBase / EnemyAI_*                GameObjectObject : ObjectBase
   │  SpawnBullet(prefab,pos,fwd)          │ 包装 GameObject
   ▼                                     │ OnSpawn → SetActive(true)
BulletPool（门面）      ──调用──▶  GameFrameWork.ObjectPool
   │ 每 prefab 一个命名 SingleSpawn 池      （ObjectPoolComponent，Home 场景常驻）
   │ 常驻池根 BulletPoolRoot（DontDestroyOnLoad）
   ▼
ProjectileBase（出池/入池重置契约，延迟回收计时）
```

- 池本体：`IObjectPool<GameObjectObject>`，`CreateSingleSpawnObjectPool<GameObjectObject>(prefabName, capacity, expireTime)`。
- 池内对象以 prefab 名注册（`m_Objects` 按名称索引，`Spawn(prefabName)` 命中）。
- 泛型实例化 `ObjectPool<GameObjectObject>` 的 T 为 AOT 类型 → 弹道热点全程 AOT，无 hybridCLR 解释执行。

## 2. GameObjectObject（AOT，GamePlay.AOT 程序集，namespace GamePlay.Pooling）

```csharp
public sealed class GameObjectObject : ObjectBase
{
    private static Transform s_PoolRoot;              // BulletPool 初始化，常驻根
    private GameObject _target;

    public static GameObjectObject Create(string name, GameObject target)
    {
        var obj = ReferencePool.Acquire<GameObjectObject>();
        obj.Initialize(name, target);
        return obj;
    }

    protected internal override void OnSpawn()
    {
        if (_target == null) return;                  // 自愈前提：实例已被场景卸载销毁
        _target.SetActive(true);
    }

    protected internal override void OnUnspawn()
    {
        if (_target == null) return;                  // 场景卸载后回收：只做登记侧清理
        _target.SetActive(false);
        if (s_PoolRoot != null)
            _target.transform.SetParent(s_PoolRoot, false);   // 脱离战斗场景，随常驻根存活
    }

    protected internal override void Release(bool isShutdown)
    {
        if (_target != null) UnityEngine.Object.Destroy(_target);
        Clear();
    }
}
```

要点：
- **所有对 `_target` 的触碰都必须空判**（Unity fake-null）：场景卸载销毁实例后，`Unspawn`/`Release` 仍可能被调用（出站回收路径），不能抛 MissingReference。
- `Initialize` 后不立即 `SetActive(false)`：新实例由门面以 inactive 状态创建（见 §4）。
- `Release` 走 `Object.Destroy`，由框架 `ReleaseAllUnused`/容量裁剪触发。

## 3. BulletPool（热更，namespace CommandoRobot）

```csharp
public static class BulletPool
{
    private static readonly Dictionary<string, IObjectPool<GameObjectObject>> s_Pools = new();
    private static readonly Dictionary<string, BulletTemplate> s_Templates = new(); // prefab 初始默认值快照
    private static readonly HashSet<GameObject> s_ActiveBullets = new();            // 出站回收遍历用
    private static Transform s_Root;                                                // BulletPoolRoot

    public static GameObject SpawnBullet(GameObject prefab, Vector3 pos, Vector3 fwd, float safetyLifetime);
    public static void RecycleBullet(GameObject bullet);
    public static void ClearBattleBullets();   // 战斗结束/场景卸载：出站强制回收
}
```

- `SpawnBullet`：
  1. 惰性建池/建根：`s_Root = new GameObject("BulletPoolRoot")`（随当前激活场景创建，**不做 DontDestroyOnLoad**，战斗结束后随场景卸载销毁）；`GameObjectObject.PoolRoot = s_Root`；`CreateSingleSpawnObjectPool<GameObjectObject>(prefab.name, 64, 60f)`。`s_Root` 静态引用经 Unity 假空判定在下次战斗自动重建。
  2. `var obj = pool.Spawn(prefab.name)`；`obj == null || obj.Target == null`（自愈）→ `Instantiate(prefab)`，置为 inactive，`pool.Register(GameObjectObject.Create(prefab.name, go), spawned: false)` 后再 `Spawn`。
  3. 出池重置：`ProjectileBase` 按 `BulletTemplate`（首次创建时快照的 prefab 序列化默认值）恢复 `m_Speed/m_Damage/m_Range/m_Radius/m_TurnSpeed/m_IsEnemyTeam/m_DetachObject/m_HitParticle`，再设 `m_StartPosition`；由调用方继续配置 `m_Creator/m_Speed/m_Damage/m_Range`。
  4. `s_ActiveBullets.Add(go)`；启动延迟回收计时（替代原 `Destroy(obj, 5/10)`，值保持 5/10）。
- `RecycleBullet`：防重入（`s_ActiveBullets` 命中才收，或 `ProjectileBase` 内 `m_IsRecycled` 标志）→ `ProjectileBase` 入池清理（先 `m_DetachObject.SetParent(null,true)` 解除挂载，清 `m_Creator`）→ `pool.Unspawn(go)` → 从 `s_ActiveBullets` 移除。
- `ClearBattleBullets`：遍历 `s_ActiveBullets`，对每个 `go` 调 `RecycleBullet(go)`（内部已空判）；随后 `GameFrameWork.ObjectPool.ReleaseAllUnused()` 裁剪空闲对象。挂接点：`GameControl.OnDestroy()`（战斗场景卸载必经之路，已有同款 `m_Current` 清理先例）+ 兜底在 `SpawnBullet` 自愈。

## 4. ProjectileBase 改造（出池/入池契约）

- `Start()` 仅首次激活执行一次 → `m_StartPosition = transform.position` 移入"出池重置"（§3-3 步骤），`Start` 只保留首次一次性逻辑。
- `Update` 中 `Destroy(gameObject)` 两处（超距、命中）→ `BulletPool.RecycleBullet(gameObject)`；`DetachOnHit` 保持先解除挂载再回收的顺序。
- 延迟回收计时：`Update` 累计 `m_TimeAlive += Time.deltaTime`，超过 `safetyLifetime` 且未被命中回收 → 自回收（替代 `Destroy(obj, t)` 语义，行为一致：超距先判、计时兜底）。
- 入池清理：`m_Creator = null`；`m_DetachObject` 已解除挂载则不触碰；`m_IsRecycled = true` 防双收。
- **`m_IsEnemyTeam` 必须恢复默认**：现调用方从不设置它，值来自 prefab 序列化；池化复用后若不清零，敌方子弹被玩家武器复用会保持敌方阵营（打自己人）。这就是 `BulletTemplate` 快照存在的意义。

## 5. 发射点改造（行为不变）

| 文件 | 原逻辑 | 改后 |
|---|---|---|
| `WeaponBase.CreateProjectile` | `Instantiate` + 配置 + `Destroy(obj,5)` | `SpawnBullet(prefab, pos, fwd, 5f)` + 配置 |
| `EnemyAI_Tank_A.ShootBullets` | 同上（10s） | `SpawnBullet(..., 10f)` |
| `EnemyAI_RingShooter.ShootRingBullet` | 同上（10s） | `SpawnBullet(..., 10f)` |
| `EnemyAI_Boss_A.ShootBullet/ShootRingBullet` | 同上（10s） | `SpawnBullet(..., 10f)` |

速度/伤害/射程/朝向计算逻辑原样保留，仅替换创建与销毁两个端点。

## 6. 跨场景生命周期（本设计的核心约束）

1. **池可达性**：`GameFrameWork.ObjectPool` 静态属性在 Home 场景 `GameFrameWork.Start()` 赋值，Home 场景受 `SceneComponent` 保护不可卸载 → 战斗任意时刻可访问，无需处理。
2. **池根与闲置实例随战斗场景**：`BulletPoolRoot` 在首次射击时创建于当前激活场景（战斗场景），不做 `DontDestroyOnLoad`；`OnUnspawn` 把闲置实例挂回池根。战斗结束场景卸载时，池根与闲置实例随场景一并销毁，内存自然回收；`s_Root` 静态引用经 Unity 假空判定在下一次战斗自动重建。
3. **幽灵防护（框架零修改）**：订阅 `SceneManager.sceneLoaded`（首次建池时注册一次；注：本引擎版本无 `sceneUnloading` 事件，编译实测）——切场景流程中新场景加载完成后旧场景才卸载（`ProcedureSceneSwitch` 时序），此时旧场景对象仍存活，`ClearBattleBullets` 全量回收在途子弹 + `ReleaseAllUnused` 清空池 → 场景卸载时池内已无条目，已销毁实例永远不会被 Home 场景的对象池持有。异常路径兜底：`GameControl.OnDestroy`（卸载期调用，已销毁实例的 Unspawn 抛异常被 catch，无害）+ 出池自愈（`Spawn` 时 Target 已销毁 → 注册新实例补建，幽灵条目占位但不影响功能）。
4. 禁止在池化对象上调用 `Destroy`（唯一例外是 `GameObjectObject.Release` 的最终销毁）。

## 7. 风险与验证

- 行为回归：五类发射点各射一轮，验证速度/伤害/射程/命中/超距回收与改造前一致；Boss 三阶段弹幕；玩家强化三连发。
- 跨场景：战斗中退回主菜单再进新关卡 → 无 MissingReference、无幽灵条目、池容量正常。
- 暂停/结算：`Time.timeScale=0` 下子弹 Update 停止（与现状一致），恢复后计时连续。
- 双收防护：命中回收与延迟计时竞争 → 无二次回收异常（`Unspawn` 对已回收对象会抛异常，需防重入）。
- 热更验证：AOT 包装类泛型链路、`GamePlay.AOT` 程序集引用关系在真机 hybridCLR 环境验证。

## 8. 已定决策（讨论结论）

- 池参数：`capacity=64`、`expireTime=60s`，验证期按实测调整。
- 出站回收挂接：仅 `GameControl.OnDestroy()`（战斗场景卸载必经路径），配合出池自愈作第二道保险；不订阅 `sceneUnloaded`，避免应用退出/编辑器停止时的重复触发。
- 门面形态：静态类 `BulletPool`，惰性初始化池与 `BulletPoolRoot`（`DontDestroyOnLoad`），与 `GameControl.m_Current` 模式一致，无场景依赖。
