namespace GameFramework.Sound
{
    /// <summary>
    /// 声音资源加载方式。
    /// </summary>
    public enum SoundLoadKind
    {
        /// <summary>
        /// YooAsset 加载（默认）。
        /// </summary>
        YooAsset = 0,

        /// <summary>
        /// Resources 内置音效加载。
        /// </summary>
        Resources = 1
    }

    /// <summary>
    /// 加载声音资源错误码。
    /// </summary>
    public enum LoadAudioClipErrorCode
    {
        /// <summary>
        /// 未知错误。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 资源不存在。
        /// </summary>
        AssetNotExist,

        /// <summary>
        /// 加载错误。
        /// </summary>
        LoadError,

        /// <summary>
        /// 资源类型错误。
        /// </summary>
        AssetTypeError
    }

    public delegate void LoadAudioClipSuccessCallback(string assetName, object asset, float duration, object userData);
    public delegate void LoadAudioClipFailureCallback(string assetName, LoadAudioClipErrorCode errorCode, string errorMessage, object userData);
    public delegate void LoadAudioClipUpdateCallback(string assetName, float progress, object userData);
    public delegate void LoadAudioClipDependencyAssetCallback(string assetName, string dependencyAssetName, int loadedCount, int totalCount, object userData);

    /// <summary>
    /// 加载声音资源回调。
    /// </summary>
    public sealed class LoadAudioClipCallbacks
    {
        private readonly LoadAudioClipSuccessCallback m_OnSuccess;
        private readonly LoadAudioClipFailureCallback m_OnFailure;
        private readonly LoadAudioClipUpdateCallback m_OnUpdate;
        private readonly LoadAudioClipDependencyAssetCallback m_OnDependencyAsset;

        public LoadAudioClipCallbacks(LoadAudioClipSuccessCallback onSuccess, LoadAudioClipFailureCallback onFailure)
            : this(onSuccess, onFailure, null, null)
        {
        }

        public LoadAudioClipCallbacks(LoadAudioClipSuccessCallback onSuccess, LoadAudioClipFailureCallback onFailure, LoadAudioClipUpdateCallback onUpdate, LoadAudioClipDependencyAssetCallback onDependencyAsset)
        {
            m_OnSuccess = onSuccess;
            m_OnFailure = onFailure;
            m_OnUpdate = onUpdate;
            m_OnDependencyAsset = onDependencyAsset;
        }

        public LoadAudioClipSuccessCallback OnSuccess => m_OnSuccess;
        public LoadAudioClipFailureCallback OnFailure => m_OnFailure;
        public LoadAudioClipUpdateCallback OnUpdate => m_OnUpdate;
        public LoadAudioClipDependencyAssetCallback OnDependencyAsset => m_OnDependencyAsset;
    }

    /// <summary>
    /// 声音资源加载器接口：按 location 异步加载 AudioClip，并负责资源释放。
    /// </summary>
    public interface ISoundAssetLoader
    {
        /// <summary>
        /// 加载声音资源。
        /// </summary>
        void LoadAudioClip(string assetName, int priority, LoadAudioClipCallbacks callbacks, object userData);

        /// <summary>
        /// 释放声音资源。
        /// </summary>
        void ReleaseAudioClip(object asset);
    }
}
