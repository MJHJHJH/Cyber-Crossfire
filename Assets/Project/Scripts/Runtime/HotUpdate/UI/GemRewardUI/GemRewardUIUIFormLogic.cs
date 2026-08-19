using System;
using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 宝石奖励 View：展示数量；自动关闭由 <see cref="GemRewardUIPresenter"/> 处理。
/// </summary>
public sealed class GemRewardUIUIFormLogic : UIFormLogic, IGemRewardUIView
{
    [SerializeField] private Text m_AmountText;

    private GemRewardUIPresenter _presenter;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        var scope = LifetimeScope.Find<UiLifetimeScope>();
        if (scope?.Container == null)
        {
            Debug.LogWarning("[GemRewardUI] UiLifetimeScope is not built; skip presenter.");
            return;
        }

        _presenter = null;
        try
        {
            _presenter = scope.Container.Resolve<GemRewardUIPresenter>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GemRewardUI] Resolve GemRewardUIPresenter failed: {e.Message}");
            return;
        }

        _presenter.Attach(this);
        if (userData is int amount)
            _presenter.ShowAmount(amount);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        _presenter?.Detach();
        _presenter = null;
        base.OnClose(isShutdown, userData);
    }

    public void BtnOk() => _presenter?.OnOkClick();

    public void SetAmount(int amount)
    {
        if (m_AmountText != null)
            m_AmountText.text = amount.ToString();
    }

    public void RequestClose()
    {
        if (UIForm != null)
            GameFrameWork.UI?.Close(UIForm);
    }
}
