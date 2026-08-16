#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// Editor 且 Yoo 未就绪时，用 AssetDatabase 从 Assets/DataTable/Data/{Json|Bin} 按 location 加载。
    /// </summary>
    public sealed class EditorAssetDatabaseLubanDataLoader : ILubanDataLoader
    {
        public UniTask<string> LoadTextAsync(string location, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(LoadText(location));
        }

        public string LoadText(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Luban json location is invalid.");

            string path = Utility.Text.Format("Assets/DataTable/Data/Json/{0}.json", location);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
                throw new GameFrameworkException(Utility.Text.Format("AssetDatabase miss Luban json: {0}", path));

            return asset.text;
        }

        public UniTask<byte[]> LoadBytesAsync(string location, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(LoadBytes(location));
        }

        public byte[] LoadBytes(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Luban bin location is invalid.");

            string path = Utility.Text.Format("Assets/DataTable/Data/Bin/{0}.bytes", location);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
                throw new GameFrameworkException(Utility.Text.Format("AssetDatabase miss Luban bin: {0}", path));

            return asset.bytes;
        }
    }
}
#endif
