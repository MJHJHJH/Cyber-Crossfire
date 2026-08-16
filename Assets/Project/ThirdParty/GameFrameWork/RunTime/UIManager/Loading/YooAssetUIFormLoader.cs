using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// 基于 YooAsset 的界面加载器（构造注入 ResourcePackage）。
    /// location = 资源文件名（无扩展名）。
    /// </summary>
    public sealed class YooAssetUIFormLoader : IUIFormLoader
    {
        private readonly ResourcePackage _package;

        public YooAssetUIFormLoader(ResourcePackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public async UniTask<UIFormLoadResult> LoadAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("UI form location is invalid.");

            AssetHandle handle = _package.LoadAssetAsync<GameObject>(location);
            try
            {
                await handle.ToUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch
            {
                handle.Release();
                throw;
            }

            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                throw new GameFrameworkException(
                    Utility.Text.Format("Load UI form '{0}' failure: {1}", location, error));
            }

            GameObject asset = handle.GetAssetObject<GameObject>();
            if (asset == null)
            {
                handle.Release();
                throw new GameFrameworkException(
                    Utility.Text.Format("UI form asset '{0}' is not a GameObject.", location));
            }

            return new UIFormLoadResult(asset, () =>
            {
                if (handle.IsValid)
                    handle.Release();
            });
        }
    }
}
