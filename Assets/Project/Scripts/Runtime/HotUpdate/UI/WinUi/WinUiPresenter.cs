using CommandoRobot.ScriptableObjects;
using GamePlay;
using UI.Mvp;

public sealed class WinUiPresenter : PanelPresenter<IWinUiView>
{
    private readonly DataStorage _storage;

    public WinUiPresenter(DataStorage storage)
    {
        _storage = storage;
    }

    public void OnContinueClick()
    {
        if (!IsAttached)
            return;

        _storage.Coin += 50;
        _storage.SaveData();
        View.RequestClose();
        ProcedureNavigator.EnterMainMenu();
    }
}
