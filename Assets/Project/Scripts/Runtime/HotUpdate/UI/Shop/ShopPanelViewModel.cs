using System;
using GamePlay.Data;
using R3;
using UI.Mvvm;

public readonly struct WeaponSlotUiState
{
    public WeaponSlotUiState(bool selected, bool unlocked, int price)
    {
        Selected = selected;
        Unlocked = unlocked;
        Price = price;
    }

    public bool Selected { get; }

    public bool Unlocked { get; }

    public int Price { get; }
}

public sealed class ShopPanelViewModel : ViewModelBase
{
    private readonly ReactiveProperty<WeaponSlotUiState[]> _weaponSlots;
    private readonly Subject<Unit> _closeRequested;

    public Observable<WeaponSlotUiState[]> WeaponSlots => _weaponSlots;

    public Observable<Unit> CloseRequested => _closeRequested;

    public ShopPanelViewModel()
    {
        _weaponSlots = CreateProperty(Array.Empty<WeaponSlotUiState>());
        _closeRequested = new Subject<Unit>();
        AddDisposable(_closeRequested);
        AddDisposable(PlayerSave.Changed.Subscribe(_ => SyncFromSave()));
    }

    public void Initialize()
    {
        PlayerSave.EnsureLoaded();
        SyncFromSave();
    }

    public void SelectWeapon(int index)
    {
        if (IsDisposed || index < 0)
            return;

        if (PlayerSave.IsWeaponUnlocked(index))
            PlayerSave.SelectWeapon(index);
        else
            PlayerSave.TryUnlockWeapon(index);
    }

    public void RequestClose()
    {
        if (IsDisposed)
            return;

        _closeRequested.OnNext(Unit.Default);
    }

    private void SyncFromSave()
    {
        bool[] unlocked = PlayerSave.WeaponsUnlocked;
        if (unlocked == null || unlocked.Length == 0)
        {
            _weaponSlots.Value = Array.Empty<WeaponSlotUiState>();
            return;
        }

        int selected = PlayerSave.SelectedWeapon;
        var slots = new WeaponSlotUiState[unlocked.Length];
        for (int i = 0; i < unlocked.Length; i++)
        {
            slots[i] = new WeaponSlotUiState(
                i == selected,
                unlocked[i],
                PlayerSave.GetWeaponPrice(i));
        }

        _weaponSlots.Value = slots;
    }
}
