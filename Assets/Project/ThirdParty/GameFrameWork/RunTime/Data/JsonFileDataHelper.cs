using System.IO;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 读写 persistentDataPath 下的 JSON 存档文件。
    /// </summary>
    public sealed class JsonFileDataHelper
    {
        public const string DefaultRelativePath = "GameData/player_data.json";

        private readonly string m_AbsolutePath;

        public JsonFileDataHelper(string relativePath = null)
        {
            string relative = string.IsNullOrEmpty(relativePath) ? DefaultRelativePath : relativePath;
            m_AbsolutePath = Path.Combine(Application.persistentDataPath, relative);
        }

        public string FilePath => m_AbsolutePath;

        public bool Exists()
        {
            return File.Exists(m_AbsolutePath);
        }

        public DataStoreFile Read()
        {
            if (!File.Exists(m_AbsolutePath))
                return new DataStoreFile();

            string json = File.ReadAllText(m_AbsolutePath);
            if (string.IsNullOrEmpty(json))
                return new DataStoreFile();

            DataStoreFile file = JsonUtility.FromJson<DataStoreFile>(json);
            if (file == null)
                return new DataStoreFile();

            if (file.entries == null)
                file.entries = new System.Collections.Generic.List<DataStoreEntry>();

            return file;
        }

        public void Write(DataStoreFile file)
        {
            if (file == null)
                file = new DataStoreFile();

            if (file.entries == null)
                file.entries = new System.Collections.Generic.List<DataStoreEntry>();

            string directory = Path.GetDirectoryName(m_AbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(file, true);
            string tempPath = m_AbsolutePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(m_AbsolutePath))
                File.Delete(m_AbsolutePath);
            File.Move(tempPath, m_AbsolutePath);
        }

        public void Delete()
        {
            if (File.Exists(m_AbsolutePath))
                File.Delete(m_AbsolutePath);

            string tempPath = m_AbsolutePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
