using UI.Mvp;

public interface IMainHUDView : IPanelView
{
    void ShowMainPanel();

    void ShowLevelPanel();

    void ShowArmoryPanel();

    void SetCoinText(int coin);

    void SetWeaponSlot(int index, bool selected, bool unlocked, int price);
}
