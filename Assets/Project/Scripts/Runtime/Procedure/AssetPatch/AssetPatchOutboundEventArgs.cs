using GameFramework;
using GameFramework.Event;

namespace GamePlay.AssetPatch
{
    public sealed class AssetPatchStepsChangeEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchStepsChangeEventArgs).GetHashCode();

        public override int Id => EventId;

        public string Tips { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchStepsChangeEventArgs Create(string tips, string packageName = null)
        {
            AssetPatchStepsChangeEventArgs e = ReferencePool.Acquire<AssetPatchStepsChangeEventArgs>();
            e.Tips = tips;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            Tips = null;
            PackageName = null;
        }
    }

    public sealed class AssetPatchInitializeFailedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchInitializeFailedEventArgs).GetHashCode();

        public override int Id => EventId;

        public string Error { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchInitializeFailedEventArgs Create(string error, string packageName = null)
        {
            AssetPatchInitializeFailedEventArgs e = ReferencePool.Acquire<AssetPatchInitializeFailedEventArgs>();
            e.Error = error;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            Error = null;
            PackageName = null;
        }
    }

    public sealed class AssetPatchVersionRequestFailedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchVersionRequestFailedEventArgs).GetHashCode();

        public override int Id => EventId;

        public string Error { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchVersionRequestFailedEventArgs Create(string error, string packageName = null)
        {
            AssetPatchVersionRequestFailedEventArgs e = ReferencePool.Acquire<AssetPatchVersionRequestFailedEventArgs>();
            e.Error = error;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            Error = null;
            PackageName = null;
        }
    }

    public sealed class AssetPatchManifestUpdateFailedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchManifestUpdateFailedEventArgs).GetHashCode();

        public override int Id => EventId;

        public string Error { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchManifestUpdateFailedEventArgs Create(string error, string packageName = null)
        {
            AssetPatchManifestUpdateFailedEventArgs e = ReferencePool.Acquire<AssetPatchManifestUpdateFailedEventArgs>();
            e.Error = error;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            Error = null;
            PackageName = null;
        }
    }

    public sealed class AssetPatchFoundUpdateFilesEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchFoundUpdateFilesEventArgs).GetHashCode();

        public override int Id => EventId;

        public int TotalCount { get; private set; }

        public long TotalBytes { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchFoundUpdateFilesEventArgs Create(
            int totalCount, long totalBytes, string packageName = null)
        {
            AssetPatchFoundUpdateFilesEventArgs e = ReferencePool.Acquire<AssetPatchFoundUpdateFilesEventArgs>();
            e.TotalCount = totalCount;
            e.TotalBytes = totalBytes;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            TotalCount = 0;
            TotalBytes = 0;
            PackageName = null;
        }
    }

    public sealed class AssetPatchDownloadUpdateEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchDownloadUpdateEventArgs).GetHashCode();

        public override int Id => EventId;

        public int TotalDownloadCount { get; private set; }

        public int CurrentDownloadCount { get; private set; }

        public long TotalDownloadBytes { get; private set; }

        public long CurrentDownloadBytes { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchDownloadUpdateEventArgs Create(
            int totalCount, int currentCount, long totalBytes, long currentBytes, string packageName = null)
        {
            AssetPatchDownloadUpdateEventArgs e = ReferencePool.Acquire<AssetPatchDownloadUpdateEventArgs>();
            e.TotalDownloadCount = totalCount;
            e.CurrentDownloadCount = currentCount;
            e.TotalDownloadBytes = totalBytes;
            e.CurrentDownloadBytes = currentBytes;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            TotalDownloadCount = 0;
            CurrentDownloadCount = 0;
            TotalDownloadBytes = 0;
            CurrentDownloadBytes = 0;
            PackageName = null;
        }
    }

    public sealed class AssetPatchDownloadFailedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchDownloadFailedEventArgs).GetHashCode();

        public override int Id => EventId;

        public string FileName { get; private set; }

        public string Error { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchDownloadFailedEventArgs Create(
            string fileName, string error, string packageName = null)
        {
            AssetPatchDownloadFailedEventArgs e = ReferencePool.Acquire<AssetPatchDownloadFailedEventArgs>();
            e.FileName = fileName;
            e.Error = error;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            FileName = null;
            Error = null;
            PackageName = null;
        }
    }

    /// <summary>设置 Loading 进度目标（0~1），UI 以固定速度追赶。</summary>
    public sealed class AssetPatchProgressEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(AssetPatchProgressEventArgs).GetHashCode();

        public override int Id => EventId;

        public float Progress { get; private set; }

        public string Tips { get; private set; }

        public string PackageName { get; private set; }

        public static AssetPatchProgressEventArgs Create(
            float progress, string tips = null, string packageName = null)
        {
            AssetPatchProgressEventArgs e = ReferencePool.Acquire<AssetPatchProgressEventArgs>();
            e.Progress = progress;
            e.Tips = tips;
            e.PackageName = packageName;
            return e;
        }

        public override void Clear()
        {
            Progress = 0f;
            Tips = null;
            PackageName = null;
        }
    }
}
