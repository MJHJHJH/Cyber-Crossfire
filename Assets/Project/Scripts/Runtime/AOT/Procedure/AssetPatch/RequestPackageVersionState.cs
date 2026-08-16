using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    public sealed class RequestPackageVersionState : AssetPatchState
    {
        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.FireSteps("请求资源版本 !");
            RunAsync(fsm, ctx).Forget();
        }

        private async UniTaskVoid RunAsync(IFsm<AssetPatchContext> fsm, AssetPatchContext ctx)
        {
            CancellationToken ct = ctx.CancellationToken;
            try
            {
                ResourcePackage package = YooAssets.GetPackage(ctx.PackageName);
                RequestPackageVersionOperation operation = package.RequestPackageVersionAsync();
                await operation.ToUniTask().AttachExternalCancellation(ct);
                if (ct.IsCancellationRequested || fsm.IsDestroyed)
                    return;

                if (operation.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogWarning(operation.Error);
                    ctx.Fire(AssetPatchVersionRequestFailedEventArgs.Create(operation.Error, ctx.PackageName));
                    return;
                }

                Debug.Log($"Request package version : {operation.PackageVersion}");
                ctx.PackageVersion = operation.PackageVersion;
                ChangeState<UpdatePackageManifestState>(fsm);
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(ex);
                ctx.Fire(AssetPatchVersionRequestFailedEventArgs.Create(ex.Message, ctx.PackageName));
            }
        }
    }
}
