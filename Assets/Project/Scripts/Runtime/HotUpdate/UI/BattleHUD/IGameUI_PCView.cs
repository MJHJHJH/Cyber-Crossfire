using CommandoRobot;
using UI.Mvp;
using UnityEngine;

public interface IGameUI_PCView : IPanelView
{
    void SetBossHealthVisible(bool visible);

    void SetAimLockVisible(bool visible);

    void SetAimLockAnchoredPosition(Vector2 anchoredPosition);

    void SetCoinText(int coin);

    void SetGunName(string title);

    void SetGrenadeText(int count);

    void SetPlayerHealthFill(float fillAmount);

    void SetBossHealthFill(float fillAmount);

    Vector2 WorldPointToUI(Vector3 worldPosition);

    Vector2 MousePosToUI();
}
