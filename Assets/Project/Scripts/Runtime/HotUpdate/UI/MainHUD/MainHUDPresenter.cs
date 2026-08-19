using CommandoRobot.ScriptableObjects;
using GamePlay;
using UI.Mvp;
using UnityEngine;

public sealed class MainHUDPresenter : PanelPresenter<IMainHUDView>
{
    private const int WeaponSlotCount = 6;

    private readonly DataStorage _storage;
    private readonly GameplayData _gameplayData;

    public MainHUDPresenter(DataStorage storage, GameplayData gameplayData)
    {
        _storage = storage;
        _gameplayData = gameplayData;
    }

    protected override void OnAttach()
    {
        _storage.LoadData();
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
        if (!IsAttached || !IsValidWeaponIndex(index))
            return;

        if (_storage.m_WeaponsUnlocked[index])
        {
            _storage.m_SelectedWeapon = index;
            return;
        }

        int price = _storage.m_WeaponsPrice[index];
        if (price > _storage.Coin)
            return;

        _storage.Coin -= price;
        _storage.m_WeaponsUnlocked[index] = true;
        _storage.m_SelectedWeapon = index;
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
        View.SetCoinText(_storage.Coin);

        int count = WeaponSlotCount;
        if (_storage.m_WeaponsUnlocked == null || _storage.m_WeaponsPrice == null)
            return;

        if (_storage.m_WeaponsUnlocked.Length < count || _storage.m_WeaponsPrice.Length < count)
            count = Mathf.Min(_storage.m_WeaponsUnlocked.Length, _storage.m_WeaponsPrice.Length);

        for (int i = 0; i < count; i++)
        {
            View.SetWeaponSlot(
                i,
                i == _storage.m_SelectedWeapon,
                _storage.m_WeaponsUnlocked[i],
                _storage.m_WeaponsPrice[i]);
        }
    }

    private bool IsValidWeaponIndex(int index)
    {
        return index >= 0 &&
               _storage.m_WeaponsUnlocked != null &&
               _storage.m_WeaponsPrice != null &&
               index < _storage.m_WeaponsUnlocked.Length &&
               index < _storage.m_WeaponsPrice.Length;
    }
}
