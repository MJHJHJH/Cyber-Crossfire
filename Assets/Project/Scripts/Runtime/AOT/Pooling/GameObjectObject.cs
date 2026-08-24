using GameFramework;
using UnityEngine;

namespace GamePlay.Pooling
{
    /// <summary>
    /// GameObject 对象池包装：把任意 GameObject 挂到框架 ObjectPool 上。
    /// OnSpawn 激活；OnUnspawn 隐藏并挂回常驻池根；Release 时销毁实例。
    /// 所有对 Target 的触碰均需空判：战斗场景卸载可能先于回收销毁实例。
    /// 注意：不得在子类声明与 ObjectBase 同名的 m_Target 字段（字段遮蔽会导致
    /// Initialize 写入基类字段而子类读到 null）；统一通过基类 Target 属性访问。
    /// </summary>
    public sealed class GameObjectObject : ObjectBase
    {
        private static Transform s_PoolRoot;

        public GameObjectObject()
        {
        }

        /// <summary>
        /// 常驻池根：回收实例的统一挂载点（须为 DontDestroyOnLoad，由门面初始化）。
        /// </summary>
        public static Transform PoolRoot
        {
            get
            {
                return s_PoolRoot;
            }
            set
            {
                s_PoolRoot = value;
            }
        }

        /// <summary>
        /// 创建 GameObject 包装对象。
        /// </summary>
        /// <param name="name">对象名称（池内注册名）。</param>
        /// <param name="target">被包装的 GameObject。</param>
        /// <returns>创建的包装对象。</returns>
        public static GameObjectObject Create(string name, GameObject target)
        {
            GameObjectObject obj = ReferencePool.Acquire<GameObjectObject>();
            obj.Initialize(name, target);
            return obj;
        }

        /// <summary>
        /// 取被包装的 GameObject（已销毁返回 null，Unity 假空判定）。
        /// </summary>
        private GameObject GetTarget()
        {
            return Target as GameObject;
        }

        protected override void OnSpawn()
        {
            GameObject target = GetTarget();
            if (target == null)
            {
                return;
            }

            target.SetActive(true);
        }

        protected override void OnUnspawn()
        {
            GameObject target = GetTarget();
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            if (s_PoolRoot != null)
            {
                target.transform.SetParent(s_PoolRoot, false);
            }
        }

        protected override void Release(bool isShutdown)
        {
            GameObject target = GetTarget();
            if (target != null)
            {
                Object.Destroy(target);
            }

            Clear();
        }
    }
}
