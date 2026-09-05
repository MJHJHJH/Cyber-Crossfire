using CommandoRobot;
using GamePlay.Data;
using UI.Mvp;
using UnityEngine;

public sealed class GameUI_PCPresenter : PanelPresenter<IGameUI_PCView>
{
    private DamageControl _bossDamageControl;

    protected override void OnAttach()
    {
        _bossDamageControl = null;
        View.SetBossHealthVisible(false);
        View.SetAimLockVisible(false);
    }

    protected override void OnDetach()
    {
        _bossDamageControl = null;
    }

    public void Tick()
    {
        if (!IsAttached)
            return;

        PlayerCharacter player = PlayerCharacter.m_Current;
        if (player == null)
            return;

        RefreshAimLock(player);
        View.SetCoinText(PlayerSave.Coin);

        if (player.m_CurrentWeapon != null)
            View.SetGunName(player.m_CurrentWeapon.Config.Title);

        View.SetGrenadeText(player.m_GrenadeCount);

        DamageControl damage = player.GetComponent<DamageControl>();
        if (damage != null && damage.MaxDamage > 0f)
            View.SetPlayerHealthFill(damage.Damage / damage.MaxDamage);

        if (_bossDamageControl != null && _bossDamageControl.MaxDamage > 0f)
            View.SetBossHealthFill(_bossDamageControl.Damage / _bossDamageControl.MaxDamage);
    }

    public void OnPauseClick()
    {
        if (!IsAttached)
            return;

        if (GameControl.m_Current != null)
            GameControl.m_Current.PauseGame();
    }

    public void ShowBossHealth(DamageControl bossDamage)
    {
        if (!IsAttached || bossDamage == null)
            return;

        _bossDamageControl = bossDamage;
        View.SetBossHealthVisible(true);
    }

    private void RefreshAimLock(PlayerCharacter player)
    {
        InputControl input = InputControl.m_Main;
        if (input == null)
        {
            View.SetAimLockVisible(false);
            return;
        }

        if (input.m_MouseAim)
        {
            View.SetAimLockVisible(true);
            View.SetAimLockAnchoredPosition(View.MousePosToUI());
            return;
        }

        if (player.m_BestTargetObject != null)
        {
            View.SetAimLockVisible(true);
            View.SetAimLockAnchoredPosition(
                View.WorldPointToUI(player.m_BestTargetObject.m_TargetCenter.position));
            return;
        }

        View.SetAimLockVisible(false);
    }
}
