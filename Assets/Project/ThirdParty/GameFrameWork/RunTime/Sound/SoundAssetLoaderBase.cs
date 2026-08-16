using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Sound
{
    /// <summary>
    /// 声音资源加载器基类：维护 location → AudioClip 的引用计数缓存。
    /// 引用归零不立即释放，而是进入空闲列表；空闲超过 <see cref="UnloadDelaySeconds"/> 仍未复用才真正卸载，
    /// 避免高频复用资源（如子弹音效）反复加载/卸载。
    /// 同一 location 并发请求会被合并：加载中只发一次真实加载，等待者挂起后在完成回调中统一派发。
    /// </summary>
    public abstract class SoundAssetLoaderBase : ISoundAssetLoader
    {
        private sealed class CachedClip
        {
            public AudioClip Clip;
            public int ReferenceCount;
            public float IdleTime;
        }

        private readonly struct LoadRequest
        {
            public readonly LoadAudioClipCallbacks Callbacks;
            public readonly object UserData;

            public LoadRequest(LoadAudioClipCallbacks callbacks, object userData)
            {
                Callbacks = callbacks;
                UserData = userData;
            }
        }

        private sealed class PendingLoad
        {
            public readonly List<LoadRequest> Requests = new List<LoadRequest>();
        }

        /// <summary>
        /// 空闲缓存卸载延迟（秒）。
        /// </summary>
        protected virtual float UnloadDelaySeconds => 10f;

        private readonly Dictionary<string, CachedClip> m_Cache = new Dictionary<string, CachedClip>();
        private readonly Dictionary<string, PendingLoad> m_Loading = new Dictionary<string, PendingLoad>();

        public void LoadAudioClip(string assetName, int priority, LoadAudioClipCallbacks callbacks, object userData)
        {
            if (m_Cache.TryGetValue(assetName, out CachedClip cached))
            {
                cached.ReferenceCount++;
                cached.IdleTime = 0f;
                callbacks.OnSuccess?.Invoke(assetName, cached.Clip, cached.Clip.length, userData);
                return;
            }

            if (m_Loading.TryGetValue(assetName, out PendingLoad pending))
            {
                pending.Requests.Add(new LoadRequest(callbacks, userData));
                return;
            }

            pending = new PendingLoad();
            pending.Requests.Add(new LoadRequest(callbacks, userData));
            m_Loading.Add(assetName, pending);
            LoadClipCore(assetName, priority);
        }

        public void ReleaseAudioClip(object asset)
        {
            if (!(asset is AudioClip clip))
            {
                return;
            }

            foreach (KeyValuePair<string, CachedClip> pair in m_Cache)
            {
                CachedClip cached = pair.Value;
                if (!ReferenceEquals(cached.Clip, clip))
                {
                    continue;
                }

                if (cached.ReferenceCount > 0)
                {
                    cached.ReferenceCount--;
                }

                if (cached.ReferenceCount == 0)
                {
                    cached.IdleTime = 0f;
                }

                break;
            }
        }

        /// <summary>
        /// 驱动空闲列表倒计时，由 SoundComponent 每帧调用。空闲超过延迟的资源被真正卸载。
        /// </summary>
        public void Update(float elapseSeconds)
        {
            List<string> removeKeys = null;
            foreach (KeyValuePair<string, CachedClip> pair in m_Cache)
            {
                CachedClip cached = pair.Value;
                if (cached.ReferenceCount > 0)
                {
                    continue;
                }

                cached.IdleTime += elapseSeconds;
                if (cached.IdleTime >= UnloadDelaySeconds)
                {
                    if (removeKeys == null)
                    {
                        removeKeys = new List<string>();
                    }

                    removeKeys.Add(pair.Key);
                }
            }

            if (removeKeys == null)
            {
                return;
            }

            for (int i = 0; i < removeKeys.Count; i++)
            {
                UnloadClip(removeKeys[i]);
            }
        }

        /// <summary>
        /// 手动立即卸载所有空闲（引用归零）资源。
        /// </summary>
        public void UnloadUnusedClips()
        {
            List<string> removeKeys = null;
            foreach (KeyValuePair<string, CachedClip> pair in m_Cache)
            {
                if (pair.Value.ReferenceCount <= 0)
                {
                    if (removeKeys == null)
                    {
                        removeKeys = new List<string>();
                    }

                    removeKeys.Add(pair.Key);
                }
            }

            if (removeKeys == null)
            {
                return;
            }

            for (int i = 0; i < removeKeys.Count; i++)
            {
                UnloadClip(removeKeys[i]);
            }
        }

        /// <summary>
        /// 真正发起一次异步加载（子类实现）。完成后必须调用 <see cref="CompleteLoad"/> 或 <see cref="FailLoad"/>。
        /// </summary>
        protected abstract void LoadClipCore(string assetName, int priority);

        /// <summary>
        /// 真正卸载资源（子类实现：Resources.UnloadAsset / YooAsset handle.Release）。
        /// </summary>
        protected abstract void UnloadClip(string assetName);

        /// <summary>
        /// 加载成功回调，向所有等待者派发成功并建立缓存（引用计数 = 等待者数量）。
        /// </summary>
        protected void CompleteLoad(string assetName, AudioClip clip, float duration)
        {
            if (!m_Loading.TryGetValue(assetName, out PendingLoad pending))
            {
                return;
            }

            m_Loading.Remove(assetName);

            if (clip == null)
            {
                FailPending(pending, assetName, LoadAudioClipErrorCode.AssetTypeError,
                    Utility.Text.Format("Asset '{0}' is not AudioClip.", assetName));
                return;
            }

            m_Cache.Add(assetName, new CachedClip
            {
                Clip = clip,
                ReferenceCount = pending.Requests.Count,
                IdleTime = 0f
            });

            for (int i = 0; i < pending.Requests.Count; i++)
            {
                LoadRequest request = pending.Requests[i];
                request.Callbacks.OnSuccess?.Invoke(assetName, clip, duration, request.UserData);
            }
        }

        /// <summary>
        /// 加载失败回调，向所有等待者派发失败。
        /// </summary>
        protected void FailLoad(string assetName, LoadAudioClipErrorCode errorCode, string errorMessage)
        {
            if (!m_Loading.TryGetValue(assetName, out PendingLoad pending))
            {
                return;
            }

            m_Loading.Remove(assetName);
            FailPending(pending, assetName, errorCode, errorMessage);
        }

        /// <summary>
        /// 加载进度回调，派发给所有等待者。
        /// </summary>
        protected void ReportProgress(string assetName, float progress)
        {
            if (!m_Loading.TryGetValue(assetName, out PendingLoad pending))
            {
                return;
            }

            for (int i = 0; i < pending.Requests.Count; i++)
            {
                LoadRequest request = pending.Requests[i];
                request.Callbacks.OnUpdate?.Invoke(assetName, progress, request.UserData);
            }
        }

        /// <summary>
        /// 按 location 查找缓存 clip。
        /// </summary>
        protected bool TryGetClip(string assetName, out AudioClip clip)
        {
            if (m_Cache.TryGetValue(assetName, out CachedClip cached))
            {
                clip = cached.Clip;
                return true;
            }

            clip = null;
            return false;
        }

        /// <summary>
        /// 从缓存移除（不含真正卸载，供子类 UnloadClip 调用）。
        /// </summary>
        protected void RemoveClip(string assetName)
        {
            m_Cache.Remove(assetName);
        }

        private static void FailPending(PendingLoad pending, string assetName, LoadAudioClipErrorCode errorCode, string errorMessage)
        {
            for (int i = 0; i < pending.Requests.Count; i++)
            {
                LoadRequest request = pending.Requests[i];
                request.Callbacks.OnFailure?.Invoke(assetName, errorCode, errorMessage, request.UserData);
            }
        }
    }
}
