using GameFramework;
using GameFramework.Event;
using GamePlay.AssetPatch;
using UI.Mvp;
using UnityEngine;

/// <summary>
/// YooAsset 初始化/热更 Presenter：订卸 Event、处理确认/离开意图。
/// </summary>
public sealed class YooAssetInitPresenter : PanelPresenter<IYooAssetInitView>
{
    private enum MessageAction
    {
        None,
        RetryInitialize,
        RetryRequestVersion,
        RetryUpdateManifest,
        RetryDownload,
        BeginDownload,
    }

    private MessageAction _pendingAction;
    private bool _eventsSubscribed;

    protected override void OnAttach()
    {
        _pendingAction = MessageAction.None;
        SubscribeEvents();
    }

    protected override void OnDetach()
    {
        UnsubscribeEvents();
        _pendingAction = MessageAction.None;
    }

    public void OnConfirmClick()
    {
        if (!IsAttached)
            return;

        MessageAction action = _pendingAction;
        if (action == MessageAction.None)
            return;

        View.HideMessageBox();
        _pendingAction = MessageAction.None;
        View.SetLoadingTips("处理中…");

        if (GameFrameWork.Event == null)
            return;

        switch (action)
        {
            case MessageAction.RetryInitialize:
                GameFrameWork.Event.Fire(this, AssetPatchUserTryInitializeEventArgs.Create());
                break;
            case MessageAction.RetryRequestVersion:
                GameFrameWork.Event.Fire(this, AssetPatchUserTryRequestVersionEventArgs.Create());
                break;
            case MessageAction.RetryUpdateManifest:
                GameFrameWork.Event.Fire(this, AssetPatchUserTryUpdateManifestEventArgs.Create());
                break;
            case MessageAction.RetryDownload:
                GameFrameWork.Event.Fire(this, AssetPatchUserTryDownloadEventArgs.Create());
                break;
            case MessageAction.BeginDownload:
                GameFrameWork.Event.Fire(this, AssetPatchUserBeginDownloadEventArgs.Create());
                break;
        }
    }

    public void OnLeaveClick()
    {
        GameEntry.Shutdown(ShutdownType.Quit);
    }

    private void SubscribeEvents()
    {
        if (GameFrameWork.Event == null)
        {
            Debug.LogError("[YooAssetInit] EventComponent is missing.");
            return;
        }

        if (_eventsSubscribed)
            return;

        GameFrameWork.Event.Subscribe(AssetPatchStepsChangeEventArgs.EventId, OnStepsChange);
        GameFrameWork.Event.Subscribe(AssetPatchProgressEventArgs.EventId, OnProgress);
        GameFrameWork.Event.Subscribe(AssetPatchInitializeFailedEventArgs.EventId, OnInitializeFailed);
        GameFrameWork.Event.Subscribe(AssetPatchVersionRequestFailedEventArgs.EventId, OnVersionFailed);
        GameFrameWork.Event.Subscribe(AssetPatchManifestUpdateFailedEventArgs.EventId, OnManifestFailed);
        GameFrameWork.Event.Subscribe(AssetPatchFoundUpdateFilesEventArgs.EventId, OnFoundUpdateFiles);
        GameFrameWork.Event.Subscribe(AssetPatchDownloadFailedEventArgs.EventId, OnDownloadFailed);
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (GameFrameWork.Event == null || !_eventsSubscribed)
            return;

        GameFrameWork.Event.Unsubscribe(AssetPatchStepsChangeEventArgs.EventId, OnStepsChange);
        GameFrameWork.Event.Unsubscribe(AssetPatchProgressEventArgs.EventId, OnProgress);
        GameFrameWork.Event.Unsubscribe(AssetPatchInitializeFailedEventArgs.EventId, OnInitializeFailed);
        GameFrameWork.Event.Unsubscribe(AssetPatchVersionRequestFailedEventArgs.EventId, OnVersionFailed);
        GameFrameWork.Event.Unsubscribe(AssetPatchManifestUpdateFailedEventArgs.EventId, OnManifestFailed);
        GameFrameWork.Event.Unsubscribe(AssetPatchFoundUpdateFilesEventArgs.EventId, OnFoundUpdateFiles);
        GameFrameWork.Event.Unsubscribe(AssetPatchDownloadFailedEventArgs.EventId, OnDownloadFailed);
        _eventsSubscribed = false;
    }

    private void OnStepsChange(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchStepsChangeEventArgs)e;
        View.SetLoadingTips(args.Tips);
    }

    private void OnProgress(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchProgressEventArgs)e;
        if (!string.IsNullOrEmpty(args.Tips))
            View.SetLoadingTips(args.Tips);

        View.SetProgressTarget(args.Progress * 100f);
    }

    private void OnInitializeFailed(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchInitializeFailedEventArgs)e;
        ShowMessageBox(
            FormatPackageTitle("资源初始化失败", args.PackageName),
            string.IsNullOrEmpty(args.Error) ? "是否重试？" : $"{args.Error}\n是否重试？",
            "重试",
            MessageAction.RetryInitialize);
    }

    private void OnVersionFailed(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchVersionRequestFailedEventArgs)e;
        ShowMessageBox(
            FormatPackageTitle("请求资源版本失败", args.PackageName),
            string.IsNullOrEmpty(args.Error) ? "是否重试？" : $"{args.Error}\n是否重试？",
            "重试",
            MessageAction.RetryRequestVersion);
    }

    private void OnManifestFailed(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchManifestUpdateFailedEventArgs)e;
        ShowMessageBox(
            FormatPackageTitle("更新资源清单失败", args.PackageName),
            string.IsNullOrEmpty(args.Error) ? "是否重试？" : $"{args.Error}\n是否重试？",
            "重试",
            MessageAction.RetryUpdateManifest);
    }

    private void OnFoundUpdateFiles(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchFoundUpdateFilesEventArgs)e;
        string title = string.IsNullOrEmpty(args.PackageName)
            ? "发现资源更新"
            : FormatPackageTitle("发现资源更新", args.PackageName);
        ShowMessageBox(
            title,
            $"文件数：{args.TotalCount}\n大小：{FormatBytes(args.TotalBytes)}\n（已汇总全部资源包）\n是否开始下载？",
            "开始下载",
            MessageAction.BeginDownload);
    }

    private void OnDownloadFailed(object sender, GameEventArgs e)
    {
        if (!IsAttached)
            return;

        var args = (AssetPatchDownloadFailedEventArgs)e;
        string detail = string.IsNullOrEmpty(args.FileName)
            ? args.Error
            : $"{args.FileName}\n{args.Error}";
        ShowMessageBox(
            FormatPackageTitle("资源下载失败", args.PackageName),
            string.IsNullOrEmpty(detail) ? "是否重试？" : $"{detail}\n是否重试？",
            "重试",
            MessageAction.RetryDownload);
    }

    private void ShowMessageBox(string title, string content, string confirmLabel, MessageAction action)
    {
        _pendingAction = action;
        View.ShowMessageBox(title, content, confirmLabel);
    }

    private static string FormatPackageTitle(string title, string packageName)
    {
        return string.IsNullOrEmpty(packageName) ? title : $"{title} ({packageName})";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024f:0.##} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024f * 1024f):0.##} MB";
        return $"{bytes / (1024f * 1024f * 1024f):0.##} GB";
    }
}
