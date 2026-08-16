using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 界面逻辑基类。
    /// </summary>
    public abstract class UIFormLogic : MonoBehaviour
    {
        private bool m_Available;
        private bool m_Visible;
        private UIForm m_UIForm;
        private Transform m_CachedTransform;
        private int m_OriginalLayer;

        public UIForm UIForm => m_UIForm;

        public string Name
        {
            get => gameObject.name;
            set => gameObject.name = value;
        }

        public bool Available => m_Available;

        public bool Visible
        {
            get => m_Available && m_Visible;
            set
            {
                if (!m_Available)
                {
                    Debug.LogWarning($"UI form '{Name}' is not available.");
                    return;
                }

                if (m_Visible == value)
                    return;

                m_Visible = value;
                InternalSetVisible(value);
            }
        }

        public Transform CachedTransform => m_CachedTransform;

        protected internal virtual void OnInit(object userData)
        {
            if (m_CachedTransform == null)
                m_CachedTransform = transform;

            m_UIForm = GetComponent<UIForm>();
            m_OriginalLayer = gameObject.layer;
        }

        protected internal virtual void OnRecycle()
        {
        }

        protected internal virtual void OnOpen(object userData)
        {
            m_Available = true;
            Visible = true;
        }

        protected internal virtual void OnClose(bool isShutdown, object userData)
        {
            gameObject.SetLayerRecursively(m_OriginalLayer);
            Visible = false;
            m_Available = false;
        }

        protected internal virtual void OnPause() => Visible = false;

        protected internal virtual void OnResume() => Visible = true;

        protected internal virtual void OnCover()
        {
        }

        protected internal virtual void OnReveal()
        {
        }

        protected internal virtual void OnRefocus(object userData)
        {
        }

        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        protected internal virtual void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
        }

        protected virtual void InternalSetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
