using UI.Mvp;

public interface IMainHUDView : IPanelView
{
    void ShowMainPanel();

    void ShowLevelPanel();

    void SetCoinText(int coin);
}
