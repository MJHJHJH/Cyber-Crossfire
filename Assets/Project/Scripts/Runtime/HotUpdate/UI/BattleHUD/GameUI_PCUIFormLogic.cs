using System;
using CommandoRobot;
using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 战斗 HUD View：控件刷新；业务由 <see cref="GameUI_PCPresenter"/> 处理。
/// </summary>
public sealed class GameUI_PCUIFormLogic : UIFormLogic, IGameUI_PCView
{
    /// <summary>当前打开的战斗 HUD，供 GameControl 等玩法侧调用。</summary>
    public static GameUI_PCUIFormLogic Current { get; private set; }

    [SerializeField] private Text m_CoinCountText;
    [SerializeField] private Text m_GunNameText;
    [SerializeField] private Text m_GrenadeText;
    [SerializeField] private Image m_TargetLockImage;
    [SerializeField] private RectTransform m_MainCanvas;
    [SerializeField] private Image m_PlayerHealth;
    [SerializeField] private Image m_BossHealthBase;
    [SerializeField] private Image m_BossHealth;

    private GameUI_PCPresenter _presenter;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        Current = this;

        var scope = LifetimeScope.Find<UiLifetimeScope>();
        if (scope?.Container == null)
        {
            Debug.LogWarning("[GameUI_PC] UiLifetimeScope is not built; skip presenter.");
            return;
        }

        _presenter = null;
        try
        {
            _presenter = scope.Container.Resolve<GameUI_PCPresenter>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameUI_PC] Resolve GameUI_PCPresenter failed: {e.Message}");
            return;
        }

        _presenter.Attach(this);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        _presenter?.Detach();
        _presenter = null;
        if (Current == this)
            Current = null;
        base.OnClose(isShutdown, userData);
    }

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        _presenter?.Tick();
    }

    public void ShowBossHealth(DamageControl bossDamage) => _presenter?.ShowBossHealth(bossDamage);

    public void BtnPause() => _presenter?.OnPauseClick();

    public void SetBossHealthVisible(bool visible)
    {
        if (m_BossHealthBase != null)
            m_BossHealthBase.gameObject.SetActive(visible);
    }

    public void SetAimLockVisible(bool visible)
    {
        if (m_TargetLockImage != null)
            m_TargetLockImage.gameObject.SetActive(visible);
    }

    public void SetAimLockAnchoredPosition(Vector2 anchoredPosition)
    {
        if (m_TargetLockImage != null)
            m_TargetLockImage.rectTransform.anchoredPosition = anchoredPosition;
    }

    public void SetCoinText(int coin)
    {
        if (m_CoinCountText != null)
            m_CoinCountText.text = coin.ToString();
    }

    public void SetGunName(string title)
    {
        if (m_GunNameText != null)
            m_GunNameText.text = title;
    }

    public void SetGrenadeText(int count)
    {
        if (m_GrenadeText != null)
            m_GrenadeText.text = "Grenade : " + count;
    }

    public void SetPlayerHealthFill(float fillAmount)
    {
        if (m_PlayerHealth != null)
            m_PlayerHealth.fillAmount = fillAmount;
    }

    public void SetBossHealthFill(float fillAmount)
    {
        if (m_BossHealth != null)
            m_BossHealth.fillAmount = fillAmount;
    }

    public Vector2 WorldPointToUI(Vector3 worldPosition)
    {
        if (m_MainCanvas == null || CameraControl.m_Current?.m_MyCamera == null)
            return Vector2.zero;

        Vector3 v = CameraControl.m_Current.m_MyCamera.WorldToScreenPoint(worldPosition);
        return ScreenPointToCanvas(v);
    }

    public Vector2 MousePosToUI()
    {
        if (m_MainCanvas == null)
            return Vector2.zero;

        return ScreenPointToCanvas(Input.mousePosition);
    }

    private Vector2 ScreenPointToCanvas(Vector3 screenPoint)
    {
        screenPoint.x = screenPoint.x / Screen.width;
        screenPoint.y = screenPoint.y / Screen.height;
        screenPoint.x = m_MainCanvas.sizeDelta.x * screenPoint.x;
        screenPoint.y = m_MainCanvas.sizeDelta.y * screenPoint.y;
        return Helper.ToVector2(screenPoint);
    }
}
