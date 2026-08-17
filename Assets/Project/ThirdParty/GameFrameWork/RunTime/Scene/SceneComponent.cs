using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// 场景管理组件：YooAsset 驱动的场景加载/卸载/显式激活。
    /// 全内聚实现，不拆逻辑层。location 为 YooAsset 资源名（无扩展名、无 Assets/ 前缀）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneComponent : GameFrameworkComponent
    {
        private enum SceneState
        {
            Loading,
            Loaded,
            Unloading,
        }

        private sealed class SceneEntry
        {
            public SceneHandle Handle;
            public SceneState State;
            public int Order;
        }

        [SerializeField]
        [Tooltip("框架备用主相机：无已加载业务场景时开启；有业务场景组（Loaded）时关闭，避免与场景自带 MainCamera 冲突。")]
        private Camera _fallbackMainCamera;

        private readonly Dictionary<string, SceneEntry> _scenes = new Dictionary<string, SceneEntry>(StringComparer.Ordinal);
        private ResourcePackage _package;
        private Scene _homeScene;
        private CancellationTokenSource _destroyCts;
        private int _nextOrder;

        /// <summary>激活场景变化事件（参数：旧激活场景、新激活场景）。仅显式激活成功后触发。</summary>
        public event Action<Scene, Scene> ActiveSceneChanged;

        /// <summary>常驻主场景（框架入口场景）。</summary>
        public Scene HomeScene
        {
            get { return _homeScene; }
        }

        /// <summary>框架备用主相机（Inspector 挂载）。</summary>
        public Camera FallbackMainCamera
        {
            get { return _fallbackMainCamera; }
        }

        protected override void Awake()
        {
            base.Awake();

            _destroyCts = new CancellationTokenSource();
            _homeScene = SceneManager.GetActiveScene();
            if (!_homeScene.IsValid())
            {
                Debug.LogError("[SceneComponent] Home scene is invalid.");
                enabled = false;
            }

            RefreshFallbackMainCamera();
        }

        private void OnDestroy()
        {
            ActiveSceneChanged = null;

            _destroyCts?.Cancel();
            _destroyCts?.Dispose();
            _destroyCts = null;

            // 退出播放/关闭应用时 YooAsset 可能已 Shutdown（AsyncOperationSystem 不可用），
            // 此时不再主动卸载场景，交给 YooAsset/Unity 自行清理。
            if (!YooAssets.IsInitialized)
            {
                _scenes.Clear();
                return;
            }

            foreach (SceneEntry entry in _scenes.Values)
            {
                if (entry.Handle == null || !entry.Handle.IsValid)
                    continue;

                if (entry.State == SceneState.Loaded)
                    entry.Handle.UnloadSceneAsync();
                else if (entry.State == SceneState.Loading)
                    entry.Handle.Release();
            }

            _scenes.Clear();
            RefreshFallbackMainCamera();
        }

        /// <summary>
        /// 注入 YooAsset 资源包。
        /// </summary>
        public void SetYooAssetPackage(ResourcePackage package)
        {
            _package = package ?? throw new GameFrameworkException("Resource package is invalid.");
        }

        /// <summary>
        /// 按当前已加载业务场景刷新备用主相机：有 Loaded 业务场景则关闭，仅剩框架场景则开启。
        /// </summary>
        public void RefreshFallbackMainCamera()
        {
            if (_fallbackMainCamera == null)
                return;

            bool enableFallback = !HasLoadedGameplayScenes();
            GameObject go = _fallbackMainCamera.gameObject;
            if (go.activeSelf != enableFallback)
                go.SetActive(enableFallback);

            if (enableFallback && !_fallbackMainCamera.enabled)
                _fallbackMainCamera.enabled = true;
        }

        /// <summary>
        /// 加载场景（完整参数）。suspendLoad 为 true 时加载完成但不激活，等待调用 <see cref="AllowSceneActivation"/>。
        /// </summary>
        public UniTask<SceneHandle> LoadSceneAsync(
            string location,
            LoadSceneMode sceneMode,
            bool suspendLoad,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return LoadSceneAsyncCore(location, sceneMode, suspendLoad, progress, cancellationToken);
        }

        /// <summary>
        /// 加载场景（Additive 便捷重载）。
        /// </summary>
        public UniTask<SceneHandle> LoadSceneAsync(
            string location,
            bool suspendLoad,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return LoadSceneAsyncCore(location, LoadSceneMode.Additive, suspendLoad, progress, cancellationToken);
        }

        /// <summary>
        /// 加载场景并激活（suspendLoad = false 便捷入口）。
        /// </summary>
        public async UniTask<SceneHandle> LoadSceneAndActivateAsync(
            string location,
            CancellationToken cancellationToken = default)
        {
            SceneHandle handle = await LoadSceneAsyncCore(location, LoadSceneMode.Additive, false, null, cancellationToken);

            if (!handle.ActivateScene())
            {
                Debug.LogWarning(Utility.Text.Format("Activate scene '{0}' failure.", location));
                return handle;
            }

            NotifyActiveSceneChanged(handle.SceneObject);
            return handle;
        }

        /// <summary>
        /// 允许场景激活（YooAsset 3.x：对应 SceneHandle.AllowSceneActivation；配合 Loading 黑屏过渡）。
        /// 失败返回 false 并记录警告，不抛异常。
        /// </summary>
        public bool AllowSceneActivation(string location)
        {
            if (string.IsNullOrEmpty(location))
                return false;

            if (!_scenes.TryGetValue(location, out SceneEntry entry) || entry.Handle == null || !entry.Handle.IsValid)
            {
                Debug.LogWarning(Utility.Text.Format("Scene '{0}' is not loading or loaded.", location));
                return false;
            }

            if (!entry.Handle.AllowSceneActivation())
            {
                Debug.LogWarning(Utility.Text.Format("AllowSceneActivation scene '{0}' failure.", location));
                return false;
            }

            // AllowSceneActivation 触发 allowSceneActivation=true，场景在下一帧才真正激活，
            // 因此事件在激活完成后（轮询到目标场景成为激活场景）再触发。
            Scene previousActive = SceneManager.GetActiveScene();
            NotifyActiveSceneChangedOnActivated(previousActive, entry.Handle.SceneObject).Forget();
            return true;
        }

        private async UniTaskVoid NotifyActiveSceneChangedOnActivated(Scene previousActive, Scene target)
        {
            for (int i = 0; i < 60; i++)
            {
                await UniTask.Yield();

                if (!target.IsValid() || !target.isLoaded)
                    return;

                if (SceneManager.GetActiveScene() == target)
                {
                    ActiveSceneChanged?.Invoke(previousActive, target);
                    return;
                }
            }
        }

        /// <summary>
        /// 显式激活场景。失败返回 false 并记录警告，不抛异常。
        /// </summary>
        public bool ActivateScene(string location)
        {
            if (!TryGetLoadedEntry(location, out SceneEntry entry))
            {
                Debug.LogWarning(Utility.Text.Format("Scene '{0}' is not loaded or invalid.", location));
                return false;
            }

            if (!entry.Handle.ActivateScene())
            {
                Debug.LogWarning(Utility.Text.Format("Activate scene '{0}' failure.", location));
                return false;
            }

            NotifyActiveSceneChanged(entry.Handle.SceneObject);
            return true;
        }

        /// <summary>
        /// 显式激活常驻主场景。
        /// </summary>
        public bool ActivateHomeScene()
        {
            if (!_homeScene.IsValid() || !_homeScene.isLoaded)
                return false;

            return Activate(_homeScene);
        }

        /// <summary>
        /// 异步卸载场景。常驻主场景拒绝卸载。卸载成功自动释放句柄引用。
        /// </summary>
        public async UniTask UnloadSceneAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Scene location is invalid.");

            if (!_scenes.TryGetValue(location, out SceneEntry entry))
                throw new GameFrameworkException(Utility.Text.Format("Scene '{0}' is not loaded.", location));

            if (entry.State != SceneState.Loaded)
                throw new GameFrameworkException(Utility.Text.Format("Scene '{0}' is not loaded.", location));

            if (IsHomeScene(entry.Handle.SceneObject))
                throw new GameFrameworkException(Utility.Text.Format("Scene '{0}' is home scene, can not unload.", location));

            entry.State = SceneState.Unloading;
            try
            {
                UnloadSceneOperation operation = entry.Handle.UnloadSceneAsync();
                await operation.ToUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                entry.State = SceneState.Loaded;
                throw;
            }
            catch (Exception ex)
            {
                entry.State = SceneState.Loaded;
                throw new GameFrameworkException(Utility.Text.Format("Unload scene '{0}' failure: {1}", location, ex.Message), ex);
            }

            _scenes.Remove(location);
            RefreshFallbackMainCamera();
        }

        /// <summary>
        /// 场景是否已加载完成。
        /// </summary>
        public bool SceneIsLoaded(string location)
        {
            return _scenes.TryGetValue(location, out SceneEntry entry) && entry.State == SceneState.Loaded;
        }

        /// <summary>
        /// 场景是否正在加载。
        /// </summary>
        public bool SceneIsLoading(string location)
        {
            return _scenes.TryGetValue(location, out SceneEntry entry) && entry.State == SceneState.Loading;
        }

        /// <summary>
        /// 获取已加载场景的 location 列表（按加载顺序）。
        /// </summary>
        public string[] GetLoadedSceneLocations()
        {
            List<string> locations = new List<string>(_scenes.Count);
            foreach (KeyValuePair<string, SceneEntry> pair in _scenes)
            {
                if (pair.Value.State == SceneState.Loaded)
                    locations.Add(pair.Key);
            }

            locations.Sort((a, b) => _scenes[a].Order.CompareTo(_scenes[b].Order));
            return locations.ToArray();
        }

        private async UniTask<SceneHandle> LoadSceneAsyncCore(
            string location,
            LoadSceneMode sceneMode,
            bool suspendLoad,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Scene location is invalid.");

            if (_package == null)
                throw new GameFrameworkException("You must call SetYooAssetPackage first.");

            if (_scenes.ContainsKey(location))
                throw new GameFrameworkException(Utility.Text.Format("Scene '{0}' is loading or loaded.", location));

            SceneEntry entry = new SceneEntry
            {
                State = SceneState.Loading,
                Order = _nextOrder++,
            };
            _scenes.Add(location, entry);

            SceneHandle handle = _package.LoadSceneAsync(location, sceneMode, LocalPhysicsMode.None, suspendLoad, 0);

            // 必须在 await 前登记句柄：suspendLoad = true 时 await 会挂起直到 AllowSceneActivation 被调用，
            // 而 AllowSceneActivation 需要在此处获取句柄。
            entry.Handle = handle;
            try
            {
                await handle.ToUniTask(progress).AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await CancelLoadAsync(handle, location);
                throw;
            }
            catch (Exception ex)
            {
                handle.Release();
                _scenes.Remove(location);
                RefreshFallbackMainCamera();
                throw new GameFrameworkException(Utility.Text.Format("Load scene '{0}' failure: {1}", location, ex.Message), ex);
            }

            entry.State = SceneState.Loaded;
            RefreshFallbackMainCamera();
            return handle;
        }

        // 方案B：取消时不立即 Release（YooAsset 无法中止进行中的底层加载，Release 会留下孤儿场景）。
        // 交给 UnloadSceneAsync：其内部会自动解除挂起并等待加载完成后卸载场景，实现彻底清理。
        private async UniTask CancelLoadAsync(SceneHandle handle, string location)
        {
            try
            {
                await handle.UnloadSceneAsync().ToUniTask();
            }
            catch
            {
                // 清理失败不阻断取消语义，仅保证占位被移除
            }

            _scenes.Remove(location);
            RefreshFallbackMainCamera();
        }

        private bool HasLoadedGameplayScenes()
        {
            foreach (KeyValuePair<string, SceneEntry> pair in _scenes)
            {
                if (pair.Value.State == SceneState.Loaded)
                    return true;
            }

            return false;
        }

        private bool TryGetLoadedEntry(string location, out SceneEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(location))
                return false;

            if (!_scenes.TryGetValue(location, out entry))
                return false;

            return entry.State == SceneState.Loaded && entry.Handle != null && entry.Handle.IsValid;
        }

        private bool IsHomeScene(Scene scene)
        {
            return scene.IsValid() && _homeScene.IsValid() && scene.path == _homeScene.path;
        }

        private bool Activate(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            Scene lastActiveScene = SceneManager.GetActiveScene();
            if (lastActiveScene == scene)
                return true;

            if (!SceneManager.SetActiveScene(scene))
                return false;

            ActiveSceneChanged?.Invoke(lastActiveScene, scene);
            return true;
        }

        private void NotifyActiveSceneChanged(Scene newActiveScene)
        {
            if (!newActiveScene.IsValid() || !newActiveScene.isLoaded)
                return;

            Scene lastActiveScene = SceneManager.GetActiveScene();
            if (lastActiveScene != newActiveScene)
                ActiveSceneChanged?.Invoke(lastActiveScene, newActiveScene);
        }
    }
}
