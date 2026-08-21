using UnityEngine;

namespace GameFramework
{
    public sealed class JsonUtilityDataSerializer : IDataSerializer
    {
        public string ToJson<T>(T value) where T : class
        {
            return value == null ? string.Empty : JsonUtility.ToJson(value);
        }

        public T FromJson<T>(string json) where T : class, new()
        {
            if (string.IsNullOrEmpty(json))
                return new T();

            T value = JsonUtility.FromJson<T>(json);
            return value ?? new T();
        }
    }
}
