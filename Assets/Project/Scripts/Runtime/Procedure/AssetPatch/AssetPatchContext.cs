using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Event;
using GameFramework.Fsm;
using UnityEngine;
using YooAsset;

namespace GamePlay.AssetPatch
{
    /// <summary>单包待下载项（检查阶段收集，统一确认后下载）。</summary>
    public sealed class PendingDownloadEntry
    {
        public string PackageName;
        public ResourceDownloaderOperation Downloader;
    }

    /// <summary>补丁内嵌 Fsm 的 Owner：配置、黑板数据、事件与完成回调。</summary>
    public sealed class AssetPatchContext
    {
        public const string FsmName = "AssetPatch";

        private readonly EventComponent m_Event;
        private readonly string[] m_PackageQueue;
        private readonly List<PendingDownloadEntry> m_PendingDownloads = new List<PendingDownloadEntry>(4);
        private IFsm<AssetPatchContext> m_Fsm;
        private bool m_UserEventsSubscribed;
        private UniTaskCompletionSource m_ProgressReadyTcs;
        private int m_PackageIndex;

        public AssetPatchContext(
            IList<string> packageQueue,
            string defaultPackageName,
            EPlayMode playMode,
            string hostServerIP,
            string fallbackHostServerIP,
            string hostAppVersion,
            EventComponent eventComponent,
            Action onSucceeded)
        {
            if (packageQueue == null || packageQueue.Count == 0)
                throw new ArgumentException("packageQueue is empty.", nameof(packageQueue));

            m_PackageQueue = new string[packageQueue.Count];
            for (int i = 0; i < packageQueue.Count; i++)
                m_PackageQueue[i] = packageQueue[i];

            m_PackageIndex = 0;
            PackageName = m_PackageQueue[0];
            DefaultPackageName = string.IsNullOrEmpty(defaultPackageName)
                ? PackageName
                : defaultPackageName;
            PlayMode = playMode;
            HostServerIP = hostServerIP;
            FallbackHostServerIP = fallbackHostServerIP;
            HostAppVersion = hostAppVersion;
            m_Event = eventComponent;
            OnSucceeded = onSucceeded;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public string PackageName { get; private set; }

        public string DefaultPackageName { get; }

        public int PackageIndex => m_PackageIndex;

        public int PackageCount => m_PackageQueue.Length;

        public IReadOnlyList<PendingDownloadEntry> PendingDownloads => m_PendingDownloads;

        public int TotalPendingDownloadCount { get; private set; }

        public long TotalPendingDownloadBytes { get; private set; }

        public EPlayMode PlayMode { get; }

        public string HostServerIP { get; }

        public string FallbackHostServerIP { get; }

        public string HostAppVersion { get; }

        public string PackageVersion { get; set; }

        public ResourceDownloaderOperation Downloader { get; set; }

        public CancellationTokenSource CancellationTokenSource { get; private set; }

        public CancellationToken CancellationToken =>
            CancellationTokenSource != null ? CancellationTokenSource.Token : CancellationToken.None;

        public Action OnSucceeded { get; }

        public void BindFsm(IFsm<AssetPatchContext> fsm)
        {
            m_Fsm = fsm;
        }

        public string GetPackageNameAt(int index)
        {
            return m_PackageQueue[index];
        }

        public void SetActivePackageName(string packageName)
        {
            if (!string.IsNullOrEmpty(packageName))
                PackageName = packageName;
        }

        public void AddPendingDownload(string packageName, ResourceDownloaderOperation downloader)
        {
            if (downloader == null || downloader.TotalDownloadCount <= 0)
                return;

            m_PendingDownloads.Add(new PendingDownloadEntry
            {
                PackageName = packageName,
                Downloader = downloader,
            });
            TotalPendingDownloadCount += downloader.TotalDownloadCount;
            TotalPendingDownloadBytes += downloader.TotalDownloadBytes;
        }

        public void SubscribeUserEvents()
        {
            if (m_Event == null || m_UserEventsSubscribed)
                return;

            m_Event.Subscribe(AssetPatchUserTryInitializeEventArgs.EventId, OnUserTryInitialize);
            m_Event.Subscribe(AssetPatchUserTryRequestVersionEventArgs.EventId, OnUserTryRequestVersion);
            m_Event.Subscribe(AssetPatchUserTryUpdateManifestEventArgs.EventId, OnUserTryUpdateManifest);
            m_Event.Subscribe(AssetPatchUserTryDownloadEventArgs.EventId, OnUserTryDownload);
            m_Event.Subscribe(AssetPatchUserBeginDownloadEventArgs.EventId, OnUserBeginDownload);
            m_Event.Subscribe(AssetPatchUserProgressReadyEventArgs.EventId, OnUserProgressReady);
            m_UserEventsSubscribed = true;
        }

        public void UnsubscribeUserEvents()
        {
            if (m_Event == null || !m_UserEventsSubscribed)
                return;

            m_Event.Unsubscribe(AssetPatchUserTryInitializeEventArgs.EventId, OnUserTryInitialize);
            m_Event.Unsubscribe(AssetPatchUserTryRequestVersionEventArgs.EventId, OnUserTryRequestVersion);
            m_Event.Unsubscribe(AssetPatchUserTryUpdateManifestEventArgs.EventId, OnUserTryUpdateManifest);
            m_Event.Unsubscribe(AssetPatchUserTryDownloadEventArgs.EventId, OnUserTryDownload);
            m_Event.Unsubscribe(AssetPatchUserBeginDownloadEventArgs.EventId, OnUserBeginDownload);
            m_Event.Unsubscribe(AssetPatchUserProgressReadyEventArgs.EventId, OnUserProgressReady);
            m_UserEventsSubscribed = false;
            m_ProgressReadyTcs?.TrySetCanceled();
            m_ProgressReadyTcs = null;
        }

        public void Cancel()
        {
            if (CancellationTokenSource == null)
                return;

            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
        }

        public void Fire(GameEventArgs e)
        {
            m_Event?.Fire(this, e);
        }

        public void FireSteps(string tips)
        {
            Fire(AssetPatchStepsChangeEventArgs.Create(FormatTips(tips), PackageName));
        }

        public void FireProgress(float progress, string tips = null)
        {
            Fire(AssetPatchProgressEventArgs.Create(progress, FormatTips(tips), PackageName));
        }

        /// <summary>设置进度目标并等待 UI 追赶到 100% 后再继续（先挂等待再 Fire，避免同步 Ready 丢失）。</summary>
        public async UniTask FireProgressAndWaitReadyAsync(float progress, string tips, CancellationToken ct)
        {
            m_ProgressReadyTcs = new UniTaskCompletionSource();
            try
            {
                FireProgress(progress, tips);
                await m_ProgressReadyTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                m_ProgressReadyTcs = null;
            }
        }

        /// <summary>切换到队列下一包并清空包级黑板；若已无下一包返回 false。</summary>
        public bool TryBeginNextPackage()
        {
            int next = m_PackageIndex + 1;
            if (next >= m_PackageQueue.Length)
                return false;

            m_PackageIndex = next;
            PackageName = m_PackageQueue[m_PackageIndex];
            PackageVersion = null;
            Downloader = null;
            return true;
        }

        public string BuildHostServerURL(string serverIP)
        {
            string platform;
#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
                platform = "Android";
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
                platform = "IPhone";
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WebGL)
                platform = "WebGL";
            else
                platform = "PC";
#else
            if (Application.platform == RuntimePlatform.Android)
                platform = "Android";
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
                platform = "IPhone";
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
                platform = "WebGL";
            else
                platform = "PC";
#endif
            return $"{serverIP}/CDN/{platform}/{HostAppVersion}/{PackageName}";
        }

