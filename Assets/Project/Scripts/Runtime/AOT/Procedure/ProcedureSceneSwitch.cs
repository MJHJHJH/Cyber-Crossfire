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
    /// suspend 加载新场景 → 先卸旧场景 → AllowSceneActivation + SetActiveScene → 再进入新场景 Start。
    /// </summary>
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

                // 新场景仍 suspend（尚无 Start）：先回 Home，再卸旧场景（如 MainMenu）。
                GameFrameWork.Scene.ActivateHomeScene();
                await UnloadOutsideGroupAsync(locations, cancellationToken);

                if (batch != null)
                {
                    await AllowActivateAndFinishAsync(
                        toLoad, batch, activeLocation, loadingLogic, cancellationToken);
                }

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
            // sceneLoaded：Awake 之后、Start 之前。此时设 Active，保证 Start/Instantiate 进新场景。
            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (!scene.IsValid() || !scene.isLoaded)
                    return;
                if (scene.name != activeLocation)
                    return;

                SceneManager.SetActiveScene(scene);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                for (int i = 0; i < toLoad.Count; i++)
                    GameFrameWork.Scene.AllowSceneActivation(toLoad[i]);

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
