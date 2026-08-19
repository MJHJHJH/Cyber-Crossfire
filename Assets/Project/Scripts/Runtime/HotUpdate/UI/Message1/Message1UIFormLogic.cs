using System;
using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 提示消息 View：展示文案；自动关闭由 <see cref="Message1Presenter"/> 处理。
/// </summary>
public sealed class Message1UIFormLogic : UIFormLogic, IMessage1View
{
    [SerializeField] private Text m_MessageText;

    private Message1Presenter _presenter;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        var scope = LifetimeScope.Find<UiLifetimeScope>();
        if (scope?.Container == null)
        {
            Debug.LogWarning("[Message1] UiLifetimeScope is not built; skip presenter.");
            return;
        }

        _presenter = null;
        try
        {
            _presenter = scope.Container.Resolve<Message1Presenter>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Message1] Resolve Message1Presenter failed: {e.Message}");
            return;
        }

        _presenter.Attach(this);
        if (userData is string message)
            _presenter.ShowMessage(message);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        _presenter?.Detach();
        _presenter = null;
        base.OnClose(isShutdown, userData);
    }

    public void SetMessage(string message)
    {
        if (m_MessageText != null)
            m_MessageText.text = message;
    }

    public void RequestClose()
    {
        if (UIForm != null)
            GameFrameWork.UI?.Close(UIForm);
    }
}
