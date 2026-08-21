using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamePlay
{
    /// <summary>
    /// 场景组中间加载态（非 Procedure）：
    /// suspend 加载新场景 → 回 Home → AllowSceneActivation → 卸旧场景 → SetActiveScene。
    /// 不可先卸旧场景：Unity 场景管线串行，Unload 会等 suspend 中的 Load 完成，而 Load 等 AllowSceneActivation，形成死锁（90%）。
    /// </summary>
    /// 
    public static class ProcedureSceneSwitch
    {
        public const string LoadingUiLocation = "LoadingPanel";

        private const string DefaultUiGroup = "Default";
        private const float SuspendReadyProgress = 0.89f;
        private const float MinLoadingDisplaySeconds = 1.5f;

        public static event Action SwitchBegin;
        public static event Action<float> SwitchProgress;
        public static event Action ScenesReady;
        public static event Action SwitchEnd;
        public static event Action<Exception> SwitchFailed;

        private static bool _busy;

        public static bool IsSwitching => _busy;

        public static async UniTask SwitchAsync(
            IReadOnlyList<string> locations,
            string activeLocation,
            CancellationToken cancellationToken = default)
        {
            ValidateArgs(locations, activeLocation);

            if (_busy)
                throw new GameFrameworkException("Scene switch is already in progress.");

            if (GameFrameWork.Scene == null)
                throw new GameFrameworkException("Scene component is missing.");

            if (IsSameGroup(locations))
            {
                GameFrameWork.Scene.ActivateScene(activeLocation);
                return;
            }

            _busy = true;
            IUIForm loadingForm = null;
            LoadingUIFormLogic loadingLogic = null;

            try
            {
                SwitchBegin?.Invoke();

                if (GameFrameWork.UI == null)
                    throw new GameFrameworkException("UI component is missing.");

                loadingForm = await GameFrameWork.UI.OpenAsync(
                    LoadingUiLocation, DefaultUiGroup, false, null, cancellationToken);
                float loadingOpenedAt = Time.realtimeSinceStartup;
                loadingLogic = GetLoadingLogic(loadingForm);
                loadingLogic?.SetLoadingTips("加载场景中…");
                loadingLogic?.SetProgressTarget(0f);
                ReportProgress(0f, loadingLogic);

                List<string> toLoad = CollectMissingLocations(locations);
                SuspendLoadBatch batch = null;

                if (toLoad.Count > 0)
                {
                    batch = BeginSuspendLoads(toLoad, loadingLogic, cancellationToken);
                    await WaitSuspendReadyAsync(batch.Progresses, cancellationToken);
                }
                else
                {
                    ReportProgress(1f, loadingLogic);
                }

                // ① 回 Home  ② AllowSceneActivation（YooAsset）  ③ Unload 旧场景（YooAsset Handle.UnloadSceneAsync）
                // 禁止在 suspend Load 未完成时 Unload，否则 Unity 管线死锁；Unload 必须走 YooAsset，不可 SceneManager 直卸。
                GameFrameWork.Scene.ActivateHomeScene();

                if (batch != null)
                {
                    await AllowActivateAndFinishAsync(
                        toLoad, batch, activeLocation, loadingLogic, cancellationToken);
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                }

                await UnloadOutsideGroupAsync(locations, cancellationToken);

                if (!GameFrameWork.Scene.ActivateScene(activeLocation))
                    Debug.LogWarning(Utility.Text.Format(
                        "[ProcedureSceneSwitch] Activate scene '{0}' failure.", activeLocation));

                ScenesReady?.Invoke();

                ReportProgress(1f, loadingLogic);
                await WaitLoadingPresentationAsync(loadingOpenedAt, loadingLogic, cancellationToken);
                SwitchEnd?.Invoke();
            }
            catch (OperationCanceledException)
            {
                CloseLoading(loadingForm);
                _busy = false;
                throw;
            }
            catch (Exception ex)
            {
                SwitchFailed?.Invoke(ex);
                CloseLoading(loadingForm);
                _busy = false;
                if (ex is GameFrameworkException)
                    throw;
                throw new GameFrameworkException(
                    Utility.Text.Format("Switch scene group failure: {0}", ex.Message), ex);
            }

            CloseLoading(loadingForm);
            _busy = false;
        }

        private sealed class SuspendLoadBatch
        {
            public List<string> Locations;
            public UniTask[] Tasks;
            public float[] Progresses;
        }

        private static void ValidateArgs(IReadOnlyList<string> locations, string activeLocation)
        {
            if (locations == null || locations.Count == 0)
                throw new GameFrameworkException("Scene locations is invalid.");

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < locations.Count; i++)
            {
                string location = locations[i];
                if (string.IsNullOrEmpty(location))
                    throw new GameFrameworkException("Scene location is invalid.");
                if (!set.Add(location))
                    throw new GameFrameworkException(
                        Utility.Text.Format("Duplicate scene location '{0}'.", location));
            }

            if (string.IsNullOrEmpty(activeLocation) || !set.Contains(activeLocation))
                throw new GameFrameworkException("Active scene location must be in the group.");
        }

        private static bool IsSameGroup(IReadOnlyList<string> locations)
        {
            string[] loaded = GameFrameWork.Scene.GetLoadedSceneLocations();
            if (loaded.Length != locations.Count)
                return false;

            var set = new HashSet<string>(locations, StringComparer.Ordinal);
            for (int i = 0; i < loaded.Length; i++)
            {
                if (!set.Contains(loaded[i]))
                    return false;
            }

            return true;
        }

        private static List<string> CollectMissingLocations(IReadOnlyList<string> locations)
        {
            var missing = new List<string>(locations.Count);
            for (int i = 0; i < locations.Count; i++)
            {
                if (!GameFrameWork.Scene.SceneIsLoaded(locations[i]))
                    missing.Add(locations[i]);
            }

            return missing;
        }

        private static SuspendLoadBatch BeginSuspendLoads(
            List<string> toLoad,
            LoadingUIFormLogic loadingLogic,
            CancellationToken cancellationToken)
        {
            int count = toLoad.Count;
            var batch = new SuspendLoadBatch
            {
                Locations = toLoad,
                Progresses = new float[count],
                Tasks = new UniTask[count],
            };

            for (int i = 0; i < count; i++)
            {
                int index = i;
                string location = toLoad[i];
                float[] progresses = batch.Progresses;
                IProgress<float> progress = Progress.Create<float>(value =>
                {
                    progresses[index] = Mathf.Clamp01(value);
                    ReportProgress(Average(progresses), loadingLogic);
                });

                batch.Tasks[i] = AwaitLoadAsync(location, progress, cancellationToken);
            }

            return batch;
        }

        private static async UniTask WaitSuspendReadyAsync(
            float[] progresses,
            CancellationToken cancellationToken)
        {
            await UniTask.WhenAny(
                UniTask.WaitUntil(
                    () => AllReadyForActivation(progresses),
                    cancellationToken: cancellationToken),
                UniTask.Delay(TimeSpan.FromSeconds(120), cancellationToken: cancellationToken));
        }

        private static async UniTask AllowActivateAndFinishAsync(
            List<string> toLoad,
            SuspendLoadBatch batch,
            string activeLocation,
            LoadingUIFormLogic loadingLogic,
            CancellationToken cancellationToken)
        {
            // sceneLoaded：Awake 之后、Start 之前。
            // 激活归属必须确定性收敛，不能按完成顺序逐个 SetActiveScene：
            // 多场景组下先完成场景的 Start 会跑在“后完成场景为激活场景”的窗口里，
            // 归属随完成顺序漂移。策略改为只提升一次：
            // ① 仅当目标场景（activeLocation）自身完成 Awake 后，才将激活场景置为它；
            // ② 目标场景就绪前，激活场景保持 Home（切换安全区），任何场景的 Start
            //    只会落在 Home 或目标场景，绝不会落在另一个玩法场景；
            // ③ 目标场景先于组内其他场景完成时，其 Start 也能确定性地落在自身。
            // 若目标场景此前已加载（不在 toLoad 中），则此处不提升，由 SwitchAsync
            // 末尾的 ActivateScene(activeLocation) 统一收口。
            var pendingLocations = new HashSet<string>(toLoad, StringComparer.Ordinal);
            bool targetActivated = false;
            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (targetActivated)
                    return;
                if (!scene.IsValid() || !scene.isLoaded)
                    return;
                if (!pendingLocations.Contains(scene.name))
                    return;
                if (scene.name != activeLocation)
                    return;

                SceneManager.SetActiveScene(scene);
                targetActivated = true;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                for (int i = 0; i < toLoad.Count; i++)
                {
                    if (!GameFrameWork.Scene.AllowSceneActivation(toLoad[i]))
                    {
                        throw new GameFrameworkException(
                            Utility.Text.Format(
                                "AllowSceneActivation scene '{0}' failure.", toLoad[i]));
                    }
                }

                await UniTask.WhenAll(batch.Tasks);
                ReportProgress(1f, loadingLogic);
            }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async UniTask AwaitLoadAsync(
            string location,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            await GameFrameWork.Scene.LoadSceneAsync(location, true, progress, cancellationToken);
        }

        private static bool AllReadyForActivation(float[] progresses)
        {
            if (progresses == null || progresses.Length == 0)
                return false;

            for (int i = 0; i < progresses.Length; i++)
            {
                if (progresses[i] < SuspendReadyProgress)
                    return false;
            }

            return true;
        }

        private static async UniTask UnloadOutsideGroupAsync(
            IReadOnlyList<string> locations,
            CancellationToken cancellationToken)
        {
            var keep = new HashSet<string>(locations, StringComparer.Ordinal);
            string[] loaded = GameFrameWork.Scene.GetLoadedSceneLocations();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (keep.Contains(loaded[i]))
                    continue;

                await GameFrameWork.Scene.UnloadSceneAsync(loaded[i], cancellationToken);
            }
        }

        private static void ReportProgress(float normalized, LoadingUIFormLogic loadingLogic)
        {
            float clamped = Mathf.Clamp01(normalized);
            SwitchProgress?.Invoke(clamped);
            loadingLogic?.SetProgressTarget(clamped * 100f);
        }

        private static async UniTask WaitLoadingPresentationAsync(
            float loadingOpenedAt,
            LoadingUIFormLogic loadingLogic,
            CancellationToken cancellationToken)
        {
            if (loadingLogic != null)
            {
                await UniTask.WaitUntil(
                    () => loadingLogic.IsVisualProgressComplete,
                    cancellationToken: cancellationToken);
            }

            float elapsed = Time.realtimeSinceStartup - loadingOpenedAt;
            float remain = MinLoadingDisplaySeconds - elapsed;
            if (remain > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(remain), cancellationToken: cancellationToken);
        }

        private static float Average(float[] values)
        {
            if (values == null || values.Length == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return sum / values.Length;
        }

        private static LoadingUIFormLogic GetLoadingLogic(IUIForm form)
        {
            if (form?.Handle is GameObject go)
                return go.GetComponent<LoadingUIFormLogic>();
            return null;
        }

        private static void CloseLoading(IUIForm form)
        {
            if (form == null)
                return;

            if (GameFrameWork.UI != null)
                GameFrameWork.UI.Close(form);
            else
                GameFrameworkEntry.GetModule<IUIManager>()?.CloseUIForm(form);
        }
    }
}
