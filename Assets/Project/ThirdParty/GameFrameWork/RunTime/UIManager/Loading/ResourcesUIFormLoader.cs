using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 基于 Unity Resources 的界面加载器。
    /// location 为相对 Resources 的路径（无扩展名），会直接传给 <see cref="Resources.LoadAsync{T}(string)"/>。
    /// 例：文件 Assets/Resources/UI/MainHud.prefab → location = "UI/MainHud"
    /// </summary>
    public sealed class ResourcesUIFormLoader : IUIFormLoader
    {
        public async UniTask<UIFormLoadResult> LoadAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("UI form location is invalid.");

            ResourceRequest request = Resources.LoadAsync<GameObject>(location);
            Object loaded = await request.ToUniTask(cancellationToken: cancellationToken);

            GameObject asset = loaded as GameObject;
            if (asset == null)
            {
                throw new GameFrameworkException(
                    Utility.Text.Format("Load UI form from Resources failure, location '{0}'.", location));
            }

            // Prefab 是 GameObject，不能 Resources.UnloadAsset；此处不持有额外句柄。
            // 实例销毁由 UIFormHelper 负责；UnloadUnusedAssets 由外部（非 UI）在适当时机调用。
            return new UIFormLoadResult(asset, null);
        }
    }
}
