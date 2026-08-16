using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework
{
    /// <summary>
    /// 按 location（文件名，无扩展名）加载单表数据。
    /// </summary>
    public interface ILubanDataLoader
    {
        UniTask<string> LoadTextAsync(string location, CancellationToken cancellationToken = default);

        UniTask<byte[]> LoadBytesAsync(string location, CancellationToken cancellationToken = default);

        /// <summary>同步加载文本（Json）。</summary>
        string LoadText(string location);

        /// <summary>同步加载字节（Bin）。</summary>
        byte[] LoadBytes(string location);
    }
}
