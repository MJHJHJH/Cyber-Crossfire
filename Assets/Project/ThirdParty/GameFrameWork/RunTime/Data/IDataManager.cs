using System;

namespace GameFramework
{
    /// <summary>
    /// 通用数据管理器：按 key 读写可序列化对象，持久化为 JSON 文件。
    /// </summary>
    public interface IDataManager
    {
        bool IsLoaded { get; }

        bool IsDirty { get; }

        string FilePath { get; }

        void Load();

        void Save();

        void Reset();

        bool Has(string key);

        void Remove(string key);

        T GetObject<T>(string key) where T : class, new();

        bool TryGetObject<T>(string key, out T value) where T : class, new();

        void SetObject<T>(string key, T value) where T : class;
    }
}
