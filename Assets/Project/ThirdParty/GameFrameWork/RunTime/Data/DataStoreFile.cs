using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 单文件多 key 容器。JsonUtility 不支持 Dictionary，用 Entry 列表表达。
    /// </summary>
    [Serializable]
    public sealed class DataStoreFile
    {
        public int version = 1;
        public List<DataStoreEntry> entries = new List<DataStoreEntry>();
    }

    [Serializable]
    public sealed class DataStoreEntry
    {
        public string key;
        public string payload;
    }
}