        public void ChangeTo<TState>() where TState : AssetPatchState
        {
            if (m_Fsm == null || m_Fsm.IsDestroyed)
                return;

            AssetPatchState current = m_Fsm.CurrentState as AssetPatchState;
            current?.Goto<TState>(m_Fsm);
        }

        private string FormatTips(string tips)
        {
            if (string.IsNullOrEmpty(tips))
                return $"[{PackageName}]";
            return $"[{PackageName}] {tips}";
        }

        private void OnUserTryInitialize(object sender, GameEventArgs e)
        {
            ChangeTo<InitializePackageState>();
        }

        private void OnUserTryRequestVersion(object sender, GameEventArgs e)
        {
            ChangeTo<RequestPackageVersionState>();
        }

        private void OnUserTryUpdateManifest(object sender, GameEventArgs e)
        {
            ChangeTo<UpdatePackageManifestState>();
        }

        private void OnUserTryDownload(object sender, GameEventArgs e)
        {
            ChangeTo<DownloadPackageFilesState>();
        }

        private void OnUserBeginDownload(object sender, GameEventArgs e)
        {
            ChangeTo<DownloadPackageFilesState>();
        }

        private void OnUserProgressReady(object sender, GameEventArgs e)
        {
            m_ProgressReadyTcs?.TrySetResult();
        }
    }
}
