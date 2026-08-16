using System;
using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 配表注册表：按接口类型存放已加载的表访问器（框架不感知具体表名）。
    /// </summary>
    public sealed class LubanTableRegistry
    {
        private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        public int Count => _map.Count;

        public void Register<T>(T table) where T : class
        {
            if (table == null)
                throw new GameFrameworkException(Utility.Text.Format("Luban table '{0}' is invalid.", typeof(T).Name));

            _map[typeof(T)] = table;
        }

        public T Get<T>() where T : class
        {
            if (!_map.TryGetValue(typeof(T), out object table) || table == null)
                throw new GameFrameworkException(Utility.Text.Format("Luban table '{0}' is not registered.", typeof(T).Name));

            return (T)table;
        }

        public bool TryGet<T>(out T table) where T : class
        {
            if (_map.TryGetValue(typeof(T), out object boxed) && boxed is T typed)
            {
                table = typed;
                return true;
            }

            table = null;
            return false;
        }
    }
}
