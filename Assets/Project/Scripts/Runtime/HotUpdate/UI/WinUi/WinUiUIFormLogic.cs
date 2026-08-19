using System;
using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 胜利界面 View：按钮转发；业务由 <see cref="WinUiPresenter"/> 处理。
/// </summary>
public sealed class WinUiUIFormLogic : UIFormLogic, IWinUiView
{
    private WinUiPresenter _presenter;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        var scope = LifetimeScope.Find<UiLifetimeScope>();
        if (scope?.Container == null)
        {
            Debug.LogWarning("[WinUi] UiLifetimeScope is not built; skip presenter.");
            return;
        }

        _presenter = null;
        try
        {
            _presenter = scope.Container.Resolve<WinUiPresenter>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WinUi] Resolve WinUiPresenter failed: {e.Message}");
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

    public void Continue() => _presenter?.OnContinueClick();

    public void RequestClose()
    {
        if (UIForm != null)
            GameFrameWork.UI?.Close(UIForm);
    }
}
