using GameFramework;
using GameFramework.Event;

namespace GamePlay.AssetPatch
{
    public sealed class AssetPatchUserTryInitializeEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchUserTryInitializeEventArgs).GetHashCode();

        public override int Id => EventId;

        public static AssetPatchUserTryInitializeEventArgs Create()
        {
            return ReferencePool.Acquire<AssetPatchUserTryInitializeEventArgs>();
        }

        public override void Clear()
        {
        }
    }

    public sealed class AssetPatchUserTryRequestVersionEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchUserTryRequestVersionEventArgs).GetHashCode();

        public override int Id => EventId;

        public static AssetPatchUserTryRequestVersionEventArgs Create()
        {
            return ReferencePool.Acquire<AssetPatchUserTryRequestVersionEventArgs>();
        }

        public override void Clear()
        {
        }
    }

    public sealed class AssetPatchUserTryUpdateManifestEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchUserTryUpdateManifestEventArgs).GetHashCode();

        public override int Id => EventId;

        public static AssetPatchUserTryUpdateManifestEventArgs Create()
        {
            return ReferencePool.Acquire<AssetPatchUserTryUpdateManifestEventArgs>();
        }

        public override void Clear()
        {
        }
    }

    public sealed class AssetPatchUserTryDownloadEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchUserTryDownloadEventArgs).GetHashCode();

        public override int Id => EventId;

        public static AssetPatchUserTryDownloadEventArgs Create()
        {
            return ReferencePool.Acquire<AssetPatchUserTryDownloadEventArgs>();
        }

        public override void Clear()
        {
        }
    }

    /// <summary>预留：确认开始下载（无 UI 阶段自动开下，可不发）。</summary>
    public sealed class AssetPatchUserBeginDownloadEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchUserBeginDownloadEventArgs).GetHashCode();

        public override int Id => EventId;

        public static AssetPatchUserBeginDownloadEventArgs Create()
        {
            return ReferencePool.Acquire<AssetPatchUserBeginDownloadEventArgs>();
        }

        public override void Clear()
        {
        }
    }

    /// <summary>Loading 显示进度已追赶到 100%，可进入下一阶段。</summary>
    public sealed class AssetPatchUserProgressReadyEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchUserProgressReadyEventArgs).GetHashCode();

        public override int Id => EventId;

        public static AssetPatchUserProgressReadyEventArgs Create()
        {
            return ReferencePool.Acquire<AssetPatchUserProgressReadyEventArgs>();
        }

        public override void Clear()
        {
        }
    }
}
