using UnityEngine;
using UnityEngine.Scripting;

namespace UI.SafeArea
{
    /// <summary>
    /// 安全区调试工具：挂到任意场景节点（如 UI 根），运行时在屏幕左上角实时输出
    /// 安全区数据，并以红色遮罩直观显示非安全区（刘海/挖孔/圆角/手势条）范围。
    /// 用于真机 / Device Simulator / 编辑器模拟下验证 SafeAreaFitter 适配正确性。
    /// </summary>
    [Preserve]
    public sealed class SafeAreaDebug : MonoBehaviour
    {
        [Tooltip("是否绘制非安全区红色遮罩 + 安全区绿色描边")]
        [SerializeField] private bool m_DrawOverlay = true;

        [Tooltip("是否显示左上角数据文本")]
        [SerializeField] private bool m_ShowText = true;

        [Tooltip("文本字号")]
        [SerializeField] private int m_FontSize = 22;

        private GUIStyle m_Style;

        private void OnGUI()
        {
            SafeAreaProvider.RefreshIfDirty();

            Rect safe = SafeAreaProvider.SafeAreaRect;
            Vector2 size = SafeAreaProvider.ScreenSize;
            Vector4 insets = SafeAreaProvider.NormalizedInsets;

            // GUI 坐标系 y 向下，屏幕坐标 y 向上，先翻转安全区。
            Rect guiSafe = new Rect(safe.xMin, size.y - safe.yMax, safe.width, safe.height);

            if (m_DrawOverlay && size.x > 0f && size.y > 0f)
            {
                // 非安全区红色遮罩：左右竖条 + 上下横条
                GUI.color = new Color(1f, 0f, 0f, 0.25f);
                DrawRect(new Rect(0f, 0f, guiSafe.xMin, size.y));                       // Left
                DrawRect(new Rect(guiSafe.xMax, 0f, size.x - guiSafe.xMax, size.y));    // Right
                DrawRect(new Rect(guiSafe.xMin, 0f, guiSafe.width, guiSafe.yMin));      // Top
                DrawRect(new Rect(guiSafe.xMin, guiSafe.yMax, guiSafe.width, size.y - guiSafe.yMax)); // Bottom

                // 安全区绿色描边
                GUI.color = new Color(0f, 1f, 0f, 0.7f);
                DrawRect(new Rect(guiSafe.xMin, guiSafe.yMin, guiSafe.width, 2f));
                DrawRect(new Rect(guiSafe.xMin, guiSafe.yMax - 2f, guiSafe.width, 2f));
                DrawRect(new Rect(guiSafe.xMin, guiSafe.yMin, 2f, guiSafe.height));
                DrawRect(new Rect(guiSafe.xMax - 2f, guiSafe.yMin, 2f, guiSafe.height));

                GUI.color = Color.white;
            }

            if (m_ShowText)
            {
                if (m_Style == null)
                {
                    m_Style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.Max(12, m_FontSize),
                        fontStyle = FontStyle.Bold,
                    };
                    m_Style.normal.textColor = Color.white;
                }

                GUILayout.BeginArea(new Rect(8f, 8f, 720f, 220f));
                GUILayout.Label($"SafeArea  : {safe}", m_Style);
                GUILayout.Label($"Screen    : {size}", m_Style);
                GUILayout.Label($"Insets    : L={insets.x:F3} B={insets.y:F3} R={insets.z:F3} T={insets.w:F3}", m_Style);
                GUILayout.EndArea();
            }
        }

        private static void DrawRect(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
