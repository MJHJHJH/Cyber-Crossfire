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
    private static readonly Color LockedWeaponColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);

    [SerializeField] private Text m_CoinText;
    [SerializeField] private Image m_LevelPanel;
    [SerializeField] private Image m_ArmoryPanel;
    [SerializeField] private Image m_MainPanel;
    [SerializeField] private Image[] m_WeaponButtons;
    [SerializeField] private Image[] m_WeaponImages;
    [SerializeField] private Text[] m_WeaponPrices;

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

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        _presenter?.Tick();
    }

    public void ShowMainPanel()
    {
        SetActive(m_LevelPanel, false);
        SetActive(m_ArmoryPanel, false);
        SetActive(m_MainPanel, true);
    }

    public void ShowLevelPanel()
    {
        SetActive(m_LevelPanel, true);
        SetActive(m_MainPanel, false);
    }

    public void ShowArmoryPanel()
    {
        SetActive(m_ArmoryPanel, true);
        SetActive(m_MainPanel, false);
    }

    public void SetCoinText(int coin)
    {
        if (m_CoinText != null)
            m_CoinText.text = coin.ToString();
    }

    public void SetWeaponSlot(int index, bool selected, bool unlocked, int price)
    {
        if (index >= 0 && m_WeaponButtons != null && index < m_WeaponButtons.Length && m_WeaponButtons[index] != null)
            m_WeaponButtons[index].color = selected ? Color.green : Color.white;

        if (index >= 0 && m_WeaponImages != null && index < m_WeaponImages.Length && m_WeaponImages[index] != null)
            m_WeaponImages[index].color = unlocked ? Color.white : LockedWeaponColor;

        if (index >= 0 && m_WeaponPrices != null && index < m_WeaponPrices.Length && m_WeaponPrices[index] != null)
        {
            m_WeaponPrices[index].gameObject.SetActive(!unlocked);
            if (!unlocked)
                m_WeaponPrices[index].text = price.ToString();
        }
    }

    public void BtnPlay() => _presenter?.OnPlayClick();

    public void BtnArmory() => _presenter?.OnArmoryClick();

    public void BtnBack() => _presenter?.OnBackClick();

    public void BtnWeapon(int num) => _presenter?.OnWeaponClick(num);

    public void BtnLevel(int num) => _presenter?.OnLevelClick(num);

    public void BtnExit() => _presenter?.OnExitClick();

    private static void SetActive(Image image, bool active)
    {
        if (image != null)
            image.gameObject.SetActive(active);
    }
}
