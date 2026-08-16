namespace GameFramework
{
    /// <summary>
    /// 界面资源加载方式（与 UIPanel.loader 整型对齐）。
    /// </summary>
    public enum UIFormLoaderKind
    {
        YooAsset = 0,
        Resources = 1
    }

    /// <summary>
    /// 打开界面所需的配表配置（框架 DTO，不引用 cfg.*）。
    /// </summary>
    public readonly struct UIFormPanelConfig
    {
        public UIFormPanelConfig(
            int id,
            string location,
            string groupName,
            bool pauseCoveredUIForm,
            int canvasMode,
            int sortOrder,
            UIFormLoaderKind loaderKind)
        {
            Id = id;
            Location = location;
            GroupName = groupName;
            PauseCoveredUIForm = pauseCoveredUIForm;
            CanvasMode = canvasMode;
            SortOrder = sortOrder;
            LoaderKind = loaderKind;
        }

        public int Id { get; }
        public string Location { get; }
        public string GroupName { get; }
        public bool PauseCoveredUIForm { get; }
        public int CanvasMode { get; }
        /// <summary>界面 Canvas.sortingOrder。</summary>
        public int SortOrder { get; }
        public UIFormLoaderKind LoaderKind { get; }
    }

    /// <summary>
    /// 按表 id 提供界面打开配置。
    /// </summary>
    public interface IUIFormPanelConfigProvider
    {
        bool TryGetById(int panelId, out UIFormPanelConfig config);
    }
}
