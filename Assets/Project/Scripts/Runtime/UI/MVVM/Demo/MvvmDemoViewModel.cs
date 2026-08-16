using System;
using R3;

namespace UI.Mvvm.Demo
{
    /// <summary>
    /// MVVM 学习 Demo：从 Model 读数，暴露给 View 的可观察状态。
    /// </summary>
    public sealed class MvvmDemoViewModel : ViewModelBase
    {
        private readonly IMvvmDemoModel _model;
        private readonly ReactiveProperty<string> _title;
        private readonly ReactiveProperty<int> _count;
        private readonly ReactiveProperty<string> _status;

        public Observable<string> Title => _title;
        public Observable<int> Count => _count;
        public Observable<string> Status => _status;

        public MvvmDemoViewModel(IMvvmDemoModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));

            _title = CreateProperty("MVVM Demo");
            _count = CreateProperty(_model.Count);
            _status = CreateProperty("Ready");

            _model.Changed += OnModelChanged;
            AddDisposable(Disposable.Create(() => _model.Changed -= OnModelChanged));
        }

        public void Increment()
        {
            if (IsDisposed)
                return;

            _model.Increment();
            _status.Value = $"Count = {_model.Count}";
        }

        public void Reset()
        {
            if (IsDisposed)
                return;

            _model.Reset();
            _status.Value = "Reset";
        }

        private void OnModelChanged()
        {
            if (IsDisposed)
                return;

            _count.Value = _model.Count;
        }
    }
}
