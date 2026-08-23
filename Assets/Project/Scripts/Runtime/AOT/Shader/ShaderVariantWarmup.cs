using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace GamePlay
{
    /// <summary>
    /// 着色器变体预热：在启动盖屏期加载 ShaderVariantCollection 并 WarmUp，
    /// 把变体编译成本（编辑器 = Unity 编译器；真机 = GPU 驱动编译）前移到初始化阶段，
    /// 避免首次进入场景时逐帧编译导致的卡顿 / shader 动画停滞。
    /// </summary>
    public static class ShaderVariantWarmup
    {
        /// <summary>YooAsset 收集器地址（AddressByFileName，对应 Assets/Project/ShaderVariants/MyShaderVariants.shadervariants）。</summary>
        public const string ShaderVariantLocation = "MyShaderVariants";

        /// <summary>会话期持有句柄：防止 unityshaders 共享包在预热后被卸载，导致已编译的 GPU 程序随 shader 对象销毁。</summary>
        private static AssetHandle _handle;

        /// <summary>
        /// 执行预热。任何失败只告警、不抛异常，绝不阻断启动流程。
        /// </summary>
        /// <param name="package">已初始化完成的资源包（DefaultPackage）。</param>
        public static async UniTask WarmupAsync(ResourcePackage package)
        {
            if (package == null)
            {
                Debug.LogWarning("[ShaderVariantWarmup] Package is null, skip warmup.");
                return;
            }

            // 已预热过（如流程重入），复用已加载的集合，WarmUp 幂等且极快
            if (_handle != null && _handle.IsValid)
            {
                ShaderVariantCollection cached = _handle.GetAssetObject<ShaderVariantCollection>();
                if (cached != null)
                {
                    cached.WarmUp();
                    return;
                }
            }

            try
            {
                _handle = package.LoadAssetAsync<ShaderVariantCollection>(ShaderVariantLocation);
                await _handle.ToUniTask();

                if (_handle.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogWarning(
                        $"[ShaderVariantWarmup] Load '{ShaderVariantLocation}' failed: {_handle.Error}");
                    _handle.Release();
                    _handle = null;
                    return;
                }

                ShaderVariantCollection svc = _handle.GetAssetObject<ShaderVariantCollection>();
                if (svc == null)
                {
                    Debug.LogWarning($"[ShaderVariantWarmup] Asset '{ShaderVariantLocation}' is null.");
                    _handle.Release();
                    _handle = null;
                    return;
                }

                svc.WarmUp();
                Debug.Log($"[ShaderVariantWarmup] Warm up completed: {svc.shaderCount} shaders, {svc.variantCount} variants.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShaderVariantWarmup] Failed: {ex.Message}");
                if (_handle != null)
                {
                    _handle.Release();
                    _handle = null;
                }
            }
        }
    }
}
