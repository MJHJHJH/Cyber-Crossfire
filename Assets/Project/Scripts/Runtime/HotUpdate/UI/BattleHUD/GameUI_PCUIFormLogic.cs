using System;
using System.Collections;
using CommandoRobot;
using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using MMProgressBar = MoreMountains.Tools.MMProgressBar;

/// <summary>
/// 战斗 HUD View：控件刷新；业务由 <see cref="GameUI_PCPresenter"/> 处理。
/// 血条动画：MMProgressBar 挂在 life 节点上，扣血时 life 上下摇晃，加血时放大拉回。
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
    [SerializeField] private MMProgressBar m_PlayerHealthBar;
    [SerializeField] private Image m_BossHealthBase;
    [SerializeField] private MMProgressBar m_BossHealthBar;

    private GameUI_PCPresenter _presenter;

    private Coroutine _playerShake;
    private Coroutine _playerPop;
    private Coroutine _bossShake;
    private Coroutine _bossPop;
    private Vector2 _playerHealthBasePos;
    private Vector2 _bossHealthBasePos;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        Current = this;

        CacheHealthBasePos();
        BindHealthEvents(true);

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

        BindHealthEvents(false);
        StopHealthAnims();
        ResetHealthTransforms();

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
        if (m_PlayerHealthBar != null)
            m_PlayerHealthBar.UpdateBar01(fillAmount);
    }

    public void SetBossHealthFill(float fillAmount)
    {
        if (m_BossHealthBar != null)
            m_BossHealthBar.UpdateBar01(fillAmount);
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

    #region HealthBarAnim

    private void CacheHealthBasePos()
    {
        _playerHealthBasePos = GetAnchoredPos(m_PlayerHealthBar);
        _bossHealthBasePos = GetAnchoredPos(m_BossHealthBar);
    }

    private Vector2 GetAnchoredPos(MMProgressBar bar)
    {
        if (bar == null)
            return Vector2.zero;
        RectTransform rt = bar.transform as RectTransform;
        return rt != null ? rt.anchoredPosition : Vector2.zero;
    }

    private void BindHealthEvents(bool bind)
    {
        BindBarEvents(m_PlayerHealthBar, OnPlayerHealthDecrease, OnPlayerHealthIncrease, bind);
        BindBarEvents(m_BossHealthBar, OnBossHealthDecrease, OnBossHealthIncrease, bind);
    }

    private void BindBarEvents(MMProgressBar bar, UnityEngine.Events.UnityAction onDecrease,
        UnityEngine.Events.UnityAction onIncrease, bool bind)
    {
        if (bar == null)
            return;
        if (bind)
        {
            bar.OnBarMovementDecreasingStart.AddListener(onDecrease);
            bar.OnBarMovementIncreasingStart.AddListener(onIncrease);
        }
        else
        {
            bar.OnBarMovementDecreasingStart.RemoveListener(onDecrease);
            bar.OnBarMovementIncreasingStart.RemoveListener(onIncrease);
        }
    }

    private void StopHealthAnims()
    {
        if (_playerShake != null) { StopCoroutine(_playerShake); _playerShake = null; }
        if (_playerPop != null) { StopCoroutine(_playerPop); _playerPop = null; }
        if (_bossShake != null) { StopCoroutine(_bossShake); _bossShake = null; }
        if (_bossPop != null) { StopCoroutine(_bossPop); _bossPop = null; }
    }

    private void ResetHealthTransforms()
    {
        ResetBarTransform(m_PlayerHealthBar, _playerHealthBasePos);
        ResetBarTransform(m_BossHealthBar, _bossHealthBasePos);
    }

    private void ResetBarTransform(MMProgressBar bar, Vector2 basePos)
    {
        if (bar == null)
            return;
        RectTransform rt = bar.transform as RectTransform;
        if (rt == null)
            return;
        rt.anchoredPosition = basePos;
        rt.localScale = Vector3.one;
    }

    private void OnPlayerHealthDecrease() => PlayShake(m_PlayerHealthBar, _playerHealthBasePos, ref _playerShake);
    private void OnPlayerHealthIncrease() => PlayPop(m_PlayerHealthBar, ref _playerPop);
    private void OnBossHealthDecrease() => PlayShake(m_BossHealthBar, _bossHealthBasePos, ref _bossShake);
    private void OnBossHealthIncrease() => PlayPop(m_BossHealthBar, ref _bossPop);

    /// <summary>扣血：life 左右摇晃（正弦衰减）。</summary>
    private void PlayShake(MMProgressBar bar, Vector2 basePos, ref Coroutine slot)
    {
        if (bar == null)
            return;
        if (slot != null)
        {
            StopCoroutine(slot);
            slot = null;
        }
        RectTransform rt = bar.transform as RectTransform;
        if (rt != null)
            rt.anchoredPosition = basePos;
        slot = StartCoroutine(ShakeCo(bar, basePos));
    }

    private IEnumerator ShakeCo(MMProgressBar bar, Vector2 basePos)
    {
        RectTransform rt = bar.transform as RectTransform;
        if (rt == null)
            yield break;

        const float duration = 0.35f;
        const float amplitude = 10f;
        const float frequency = 28f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float decay = 1f - t;
            float x = Mathf.Sin(elapsed * frequency) * amplitude * decay;
            rt.anchoredPosition = basePos + new Vector2(x, 0f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    /// <summary>加血：life 放大后拉回（正弦脉冲）。</summary>
    private void PlayPop(MMProgressBar bar, ref Coroutine slot)
    {
        if (bar == null)
            return;
        if (slot != null)
        {
            StopCoroutine(slot);
            slot = null;
        }
        RectTransform rt = bar.transform as RectTransform;
        if (rt != null)
            rt.localScale = Vector3.one;
        slot = StartCoroutine(PopCo(bar));
    }

    private IEnumerator PopCo(MMProgressBar bar)
    {
        RectTransform rt = bar.transform as RectTransform;
        if (rt == null)
            yield break;

        const float duration = 0.25f;
        const float peak = 1.12f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + (peak - 1f) * Mathf.Sin(t * Mathf.PI);
            rt.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    #endregion
}
