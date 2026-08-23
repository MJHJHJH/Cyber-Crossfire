//------------------------------------------------------------
// Adapted from Game Framework ? UIManager with OpenAsync + IUIFormLoader
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using static GameFramework.UIConfig;

namespace GameFramework
{
    internal sealed partial class UIManager : GameFrameworkModule, IUIManager
    {
        private readonly Dictionary<string, UIGroup> m_UIGroups;
        private readonly Dictionary<int, string> m_UIFormsBeingLoaded;
        private readonly HashSet<int> m_UIFormsToReleaseOnLoad;
        private readonly Queue<IUIForm> m_RecycleQueue;
        private readonly HashSet<string> m_UIFormsBeingPreloaded;
        private readonly Dictionary<string, PreloadEntry> m_PreloadedInstances;
        private IObjectPoolManager m_ObjectPoolManager;
        private readonly ResourcesUIFormLoader m_ResourcesLoader;
        private YooAssetUIFormLoader m_YooAssetLoader;
        private IUIFormPanelConfigProvider m_PanelConfigProvider;
        private IObjectPool<UIFormInstanceObject> m_InstancePool;
        private IUIFormHelper m_UIFormHelper;
        private int m_Serial;
        private bool m_IsShutdown;
        private EventHandler<CloseUIFormCompleteEventArgs> m_CloseUIFormCompleteEventHandler;

        public UIManager()
        {
            m_UIGroups = new Dictionary<string, UIGroup>(StringComparer.Ordinal);
            m_UIFormsBeingLoaded = new Dictionary<int, string>();
            m_UIFormsToReleaseOnLoad = new HashSet<int>();
            m_RecycleQueue = new Queue<IUIForm>();
            m_UIFormsBeingPreloaded = new HashSet<string>(StringComparer.Ordinal);
            m_PreloadedInstances = new Dictionary<string, PreloadEntry>(StringComparer.Ordinal);
            m_ObjectPoolManager = null;
            m_ResourcesLoader = new ResourcesUIFormLoader();
            m_YooAssetLoader = null;
            m_PanelConfigProvider = null;
            m_InstancePool = null;
            m_UIFormHelper = null;
            m_Serial = 0;
            m_IsShutdown = false;
            m_CloseUIFormCompleteEventHandler = null;
        }

        public int UIGroupCount => m_UIGroups.Count;

        public event EventHandler<CloseUIFormCompleteEventArgs> CloseUIFormComplete
        {
            add => m_CloseUIFormCompleteEventHandler += value;
            remove => m_CloseUIFormCompleteEventHandler -= value;
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            ExpirePreloads(realElapseSeconds);

            while (m_RecycleQueue.Count > 0)
            {
                IUIForm uiForm = m_RecycleQueue.Dequeue();
                uiForm.OnRecycle();
                m_InstancePool.Unspawn(uiForm.Handle);
            }

            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                uiGroup.Value.Update(elapseSeconds, realElapseSeconds);
            }
        }

        internal override void Shutdown()
        {
            m_IsShutdown = true;
            CloseAllLoadedUIForms();
            m_UIGroups.Clear();
            m_UIFormsBeingLoaded.Clear();
            m_UIFormsToReleaseOnLoad.Clear();
            m_RecycleQueue.Clear();
            m_UIFormsBeingPreloaded.Clear();
            m_PreloadedInstances.Clear();
        }

        public void SetObjectPoolManager(IObjectPoolManager objectPoolManager, UIObjectPoolConfig uIObjectPoolConfig)
        {
            if (objectPoolManager == null)
                throw new GameFrameworkException("Object pool manager is invalid.");

            m_ObjectPoolManager = objectPoolManager;
            m_InstancePool = m_ObjectPoolManager.CreateSingleSpawnObjectPool<UIFormInstanceObject>("UI Instance Pool");
            m_InstancePool.AutoReleaseInterval = uIObjectPoolConfig.InstanceAutoReleaseInterval;
            m_InstancePool.Capacity = uIObjectPoolConfig.InstanceCapacity;
            m_InstancePool.ExpireTime = uIObjectPoolConfig.InstanceExpireTime;
            m_InstancePool.Priority = uIObjectPoolConfig.InstancePriority;
        }

