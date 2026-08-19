using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UI.Mvp;

public sealed class Message1Presenter : PanelPresenter<IMessage1View>
{
    private const int AutoCloseMs = 2000;

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

    public void ShowMessage(string message)
    {
        if (!IsAttached || string.IsNullOrEmpty(message))
            return;

        View.SetMessage(message);
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
