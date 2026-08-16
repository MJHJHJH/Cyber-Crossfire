using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// YooAsset 配表加载：Json→TextAsset，Bin→RawFileObject（YooAsset 3.x）；location=文件名。
    /// </summary>
    public sealed class YooLubanDataLoader : ILubanDataLoader
    {
        private readonly ResourcePackage _package;

        public YooLubanDataLoader(ResourcePackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public async UniTask<string> LoadTextAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Luban json location is invalid.");

            AssetHandle handle = _package.LoadAssetAsync<TextAsset>(location);
            try
            {
                await handle.ToUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch
            {
                handle.Release();
                throw;
            }

            return TakeText(handle, location);
        }

        public string LoadText(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Luban json location is invalid.");

            AssetHandle handle = _package.LoadAssetAsync<TextAsset>(location);
            handle.WaitForAsyncComplete();
            return TakeText(handle, location);
        }

        public async UniTask<byte[]> LoadBytesAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Luban bin location is invalid.");

            AssetHandle handle = _package.LoadAssetAsync<RawFileObject>(location);
            try
            {
                await handle.ToUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch
            {
                handle.Release();
                throw;
            }

            return TakeBytes(handle, location);
        }

        public byte[] LoadBytes(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Luban bin location is invalid.");

            AssetHandle handle = _package.LoadAssetAsync<RawFileObject>(location);
            handle.WaitForAsyncComplete();
            return TakeBytes(handle, location);
        }

        private static string TakeText(AssetHandle handle, string location)
        {
            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                throw new GameFrameworkException(
                    Utility.Text.Format("Load Luban json '{0}' failure: {1}", location, error));
            }

            TextAsset asset = handle.GetAssetObject<TextAsset>();
            if (asset == null)
            {
                handle.Release();
                throw new GameFrameworkException(
                    Utility.Text.Format("Luban json '{0}' is not a TextAsset.", location));
            }

            string text = asset.text;
            handle.Release();
            return text;
        }

        private static byte[] TakeBytes(AssetHandle handle, string location)
        {
            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                throw new GameFrameworkException(
                    Utility.Text.Format("Load Luban bin '{0}' failure: {1}", location, error));
            }

            RawFileObject raw = handle.GetAssetObject<RawFileObject>();
            if (raw == null)
            {
                handle.Release();
                throw new GameFrameworkException(
                    Utility.Text.Format("Luban bin '{0}' is not a RawFileObject.", location));
            }

            byte[] bytes = raw.GetBytes();
            handle.Release();
            if (bytes == null || bytes.Length == 0)
            {
                throw new GameFrameworkException(
                    Utility.Text.Format("Luban bin '{0}' raw data is empty.", location));
            }

            return bytes;
        }
    }
}
