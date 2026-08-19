using UI.Mvp;

public interface IMessage1View : IPanelView
{
    void SetMessage(string message);

    void RequestClose();
}
