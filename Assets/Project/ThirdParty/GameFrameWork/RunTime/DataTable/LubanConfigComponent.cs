using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// Luban 配置组件：加载后按类型取表，框架不随业务表增减改动。
    /// 示例：<c>GameFrameWork.DataTable.GetTable&lt;IShop&gt;().Get(1001)</c>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/Data Table")]
    public sealed class LubanConfigComponent : GameFrameworkComponent
    {
        // DataTablePackage 为表格专属资源包裹 - 根据情况设定为RawPackage or AssetPackage 
        public const string DataTablePackageName = "DataTablePackage";

        [SerializeField]
        private LubanDataMode m_DataMode = LubanDataMode.Json;

        private ILubanDataLoader m_DataLoader;
        private ILubanTablesFactory m_TablesFactory;
        private LubanTableRegistry m_Registry;
        private bool m_IsLoaded;
        private CancellationTokenSource m_DestroyCts;

        public LubanDataMode DataMode
        {
            get => m_DataMode;
            set => m_DataMode = value;
        }

        public bool IsLoaded => m_IsLoaded;

        protected override void Awake()
        {
            base.Awake();
            m_DestroyCts = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            m_DestroyCts?.Cancel();
            m_DestroyCts?.Dispose();
            m_DestroyCts = null;
            ClearTables();
        }

        public void SetDataLoader(ILubanDataLoader loader)
        {
            m_DataLoader = loader;
        }

        public void SetTablesFactory(ILubanTablesFactory factory)
        {
            m_TablesFactory = factory;
        }

        public async UniTask LoadAsync(CancellationToken cancellationToken = default)
        {
            if (m_TablesFactory == null)
                throw new GameFrameworkException("Luban tables factory is not set.");

            ILubanDataLoader loader = ResolveDataLoader();
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                m_DestroyCts != null ? m_DestroyCts.Token : CancellationToken.None);

            ClearTables();

            try
            {
                LubanTableRegistry registry = await m_TablesFactory.CreateAsync(m_DataMode, loader, linked.Token);
                if (registry == null)
                    throw new GameFrameworkException("Luban tables factory returned null.");

                m_Registry = registry;
                m_IsLoaded = true;
            }
            catch
            {
                ClearTables();
                throw;
            }
        }

        /// <summary>按接口类型取已注册的表，例如 <c>GetTable&lt;IShop&gt;()</c>。</summary>
        public T GetTable<T>() where T : class
        {
            EnsureLoaded();
            return m_Registry.Get<T>();
        }

        public bool TryGetTable<T>(out T table) where T : class
        {
            if (!m_IsLoaded || m_Registry == null)
            {
                table = null;
                return false;
            }

            return m_Registry.TryGet(out table);
        }

        private void EnsureLoaded()
        {
            if (!m_IsLoaded || m_Registry == null)
                throw new GameFrameworkException("Luban tables are not loaded. Call LoadAsync first.");
        }

        private void ClearTables()
        {
            m_Registry = null;
            m_IsLoaded = false;
        }

        private ILubanDataLoader ResolveDataLoader()
        {
            if (m_DataLoader != null)
                return m_DataLoader;

            if (YooAssets.IsInitialized)
            {
                if (!YooAssets.TryGetPackage(DataTablePackageName, out ResourcePackage package) || package == null)
                {
                    throw new GameFrameworkException(
                        Utility.Text.Format("YooAsset {0} is invalid.", DataTablePackageName));
                }

                return new YooLubanDataLoader(package);
            }

#if UNITY_EDITOR
            return new EditorAssetDatabaseLubanDataLoader();
#else
            throw new GameFrameworkException("Luban data loader is not set and YooAsset is not initialized.");
#endif
        }
    }
}
