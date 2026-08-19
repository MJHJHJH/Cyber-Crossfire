using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UI.Mvp;

public sealed class GemRewardUIPresenter : PanelPresenter<IGemRewardUIView>
{
    private const int AutoCloseMs = 3000;

    private CancellationTokenSource _cts;

    protected override void OnAttach()
    {
        _cts = new CancellationTokenSource();
        AutoCloseAsync(_cts.Token).Forget();
    }

    protected override void OnDetach()
    {
        if (_cts == null)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    public void ShowAmount(int amount)
    {
        if (!IsAttached)
            return;

        View.SetAmount(amount);
    }

    public void OnOkClick()
    {
        if (!IsAttached)
            return;

        View.RequestClose();
    }

    private async UniTaskVoid AutoCloseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Delay(AutoCloseMs, cancellationToken: cancellationToken);
            if (IsAttached)
                View.RequestClose();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
