using System;
using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 暂停界面 View：按钮转发；业务由 <see cref="PauseUiPresenter"/> 处理。
/// </summary>
public sealed class PauseUiUIFormLogic : UIFormLogic, IPauseUiView
{
    private PauseUiPresenter _presenter;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        var scope = LifetimeScope.Find<UiLifetimeScope>();
        if (scope?.Container == null)
        {
            Debug.LogWarning("[PauseUi] UiLifetimeScope is not built; skip presenter.");
            return;
        }

        _presenter = null;
        try
        {
            _presenter = scope.Container.Resolve<PauseUiPresenter>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PauseUi] Resolve PauseUiPresenter failed: {e.Message}");
            return;
        }

        _presenter.Attach(this);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        _presenter?.Detach();
        _presenter = null;
        base.OnClose(isShutdown, userData);
    }

    public void BtnContinue() => _presenter?.OnContinueClick();

    public void BtnExit() => _presenter?.OnExitClick();

    public void RequestClose()
    {
        if (UIForm != null)
            GameFrameWork.UI?.Close(UIForm);
    }
}
