using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    /// <summary>
    /// 为当前包创建下载器并收集待下载项；全部包检查完后进入统一确认。
    /// </summary>
    public sealed class CreateDownloaderState : AssetPatchState
    {
        private const int DownloadingMaxNum = 10;
        private const int FailedTryAgain = 3;

        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.FireSteps("检查资源更新…");

            try
            {
                ResourcePackage package = YooAssets.GetPackage(ctx.PackageName);
                var options = new ResourceDownloaderOptions(DownloadingMaxNum, FailedTryAgain);
                ResourceDownloaderOperation downloader = package.CreateResourceDownloader(options);
                ctx.Downloader = downloader;

                if (downloader.TotalDownloadCount > 0)
                {
                    Debug.Log(
                        $"[AssetPatch][{ctx.PackageName}] pending download " +
                        $"{downloader.TotalDownloadCount} files, {downloader.TotalDownloadBytes} bytes");
                    ctx.AddPendingDownload(ctx.PackageName, downloader);
                }
                else
                {
                    Debug.Log($"[AssetPatch][{ctx.PackageName}] no download files.");
                }

                if (ctx.TryBeginNextPackage())
                {
                    ChangeState<InitializePackageState>(fsm);
                    return;
                }

                ChangeState<ConfirmDownloadState>(fsm);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(ex);
            }
        }
    }
}
