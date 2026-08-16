using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    public sealed class InitializePackageState : AssetPatchState
    {
        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.FireSteps("初始化资源包！");
            RunAsync(fsm, ctx).Forget();
        }

        private async UniTaskVoid RunAsync(IFsm<AssetPatchContext> fsm, AssetPatchContext ctx)
        {
            CancellationToken ct = ctx.CancellationToken;
            try
            {
                if (!YooAssets.IsInitialized)
                    YooAssets.Initialize();

                if (!YooAssets.TryGetPackage(ctx.PackageName, out ResourcePackage package) || package == null)
                    package = YooAssets.CreatePackage(ctx.PackageName);

                InitializePackageOperation initializationOperation = CreateInitializeOperation(package, ctx);
                if (initializationOperation == null)
                {
                    ctx.Fire(AssetPatchInitializeFailedEventArgs.Create(
                        $"Unsupported play mode: {ctx.PlayMode}", ctx.PackageName));
                    return;
                }

                await initializationOperation.ToUniTask().AttachExternalCancellation(ct);
                if (ct.IsCancellationRequested || fsm.IsDestroyed)
                    return;

                if (initializationOperation.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogWarning(initializationOperation.Error);
                    ctx.Fire(AssetPatchInitializeFailedEventArgs.Create(
                        initializationOperation.Error, ctx.PackageName));
                    return;
                }

                ChangeState<RequestPackageVersionState>(fsm);
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(ex);
                ctx.Fire(AssetPatchInitializeFailedEventArgs.Create(ex.Message, ctx.PackageName));
            }
        }

        private static InitializePackageOperation CreateInitializeOperation(ResourcePackage package, AssetPatchContext ctx)
        {
            EPlayMode playMode = ctx.PlayMode;

            if (playMode == EPlayMode.EditorSimulateMode)
            {
                var buildResult = EditorSimulateBuildInvoker.Build(
                    ctx.PackageName, (int)EBundleType.VirtualAssetBundle);
                var createParameters = new EditorSimulateModeOptions();
                createParameters.EditorFileSystemParameters =
                    FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
                return package.InitializePackageAsync(createParameters);
            }

            if (playMode == EPlayMode.OfflinePlayMode)
            {
                var createParameters = new OfflinePlayModeOptions();
                createParameters.BuiltinFileSystemParameters =
                    FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                return package.InitializePackageAsync(createParameters);
            }

            if (playMode == EPlayMode.HostPlayMode)
            {
                string defaultHostServer = ctx.BuildHostServerURL(ctx.HostServerIP);
                string fallbackHostServer = ctx.BuildHostServerURL(ctx.FallbackHostServerIP);
                IRemoteService remoteService = new RemoteService(defaultHostServer, fallbackHostServer);
                var createParameters = new HostPlayModeOptions();
                createParameters.BuiltinFileSystemParameters =
                    FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                createParameters.CacheFileSystemParameters =
                    FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
                return package.InitializePackageAsync(createParameters);
            }

            if (playMode == EPlayMode.WebPlayMode)
            {
                var createParameters = new WebPlayModeOptions();
                createParameters.WebServerFileSystemParameters =
                    FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
                return package.InitializePackageAsync(createParameters);
            }

            return null;
        }

        private sealed class RemoteService : IRemoteService
        {
            private readonly string m_DefaultHostServer;
            private readonly string m_FallbackHostServer;

            public RemoteService(string defaultHostServer, string fallbackHostServer)
            {
                m_DefaultHostServer = defaultHostServer;
                m_FallbackHostServer = fallbackHostServer;
            }

            public IReadOnlyList<string> GetRemoteUrls(string fileName)
            {
                return new[]
                {
                    $"{m_DefaultHostServer}/{fileName}",
                    $"{m_FallbackHostServer}/{fileName}",
                };
            }
        }
    }
}
