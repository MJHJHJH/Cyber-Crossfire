using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace CommandoRobot
{
    /// <summary>
    /// 枪械逻辑 Prefab 的 YooAsset handle 缓存：同 location 复用，不重复加载。
    /// </summary>
    public sealed class WeaponPrefabCache
    {
        private const string DefaultPackageName = "DefaultPackage";

        private readonly Dictionary<string, AssetHandle> _handles =
            new Dictionary<string, AssetHandle>(StringComparer.Ordinal);

        public async UniTask<GameObject> GetAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                return null;

            if (_handles.TryGetValue(location, out AssetHandle cached) &&
                cached != null &&
                cached.IsValid &&
                cached.Status == EOperationStatus.Succeeded)
            {
                return cached.GetAssetObject<GameObject>();
            }

            if (!YooAssets.TryGetPackage(DefaultPackageName, out ResourcePackage package) || package == null)
            {
                Debug.LogWarning($"[WeaponPrefabCache] Package '{DefaultPackageName}' unavailable. location={location}");
                return null;
            }

            if (cached != null)
            {
                if (cached.IsValid)
                    cached.Release();
                _handles.Remove(location);
            }

            AssetHandle handle = package.LoadAssetAsync<GameObject>(location);
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
                Debug.LogWarning($"[WeaponPrefabCache] Load '{location}' failure: {error}");
                return null;
            }

            GameObject prefab = handle.GetAssetObject<GameObject>();
            if (prefab == null)
            {
                handle.Release();
                Debug.LogWarning($"[WeaponPrefabCache] '{location}' is not a GameObject.");
                return null;
            }

            _handles[location] = handle;
            return prefab;
        }

        public void ReleaseAll()
        {
            foreach (KeyValuePair<string, AssetHandle> pair in _handles)
            {
                AssetHandle handle = pair.Value;
                if (handle != null && handle.IsValid)
                    handle.Release();
            }

            _handles.Clear();
        }
    }
}
