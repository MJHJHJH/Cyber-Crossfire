using System;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 界面挂载的目标 Canvas。
    /// </summary>
    public enum UIFormCanvasKind
    {
        Overlay = 0,
        Camera = 1
    }

    /// <summary>
    /// UGUI 界面实例。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIForm : MonoBehaviour, IUIForm
    {
        [SerializeField] UIFormCanvasKind m_CanvasKind = UIFormCanvasKind.Overlay;

        [SerializeField]
        [Tooltip("是否允许同 location 多开；默认 false（单例）。后续可由配置表写入。")]
        private bool m_AllowMultiInstance = false;

        private int m_SerialId;
        private string m_UIFormAssetName;
        private IUIGroup m_UIGroup;
        private int m_DepthInUIGroup;
        private bool m_PauseCoveredUIForm;
        private UIFormLogic m_UIFormLogic;

        public int SerialId => m_SerialId;
        public string UIFormAssetName => m_UIFormAssetName;
        public object Handle => gameObject;
        public IUIGroup UIGroup => m_UIGroup;
        public int DepthInUIGroup => m_DepthInUIGroup;
        public bool PauseCoveredUIForm => m_PauseCoveredUIForm;
        public UIFormLogic Logic => m_UIFormLogic;

        public bool AllowMultiInstance
        {
            get => m_AllowMultiInstance;
            set => m_AllowMultiInstance = value;
        }

        public UIFormCanvasKind CanvasKind
        {
            get => m_CanvasKind;
            set => m_CanvasKind = value;
        }

        public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
        {
            m_SerialId = serialId;
            m_UIFormAssetName = uiFormAssetName;
            m_UIGroup = uiGroup;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = pauseCoveredUIForm;

            if (!isNewInstance)
                return;

            m_UIFormLogic = GetComponent<UIFormLogic>();
            if (m_UIFormLogic == null)
            {
                Debug.LogError($"UI form '{uiFormAssetName}' can not get UI form logic.");
                return;
            }

            try
            {
                m_UIFormLogic.OnInit(userData);
            }
            catch (Exception e)
            {
                Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnInit exception: {e}");
            }
        }

        public void OnRecycle()
        {
            try
            {
                m_UIFormLogic?.OnRecycle();
            }
            catch (Exception e)
            {
                Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnRecycle exception: {e}");
            }

            m_SerialId = 0;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = true;
        }

        public void OnOpen(object userData)
        {
            try { m_UIFormLogic?.OnOpen(userData); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnOpen exception: {e}"); }
        }

        public void OnClose(bool isShutdown, object userData)
        {
            try { m_UIFormLogic?.OnClose(isShutdown, userData); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnClose exception: {e}"); }
        }

        public void OnPause()
        {
            try { m_UIFormLogic?.OnPause(); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnPause exception: {e}"); }
        }

        public void OnResume()
        {
            try { m_UIFormLogic?.OnResume(); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnResume exception: {e}"); }
        }

        public void OnCover()
        {
            try { m_UIFormLogic?.OnCover(); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnCover exception: {e}"); }
        }

        public void OnReveal()
        {
            try { m_UIFormLogic?.OnReveal(); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnReveal exception: {e}"); }
        }

        public void OnRefocus(object userData)
        {
            try { m_UIFormLogic?.OnRefocus(userData); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnRefocus exception: {e}"); }
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            try { m_UIFormLogic?.OnUpdate(elapseSeconds, realElapseSeconds); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnUpdate exception: {e}"); }
        }

        public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            m_DepthInUIGroup = depthInUIGroup;
            try { m_UIFormLogic?.OnDepthChanged(uiGroupDepth, depthInUIGroup); }
            catch (Exception e) { Debug.LogError($"UI Form '[{m_SerialId}]{m_UIFormAssetName}' OnDepthChanged exception: {e}"); }
        }
    }
}
