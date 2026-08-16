using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;

namespace GamePlay.AssetPatch
{
    /// <summary>
    /// 汇总全部包待下载量：有差异则弹一次确认；无差异则假进度后清理收尾。
    /// </summary>
    public sealed class ConfirmDownloadState : AssetPatchState
    {
        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.SetActivePackageName(ctx.DefaultPackageName);
            RunAsync(fsm, ctx).Forget();
        }

        private async UniTaskVoid RunAsync(IFsm<AssetPatchContext> fsm, AssetPatchContext ctx)
        {
            CancellationToken ct = ctx.CancellationToken;
            try
            {
                if (ctx.TotalPendingDownloadCount <= 0)
                {
                    ctx.FireSteps("资源已全部就绪！");
                    await ctx.FireProgressAndWaitReadyAsync(1f, "资源已就绪…", ct);
                    if (ct.IsCancellationRequested || fsm.IsDestroyed)
                        return;

                    ChangeState<ClearCacheBundleState>(fsm);
                    return;
                }

                ctx.FireSteps("发现资源更新，等待确认下载…");
                ctx.Fire(AssetPatchFoundUpdateFilesEventArgs.Create(
                    ctx.TotalPendingDownloadCount,
                    ctx.TotalPendingDownloadBytes,
                    null));
                // 等待 UI：AssetPatchUserBeginDownload → DownloadPackageFilesState
            }
            catch (System.OperationCanceledException)
            {
            }
        }
    }
}
