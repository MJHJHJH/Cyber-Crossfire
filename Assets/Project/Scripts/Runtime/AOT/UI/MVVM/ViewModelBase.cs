using System;
using R3;

namespace UI.Mvvm
{
    /// <summary>
    /// ViewModel 基类：纯 C#，持有内部订阅袋；Dispose 时释放。
    /// </summary>
    public abstract class ViewModelBase : IDisposable
    {
        private DisposableBag _disposables;
        private bool _disposed;

        protected bool IsDisposed => _disposed;

        protected void AddDisposable(IDisposable disposable)
        {
            if (disposable == null || _disposed)
            {
                disposable?.Dispose();
                return;
            }

            disposable.AddTo(ref _disposables);
        }

        protected ReactiveProperty<T> CreateProperty<T>(T initialValue = default)
        {
            var property = new ReactiveProperty<T>(initialValue);
            AddDisposable(property);
            return property;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            OnDispose();
            _disposables.Dispose();
            _disposables = default;
        }

        protected virtual void OnDispose()
        {
        }
    }
}
