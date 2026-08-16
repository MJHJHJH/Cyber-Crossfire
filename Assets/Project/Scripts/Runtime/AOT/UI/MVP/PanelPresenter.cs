namespace UI.Mvp
{
    /// <summary>
    /// Panel Presenter 基类：Attach/Detach 对齐 UIFormLogic Open/Close。
    /// </summary>
    public abstract class PanelPresenter<TView> where TView : class, IPanelView
    {
        private TView _view;

        public bool IsAttached => _view != null;

        protected TView View => _view;

        public void Attach(TView view)
        {
            if (view == null)
                return;

            if (IsAttached)
                Detach();

            _view = view;
            OnAttach();
        }

        public void Detach()
        {
            if (!IsAttached)
                return;

            OnDetach();
            _view = null;
        }

        protected virtual void OnAttach()
        {
        }

        protected virtual void OnDetach()
        {
        }
    }
}
