using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 默认界面组辅助器。
    /// </summary>
    public sealed class DefaultUIGroupHelper : MonoBehaviour, IUIGroupHelper
    {
        public void SetDepth(int depth)
        {
            // UGUI：可用 Canvas.sortingOrder = depth * 100 等扩展；首版留空
        }
    }
}
