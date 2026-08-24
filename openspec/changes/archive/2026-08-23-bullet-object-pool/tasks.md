## 1. AOT 层（GamePlay.AOT，namespace GamePlay.Pooling）

- [x] 1.1 新增 `GameObjectObject : ObjectBase`：静态工厂 `Create(name, target)`（ReferencePool 获取 + `Initialize`）；`OnSpawn` 对目标空判后 `SetActive(true)`；`OnUnspawn` 空判后 `SetActive(false)` + `SetParent(s_PoolRoot, false)`；`Release(isShutdown)` 空判后 `Object.Destroy` + `Clear()`；静态 `PoolRoot` 属性（由 `BulletPool` 初始化）
- [x] 1.1a **修复字段遮蔽 bug（实测发现）**：`GameObjectObject` 原先声明了与 `ObjectBase.m_Target`（private object）同名的 `private GameObject m_Target`，`Initialize` 写基类字段而 `OnSpawn/OnUnspawn/Release` 读子类字段（恒 null）→ 出池激活逻辑被空判短路、子弹始终 inactive。已删除子类字段，统一经基类 `Target` 属性 + `as GameObject` 访问（保留 Unity 假空判定），与框架 `UIFormInstanceObject` 的用法一致
- [x] 1.2 新文件已加入 `GamePlay.AOT.csproj`（Unity 下次打开会按 asmdef 重新生成，内容等价）；程序集引用链（GamePlay.AOT → GameFrameWork.RunTime）可用
- [x] 1.3 框架侧：**零修改**（讨论结论——方案：切场景前置全量回收，框架 `ObjectPoolManager` 保持原样，`target == null` 快速失败语义不变，不对底层做任何改动）
- [x] 1.4 引擎 API 实测发现：本机 Unity 2022.3.37f1c1 的 `UnityEngine.CoreModule` **无 `SceneManager.sceneUnloading` 事件**（编译实测 CS0117；`sceneLoaded` 存在）——场景挂接改用 `sceneLoaded`

## 2. 热更层：BulletPool 门面（namespace CommandoRobot）

- [x] 2.1 静态类 `BulletPool` 骨架：`s_Pools`（Dictionary<string, IObjectPool<GameObjectObject>>）、`s_Templates`（BulletTemplate 快照）、`s_ActiveBullets`（Dictionary<GameObject, string> 子弹→池名，兼作防重入与出站回收登记）、`s_Root`（BulletPoolRoot，惰性 `new GameObject` **随当前激活场景创建，不做 DontDestroyOnLoad**——战斗结束随场景卸载销毁，静态引用假空自动重建，赋给 `GameObjectObject.PoolRoot`）
- [x] 2.2 `SpawnBullet(GameObject prefab, Vector3 pos, Vector3 fwd, float safetyLifetime)`：惰性建池 `CreateSingleSpawnObjectPool<GameObjectObject>(prefabName, 64, 60f)` → `Spawn(prefabName)` → 自愈（返回 null 或 `Target == null` 时先清理幽灵条目：Unspawn 归零 + ReleaseObject 移除，再 `Instantiate` 置 inactive → `Register(spawned:false)` 后 Spawn）→ `BulletTemplate.ApplyTo` 恢复默认 → `ResetForSpawn(position)` → 设 `m_SafetyLifetime` → 设 transform → 登记 `s_ActiveBullets`
- [x] 2.3 `BulletTemplate`：首次建池时从 **prefab 资产**（非实例）捕获 `ProjectileBase` 序列化初始值（m_Speed / m_Damage / m_Range / m_Radius / m_TurnSpeed / m_IsEnemyTeam / m_HitParticle / m_DetachObject 子节点名），`ApplyTo` 恢复默认；脱离物按子节点名 `transform.Find` 重新挂接
- [x] 2.4 `RecycleBullet(GameObject)`：防重入（`s_ActiveBullets` 含才处理）→ `OnRecycle` 清理 → `pool.Unspawn(bullet)`（try/catch 容错，兼容已被场景销毁的实例）
- [x] 2.5 `ClearBattleBullets()`：遍历 `s_ActiveBullets` 调 `RecycleBullet` → `GameFrameWork.ObjectPool.ReleaseAllUnused()`（ObjectPool 为空时跳过）。**挂接改为 `SceneManager.sceneLoaded` 事件**（首次建池时订阅一次）：切场景流程中新场景加载完成后旧场景才卸载，此时旧场景对象仍存活，全量回收在途子弹 + 清空池 → 场景卸载时池内已无条目，幽灵对象不会被 Home 池持有；`GameControl.OnDestroy` 与出池自愈仅作异常路径兜底

