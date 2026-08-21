using CommandoRobot.ScriptableObjects;
using GamePlay;
using GamePlay.Data;
using UI.Mvp;
using UnityEngine;

public sealed class MainHUDPresenter : PanelPresenter<IMainHUDView>
{
    private readonly GameplayData _gameplayData;

    public MainHUDPresenter(GameplayData gameplayData)
    {
        _gameplayData = gameplayData;
    }

    protected override void OnAttach()
    {
        PlayerSave.EnsureLoaded();
        View.ShowMainPanel();
        Refresh();
    }

    public void Tick()
    {
        if (!IsAttached)
            return;

        Refresh();
    }

    public void OnPlayClick()
    {
        if (!IsAttached)
            return;

        View.ShowLevelPanel();
    }

    public void OnArmoryClick()
    {
        if (!IsAttached)
            return;

        View.ShowArmoryPanel();
    }

    public void OnBackClick()
    {
        if (!IsAttached)
            return;

        View.ShowMainPanel();
    }

    public void OnWeaponClick(int index)
    {
        if (!IsAttached || index < 0)
            return;

        if (PlayerSave.IsWeaponUnlocked(index))
        {
            PlayerSave.SelectWeapon(index);
            return;
        }

        PlayerSave.TryUnlockWeapon(index);
    }

    public void OnLevelClick(int index)
    {
        if (!IsAttached)
            return;

        _gameplayData.LevelNumber = index;
        if (!LevelSceneLocations.TryGet(index, out string location))
        {
            Debug.LogError($"[MainHUD] Invalid level index: {index}");
            return;
        }

        ProcedureNavigator.EnterBattle(location);
    }

    public void OnExitClick()
    {
        Application.Quit();
    }

    private void Refresh()
    {
        View.SetCoinText(PlayerSave.Coin);

        bool[] unlocked = PlayerSave.WeaponsUnlocked;
        if (unlocked == null)
            return;

        int count = unlocked.Length;
        int selected = PlayerSave.SelectedWeapon;
        for (int i = 0; i < count; i++)
        {
            View.SetWeaponSlot(
                i,
                i == selected,
                unlocked[i],
                PlayerSave.GetWeaponPrice(i));
        }
    }
}
