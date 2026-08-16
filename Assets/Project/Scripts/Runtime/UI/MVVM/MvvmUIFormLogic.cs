using System;
using GameFramework;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI.Mvvm
{
    /// <summary>
    /// MVVM 面板基类：Open Resolve+Bind；Close Unbind+Dispose VM（策略 A）；Recycle 兜底。
    /// Logic 绑定袋与 ViewModel 内部袋分离。
    /// </summary>
    public abstract class MvvmUIFormLogic<TViewModel> : UIFormLogic
        where TViewModel : class, IDisposable
    {
        private TViewModel _viewModel;
        private DisposableBag _bindings;
        private bool _bound;

        protected TViewModel ViewModel => _viewModel;

        protected bool IsBound => _bound;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            TearDownViewModel(logIfUnexpected: false);

            if (!TryResolveViewModel(out _viewModel) || _viewModel == null)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] Resolve {typeof(TViewModel).Name} failed; skip Bind.");
                _viewModel = null;
                return;
            }

            OnBind(_viewModel);
            _bound = true;
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            TearDownViewModel(logIfUnexpected: false);
            base.OnClose(isShutdown, userData);
        }

        protected override void OnRecycle()
        {
            if (_viewModel != null || _bound)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] ViewModel still held on Recycle; disposing as safety net.");
                TearDownViewModel(logIfUnexpected: false);
            }

            base.OnRecycle();
        }

        /// <summary>子类在此手写 Subscribe / R3.Unity 扩展，并通过 <see cref="AddBinding"/> 收纳。</summary>
        protected abstract void OnBind(TViewModel viewModel);

        /// <summary>默认无操作；绑定清理由基类 Dispose 绑定袋完成。</summary>
        protected virtual void OnUnbind()
        {
        }

        protected void AddBinding(IDisposable disposable)
        {
            if (disposable == null)
                return;

            disposable.AddTo(ref _bindings);
        }

        /// <summary>默认从 <see cref="UiLifetimeScope"/> Resolve；可 override。</summary>
        protected virtual bool TryResolveViewModel(out TViewModel viewModel)
        {
            viewModel = null;

            var scope = LifetimeScope.Find<UiLifetimeScope>();
            if (scope?.Container == null)
                return false;

            try
            {
                viewModel = scope.Container.Resolve<TViewModel>();
                return viewModel != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{GetType().Name}] Resolve {typeof(TViewModel).Name} exception: {e.Message}");
                viewModel = null;
                return false;
            }
        }

        private void TearDownViewModel(bool logIfUnexpected)
        {
            if (_bound)
            {
                try
                {
                    OnUnbind();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{GetType().Name}] OnUnbind exception: {e.Message}");
                }

                _bound = false;
            }

            _bindings.Dispose();
            _bindings = default;

            if (_viewModel != null)
            {
                if (logIfUnexpected)
                    Debug.LogWarning($"[{GetType().Name}] Disposing leftover ViewModel.");

                try
                {
                    _viewModel.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{GetType().Name}] ViewModel.Dispose exception: {e.Message}");
                }

                _viewModel = null;
            }
        }
    }
}