        public void SetYooAssetPackage(ResourcePackage package)
        {
            if (package == null)
                throw new GameFrameworkException("Resource package is invalid.");
            m_YooAssetLoader = new YooAssetUIFormLoader(package);
        }

        public void SetUIFormPanelConfigProvider(IUIFormPanelConfigProvider provider)
        {
            m_PanelConfigProvider = provider ?? throw new GameFrameworkException("UI form panel config provider is invalid.");
        }

        public void SetUIFormHelper(IUIFormHelper uiFormHelper)
        {
            m_UIFormHelper = uiFormHelper ?? throw new GameFrameworkException("UI form helper is invalid.");
        }

        public bool TryGetPanelConfig(int panelId, out UIFormPanelConfig config)
        {
            if (m_PanelConfigProvider == null)
            {
                config = default;
                return false;
            }

            return m_PanelConfigProvider.TryGetById(panelId, out config);
        }

        public bool HasUIGroup(string uiGroupName)
        {
            if (string.IsNullOrEmpty(uiGroupName))
                throw new GameFrameworkException("UI group name is invalid.");
            return m_UIGroups.ContainsKey(uiGroupName);
        }

        public IUIGroup GetUIGroup(string uiGroupName)
        {
            if (string.IsNullOrEmpty(uiGroupName))
                throw new GameFrameworkException("UI group name is invalid.");

            return m_UIGroups.TryGetValue(uiGroupName, out UIGroup group) ? group : null;
        }

        public IUIGroup[] GetAllUIGroups()
        {
            int index = 0;
            IUIGroup[] results = new IUIGroup[m_UIGroups.Count];
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
                results[index++] = uiGroup.Value;
            return results;
        }

        public void GetAllUIGroups(List<IUIGroup> results)
        {
            if (results == null)
                throw new GameFrameworkException("Results is invalid.");

            results.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
                results.Add(uiGroup.Value);
        }

        public bool AddUIGroup(string uiGroupName, IUIGroupHelper uiGroupHelper)
        {
            return AddUIGroup(uiGroupName, 0, uiGroupHelper, UIGroupOpenMode.ClearToTop);
        }

        public bool AddUIGroup(string uiGroupName, int depth, IUIGroupHelper uiGroupHelper)
        {
            return AddUIGroup(uiGroupName, depth, uiGroupHelper, UIGroupOpenMode.ClearToTop);
        }

        public bool AddUIGroup(string uiGroupName, int depth, IUIGroupHelper uiGroupHelper, UIGroupOpenMode openMode)
        {
            if (string.IsNullOrEmpty(uiGroupName))
                throw new GameFrameworkException("UI group name is invalid.");
            if (uiGroupHelper == null)
                throw new GameFrameworkException("UI group helper is invalid.");
            if (HasUIGroup(uiGroupName))
                return false;

            m_UIGroups.Add(uiGroupName, new UIGroup(uiGroupName, depth, uiGroupHelper, openMode));
            return true;
        }

