using Cysharp.Threading.Tasks;
using GameFramework.Sound;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// Resources 声音资源加载器：YooAsset 未初始化时播放内置音效用，location 为 Resources 相对路径（无扩展名）。
    /// 继承 <see cref="SoundAssetLoaderBase"/> 获得引用计数缓存与延迟卸载。
    /// </summary>
    public sealed class ResourcesSoundAssetLoader : SoundAssetLoaderBase
    {
        protected override void LoadClipCore(string assetName, int priority)
        {
            ResourceRequest request = Resources.LoadAsync<AudioClip>(assetName);
            if (request == null)
            {
                FailLoad(assetName, LoadAudioClipErrorCode.AssetNotExist,
                    Utility.Text.Format("Resources has no audio clip '{0}'.", assetName));
                return;
            }

            LoadAudioClipAsync(request, assetName).Forget();
        }

        protected override void UnloadClip(string assetName)
        {
            if (!TryGetClip(assetName, out AudioClip clip))
            {
                return;
            }

            RemoveClip(assetName);
            Resources.UnloadAsset(clip);
        }

        private async UniTaskVoid LoadAudioClipAsync(ResourceRequest request, string assetName)
        {
            while (!request.isDone)
            {
                ReportProgress(assetName, request.progress);
                await UniTask.Yield();
            }

            AudioClip clip = request.asset as AudioClip;
            CompleteLoad(assetName, clip, clip != null ? clip.length : 0f);
        }
    }
}
