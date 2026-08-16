using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameFramework.Sound;
using UnityEngine;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// YooAsset 声音资源加载器：内部缓存 AssetHandle，继承 <see cref="SoundAssetLoaderBase"/> 获得引用计数缓存与延迟卸载。
    /// </summary>
    public sealed class YooAssetSoundAssetLoader : SoundAssetLoaderBase
    {
        private readonly ResourcePackage _package;
        private readonly Dictionary<AudioClip, AssetHandle> m_Handles = new Dictionary<AudioClip, AssetHandle>();

        public YooAssetSoundAssetLoader(ResourcePackage package)
        {
            _package = package ?? throw new GameFrameworkException("Resource package is invalid.");
        }

        protected override void LoadClipCore(string assetName, int priority)
        {
            AssetHandle handle = _package.LoadAssetAsync<AudioClip>(assetName, (uint)Mathf.Max(0, priority));
            LoadAudioClipAsync(handle, assetName).Forget();
        }

        protected override void UnloadClip(string assetName)
        {
            if (!TryGetClip(assetName, out AudioClip clip))
            {
                return;
            }

            RemoveClip(assetName);
            if (m_Handles.TryGetValue(clip, out AssetHandle handle))
            {
                m_Handles.Remove(clip);
                if (handle.IsValid)
                {
                    handle.Release();
                }
            }
        }

        /// <summary>按 clip 维护的 handle 表，卸载时随 clip 一并释放。</summary>
        private async UniTaskVoid LoadAudioClipAsync(AssetHandle handle, string assetName)
        {
            try
            {
                await handle.ToUniTask();
            }
            catch
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }

                FailLoad(assetName, LoadAudioClipErrorCode.LoadError, "Load audio clip task failed.");
                return;
            }

            if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
            {
                string error = handle.Error;
                handle.Release();
                FailLoad(assetName, LoadAudioClipErrorCode.LoadError, error);
                return;
            }

            AudioClip clip = handle.GetAssetObject<AudioClip>();
            if (clip == null)
            {
                handle.Release();
                FailLoad(assetName, LoadAudioClipErrorCode.AssetTypeError, "Asset is not AudioClip.");
                return;
            }

            m_Handles[clip] = handle;
            CompleteLoad(assetName, clip, clip.length);
        }
    }
}
