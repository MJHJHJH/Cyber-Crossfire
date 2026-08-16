using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 默认界面辅助器：按 <see cref="UIForm.CanvasKind"/> 挂到 DualUIGroupHelper 对应物理根。
    /// </summary>
    public sealed class DefaultUIFormHelper : MonoBehaviour, IUIFormHelper
    {
        public object InstantiateUIForm(object uiFormAsset)
        {
            return Instantiate((Object)uiFormAsset);
        }

        public IUIForm CreateUIForm(object uiFormInstance, IUIGroup uiGroup, object userData)
        {
            GameObject go = uiFormInstance as GameObject;
            if (go == null)
            {
                Debug.LogError("UI form instance is invalid.");
                return null;
            }

            UIForm uiForm = go.GetOrAddComponent<UIForm>();
            Transform parent = ResolveParent(uiGroup, uiForm.CanvasKind);
            if (parent == null)
            {
                Debug.LogError($"UI form '{go.name}' can not resolve parent under group '{uiGroup?.Name}'.");
                return null;
            }

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localScale = Vector3.one;
            StretchFull(t as RectTransform);

            return uiForm;
        }

        public void ReleaseUIForm(object uiFormAsset, object uiFormInstance)
        {
            if (uiFormInstance is Object unityObj)
                Destroy(unityObj);
        }

        static Transform ResolveParent(IUIGroup uiGroup, UIFormCanvasKind canvasKind)
        {
            if (uiGroup?.Helper == null)
                return null;

            if (uiGroup.Helper is DualUIGroupHelper dual)
                return dual.GetParent(canvasKind);

            if (uiGroup.Helper is MonoBehaviour mb)
                return mb.transform;

            return null;
        }

        static void StretchFull(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
