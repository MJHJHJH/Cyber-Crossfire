//------------------------------------------------------------
// YooAsset package list for bootstrap (manual Inspector config)
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 启动时需要初始化的 Yoo Package 名单；由人工配置，经 <see cref="BaseComponent"/> 拖引用使用。
    /// </summary>
    [CreateAssetMenu(fileName = "YooPackageCatalog", menuName = "Game Framework/Yoo Package Catalog")]
    public sealed class YooPackageCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string PackageName = "DefaultPackage";
            public bool IsDefault;
            public bool Enabled = true;
        }

        [SerializeField]
        private Entry[] m_Packages = new Entry[]
        {
            new Entry { PackageName = "DefaultPackage", IsDefault = true, Enabled = true },
        };

        public IReadOnlyList<Entry> Packages => m_Packages;

        /// <summary>
        /// 解析启用中的包队列与主包名。无有效条目时返回 false。
        /// </summary>
        public bool TryBuildQueue(List<string> packageNames, out string defaultPackageName)
        {
            packageNames.Clear();
            defaultPackageName = null;
            if (m_Packages == null || m_Packages.Length == 0)
                return false;

            string markedDefault = null;
            for (int i = 0; i < m_Packages.Length; i++)
            {
                Entry entry = m_Packages[i];
                if (entry == null || !entry.Enabled)
                    continue;

                string name = entry.PackageName;
                if (string.IsNullOrEmpty(name))
                    continue;

                if (packageNames.Contains(name))
                    continue;

                packageNames.Add(name);
                if (entry.IsDefault && markedDefault == null)
                    markedDefault = name;
            }

            if (packageNames.Count == 0)
                return false;

            defaultPackageName = markedDefault ?? packageNames[0];
            return true;
        }
    }
}
