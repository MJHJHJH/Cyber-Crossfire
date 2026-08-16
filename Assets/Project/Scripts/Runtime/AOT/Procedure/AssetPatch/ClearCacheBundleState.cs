using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    /// <summary>对队列内全部包清理未使用缓存后进入完成。</summary>
    public sealed class ClearCacheBundleState : AssetPatchState
    {
        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.FireSteps("清理未使用的缓存文件！");
            RunAsync(fsm, ctx).Forget();
        }

        private async UniTaskVoid RunAsync(IFsm<AssetPatchContext> fsm, AssetPatchContext ctx)
        {
            CancellationToken ct = ctx.CancellationToken;
            try
            {
                for (int i = 0; i < ctx.PackageCount; i++)
                {
                    string packageName = ctx.GetPackageNameAt(i);
                    ctx.SetActivePackageName(packageName);
                    ResourcePackage package = YooAssets.GetPackage(packageName);
                    if (package == null)
                        continue;

                    var options = new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles);
                    ClearCacheOperation operation = package.ClearCacheAsync(options);
                    await operation.ToUniTask().AttachExternalCancellation(ct);
                    if (ct.IsCancellationRequested || fsm.IsDestroyed)
                        return;
                }

                ChangeState<PatchDoneState>(fsm);
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(ex);
                if (!fsm.IsDestroyed)
                    ChangeState<PatchDoneState>(fsm);
            }
        }
    }
}
