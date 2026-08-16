using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    public sealed class UpdatePackageManifestState : AssetPatchState
    {
        private const int ManifestTimeoutSeconds = 60;

        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.FireSteps("更新资源清单！");
            RunAsync(fsm, ctx).Forget();
        }

        private async UniTaskVoid RunAsync(IFsm<AssetPatchContext> fsm, AssetPatchContext ctx)
        {
            CancellationToken ct = ctx.CancellationToken;
            try
            {
                ResourcePackage package = YooAssets.GetPackage(ctx.PackageName);
                var options = new LoadPackageManifestOptions(ctx.PackageVersion, ManifestTimeoutSeconds);
                LoadPackageManifestOperation operation = package.LoadPackageManifestAsync(options);
                await operation.ToUniTask().AttachExternalCancellation(ct);
                if (ct.IsCancellationRequested || fsm.IsDestroyed)
                    return;

                if (operation.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogWarning(operation.Error);
                    ctx.Fire(AssetPatchManifestUpdateFailedEventArgs.Create(operation.Error, ctx.PackageName));
                    return;
                }

                ChangeState<CreateDownloaderState>(fsm);
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(ex);
                ctx.Fire(AssetPatchManifestUpdateFailedEventArgs.Create(ex.Message, ctx.PackageName));
            }
        }
    }
}