## 3. ProjectileBase 出池/入池契约

- [x] 3.1 出池重置：移除 `Start()` 中的 `m_StartPosition` 初始化；新增 `ResetForSpawn(Vector3 startPos)`：设 `m_StartPosition`、清零 `m_TimeAlive`、置 `m_IsRecycled = false`、清 `m_Creator`
- [x] 3.2 自毁改回收：超距与命中路径 `Destroy(gameObject)` → `Recycle()`（命中路径保持 `CreateHitParticle` → `DetachOnHit` → 回收的顺序）
- [x] 3.3 延迟回收：`Update` 累计 `m_TimeAlive`，超过 `m_SafetyLifetime` 且未回收 → 自回收（替代调用方 `Destroy(obj, 5/10)`，时长保持 5/10）
- [x] 3.4 入池清理：`OnRecycle()` 清 `m_Creator`、清 `m_DetachObject`（已脱离的挂在世界中，仅断开引用）、置 `m_IsRecycled = true`，防双收
- [x] 3.5 **修复：命中后 particle-1 粒子场景永久残留（实测发现）**：`playerbullet-2/5` 的 `m_DetachObject` 指向循环粒子子物体（looping + stopAction=None 永不自毁），`DetachOnHit` 分离后无人销毁。修复：`OnRecycle` 对已分离（parent==null）且为 ParticleSystem 的脱离物执行 `Destroy`；未分离的随子弹回池复用
- [x] 3.6 **修复：复用实例粒子不重播**：PlayOnAwake 仅首次激活触发，回池再出池后粒子停在原时刻。修复：`ResetForSpawn` 对子粒子 `Clear + time=0 + Play(true)`（已分离实例无粒子自然跳过）

## 4. 发射点改造（行为不变）

- [x] 4.1 `WeaponBase.CreateProjectile`：`Instantiate`/`Destroy(obj,5)` → `SpawnBullet(prefab, pos, fwd, 5f)`（强化三连发路径复用同一方法；prefab 空判）
- [x] 4.2 `EnemyAI_Tank_A.ShootBullets` → `SpawnBullet(..., 10f)`
- [x] 4.3 `EnemyAI_RingShooter.ShootRingBullet` → `SpawnBullet(..., 10f)`
- [x] 4.4 `EnemyAI_Boss_A.ShootBullet` / `ShootRingBullet` → `SpawnBullet(..., 10f)`（抽 `FirePointPosition`/`SpawnBossBullet` 辅助方法，朝向计算逻辑保持）

## 5. 战斗退出挂接

- [x] 5.1 `GameControl.OnDestroy()` 调 `BulletPool.ClearBattleBullets()`（内部对 `GameFrameWork.ObjectPool` 空判，应用退出/编辑器停止安全）

## 6. 验证

- [x] 6.0 编译级验证：GameFrameWork.RunTime / GamePlay.AOT / HotUpdate 三程序集经 Roslyn csc 全量编译 0 错误（绕过本机损坏的 dotnet SDK 还原；警告均为验证构建引用旧 DLL 的 CS0436 假阳性，真实 Unity 构建不存在）
- [x] 6.1 行为回归：五类发射点各射一轮，速度/伤害/射程/命中/超距与改造前一致；Boss 三阶段弹幕；玩家强化三连发（用户编辑器实测通过）
- [x] 6.2 复用验证：连射后命中/超距子弹回池复用、无新增 Instantiate（用户编辑器实测通过）
- [x] 6.3 跨场景：战斗中退出战斗再进关卡 → 无 MissingReference、无幽灵条目、池清理干净（用户实测确认："正常清理干净了"）
- [x] 6.4 双收防护：命中回收与延迟计时竞争场景无异常（不重复 Unspawn）（用户编辑器实测通过）
- [x] 6.5 `m_IsEnemyTeam` 回归：玩家子弹不伤己、敌方子弹伤玩家（池化复用后阵营正确）（用户编辑器实测通过）
- [x] 6.6 暂停/结算：`timeScale = 0` 子弹冻结、恢复后计时连续（用户编辑器实测通过）
- [ ] 6.7 真机 hybridCLR：AOT 包装类链路与 `GamePlay.AOT` 引用关系真机验证；Profiler 对比改造前后 GC / Instantiate 次数（**非阻塞项，留待真机构建时验证**）

## 7. 收尾说明

- [x] 7.1 备注：命中特效/枪口特效/手雷（`PlayerGrenade`）池化为后续变更候选（见 proposal/design "明确不做"）
