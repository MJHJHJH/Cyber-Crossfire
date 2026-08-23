using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YooAsset;
using static GameFramework.UIConfig;

namespace GameFramework
{
    /// <summary>
    /// UI 管理宿主：挂在空节点上，持有 Overlay / Camera 双 Canvas 并驱动 UIManager。
    /// Group 由初始化代码（或后续配置表）调用 <see cref="AddGroup"/> 注册。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIComponent : GameFrameworkComponent
    {
        [Header("Canvas Refs")]
        [SerializeField] Canvas m_OverlayCanvas;
        [SerializeField] Canvas m_CameraCanvas;

        [Header("Misc")]
        [SerializeField] bool m_EnsureEventSystem = true;
        [SerializeField] bool m_EnableCloseUIFormCompleteEvent = true;

        UIConfig.OverlayCanvasConfig m_OverlayConfig = UIConfig.OverlayCanvasConfig.Default;
        UIConfig.CameraCanvasConfig m_CameraConfig = UIConfig.CameraCanvasConfig.Default;

        IObjectPoolManager m_ObjectPoolManager;
        DefaultUIFormHelper m_FormHelper;
        CancellationTokenSource m_DestroyCts;
        EventComponent m_EventComponent;

        private IUIManager m_UIManager = null;
        public Canvas OverlayCanvas => m_OverlayCanvas;
        public Canvas CameraCanvas => m_CameraCanvas;
        public UIConfig.OverlayCanvasConfig OverlayConfig => m_OverlayConfig;
        public UIConfig.CameraCanvasConfig CameraConfig => m_CameraConfig;

        protected override void Awake()
        {
            base.Awake();

            if (m_OverlayCanvas == null || m_CameraCanvas == null)
            {
                Debug.LogError("[UIManagerHost] OverlayCanvas and CameraCanvas must be assigned. Do not put Canvas on the UI root.");
                enabled = false;
                return;
            }

            ApplyCanvasConfigs();

            m_DestroyCts = new CancellationTokenSource();
            m_ObjectPoolManager = GameFrameworkEntry.GetModule<IObjectPoolManager>();
            m_UIManager = GameFrameworkEntry.GetModule<IUIManager>();
            m_UIManager.SetObjectPoolManager(m_ObjectPoolManager, UIObjectPoolConfig.Default);

            m_FormHelper = gameObject.GetOrAddComponent<DefaultUIFormHelper>();
            m_UIManager.SetUIFormHelper(m_FormHelper);

            if (m_EnsureEventSystem)
                EnsureEventSystem();

            if (m_EnableCloseUIFormCompleteEvent)
                m_UIManager.CloseUIFormComplete += OnCloseUIFormComplete;
        }

        void Start()
        {
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            if (m_EventComponent == null)
                Debug.LogError("[UIComponent] Event component is invalid.");
        }

        void OnDestroy()
        {
            if (m_UIManager != null && m_EnableCloseUIFormCompleteEvent)
                m_UIManager.CloseUIFormComplete -= OnCloseUIFormComplete;

            m_DestroyCts?.Cancel();
            m_DestroyCts?.Dispose();
            m_DestroyCts = null;

            m_UIManager = null;
            m_ObjectPoolManager = null;
            m_EventComponent = null;
        }

        void OnCloseUIFormComplete(object sender, CloseUIFormCompleteEventArgs e)
        {
            if (m_EventComponent == null)
                return;

            m_EventComponent.Fire(this, CloseUIFormCompleteEventArgs.Create(e));
        }

        public void SetOverlayConfig(UIConfig.OverlayCanvasConfig config, bool applyImmediately = true)
        {
            m_OverlayConfig = config;
            if (applyImmediately && m_OverlayCanvas != null)
                ApplyOverlay(m_OverlayCanvas, m_OverlayConfig);
        }

        public void SetCameraConfig(UIConfig.CameraCanvasConfig config, bool applyImmediately = true)
        {
            m_CameraConfig = config;
            if (applyImmediately && m_CameraCanvas != null)
                ApplyCamera(m_CameraCanvas, m_CameraConfig);
        }

        public void ApplyCanvasConfigs()
        {
            if (m_OverlayCanvas != null)
                ApplyOverlay(m_OverlayCanvas, m_OverlayConfig);

            if (m_CameraCanvas != null)
                ApplyCamera(m_CameraCanvas, m_CameraConfig);
        }

        public void SetYooAssetPackage(ResourcePackage package)
        {
            m_UIManager.SetYooAssetPackage(package);
        }

        public void SetUIFormPanelConfigProvider(IUIFormPanelConfigProvider provider)
        {
            m_UIManager.SetUIFormPanelConfigProvider(provider);
        }

        public bool AddGroup(string groupName, int depth, UIGroupOpenMode openMode = UIGroupOpenMode.ClearToTop)
        {
            if (m_UIManager == null || m_UIManager.HasUIGroup(groupName))
                return false;

            if (m_OverlayCanvas == null || m_CameraCanvas == null)
            {
                Debug.LogError("[UIManagerHost] Cannot AddGroup without Overlay/Camera canvases.");
                return false;
            }

            Transform overlayRoot = CreateGroupRoot(m_OverlayCanvas.transform, groupName);
            Transform cameraRoot = CreateGroupRoot(m_CameraCanvas.transform, groupName);

            var helperGo = new GameObject($"GroupHelper_{groupName}", typeof(DualUIGroupHelper));
            helperGo.transform.SetParent(transform, false);
            DualUIGroupHelper helper = helperGo.GetComponent<DualUIGroupHelper>();
            helper.Bind(overlayRoot, cameraRoot);

            if (!m_UIManager.AddUIGroup(groupName, depth, helper, openMode))
            {
                Destroy(helperGo);
                Destroy(overlayRoot.gameObject);
                Destroy(cameraRoot.gameObject);
                return false;
            }

            helper.SetDepth(depth);
            return true;
        }

        public bool AddGroup(UIConfig.UIGroupConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Name))
                return false;
            return AddGroup(config.Name, config.Depth, config.OpenMode);
        }

        public UniTask<IUIForm> OpenAsync(string location, string groupName = "Default", bool pauseCoveredUIForm = false, object userData = null, CancellationToken cancellationToken = default)
        {
            if (m_DestroyCts == null)
                return m_UIManager.OpenAsync(location, groupName, pauseCoveredUIForm, userData, cancellationToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_DestroyCts.Token);
            return OpenAsyncLinked(location, groupName, pauseCoveredUIForm, userData, linked);
        }

        public UniTask<IUIForm> OpenAsync(int panelId, object userData = null, CancellationToken cancellationToken = default)
        {
            if (m_DestroyCts == null)
                return OpenByPanelIdAsync(panelId, userData, cancellationToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_DestroyCts.Token);
            return OpenByPanelIdLinked(panelId, userData, linked);
        }

        async UniTask<IUIForm> OpenAsyncLinked(string location, string groupName, bool pauseCoveredUIForm, object userData, CancellationTokenSource linked)
        {
            try
            {
                return await m_UIManager.OpenAsync(location, groupName, pauseCoveredUIForm, userData, linked.Token);
            }
            finally
            {
                linked.Dispose();
            }
        }

        async UniTask<IUIForm> OpenByPanelIdLinked(int panelId, object userData, CancellationTokenSource linked)
        {
            try
            {
                return await OpenByPanelIdAsync(panelId, userData, linked.Token);
            }
            finally
            {
                linked.Dispose();
            }
        }

        async UniTask<IUIForm> OpenByPanelIdAsync(int panelId, object userData, CancellationToken cancellationToken)
        {
            if (!m_UIManager.TryGetPanelConfig(panelId, out UIFormPanelConfig config))
                throw new GameFrameworkException(Utility.Text.Format("UI panel config id '{0}' is not found.", panelId.ToString()));

            if (!m_UIManager.HasUIGroup(config.GroupName))
                AddGroup(config.GroupName, 0);

            return await m_UIManager.OpenAsync(panelId, userData, cancellationToken);
        }

        public UniTask PreloadAsync(int panelId, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default)
        {
            if (m_DestroyCts == null)
                return PreloadByPanelIdAsync(panelId, keepAliveSeconds, cancellationToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_DestroyCts.Token);
            return PreloadByPanelIdLinked(panelId, keepAliveSeconds, linked);
        }

        public UniTask PreloadAsync(IEnumerable<int> panelIds, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default)
        {
            EnsurePanelGroups(panelIds);

            if (m_DestroyCts == null)
                return m_UIManager.PreloadAsync(panelIds, keepAliveSeconds, cancellationToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_DestroyCts.Token);
            return PreloadBatchLinked(panelIds, keepAliveSeconds, linked);
        }

        /// <summary>
        /// 预载前置：确保所有面板所属 UI 组已注册（与 <see cref="PreloadByPanelIdAsync"/> 一致，
        /// 否则 UIManager 层预载会因组不存在而失败）。
        /// </summary>
        void EnsurePanelGroups(IEnumerable<int> panelIds)
        {
            if (panelIds == null)
                return;

            foreach (int panelId in panelIds)
            {
                if (!m_UIManager.TryGetPanelConfig(panelId, out UIFormPanelConfig config))
                    continue;

                if (!m_UIManager.HasUIGroup(config.GroupName))
                    AddGroup(config.GroupName, 0);
            }
        }

        public UniTask PreloadAsync(string location, string uiGroupName = "Default", float keepAliveSeconds = 0f, CancellationToken cancellationToken = default)
        {
            if (m_DestroyCts == null)
                return m_UIManager.PreloadAsync(location, uiGroupName, keepAliveSeconds, cancellationToken);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_DestroyCts.Token);
            return PreloadLocationLinked(location, uiGroupName, keepAliveSeconds, linked);
        }

        async UniTask PreloadByPanelIdLinked(int panelId, float keepAliveSeconds, CancellationTokenSource linked)
        {
            try
            {
                await PreloadByPanelIdAsync(panelId, keepAliveSeconds, linked.Token);
            }
            finally
            {
                linked.Dispose();
            }
        }

        async UniTask PreloadBatchLinked(IEnumerable<int> panelIds, float keepAliveSeconds, CancellationTokenSource linked)
        {
            try
            {
                await m_UIManager.PreloadAsync(panelIds, keepAliveSeconds, linked.Token);
            }
            finally
            {
                linked.Dispose();
            }
        }

        async UniTask PreloadLocationLinked(string location, string uiGroupName, float keepAliveSeconds, CancellationTokenSource linked)
        {
            try
            {
                await m_UIManager.PreloadAsync(location, uiGroupName, keepAliveSeconds, linked.Token);
            }
            finally
            {
                linked.Dispose();
            }
        }

        async UniTask PreloadByPanelIdAsync(int panelId, float keepAliveSeconds, CancellationToken cancellationToken)
        {
            if (!m_UIManager.TryGetPanelConfig(panelId, out UIFormPanelConfig config))
                throw new GameFrameworkException(Utility.Text.Format("UI panel config id '{0}' is not found.", panelId.ToString()));

            if (!m_UIManager.HasUIGroup(config.GroupName))
                AddGroup(config.GroupName, 0);

            await m_UIManager.PreloadAsync(panelId, keepAliveSeconds, cancellationToken);
        }

        public bool UnloadPreload(int panelId)
        {
            if (!m_UIManager.TryGetPanelConfig(panelId, out UIFormPanelConfig config))
                return false;

            return m_UIManager.UnloadPreload(config.Location);
        }

        public bool UnloadPreload(string location) => m_UIManager.UnloadPreload(location);

        public void UnloadAllPreloads() => m_UIManager.UnloadAllPreloads();

        public bool IsPreloaded(int panelId)
        {
            if (!m_UIManager.TryGetPanelConfig(panelId, out UIFormPanelConfig config))
                return false;

            return m_UIManager.IsPreloaded(config.Location);
        }

        public bool IsPreloaded(string location) => m_UIManager.IsPreloaded(location);

        public void Close(IUIForm form) => m_UIManager.CloseUIForm(form);

        public void Close(int serialId) => m_UIManager.CloseUIForm(serialId);

        static Transform CreateGroupRoot(Transform canvasTransform, string groupName)
        {
            Transform existing = canvasTransform.Find(groupName);
            if (existing != null)
                return existing;

            var go = new GameObject(groupName, typeof(RectTransform));
            go.transform.SetParent(canvasTransform, false);
            StretchFull(go.GetComponent<RectTransform>());
            return go.transform;
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

        static void ApplyOverlay(Canvas canvas, UIConfig.OverlayCanvasConfig cfg)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = cfg.SortingOrder;

            if (cfg.ApplyScaler)
                ApplyScaler(canvas.gameObject, cfg.ScaleMode, cfg.ReferenceResolution, cfg.MatchWidthOrHeight);

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null && cfg.RaycasterEnabled)
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = cfg.RaycasterEnabled;
        }

        static void ApplyCamera(Canvas canvas, UIConfig.CameraCanvasConfig cfg)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = cfg.PlaneDistance;
            canvas.sortingOrder = cfg.SortingOrder;

            if (cfg.ApplyScaler)
                ApplyScaler(canvas.gameObject, cfg.ScaleMode, cfg.ReferenceResolution, cfg.MatchWidthOrHeight);

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null && cfg.RaycasterEnabled)
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = cfg.RaycasterEnabled;
        }

        static void ApplyScaler(GameObject canvasGo, CanvasScaler.ScaleMode mode, Vector2 referenceResolution, float match)
        {
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvasGo.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = mode;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = match;
        }

        public bool IsValidUIForm(IUIForm uiForm)
        {
            return m_UIManager.IsValidUIForm(uiForm);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
