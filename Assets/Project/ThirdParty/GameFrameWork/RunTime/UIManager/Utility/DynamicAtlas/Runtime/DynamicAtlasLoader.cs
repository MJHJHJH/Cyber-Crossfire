using System;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace DynamicAtlas
{
    /// <summary>
    /// 收束 YooAsset 加载：合图成功或失败后均释放 Handle。
    /// 同一 Location+分组 的并发请求会合并为一次加载与一次装箱。
    /// <para>约定：图集资源仅收集进 DefaultPackage，经该包 <c>LoadAssetAsync</c>/<c>LoadAssetSync</c> 加载。</para>
    /// </summary>
    public static class DynamicAtlasLoader
    {
        /// <summary>动态图集专用资源包名（与项目 DefaultPackage 一致）。</summary>
        public const string DefaultPackageName = "DefaultPackage";

        private static readonly Dictionary<string, List<Action<AtlasInsertResult>>> s_pending =
            new Dictionary<string, List<Action<AtlasInsertResult>>>();

        public static bool IsYooAssetReady()
        {
            if (!Application.isPlaying || !YooAssets.IsInitialized)
                return false;

            if (!YooAssets.TryGetPackage(DefaultPackageName, out ResourcePackage package) || package == null)
                return false;

            return package.InitializeStatus == EOperationStatus.Succeeded;
        }

        public static void LoadAndInsertAsync(
            string location,
            DynamicAtlasGroup group,
            Action<AtlasInsertResult> onCompleted)
        {
            if (string.IsNullOrEmpty(location))
            {
                onCompleted?.Invoke(AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture));
                return;
            }

            if (!TryGetDefaultPackage(out ResourcePackage package))
            {
                onCompleted?.Invoke(AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture));
                return;
            }

            if (DynamicAtlasManager.Instance.TryGet(location, group, out AtlasEntry existing))
            {
                existing.RefCount++;
                onCompleted?.Invoke(AtlasInsertResult.Ok(existing));
                return;
            }

            string pendingKey = DynamicAtlasManager.MakeEntryKey(location, group);
            if (s_pending.TryGetValue(pendingKey, out List<Action<AtlasInsertResult>> waiters))
            {
                if (onCompleted != null)
                    waiters.Add(onCompleted);
                return;
            }

            var list = new List<Action<AtlasInsertResult>>();
            if (onCompleted != null)
                list.Add(onCompleted);
            s_pending[pendingKey] = list;

            AssetHandle handle = package.LoadAssetAsync<UnityEngine.Object>(location);
            handle.Completed += completedHandle =>
            {
                AtlasInsertResult result;
                try
                {
                    if (completedHandle == null || completedHandle.Status != EOperationStatus.Succeeded)
                    {
                        result = AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);
                    }
                    else
                    {
                        result = InsertFromAsset(location, completedHandle.AssetObject, group);
                    }
                }
                finally
                {
                    completedHandle?.Release();
                }

                if (!s_pending.TryGetValue(pendingKey, out List<Action<AtlasInsertResult>> callbacks))
                    return;

                s_pending.Remove(pendingKey);

                // 两阶段分发：先一次性补齐所有等待者的引用份额，再逐个回调。
                // 不能在循环内"边分配边回调"：过期回调（组件已销毁/OnDisable）会立即 Release，
                // 若其份额尚未补全，会把 RefCount 归零导致条目被销毁，
                // 后续有效回调将绑定到已销毁的 Sprite（白图）。
                if (result.Success && result.Entry != null)
                {
                    for (int i = 1; i < callbacks.Count; i++)
                        result.Entry.RefCount++;

                    // 无等待者（onCompleted 为 null 的调用）：加载结果无人消费，归还初始份额，
                    // 条目释放进脏缓存（像素保留，后续同 key 请求可零拷贝复活）。
                    if (callbacks.Count == 0)
                        DynamicAtlasManager.Instance.Release(result.Entry.Key);
                }

                for (int i = 0; i < callbacks.Count; i++)
                    callbacks[i]?.Invoke(result);
            };
        }

        public static AtlasInsertResult LoadAndInsertSync(string location, DynamicAtlasGroup group)
        {
            if (string.IsNullOrEmpty(location))
                return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);

            if (!TryGetDefaultPackage(out ResourcePackage package))
                return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);

            if (DynamicAtlasManager.Instance.TryGet(location, group, out AtlasEntry existing))
            {
                existing.RefCount++;
                return AtlasInsertResult.Ok(existing);
            }

            AssetHandle handle = package.LoadAssetSync<UnityEngine.Object>(location);
            try
            {
                if (handle == null || handle.Status != EOperationStatus.Succeeded)
                    return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);

                return InsertFromAsset(location, handle.AssetObject, group);
            }
            finally
            {
                handle?.Release();
            }
        }

        private static bool TryGetDefaultPackage(out ResourcePackage package)
        {
            package = null;
            if (!IsYooAssetReady())
                return false;

            return YooAssets.TryGetPackage(DefaultPackageName, out package) && package != null;
        }

        private static AtlasInsertResult InsertFromAsset(string location, UnityEngine.Object asset, DynamicAtlasGroup group)
        {
            if (asset is Sprite sprite)
                return DynamicAtlasManager.Instance.Insert(location, sprite, group);

            if (asset is Texture texture)
                return DynamicAtlasManager.Instance.Insert(location, texture, group);

            return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);
        }
    }
}
