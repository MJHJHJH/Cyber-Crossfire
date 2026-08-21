using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 数据管理器：内存 KV + JSON 文件持久化。
    /// </summary>
    internal sealed class DataManager : GameFrameworkModule, IDataManager
    {
        private readonly Dictionary<string, string> m_Payloads;
        private JsonFileDataHelper m_Helper;
        private IDataSerializer m_Serializer;
        private bool m_IsLoaded;
        private bool m_IsDirty;

        public DataManager()
        {
            m_Payloads = new Dictionary<string, string>();
            m_Serializer = new JsonUtilityDataSerializer();
            m_Helper = new JsonFileDataHelper();
        }

        public bool IsLoaded => m_IsLoaded;

        public bool IsDirty => m_IsDirty;

        public string FilePath => m_Helper != null ? m_Helper.FilePath : string.Empty;

        internal override int Priority => 50;

        public void SetHelper(JsonFileDataHelper helper)
        {
            m_Helper = helper ?? new JsonFileDataHelper();
        }

        public void SetSerializer(IDataSerializer serializer)
        {
            m_Serializer = serializer ?? new JsonUtilityDataSerializer();
        }

        public void Load()
        {
            m_Payloads.Clear();
            DataStoreFile file = m_Helper.Read();
            if (file.entries != null)
            {
                for (int i = 0; i < file.entries.Count; i++)
                {
                    DataStoreEntry entry = file.entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                        continue;

                    m_Payloads[entry.key] = entry.payload ?? string.Empty;
                }
            }

            m_IsLoaded = true;
            m_IsDirty = false;
        }

        public void Save()
        {
            if (!m_IsLoaded)
                Load();

            DataStoreFile file = new DataStoreFile { version = 1 };
            foreach (KeyValuePair<string, string> pair in m_Payloads)
            {
                file.entries.Add(new DataStoreEntry
                {
                    key = pair.Key,
                    payload = pair.Value
                });
            }

            m_Helper.Write(file);
            m_IsDirty = false;
        }

        public void Reset()
        {
            m_Payloads.Clear();
            m_Helper.Delete();
            m_IsLoaded = true;
            m_IsDirty = false;
        }

        public bool Has(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(key) && m_Payloads.ContainsKey(key);
        }

        public void Remove(string key)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key))
                return;

            if (m_Payloads.Remove(key))
                m_IsDirty = true;
        }

        public T GetObject<T>(string key) where T : class, new()
        {
            if (TryGetObject(key, out T value))
                return value;
            return new T();
        }

        public bool TryGetObject<T>(string key, out T value) where T : class, new()
        {
            EnsureLoaded();
            value = null;
            if (string.IsNullOrEmpty(key))
                return false;

            if (!m_Payloads.TryGetValue(key, out string payload) || string.IsNullOrEmpty(payload))
                return false;

            value = m_Serializer.FromJson<T>(payload);
            return value != null;
        }

        public void SetObject<T>(string key, T value) where T : class
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key))
                throw new GameFrameworkException("Data key is invalid.");

            if (value == null)
            {
                Remove(key);
                return;
            }

            m_Payloads[key] = m_Serializer.ToJson(value);
            m_IsDirty = true;
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        internal override void Shutdown()
        {
            if (m_IsDirty)
                Save();

            m_Payloads.Clear();
            m_IsLoaded = false;
            m_IsDirty = false;
        }

        private void EnsureLoaded()
        {
            if (!m_IsLoaded)
                Load();
        }
    }
}
