using UI.Mvp;

/// <summary>
/// YooAsset 初始化/热更界面 View 契约。
/// </summary>
public interface IYooAssetInitView : IPanelView
{
    void SetLoadingTips(string tips);

    void SetProgressTarget(float targetPercent);

    void ShowMessageBox(string title, string content, string confirmLabel);

    void HideMessageBox();
}
