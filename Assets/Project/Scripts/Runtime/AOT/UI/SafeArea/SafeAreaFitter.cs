using UnityEngine;
using UnityEngine.Scripting;

namespace UI.SafeArea
{
    /// <summary>
    /// 安全区适配组件：挂在任意 RectTransform 上（面板根节点或内容节点），
    /// 按 <see cref="SafeAreaProvider"/> 的归一化 insets 折算到画布坐标系并叠加到 offsetMin/offsetMax。
    ///
    /// 设计要点：
    /// - 锚点保持：不改 anchorMin/Max，只在基线 offset 上叠加增量，与美术锚点布局完全兼容；
    /// - PC / 无刘海设备自动零偏移（insets 全 0）；
    /// - 对象池安全：基线只采集一次，复用实例重复应用同一基线；
    /// - 编辑器编辑态不写 rect（避免污染 prefab 序列化），仅绘制 Gizmo 预览。
    /// </summary>
    [DisallowMultipleComponent]
    [Preserve]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [System.Flags]
        public enum SafeAreaEdge
        {
            None = 0,
            Left = 1 << 0,
            Right = 1 << 1,
            Top = 1 << 2,
            Bottom = 1 << 3,
            All = Left | Right | Top | Bottom,
        }

        [Header("Safe Area")]
        [Tooltip("参与缩进的边；未勾选的边不响应安全区（padding 仍生效）")]
        [SerializeField] private SafeAreaEdge m_Edges = SafeAreaEdge.All;

        [Tooltip("Expand：向刘海/圆角区域反向扩展（用于全屏背景图铺满安全区外）")]
        [SerializeField] private bool m_Expand;

        [Header("Padding (canvas units)")]
        [Tooltip("在安全区基础上每边额外留白（canvas 单位）。x=Left, y=Bottom, z=Right, w=Top")]
        [SerializeField] private Vector4 m_Padding;

        private RectTransform m_Rect;
        private RectTransform m_CanvasRect;
        private Vector2 m_BaseOffsetMin;
        private Vector2 m_BaseOffsetMax;
        private bool m_BaseCaptured;
        private bool m_Applying;

        private void Awake()
        {
            m_Rect = (RectTransform)transform;
            ResolveCanvas();
            CaptureBaseline();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            // Inspector 修改时（含运行期）立即生效；编辑态 Apply 内部会跳过。
            if (m_BaseCaptured)
                Apply();
        }

        private void Update()
        {
            // 方向/分辨率/开孔变化：脏检查成本仅两次缓存比较。
            if (SafeAreaProvider.RefreshIfDirty())
                Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!Application.isPlaying)
                return;

            // 画布尺寸变化（横竖屏/分辨率）或父级布局驱动本节点尺寸变化时重算。
            if (m_Rect != null && !m_Applying)
                Apply();
        }

        /// <summary>立即按当前安全区重算一次（代码动态修改锚点/尺寸后调用）。</summary>
        public void ApplyNow()
        {
            CaptureBaselineIfNeeded();
            Apply();
        }

        /// <summary>当前生效的安全区边缘（供调试/动态布局查询）。</summary>
        public SafeAreaEdge Edges => m_Edges;

        private void ResolveCanvas()
        {
            if (m_CanvasRect != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            m_CanvasRect = canvas != null ? (RectTransform)canvas.transform : null;

            if (m_CanvasRect == null)
                Debug.LogWarning($"[SafeAreaFitter] '{name}' 未找到上级 Canvas，已跳过适配。", this);
        }

        private void CaptureBaseline()
        {
            if (m_Rect == null)
                return;

            m_BaseOffsetMin = m_Rect.offsetMin;
            m_BaseOffsetMax = m_Rect.offsetMax;
            m_BaseCaptured = true;
        }

        private void CaptureBaselineIfNeeded()
        {
            if (!m_BaseCaptured)
                CaptureBaseline();
        }

        private void Apply()
        {
            if (!Application.isPlaying)
                return; // 编辑态只做 Gizmo 预览，不修改 rect（避免污染 prefab 序列化）

            if (m_Applying)
                return;

            m_Applying = true;
            try
            {
                if (m_Rect == null)
                    m_Rect = (RectTransform)transform;

                ResolveCanvas();
                CaptureBaselineIfNeeded();

                if (m_Rect == null || m_CanvasRect == null)
                    return;

                SafeAreaProvider.RefreshIfDirty();

                Vector4 insets = SafeAreaProvider.NormalizedInsets;
                float canvasWidth = m_CanvasRect.rect.width;
                float canvasHeight = m_CanvasRect.rect.height;
                float sign = m_Expand ? -1f : 1f;

                float left = (m_Padding.x + (m_Edges.HasFlag(SafeAreaEdge.Left) ? insets.x * canvasWidth : 0f)) * sign;
                float bottom = (m_Padding.y + (m_Edges.HasFlag(SafeAreaEdge.Bottom) ? insets.y * canvasHeight : 0f)) * sign;
                float right = (m_Padding.z + (m_Edges.HasFlag(SafeAreaEdge.Right) ? insets.z * canvasWidth : 0f)) * sign;
                float top = (m_Padding.w + (m_Edges.HasFlag(SafeAreaEdge.Top) ? insets.w * canvasHeight : 0f)) * sign;

                m_Rect.offsetMin = m_BaseOffsetMin + new Vector2(left, bottom);
                m_Rect.offsetMax = m_BaseOffsetMax + new Vector2(-right, -top);
            }
            finally
            {
                m_Applying = false;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enabled)
                return;

            // 仅运行态绘制（Device Simulator / 真机数据），编辑态不画。
            if (!Application.isPlaying)
                return;

            DrawSafeAreaGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawSafeAreaGizmo(true);
        }

        private void DrawSafeAreaGizmo(bool selected)
        {
            RectTransform canvasRect = m_CanvasRect;
            if (canvasRect == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            }

            if (canvasRect == null)
                return;

            Rect canvasLocal = canvasRect.rect;
            Vector4 insets = SafeAreaProvider.NormalizedInsets;

            Rect safeLocal = new Rect(
                canvasLocal.x + insets.x * canvasLocal.width,
                canvasLocal.y + insets.y * canvasLocal.height,
                canvasLocal.width * (1f - insets.x - insets.z),
                canvasLocal.height * (1f - insets.y - insets.w));

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = canvasRect.localToWorldMatrix;

            // 安全区线框
            Gizmos.color = selected ? new Color(0f, 1f, 0.4f, 1f) : new Color(0f, 0.85f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(safeLocal.center, safeLocal.size);

            // 非安全区（刘海/圆角）着色
            Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.10f);
            DrawSolidRect(new Rect(canvasLocal.xMin, canvasLocal.yMin, safeLocal.xMin - canvasLocal.xMin, canvasLocal.height));
            DrawSolidRect(new Rect(safeLocal.xMax, canvasLocal.yMin, canvasLocal.xMax - safeLocal.xMax, canvasLocal.height));
            DrawSolidRect(new Rect(safeLocal.xMin, canvasLocal.yMin, safeLocal.width, safeLocal.yMin - canvasLocal.yMin));
            DrawSolidRect(new Rect(safeLocal.xMin, safeLocal.yMax, safeLocal.width, canvasLocal.yMax - safeLocal.yMax));

            Gizmos.matrix = prevMatrix;
        }

        private static void DrawSolidRect(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Gizmos.DrawCube(rect.center, rect.size);
        }
#endif
    }
}
