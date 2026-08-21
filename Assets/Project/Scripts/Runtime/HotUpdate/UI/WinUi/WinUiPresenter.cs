using GamePlay;
using GamePlay.Data;
using UI.Mvp;

public sealed class WinUiPresenter : PanelPresenter<IWinUiView>
{
    public void OnContinueClick()
    {
        if (!IsAttached)
            return;

        PlayerSave.AddCoin(50);

        View.RequestClose();
        ProcedureNavigator.EnterMainMenu();
    }
}
