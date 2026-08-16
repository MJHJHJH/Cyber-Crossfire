using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 界面加载结果。
    /// </summary>
    public sealed class UIFormLoadResult
    {
        public GameObject Asset { get; }
        public Action ReleaseAsset { get; }

        public UIFormLoadResult(GameObject asset, Action releaseAsset)
        {
            Asset = asset;
            ReleaseAsset = releaseAsset;
        }
    }

    /// <summary>
    /// 界面资源加载器。
    /// </summary>
    public interface IUIFormLoader
    {
        UniTask<UIFormLoadResult> LoadAsync(string location, CancellationToken cancellationToken = default);
    }
}
