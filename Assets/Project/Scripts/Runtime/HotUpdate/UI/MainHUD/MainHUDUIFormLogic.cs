using System;
using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 主菜单界面 View：控件显隐与刷新；业务由 <see cref="MainHUDPresenter"/> 处理。
/// </summary>
public sealed class MainHUDUIFormLogic : UIFormLogic, IMainHUDView
{
    [SerializeField] private Text m_CoinText;
    [SerializeField] private Image m_LevelPanel;
    [SerializeField] private Image m_MainPanel;

    private MainHUDPresenter _presenter;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        var scope = LifetimeScope.Find<UiLifetimeScope>();
        if (scope?.Container == null)
        {
            Debug.LogWarning("[MainHUD] UiLifetimeScope is not built; skip presenter.");
            return;
        }

        _presenter = null;
        try
        {
            _presenter = scope.Container.Resolve<MainHUDPresenter>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MainHUD] Resolve MainHUDPresenter failed: {e.Message}");
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

    public void ShowMainPanel()
    {
        SetActive(m_LevelPanel, false);
        SetActive(m_MainPanel, true);
    }

    public void ShowLevelPanel()
    {
        SetActive(m_LevelPanel, true);
        SetActive(m_MainPanel, false);
    }

    public void SetCoinText(int coin)
    {
        if (m_CoinText != null)
            m_CoinText.text = coin.ToString();
    }

    public void BtnPlay() => _presenter?.OnPlayClick();

    public void BtnArmory() => _presenter?.OnArmoryClick();

    public void BtnBack() => _presenter?.OnBackClick();

    public void BtnLevel(int num) => _presenter?.OnLevelClick(num);

    public void BtnExit() => _presenter?.OnExitClick();

    private static void SetActive(Image image, bool active)
    {
        if (image != null)
            image.gameObject.SetActive(active);
    }
}
