using System;
using System.Collections.Generic;
using GameFramework;
using GamePlay.Pooling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CommandoRobot
{
    /// <summary>
    /// 子弹对象池门面：基于框架 ObjectPoolComponent（常驻 Home 场景）的 GameObject 池化。
    /// 每 prefab 一个命名 SingleSpawn 池；池实例统一挂在 DontDestroyOnLoad 常驻根下，
    /// 与战斗场景解耦（战斗场景卸载不会销毁闲置实例）。
    /// 跨场景兜底：战斗退出强制回收（ClearBattleBullets）+ 出池自愈（Target 已销毁时清理幽灵并补建）。
    /// </summary>
    public static class BulletPool
    {
        private const int PoolCapacity = 64;
        private const float PoolExpireTime = 60f;

        private static readonly Dictionary<string, IObjectPool<GameObjectObject>> s_Pools = new Dictionary<string, IObjectPool<GameObjectObject>>();
        private static readonly Dictionary<string, BulletTemplate> s_Templates = new Dictionary<string, BulletTemplate>();
        private static readonly Dictionary<GameObject, string> s_ActiveBullets = new Dictionary<GameObject, string>();
        private static Transform s_Root;
        private static bool s_SceneHookSubscribed;

        /// <summary>
        /// 从池中取一颗子弹并完成出池重置；池空或实例已被场景卸载销毁时自动补建（自愈）。
        /// 调用方在返回后按需配置 m_Creator / m_Speed / m_Damage / m_Range。
        /// </summary>
        /// <param name="prefab">子弹 prefab（按 prefab 名建池与登记）。</param>
        /// <param name="position">出生位置。</param>
        /// <param name="forward">出生朝向。</param>
        /// <param name="safetyLifetime">安全存活时长（替代 Destroy(obj, t) 兜底）。</param>
        /// <returns>出池并重置后的子弹 GameObject；失败返回 null。</returns>
        public static GameObject SpawnBullet(GameObject prefab, Vector3 position, Vector3 forward, float safetyLifetime)
        {
            if (prefab == null)
            {
                return null;
            }

            EnsureRoot();
            string poolName = prefab.name;
            IObjectPool<GameObjectObject> pool = GetOrCreatePool(poolName);
            if (pool == null)
            {
                return null;
            }

            GameObject bullet = SpawnOrCreateInstance(pool, poolName, prefab);
            if (bullet == null)
            {
                return null;
            }

            // OnSpawn 会在旧世界坐标先激活；立刻关掉再设位，避免 World 粒子/拖尾在错误位置预热出假轨迹
            bullet.SetActive(false);
            bullet.transform.SetParent(null, false);
            bullet.transform.position = position;
            bullet.transform.forward = forward;

            ProjectileBase projectile = bullet.GetComponent<ProjectileBase>();
            if (projectile != null)
            {
                GetOrCreateTemplate(prefab, poolName).ApplyTo(projectile);
                projectile.ResetForSpawn(position);
                projectile.m_SafetyLifetime = safetyLifetime;
            }

            bullet.SetActive(true);
            if (projectile != null)
            {
                // 激活后（含 PlayOnAwake）再清一次并重播，保证特效从正确出生点开始
                projectile.ResetVisualEffects();
            }

            s_ActiveBullets[bullet] = poolName;
            return bullet;
        }

        /// <summary>
        /// 回收子弹到池（幂等）。实例已被场景卸载销毁时仅做登记与池条目清理，不触碰实例。
        /// </summary>
        public static void RecycleBullet(GameObject bullet)
        {
            if (!s_ActiveBullets.TryGetValue(bullet, out string poolName))
            {
                return;
            }

            s_ActiveBullets.Remove(bullet);

            if (bullet != null)
            {
                ProjectileBase projectile = bullet.GetComponent<ProjectileBase>();
                if (projectile != null)
                {
                    projectile.OnRecycle();
                }
            }

            if (GameFrameWork.ObjectPool != null && s_Pools.TryGetValue(poolName, out IObjectPool<GameObjectObject> pool))
            {
                try
                {
                    // 包装对象对已销毁 Target 空判；已销毁实例仍可按引用完成 Unspawn（幽灵条目清理）
                    pool.Unspawn(bullet);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(Utility.Text.Format("[BulletPool] Recycle bullet '{0}' failure: {1}", bullet != null ? bullet.name : poolName, ex.Message));
                }
            }
        }

        /// <summary>
        /// 战斗退出/场景切换：强制回收在途子弹并裁剪空闲对象，避免池中残留幽灵条目。
        /// 应用退出/编辑器停止时 ObjectPool 可能已销毁，全部路径容错。
        /// </summary>
        public static void ClearBattleBullets()
        {
            if (s_ActiveBullets.Count == 0)
            {
                return;
            }

            List<GameObject> bullets = new List<GameObject>(s_ActiveBullets.Keys);
            for (int i = 0; i < bullets.Count; i++)
            {
                RecycleBullet(bullets[i]);
            }

            if (GameFrameWork.ObjectPool != null)
            {
                GameFrameWork.ObjectPool.ReleaseAllUnused();
            }
        }

        /// <summary>
        /// 订阅场景加载完成事件（首次建池时注册一次）：切场景流程中新场景加载完成后旧场景才卸载，
        /// 此时旧场景对象仍存活，全量回收在途子弹并清空池，避免已销毁实例的条目残留在 Home 对象池。
        /// 注：本引擎版本 UnityEngine.CoreModule 无 sceneUnloading 事件（编译实测），故用 sceneLoaded。
        /// </summary>
        private static void EnsureSceneHook()
        {
            if (s_SceneHookSubscribed)
            {
                return;
            }

            s_SceneHookSubscribed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearBattleBullets();
        }

        private static void EnsureRoot()
        {
            // 池根随当前激活场景（战斗场景）创建，不做 DontDestroyOnLoad：
            // 战斗结束场景卸载时，闲置实例随池根一并销毁，内存随场景自然回收；
            // 池条目残留由出池自愈（Spawn 时 Target 已销毁 → 清理幽灵并补建）与
            // 出站回收（ClearBattleBullets）兜底。s_Root 为静态引用，场景卸载后
            // Unity 假空判定（== null）自动触发下次重建。
            EnsureSceneHook();
            if (s_Root != null)
            {
                return;
            }

            GameObject rootGo = new GameObject("BulletPoolRoot");
            s_Root = rootGo.transform;
            GameObjectObject.PoolRoot = s_Root;
        }

        private static IObjectPool<GameObjectObject> GetOrCreatePool(string poolName)
        {
            if (s_Pools.TryGetValue(poolName, out IObjectPool<GameObjectObject> pool))
            {
                return pool;
            }

            ObjectPoolComponent objectPool = GameFrameWork.ObjectPool;
            if (objectPool == null)
            {
                Debug.LogError("[BulletPool] ObjectPoolComponent is missing.");
                return null;
            }

            pool = objectPool.CreateSingleSpawnObjectPool<GameObjectObject>(poolName, PoolCapacity, PoolExpireTime);
            s_Pools.Add(poolName, pool);
            return pool;
        }

        private static BulletTemplate GetOrCreateTemplate(GameObject prefab, string poolName)
        {
            if (!s_Templates.TryGetValue(poolName, out BulletTemplate template))
            {
                template = BulletTemplate.Capture(prefab);
                s_Templates.Add(poolName, template);
            }

            return template;
        }

        private static GameObject SpawnOrCreateInstance(IObjectPool<GameObjectObject> pool, string poolName, GameObject prefab)
        {
            // 循环清理幽灵条目：池内可能残留多个已被场景卸载销毁的注册项（旧战斗场景的
            // 闲置实例随池根销毁后包装仍在池中），必须逐个清到能拿到有效实例为止，
            // 否则第一发会命中幽灵返回 null，表现为子弹未激活/不可见。
            for (int attempt = 0; attempt < 8; attempt++)
            {
                GameObjectObject pooled = pool.Spawn(poolName);
                GameObject bullet = pooled != null ? pooled.Target as GameObject : null;
                if (bullet != null)
                {
                    return bullet;
                }

                // 幽灵条目：target 已被销毁。先 Unspawn 使 SpawnCount 归零，再 ReleaseObject 移除，继续尝试。
                if (pooled != null)
                {
                    try
                    {
                        pool.Unspawn(pooled);
                        pool.ReleaseObject(pooled);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(Utility.Text.Format("[BulletPool] Clean ghost bullet '{0}' failure: {1}", poolName, ex.Message));
                    }
                }

                // 池空（无闲置可拿）且无幽灵可清：退出循环补建新实例
                break;
            }

            GameObject fresh = UnityEngine.Object.Instantiate(prefab);
            fresh.SetActive(false);
            pool.Register(GameObjectObject.Create(poolName, fresh), false);
            GameObjectObject spawned = pool.Spawn(poolName);
            return spawned != null ? spawned.Target as GameObject : null;
        }

        /// <summary>
        /// prefab 序列化初始值快照：出池时恢复默认，避免池化复用后运行态字段污染（如 m_IsEnemyTeam 阵营串味）。
        /// 脱离物（m_DetachObject）按子节点名恢复：命中解除挂载后该实例不再具备脱离物（视觉降级，不报错）。
        /// </summary>
        private sealed class BulletTemplate
        {
            public float Speed;
            public float Damage;
            public float Range;
            public float Radius;
            public float TurnSpeed;
            public bool IsEnemyTeam;
            public GameObject HitParticle;
            public string DetachChildName;

            public static BulletTemplate Capture(GameObject prefab)
            {
                BulletTemplate template = new BulletTemplate();
                ProjectileBase source = prefab.GetComponent<ProjectileBase>();
                if (source == null)
                {
                    return template;
                }

                template.Speed = source.m_Speed;
                template.Damage = source.m_Damage;
                template.Range = source.m_Range;
                template.Radius = source.m_Radius;
                template.TurnSpeed = source.m_TurnSpeed;
                template.IsEnemyTeam = source.m_IsEnemyTeam;
                template.HitParticle = source.m_HitParticle;
                if (source.m_DetachObject != null)
                {
                    template.DetachChildName = source.m_DetachObject.name;
                }

                return template;
            }

            public void ApplyTo(ProjectileBase projectile)
            {
                projectile.m_Speed = Speed;
                projectile.m_Damage = Damage;
                projectile.m_Range = Range;
                projectile.m_Radius = Radius;
                projectile.m_TurnSpeed = TurnSpeed;
                projectile.m_IsEnemyTeam = IsEnemyTeam;
                projectile.m_HitParticle = HitParticle;
                projectile.m_DetachObject = string.IsNullOrEmpty(DetachChildName) ? null : FindDetachChild(projectile);
            }

            private GameObject FindDetachChild(ProjectileBase projectile)
            {
                Transform child = projectile.transform.Find(DetachChildName);
                return child != null ? child.gameObject : null;
            }
        }
    }
}
