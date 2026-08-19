using UI.Mvp;

public interface IGemRewardUIView : IPanelView
{
    void SetAmount(int amount);

    void RequestClose();
}