        public bool HasUIForm(int serialId)
        {
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                if (uiGroup.Value.HasUIForm(serialId))
                    return true;
            }
            return false;
        }

        public bool HasUIForm(string uiFormAssetName)
        {
            if (string.IsNullOrEmpty(uiFormAssetName))
                throw new GameFrameworkException("UI form asset name is invalid.");

            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                if (uiGroup.Value.HasUIForm(uiFormAssetName))
                    return true;
            }
            return false;
        }

        public IUIForm GetUIForm(int serialId)
        {
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                IUIForm uiForm = uiGroup.Value.GetUIForm(serialId);
                if (uiForm != null)
                    return uiForm;
            }
            return null;
        }

        public IUIForm GetUIForm(string uiFormAssetName)
        {
            if (string.IsNullOrEmpty(uiFormAssetName))
                throw new GameFrameworkException("UI form asset name is invalid.");

            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                IUIForm uiForm = uiGroup.Value.GetUIForm(uiFormAssetName);
                if (uiForm != null)
                    return uiForm;
            }
            return null;
        }

        public IUIForm[] GetUIForms(string uiFormAssetName)
        {
            if (string.IsNullOrEmpty(uiFormAssetName))
                throw new GameFrameworkException("UI form asset name is invalid.");

            List<IUIForm> results = new List<IUIForm>();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
                uiGroup.Value.GetUIForms(uiFormAssetName, results);
            return results.ToArray();
        }

        public void GetUIForms(string uiFormAssetName, List<IUIForm> results)
        {
            if (string.IsNullOrEmpty(uiFormAssetName))
                throw new GameFrameworkException("UI form asset name is invalid.");
            if (results == null)
                throw new GameFrameworkException("Results is invalid.");

            results.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
                uiGroup.Value.GetUIForms(uiFormAssetName, results);
        }

        public IUIForm[] GetAllLoadedUIForms()
        {
            List<IUIForm> results = new List<IUIForm>();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
                uiGroup.Value.GetAllUIForms(results);
            return results.ToArray();
        }

        public void GetAllLoadedUIForms(List<IUIForm> results)
        {
            if (results == null)
                throw new GameFrameworkException("Results is invalid.");

            results.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
                uiGroup.Value.GetAllUIForms(results);
        }

        public int[] GetAllLoadingUIFormSerialIds()
        {
            int index = 0;
            int[] results = new int[m_UIFormsBeingLoaded.Count];
            foreach (KeyValuePair<int, string> uiFormBeingLoaded in m_UIFormsBeingLoaded)
                results[index++] = uiFormBeingLoaded.Key;
            return results;
        }

        public void GetAllLoadingUIFormSerialIds(List<int> results)
        {
            if (results == null)
                throw new GameFrameworkException("Results is invalid.");

            results.Clear();
            foreach (KeyValuePair<int, string> uiFormBeingLoaded in m_UIFormsBeingLoaded)
                results.Add(uiFormBeingLoaded.Key);
        }

        public bool IsLoadingUIForm(int serialId) => m_UIFormsBeingLoaded.ContainsKey(serialId);

        public bool IsLoadingUIForm(string uiFormAssetName)
        {
            if (string.IsNullOrEmpty(uiFormAssetName))
                throw new GameFrameworkException("UI form asset name is invalid.");
            return m_UIFormsBeingLoaded.ContainsValue(uiFormAssetName);
        }

        public bool IsValidUIForm(IUIForm uiForm)
        {
            if (uiForm == null)
                return false;
            return HasUIForm(uiForm.SerialId);
        }

        public UniTask<IUIForm> OpenAsync(string location, string uiGroupName, bool pauseCoveredUIForm = false, object userData = null, CancellationToken cancellationToken = default)
        {
            return OpenAsyncWithLoader(location, uiGroupName, pauseCoveredUIForm, userData, cancellationToken, m_ResourcesLoader);
        }

        public async UniTask<IUIForm> OpenAsync(int panelId, object userData = null, CancellationToken cancellationToken = default)
        {
            if (m_PanelConfigProvider == null)
                throw new GameFrameworkException("UI form panel config provider is not set.");

            if (!m_PanelConfigProvider.TryGetById(panelId, out UIFormPanelConfig config))
                throw new GameFrameworkException(Utility.Text.Format("UI panel config id '{0}' is not found.", panelId.ToString()));

            if (string.IsNullOrEmpty(config.GroupName))
                throw new GameFrameworkException(Utility.Text.Format("UI panel '{0}' group name is invalid.", panelId.ToString()));

            if (!HasUIGroup(config.GroupName))
                throw new GameFrameworkException(Utility.Text.Format("UI group '{0}' is not exist. Add group before OpenAsync(panelId).", config.GroupName));

            IUIFormLoader loader = ResolveLoader(config.LoaderKind);
            IUIForm form = await OpenAsyncWithLoader(
                config.Location,
                config.GroupName,
                config.PauseCoveredUIForm,
                userData,
                cancellationToken,
                loader);

            ApplyPanelConfig(form, config);
            return form;
        }

        public async UniTask PreloadAsync(int panelId, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default)
        {
            if (m_PanelConfigProvider == null)
                throw new GameFrameworkException("UI form panel config provider is not set.");

            if (!m_PanelConfigProvider.TryGetById(panelId, out UIFormPanelConfig config))
                throw new GameFrameworkException(Utility.Text.Format("UI panel config id '{0}' is not found.", panelId.ToString()));

            if (string.IsNullOrEmpty(config.GroupName))
                throw new GameFrameworkException(Utility.Text.Format("UI panel '{0}' group name is invalid.", panelId.ToString()));

            if (!HasUIGroup(config.GroupName))
                throw new GameFrameworkException(Utility.Text.Format("UI group '{0}' is not exist. Add group before PreloadAsync(panelId).", config.GroupName));

            IUIFormLoader loader = ResolveLoader(config.LoaderKind);
            await PreloadAsyncCore(config.Location, config.GroupName, config.CanvasMode, keepAliveSeconds, loader, cancellationToken);
        }

        public UniTask PreloadAsync(string location, string uiGroupName = "Default", float keepAliveSeconds = 0f, CancellationToken cancellationToken = default)
        {
            return PreloadAsyncCore(location, uiGroupName, null, keepAliveSeconds, m_ResourcesLoader, cancellationToken);
        }

        public async UniTask PreloadAsync(IEnumerable<int> panelIds, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default)
        {
            if (panelIds == null)
                throw new GameFrameworkException("UI panel ids is invalid.");

            List<UniTask> tasks = new List<UniTask>();
            foreach (int panelId in panelIds)
                tasks.Add(PreloadSoftAsync(panelId, keepAliveSeconds, cancellationToken));

            await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// 预载核心：加载资源 → 实例化（Awake 提前执行）→ 挂入所属组物理根 → SetActive(false) 停放
        /// → 注册进 UI 实例池（未占用 + 锁定）→ 记入预载记录。预载期不执行任何 UIForm 生命周期。
        /// </summary>
        private async UniTask PreloadAsyncCore(
            string location,
            string uiGroupName,
            int? canvasMode,
            float keepAliveSeconds,
            IUIFormLoader loader,
            CancellationToken cancellationToken)
        {
            if (loader == null)
                throw new GameFrameworkException("UI form loader is invalid.");
            if (m_UIFormHelper == null)
                throw new GameFrameworkException("You must set UI form helper first.");
            if (m_InstancePool == null)
                throw new GameFrameworkException("You must set object pool manager first.");
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("UI form location is invalid.");
            if (string.IsNullOrEmpty(uiGroupName))
                throw new GameFrameworkException("UI group name is invalid.");

            UIGroup uiGroup = (UIGroup)GetUIGroup(uiGroupName);
            if (uiGroup == null)
                throw new GameFrameworkException(Utility.Text.Format("UI group '{0}' is not exist.", uiGroupName));

            cancellationToken.ThrowIfCancellationRequested();

            // 已打开 / 已停放：无需预载
            if (GetUIForm(location) != null || IsPreloaded(location))
                return;

            // 在途打开：等其结束再决定（打开完成 → 无需预载）
            while (IsLoadingUIForm(location))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(cancellationToken);
            }

            if (GetUIForm(location) != null || IsPreloaded(location))
                return;

            // 在途预载互斥：并发调用时等对方完成
            if (!m_UIFormsBeingPreloaded.Add(location))
            {
                while (m_UIFormsBeingPreloaded.Contains(location))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(cancellationToken);
                }

                return;
            }

            UIFormLoadResult loadResult = null;
            object uiFormInstance = null;
            bool registered = false;
            try
            {
                loadResult = await loader.LoadAsync(location, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // 加载期间被打开（互斥下的双保险）：放弃预载并释放资源
                if (GetUIForm(location) != null)
                {
                    loadResult.ReleaseAsset?.Invoke();
                    return;
                }

                uiFormInstance = m_UIFormHelper.InstantiateUIForm(loadResult.Asset);
                GameObject go = uiFormInstance as GameObject;
                if (go == null)
                    throw new GameFrameworkException(Utility.Text.Format("UI form instance '{0}' is not a GameObject.", location));

                UIForm uiForm = go.GetOrAddComponent<UIForm>();
                UIFormCanvasKind kind = canvasMode.HasValue
                    ? (canvasMode.Value == (int)UIFormCanvasKind.Camera ? UIFormCanvasKind.Camera : UIFormCanvasKind.Overlay)
                    : uiForm.CanvasKind;

                Transform parent = ResolveGroupRoot(uiGroup, kind);
                if (parent == null)
                    throw new GameFrameworkException(Utility.Text.Format("UI group '{0}' can not resolve physical root for canvas '{1}'.", uiGroupName, kind.ToString()));

                Transform t = go.transform;
                t.SetParent(parent, false);
                t.localScale = Vector3.one;
                StretchFull(t as RectTransform);

                // 停放：等待激活，打开时零根节点切换
                go.SetActive(false);

                UIFormInstanceObject instanceObject = UIFormInstanceObject.Create(location, loadResult.Asset, uiFormInstance, m_UIFormHelper, loadResult.ReleaseAsset);
                m_InstancePool.Register(instanceObject, false);
                registered = true;
                m_InstancePool.SetLocked(uiFormInstance, true);

                m_PreloadedInstances[location] = new PreloadEntry(uiFormInstance, keepAliveSeconds);
            }
            catch (Exception)
            {
                if (!registered)
                {
                    if (uiFormInstance != null)
                        m_UIFormHelper.ReleaseUIForm(loadResult?.Asset, uiFormInstance);
                    loadResult?.ReleaseAsset?.Invoke();
                }

                throw;
            }
            finally
            {
                m_UIFormsBeingPreloaded.Remove(location);
            }
        }

        private async UniTask PreloadSoftAsync(int panelId, float keepAliveSeconds, CancellationToken cancellationToken)
        {
            try
            {
                await PreloadAsync(panelId, keepAliveSeconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Utility.Text.Format("[UIManager] Preload UI panel '{0}' failed: {1}", panelId.ToString(), ex.Message));
            }
        }

        public bool UnloadPreload(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("UI form location is invalid.");

            if (!m_PreloadedInstances.TryGetValue(location, out PreloadEntry entry))
                return false;

            m_PreloadedInstances.Remove(location);
            m_InstancePool.SetLocked(entry.InstanceTarget, false);
            m_InstancePool.ReleaseObject(entry.InstanceTarget);
            return true;
        }

        public void UnloadAllPreloads()
        {
            if (m_PreloadedInstances.Count == 0)
                return;

            List<string> locations = new List<string>(m_PreloadedInstances.Keys);
            for (int i = 0; i < locations.Count; i++)
                UnloadPreload(locations[i]);
        }

        public bool IsPreloaded(string location)
        {
            return !string.IsNullOrEmpty(location) && m_PreloadedInstances.ContainsKey(location);
        }

        private void ExpirePreloads(float realElapseSeconds)
        {
            if (m_PreloadedInstances.Count == 0)
                return;

            List<string> expired = null;
            foreach (KeyValuePair<string, PreloadEntry> kv in m_PreloadedInstances)
            {
                PreloadEntry entry = kv.Value;
                if (entry.RemainingSeconds < 0f)
                    continue;

                entry.RemainingSeconds -= realElapseSeconds;
                if (entry.RemainingSeconds <= 0f)
                {
                    if (expired == null)
                        expired = new List<string>();
                    expired.Add(kv.Key);
                }
            }

            if (expired == null)
                return;

            for (int i = 0; i < expired.Count; i++)
                UnloadPreload(expired[i]);
        }

        private static Transform ResolveGroupRoot(UIGroup uiGroup, UIFormCanvasKind kind)
        {
            if (uiGroup.Helper is DualUIGroupHelper dual)
                return dual.GetParent(kind);

            if (uiGroup.Helper is MonoBehaviour mb)
                return mb.transform;

            return null;
        }

        private static void StretchFull(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private IUIFormLoader ResolveLoader(UIFormLoaderKind loaderKind)
        {
            switch (loaderKind)
            {
                case UIFormLoaderKind.Resources:
                    return m_ResourcesLoader;
                case UIFormLoaderKind.YooAsset:
                    if (m_YooAssetLoader == null)
                        throw new GameFrameworkException("YooAsset UI form loader is not ready. Call SetYooAssetPackage first.");
                    return m_YooAssetLoader;
                default:
                    throw new GameFrameworkException(Utility.Text.Format("UI form loader kind '{0}' is invalid.", loaderKind.ToString()));
            }
        }

        private static void ApplyPanelConfig(IUIForm form, UIFormPanelConfig config)
        {
            if (form is not UIForm uiForm)
                return;

            ApplySortOrder(uiForm, config.SortOrder);
            ApplyCanvasMode(uiForm, config.CanvasMode);
        }

        private static void ApplySortOrder(UIForm uiForm, int sortOrder)
        {
            Canvas canvas = uiForm.GetComponent<Canvas>();
            if (canvas == null)
                canvas = uiForm.gameObject.AddComponent<Canvas>();

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortOrder;
        }

        private static void ApplyCanvasMode(UIForm uiForm, int canvasMode)
        {
            UIFormCanvasKind kind = canvasMode == (int)UIFormCanvasKind.Camera
                ? UIFormCanvasKind.Camera
                : UIFormCanvasKind.Overlay;

            if (uiForm.CanvasKind == kind)
                return;

            uiForm.CanvasKind = kind;
            if (uiForm.UIGroup?.Helper is not DualUIGroupHelper dual)
                return;

            Transform parent = dual.GetParent(kind);
            if (parent == null || uiForm.Handle is not UnityEngine.GameObject go)
                return;

            go.transform.SetParent(parent, false);
        }

        private async UniTask<IUIForm> OpenAsyncWithLoader(
            string location,
            string uiGroupName,
            bool pauseCoveredUIForm,
            object userData,
            CancellationToken cancellationToken,
            IUIFormLoader loader)
        {
            if (loader == null)
                throw new GameFrameworkException("UI form loader is invalid.");
            if (m_UIFormHelper == null)
                throw new GameFrameworkException("You must set UI form helper first.");
            if (m_InstancePool == null)
                throw new GameFrameworkException("You must set object pool manager first.");
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("UI form location is invalid.");
            if (string.IsNullOrEmpty(uiGroupName))
                throw new GameFrameworkException("UI group name is invalid.");

            UIGroup uiGroup = (UIGroup)GetUIGroup(uiGroupName);
            if (uiGroup == null)
                throw new GameFrameworkException(Utility.Text.Format("UI group '{0}' is not exist.", uiGroupName));

            cancellationToken.ThrowIfCancellationRequested();

            IUIForm existing = TryGetSingletonUIForm(location);
            if (existing != null)
                return ApplySingletonOpenMode(existing, userData);

            // 默认单例：等同 location 在途加载/预载结束，再决定复用或继续开
            while (IsLoadingUIForm(location) || m_UIFormsBeingPreloaded.Contains(location))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(cancellationToken);
            }

            existing = TryGetSingletonUIForm(location);
            if (existing != null)
                return ApplySingletonOpenMode(existing, userData);

            int serialId = ++m_Serial;
            UIFormInstanceObject instanceObject = m_InstancePool.Spawn(location);
            if (instanceObject != null)
            {
                if (TryRejectMultiInstance(location, instanceObject.Target, out IUIForm singleton))
                {
                    m_InstancePool.Unspawn(instanceObject.Target);
                    return ApplySingletonOpenMode(singleton, userData);
                }

                // 预载停放实例首次打开：解锁（此后按普通池实例过期回收）并以新实例语义补执行 OnInit
                bool isPreloadedNewInstance = m_PreloadedInstances.Remove(location);
                if (isPreloadedNewInstance)
                    m_InstancePool.SetLocked(instanceObject.Target, false);

                return InternalOpenUIForm(serialId, location, uiGroup, instanceObject.Target, pauseCoveredUIForm, isPreloadedNewInstance, userData);
            }

            m_UIFormsBeingLoaded.Add(serialId, location);
            UIFormLoadResult loadResult = null;
            object uiFormInstance = null;
            bool ownedByPool = false;
            try
            {
                loadResult = await loader.LoadAsync(location, cancellationToken);
                //异步加载期间是否被放入销毁列表
                if (m_UIFormsToReleaseOnLoad.Contains(serialId))
                {
                    m_UIFormsToReleaseOnLoad.Remove(serialId);
                    loadResult.ReleaseAsset?.Invoke();
                    //让上层 await OpenAsync 走取消语义。
                    throw new OperationCanceledException("UI form open was canceled while loading.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                existing = TryGetSingletonUIForm(location);
                if (existing != null)
                {
                    m_UIFormsBeingLoaded.Remove(serialId);
                    loadResult.ReleaseAsset?.Invoke();
                    return ApplySingletonOpenMode(existing, userData);
                }

                uiFormInstance = m_UIFormHelper.InstantiateUIForm(loadResult.Asset);
                if (TryRejectMultiInstance(location, uiFormInstance, out IUIForm loadedSingleton))
                {
                    m_UIFormsBeingLoaded.Remove(serialId);
                    m_UIFormHelper.ReleaseUIForm(loadResult.Asset, uiFormInstance);
                    loadResult.ReleaseAsset?.Invoke();
                    return ApplySingletonOpenMode(loadedSingleton, userData);
                }

                instanceObject = UIFormInstanceObject.Create(location, loadResult.Asset, uiFormInstance, m_UIFormHelper, loadResult.ReleaseAsset);
                m_InstancePool.Register(instanceObject, true);
                ownedByPool = true;
                m_UIFormsBeingLoaded.Remove(serialId);

                return InternalOpenUIForm(serialId, location, uiGroup, uiFormInstance, pauseCoveredUIForm, true, userData);
            }
            catch (Exception)
            {
                m_UIFormsBeingLoaded.Remove(serialId);
                m_UIFormsToReleaseOnLoad.Remove(serialId);

                if (!ownedByPool)
                {
                    if (uiFormInstance != null)
                        m_UIFormHelper.ReleaseUIForm(loadResult?.Asset, uiFormInstance);
                    loadResult?.ReleaseAsset?.Invoke();
                }

                throw;
            }
        }

        /// <summary>
        /// 单例界面已存在时，按其所在组的 OpenMode 处理栈。
        /// </summary>
        private IUIForm ApplySingletonOpenMode(IUIForm existing, object userData)
        {
            UIGroup uiGroup = (UIGroup)existing.UIGroup;
            if (uiGroup == null)
                throw new GameFrameworkException("UI group is invalid.");

            if (uiGroup.OpenMode == UIGroupOpenMode.ClearToTop)
            {
                if (uiGroup.CurrentUIForm == existing)
                    return existing;

                var above = new List<IUIForm>();
                uiGroup.GetUIFormsAbove(existing, above);
                for (int i = 0; i < above.Count; i++)
                {
                    if (HasUIForm(above[i].SerialId))
                        CloseUIForm(above[i]);
                }

                existing.OnRefocus(userData);
                return existing;
            }

            RefocusUIForm(existing, userData);
            return existing;
        }

        /// <summary>
        /// 已打开且不允许多实例时返回该界面；否则 null。
        /// </summary>
        private IUIForm TryGetSingletonUIForm(string location)
        {
            IUIForm existing = GetUIForm(location);
            if (existing != null && !existing.AllowMultiInstance)
                return existing;
            return null;
        }

        /// <summary>
        /// 新实例不允许多开且已有同 location 界面时，拒绝本次打开。
        /// </summary>
        private bool TryRejectMultiInstance(string location, object uiFormInstance, out IUIForm existingSingleton)
        {
            existingSingleton = null;
            bool allowMulti = false;
            if (uiFormInstance is UnityEngine.GameObject go)
            {
                UIForm uiForm = go.GetComponent<UIForm>();
                if (uiForm != null)
                    allowMulti = uiForm.AllowMultiInstance;
            }

            if (allowMulti)
                return false;

            existingSingleton = GetUIForm(location);
            return existingSingleton != null;
        }

        public void CloseUIForm(int serialId) => CloseUIForm(serialId, null);

        public void CloseUIForm(int serialId, object userData)
        {
            if (IsLoadingUIForm(serialId))
            {
                m_UIFormsToReleaseOnLoad.Add(serialId);
                m_UIFormsBeingLoaded.Remove(serialId);
                return;
            }

            IUIForm uiForm = GetUIForm(serialId);
            if (uiForm == null)
                throw new GameFrameworkException(Utility.Text.Format("Can not find UI form '{0}'.", serialId.ToString()));

            CloseUIForm(uiForm, userData);
        }

        public void CloseUIForm(IUIForm uiForm) => CloseUIForm(uiForm, null);

        public void CloseUIForm(IUIForm uiForm, object userData)
        {
            if (uiForm == null)
                throw new GameFrameworkException("UI form is invalid.");

            UIGroup uiGroup = (UIGroup)uiForm.UIGroup;
            if (uiGroup == null)
                throw new GameFrameworkException("UI group is invalid.");

            uiGroup.RemoveUIForm(uiForm);
            uiForm.OnClose(m_IsShutdown, userData);
            uiGroup.Refresh();

            if (m_CloseUIFormCompleteEventHandler != null)
            {
                CloseUIFormCompleteEventArgs args = CloseUIFormCompleteEventArgs.Create(uiForm.SerialId, uiForm.UIFormAssetName, uiGroup, userData);
                m_CloseUIFormCompleteEventHandler(this, args);
                ReferencePool.Release(args);
            }

            m_RecycleQueue.Enqueue(uiForm);
        }

        public void CloseAllLoadedUIForms() => CloseAllLoadedUIForms(null);

        public void CloseAllLoadedUIForms(object userData)
        {
            IUIForm[] uiForms = GetAllLoadedUIForms();
            foreach (IUIForm uiForm in uiForms)
            {
                if (!HasUIForm(uiForm.SerialId))
                    continue;
                CloseUIForm(uiForm, userData);
            }
        }

        public void CloseAllLoadingUIForms()
        {
            foreach (KeyValuePair<int, string> uiFormBeingLoaded in m_UIFormsBeingLoaded)
                m_UIFormsToReleaseOnLoad.Add(uiFormBeingLoaded.Key);
            m_UIFormsBeingLoaded.Clear();
        }

        public void RefocusUIForm(IUIForm uiForm) => RefocusUIForm(uiForm, null);

        public void RefocusUIForm(IUIForm uiForm, object userData)
        {
            if (uiForm == null)
                throw new GameFrameworkException("UI form is invalid.");

            UIGroup uiGroup = (UIGroup)uiForm.UIGroup;
            if (uiGroup == null)
                throw new GameFrameworkException("UI group is invalid.");

            uiGroup.RefocusUIForm(uiForm, userData);
            uiGroup.Refresh();
            uiForm.OnRefocus(userData);
        }

        public void SetUIFormInstanceLocked(object uiFormInstance, bool locked)
        {
            if (uiFormInstance == null)
                throw new GameFrameworkException("UI form instance is invalid.");
            m_InstancePool.SetLocked(uiFormInstance, locked);
        }

        public void SetUIFormInstancePriority(object uiFormInstance, int priority)
        {
            if (uiFormInstance == null)
                throw new GameFrameworkException("UI form instance is invalid.");
            m_InstancePool.SetPriority(uiFormInstance, priority);
        }

        private IUIForm InternalOpenUIForm(int serialId, string uiFormAssetName, UIGroup uiGroup, object uiFormInstance, bool pauseCoveredUIForm, bool isNewInstance, object userData)
        {
            IUIForm uiForm = m_UIFormHelper.CreateUIForm(uiFormInstance, uiGroup, userData);
            if (uiForm == null)
                throw new GameFrameworkException("Can not create UI form in UI form helper.");

            uiForm.OnInit(serialId, uiFormAssetName, uiGroup, pauseCoveredUIForm, isNewInstance, userData);
            uiGroup.AddUIForm(uiForm);
            uiForm.OnOpen(userData);
            uiGroup.Refresh();
            return uiForm;
        }

        /// <summary>
        /// 预载停放记录：池实例 + 存活倒计时（RemainingSeconds &lt; 0 表示永久保留，仅手动卸载）。
        /// </summary>
        private sealed class PreloadEntry
        {
            public readonly object InstanceTarget;
            public float RemainingSeconds;

            public PreloadEntry(object instanceTarget, float keepAliveSeconds)
            {
                InstanceTarget = instanceTarget;
                RemainingSeconds = keepAliveSeconds <= 0f ? -1f : keepAliveSeconds;
            }
        }
    }
}
