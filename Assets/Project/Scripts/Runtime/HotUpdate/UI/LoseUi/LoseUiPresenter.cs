using GamePlay;
using UI.Mvp;

public sealed class LoseUiPresenter : PanelPresenter<ILoseUiView>
{
    public void OnRestartClick()
    {
        if (!IsAttached)
            return;

        View.RequestClose();
        ProcedureNavigator.RestartBattle();
    }

    public void OnExitClick()
    {
        if (!IsAttached)
            return;

        View.RequestClose();
        ProcedureNavigator.EnterMainMenu();
    }
}
