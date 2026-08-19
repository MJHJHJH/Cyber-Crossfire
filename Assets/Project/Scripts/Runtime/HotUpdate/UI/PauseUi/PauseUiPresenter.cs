using CommandoRobot;
using UI.Mvp;

public sealed class PauseUiPresenter : PanelPresenter<IPauseUiView>
{
    public void OnContinueClick()
    {
        if (!IsAttached)
            return;

        GameControl.m_Current?.ResumeGame();
    }

    public void OnExitClick()
    {
        if (!IsAttached)
            return;

        View.RequestClose();
        GameControl.m_Current?.ExitGame();
    }
}
