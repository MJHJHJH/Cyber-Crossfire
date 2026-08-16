using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 双 Canvas 下同名物理 Group 根；逻辑 Group 仍只有一份栈。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DualUIGroupHelper : MonoBehaviour, IUIGroupHelper
    {
        Transform m_OverlayRoot;
        Transform m_CameraRoot;
        int m_Depth;

        public Transform OverlayRoot => m_OverlayRoot;
        public Transform CameraRoot => m_CameraRoot;
        public int Depth => m_Depth;

        public void Bind(Transform overlayRoot, Transform cameraRoot)
        {
            m_OverlayRoot = overlayRoot;
            m_CameraRoot = cameraRoot;
        }

        public Transform GetParent(UIFormCanvasKind canvas)
        {
            return canvas == UIFormCanvasKind.Camera ? m_CameraRoot : m_OverlayRoot;
        }

        public void SetDepth(int depth)
        {
            m_Depth = depth;
            // 逻辑深度由 UIManager UIGroup 维护；跨 Canvas 的显示序主要靠各 Canvas.sortingOrder
            // 此处预留扩展（例如同步子 Canvas 的 local sorting）
        }
    }
}
