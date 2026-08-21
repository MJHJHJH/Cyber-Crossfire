using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 全局数据组件：通用 KV/对象读写，JSON 文件持久化。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/Data")]
    public sealed class DataComponent : GameFrameworkComponent
    {
        [SerializeField]
        private string m_RelativeFilePath = JsonFileDataHelper.DefaultRelativePath;

        private IDataManager m_DataManager;

        public bool IsLoaded => m_DataManager != null && m_DataManager.IsLoaded;

        public bool IsDirty => m_DataManager != null && m_DataManager.IsDirty;

        public string FilePath => m_DataManager != null ? m_DataManager.FilePath : string.Empty;

        protected override void Awake()
        {
            base.Awake();

            m_DataManager = GameFrameworkEntry.GetModule<IDataManager>();
            if (m_DataManager == null)
            {
                Debug.LogError("[DataComponent] Data manager is invalid.");
                return;
            }

            if (m_DataManager is DataManager manager)
                manager.SetHelper(new JsonFileDataHelper(m_RelativeFilePath));
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                FlushIfDirty();
        }

        private void OnApplicationQuit()
        {
            FlushIfDirty();
        }

        public void Load()
        {
            EnsureManager();
            m_DataManager.Load();
        }

        public void Save()
        {
            EnsureManager();
            m_DataManager.Save();
        }

        public void Reset()
        {
            EnsureManager();
            m_DataManager.Reset();
        }

        public bool Has(string key)
        {
            EnsureManager();
            return m_DataManager.Has(key);
        }

        public void Remove(string key)
        {
            EnsureManager();
            m_DataManager.Remove(key);
        }

        public T GetObject<T>(string key) where T : class, new()
        {
            EnsureManager();
            return m_DataManager.GetObject<T>(key);
        }

        public bool TryGetObject<T>(string key, out T value) where T : class, new()
        {
            EnsureManager();
            return m_DataManager.TryGetObject(key, out value);
        }

        public void SetObject<T>(string key, T value) where T : class
        {
            EnsureManager();
            m_DataManager.SetObject(key, value);
        }

        private void FlushIfDirty()
        {
            if (m_DataManager == null || !m_DataManager.IsDirty)
                return;

            m_DataManager.Save();
        }

        private void EnsureManager()
        {
            if (m_DataManager == null)
                throw new GameFrameworkException("Data manager is invalid.");
        }
    }
}
