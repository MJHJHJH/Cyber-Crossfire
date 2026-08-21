namespace GameFramework
{
    /// <summary>
    /// 数据序列化器。默认实现使用 JsonUtility。
    /// </summary>
    public interface IDataSerializer
    {
        string ToJson<T>(T value) where T : class;

        T FromJson<T>(string json) where T : class, new();
    }
}
