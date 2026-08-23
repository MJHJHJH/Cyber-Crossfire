#if UNITY_EDITOR
using UI.SafeArea;
using UnityEditor;
using UnityEngine;

namespace UI.SafeArea.EditorTools
{
    /// <summary>
    /// SafeArea 编辑器工具：Scene 视图安全区参考线 + Hierarchy 快捷添加组件。
    /// 安全区数据来自真实 Screen.safeArea（Device Simulator / 真机），本工具不提供内置模拟。
    /// </summary>
    [InitializeOnLoad]
    public static class SafeAreaEditorTools
    {
        const string k_OverlayKey = "UI.SafeArea.Overlay";

        static SafeAreaEditorTools()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // ---------------- Scene 参考线 ----------------

        [MenuItem("Tools/Safe Area/Scene Overlay (参考线)", false, 30)]
        static void ToggleOverlay()
        {
            EditorPrefs.SetBool(k_OverlayKey, !EditorPrefs.GetBool(k_OverlayKey, true));
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Safe Area/Scene Overlay (参考线)", true, 30)]
        static bool ToggleOverlayValidate()
        {
            Menu.SetChecked("Tools/Safe Area/Scene Overlay (参考线)", EditorPrefs.GetBool(k_OverlayKey, true));
            return true;
        }

        // ---------------- Hierarchy 快捷添加 ----------------

        [MenuItem("GameObject/UI/Safe Area Fitter", false, 12)]
        static void AddToSelection()
        {
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
                return;

            foreach (GameObject go in Selection.gameObjects)
            {
                if (!go.TryGetComponent<SafeAreaFitter>(out _))
                    go.AddComponent<SafeAreaFitter>();
            }
        }

        // ---------------- Scene 视图绘制 ----------------

        /// <summary>
        /// 运行态（Device Simulator / 真机）下在 Scene 视图绘制安全区参考线，
        /// 便于调 prefab 时对齐真实安全区边界。
        /// </summary>
        static void OnSceneGUI(SceneView sceneView)
        {
            if (!EditorPrefs.GetBool(k_OverlayKey, true))
                return;

            if (!Application.isPlaying)
                return;

            if (SafeAreaProvider.NormalizedInsets == Vector4.zero)
                return;

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
                return;

            RectTransform canvasRect = (RectTransform)canvas.transform;
            Rect canvasLocal = canvasRect.rect;
            Vector4 insets = SafeAreaProvider.NormalizedInsets;

            Rect safeLocal = new Rect(
                canvasLocal.x + insets.x * canvasLocal.width,
                canvasLocal.y + insets.y * canvasLocal.height,
                canvasLocal.width * (1f - insets.x - insets.z),
                canvasLocal.height * (1f - insets.y - insets.w));

            Vector3[] corners =
            {
                new Vector3(safeLocal.xMin, safeLocal.yMin, 0f),
                new Vector3(safeLocal.xMax, safeLocal.yMin, 0f),
                new Vector3(safeLocal.xMax, safeLocal.yMax, 0f),
                new Vector3(safeLocal.xMin, safeLocal.yMax, 0f),
            };

            Handles.matrix = canvasRect.localToWorldMatrix;
            Handles.DrawSolidRectangleWithOutline(
                corners,
                new Color(0f, 1f, 0f, 0.05f),
                new Color(0f, 1f, 0.4f, 0.8f));
            Handles.Label(corners[3] + new Vector3(0f, 14f, 0f), "Safe Area");
            Handles.matrix = Matrix4x4.identity;
        }
    }
}
#endif
