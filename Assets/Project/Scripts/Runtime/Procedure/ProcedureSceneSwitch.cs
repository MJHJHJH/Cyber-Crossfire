using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 场景组中间加载态（非 Procedure）：并行加载 → 激活主场景 → 卸旧（只留组 + Home）。
    /// 任意业务流程可在 OnEnter 中 await SwitchAsync。
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
                if (toLoad.Count > 0)
                    await LoadGroupParallelAsync(toLoad, loadingLogic, cancellationToken);
                else
                    ReportProgress(1f, loadingLogic);

                if (!GameFrameWork.Scene.ActivateScene(activeLocation))
                    Debug.LogWarning(Utility.Text.Format(
                        "[ProcedureSceneSwitch] Activate scene '{0}' failure.", activeLocation));

                ScenesReady?.Invoke();

                await UnloadOutsideGroupAsync(locations, cancellationToken);

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

        private static async UniTask LoadGroupParallelAsync(
            List<string> toLoad,
            LoadingUIFormLogic loadingLogic,
            CancellationToken cancellationToken)
        {
            int count = toLoad.Count;
            float[] progresses = new float[count];
            UniTask[] tasks = new UniTask[count];

            for (int i = 0; i < count; i++)
            {
                int index = i;
                string location = toLoad[i];
                IProgress<float> progress = Progress.Create<float>(value =>
                {
                    progresses[index] = Mathf.Clamp01(value);
                    ReportProgress(Average(progresses), loadingLogic);
                });

                // 转成 UniTask：suspend 完成前会挂起，需在下方统一 AllowSceneActivation 后再 WhenAll
                tasks[i] = AwaitLoadAsync(location, progress, cancellationToken);
            }

            // suspendLoad 下 await 会挂到 AllowSceneActivation；优先等进度到挂起点，超时则仍解除以免死锁
            await UniTask.WhenAny(
                UniTask.WaitUntil(() => AllReadyForActivation(progresses), cancellationToken: cancellationToken),
                UniTask.Delay(TimeSpan.FromSeconds(120), cancellationToken: cancellationToken));

            for (int i = 0; i < count; i++)
                GameFrameWork.Scene.AllowSceneActivation(toLoad[i]);

            await UniTask.WhenAll(tasks);
            ReportProgress(1f, loadingLogic);
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
            for (int i = 0; i < progresses.Length; i++)
            {
                if (progresses[i] < SuspendReadyProgress)
                    return false;
            }

            return progresses.Length > 0;
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

        /// <summary>进度条展示跑满，且 Loading 至少显示 MinLoadingDisplaySeconds。</summary>
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
