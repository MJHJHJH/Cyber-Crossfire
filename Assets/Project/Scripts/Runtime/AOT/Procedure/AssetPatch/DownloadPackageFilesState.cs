using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    /// <summary>
    /// 按汇总字节串行下载全部待更新包；进度 = (已完成字节 + 当前包已下字节) / 总字节。
    /// </summary>
    public sealed class DownloadPackageFilesState : AssetPatchState
    {
        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.FireSteps("开始下载资源文件！");
            RunAsync(fsm, ctx).Forget();
        }

        private async UniTaskVoid RunAsync(IFsm<AssetPatchContext> fsm, AssetPatchContext ctx)
        {
            CancellationToken ct = ctx.CancellationToken;
            if (ctx.PendingDownloads == null || ctx.PendingDownloads.Count == 0)
            {
                await ctx.FireProgressAndWaitReadyAsync(1f, "无需下载…", ct);
                if (!ct.IsCancellationRequested && !fsm.IsDestroyed)
                    ChangeState<ClearCacheBundleState>(fsm);
                return;
            }

            long totalBytes = ctx.TotalPendingDownloadBytes;
            int totalCount = ctx.TotalPendingDownloadCount;
            long finishedBytes = 0;
            int finishedCount = 0;

            try
            {
                for (int i = 0; i < ctx.PendingDownloads.Count; i++)
                {
                    PendingDownloadEntry entry = ctx.PendingDownloads[i];
                    ResourceDownloaderOperation downloader = entry.Downloader;
                    if (downloader == null)
                    {
                        ctx.Fire(AssetPatchDownloadFailedEventArgs.Create(
                            null, "Downloader is null.", entry.PackageName));
                        return;
                    }

                    ctx.SetActivePackageName(entry.PackageName);
                    ctx.Downloader = downloader;
                    ctx.FireSteps("下载资源中…");

                    long packageTotalBytes = downloader.TotalDownloadBytes;
                    int packageTotalCount = downloader.TotalDownloadCount;

                    void OnDownloadError(DownloadErrorEventArgs data)
                    {
                        ctx.Fire(AssetPatchDownloadFailedEventArgs.Create(
                            data.FileName, data.ErrorInfo, entry.PackageName));
                    }

                    void OnDownloadProgress(DownloadProgressChangedEventArgs data)
                    {
                        long globalBytes = finishedBytes + data.CurrentDownloadBytes;
                        int globalCount = finishedCount + data.CurrentDownloadCount;

                        ctx.Fire(AssetPatchDownloadUpdateEventArgs.Create(
                            totalCount,
                            globalCount,
                            totalBytes,
                            globalBytes,
                            entry.PackageName));

                        float progress = totalBytes > 0
                            ? (float)globalBytes / totalBytes
                            : (totalCount > 0 ? (float)globalCount / totalCount : 0f);

                        if (progress < 1f)
                        {
                            string tips =
                                $"下载资源中… {globalCount}/{totalCount}\n" +
                                $"{FormatBytes(globalBytes)} / {FormatBytes(totalBytes)}";
                            ctx.FireProgress(progress, tips);
                        }
                    }

                    downloader.DownloadError += OnDownloadError;
                    downloader.DownloadProgressChanged += OnDownloadProgress;

                    try
                    {
                        downloader.StartDownload();
                        await downloader.ToUniTask().AttachExternalCancellation(ct);
                    }
                    finally
                    {
                        downloader.DownloadError -= OnDownloadError;
                        downloader.DownloadProgressChanged -= OnDownloadProgress;
                    }

                    if (ct.IsCancellationRequested || fsm.IsDestroyed)
                        return;

                    if (downloader.Status != EOperationStatus.Succeeded)
                    {
                        Debug.LogWarning(downloader.Error);
                        ctx.Fire(AssetPatchDownloadFailedEventArgs.Create(
                            null, downloader.Error, entry.PackageName));
                        return;
                    }

                    finishedBytes += packageTotalBytes > 0
                        ? packageTotalBytes
                        : downloader.TotalDownloadBytes;
                    finishedCount += packageTotalCount > 0
                        ? packageTotalCount
                        : downloader.TotalDownloadCount;
                }

                await ctx.FireProgressAndWaitReadyAsync(1f, "下载完成！", ct);
                if (ct.IsCancellationRequested || fsm.IsDestroyed)
                    return;

                ChangeState<ClearCacheBundleState>(fsm);
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(ex);
                ctx.Fire(AssetPatchDownloadFailedEventArgs.Create(null, ex.Message, ctx.PackageName));
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:0.##} KB";
            if (bytes < 1024L * 1024 * 1024)
                return $"{bytes / (1024f * 1024f):0.##} MB";
            return $"{bytes / (1024f * 1024f * 1024f):0.##} GB";
        }
    }
}
