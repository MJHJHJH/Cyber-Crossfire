using GameFramework;

namespace GamePlay
{
    /// <summary>
    /// 基于 <see cref="IUIPanel"/> 的界面打开配置 Provider（按表 id 查询）。
    /// </summary>
    public sealed class UIPanelConfigProvider : IUIFormPanelConfigProvider
    {
        public bool TryGetById(int panelId, out UIFormPanelConfig config)
        {
            if (GameFrameWork.DataTable == null ||
                !GameFrameWork.DataTable.TryGetTable(out IUIPanel table) ||
                !table.TryGet(panelId, out UIPanel_Record record))
            {
                config = default;
                return false;
            }

            config = ToConfig(record);
            return true;
        }

        private static UIFormPanelConfig ToConfig(UIPanel_Record record)
        {
            UIFormLoaderKind loaderKind = record.Loader == (int)UIFormLoaderKind.YooAsset
                ? UIFormLoaderKind.YooAsset
                : UIFormLoaderKind.Resources;

            return new UIFormPanelConfig(
                record.Id,
                record.Location,
                record.GroupName,
                record.PauseCoveredUIForm,
                record.CanvasMode,
                record.SortOrder,
                loaderKind);
        }
    }
}
