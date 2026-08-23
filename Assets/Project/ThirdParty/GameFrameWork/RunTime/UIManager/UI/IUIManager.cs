//------------------------------------------------------------
// Adapted from Game Framework UI — OpenAsync + IUIFormLoader
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;
using static GameFramework.UIConfig;

namespace GameFramework
{
    /// <summary>
    /// 界面管理器接口。
    /// </summary>
    public interface IUIManager
    {
        int UIGroupCount { get; }

        event EventHandler<CloseUIFormCompleteEventArgs> CloseUIFormComplete;

        void SetObjectPoolManager(IObjectPoolManager objectPoolManager, UIObjectPoolConfig uIObjectPoolConfig);

        void SetYooAssetPackage(ResourcePackage package);

        void SetUIFormPanelConfigProvider(IUIFormPanelConfigProvider provider);

        void SetUIFormHelper(IUIFormHelper uiFormHelper);

        bool TryGetPanelConfig(int panelId, out UIFormPanelConfig config);

        bool HasUIGroup(string uiGroupName);

        IUIGroup GetUIGroup(string uiGroupName);

        IUIGroup[] GetAllUIGroups();

        void GetAllUIGroups(List<IUIGroup> results);

        bool AddUIGroup(string uiGroupName, IUIGroupHelper uiGroupHelper);

        bool AddUIGroup(string uiGroupName, int depth, IUIGroupHelper uiGroupHelper);

        bool AddUIGroup(string uiGroupName, int depth, IUIGroupHelper uiGroupHelper, UIGroupOpenMode openMode);

        bool HasUIForm(int serialId);

        bool HasUIForm(string uiFormAssetName);

        IUIForm GetUIForm(int serialId);

        IUIForm GetUIForm(string uiFormAssetName);

        IUIForm[] GetUIForms(string uiFormAssetName);

        void GetUIForms(string uiFormAssetName, List<IUIForm> results);

        IUIForm[] GetAllLoadedUIForms();

        void GetAllLoadedUIForms(List<IUIForm> results);

        int[] GetAllLoadingUIFormSerialIds();

        void GetAllLoadingUIFormSerialIds(List<int> results);

        bool IsLoadingUIForm(int serialId);

        bool IsLoadingUIForm(string uiFormAssetName);

        bool IsValidUIForm(IUIForm uiForm);

        UniTask<IUIForm> OpenAsync(string location, string uiGroupName, bool pauseCoveredUIForm = false, object userData = null, CancellationToken cancellationToken = default);

        UniTask<IUIForm> OpenAsync(int panelId, object userData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 预载面板实例：加载资源并实例化后停放入 UI 实例池，等待后续 <see cref="OpenAsync(int, object, CancellationToken)"/> 复用。
        /// 预载期不执行任何 UIForm 生命周期（OnInit/OnOpen），首次打开时按新实例语义执行。
        /// </summary>
        /// <param name="panelId">面板配置 id。</param>
        /// <param name="keepAliveSeconds">存活秒数：&lt;= 0 永久保留（手动卸载）；&gt; 0 存活 N 秒未被打开则自动销毁。</param>
        /// <param name="cancellationToken">取消标记。</param>
        UniTask PreloadAsync(int panelId, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default);

        /// <summary>
        /// 按 location 预载面板实例（Resources loader，与 <see cref="OpenAsync(string, string, bool, object, CancellationToken)"/> 对齐）。
        /// </summary>
        /// <param name="location">界面资源名。</param>
        /// <param name="uiGroupName">所属界面组。</param>
        /// <param name="keepAliveSeconds">存活秒数：&lt;= 0 永久保留；&gt; 0 存活 N 秒未被打开则自动销毁。</param>
        /// <param name="cancellationToken">取消标记。</param>
        UniTask PreloadAsync(string location, string uiGroupName, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量预载面板实例（逐面板软失败：单个失败仅告警，不影响其余）。
        /// </summary>
        /// <param name="panelIds">面板配置 id 集合。</param>
        /// <param name="keepAliveSeconds">统一存活秒数：&lt;= 0 永久保留；&gt; 0 存活 N 秒未被打开则自动销毁。</param>
        /// <param name="cancellationToken">取消标记。</param>
        UniTask PreloadAsync(IEnumerable<int> panelIds, float keepAliveSeconds = 0f, CancellationToken cancellationToken = default);

        /// <summary>
        /// 卸载指定 location 的停放预载实例（Destroy 实例并释放资源句柄）。
        /// </summary>
        /// <param name="location">界面资源名。</param>
        /// <returns>是否确实卸载了停放实例。</returns>
        bool UnloadPreload(string location);

        /// <summary>
        /// 卸载全部停放预载实例；已打开（移出预载记录）的实例不受影响。
        /// </summary>
        void UnloadAllPreloads();

        /// <summary>
        /// 指定 location 是否已有停放（未打开、未过期、未卸载）的预载实例。
        /// </summary>
        bool IsPreloaded(string location);

        void CloseUIForm(int serialId);

        void CloseUIForm(int serialId, object userData);

        void CloseUIForm(IUIForm uiForm);

        void CloseUIForm(IUIForm uiForm, object userData);

        void CloseAllLoadedUIForms();

        void CloseAllLoadedUIForms(object userData);

        void CloseAllLoadingUIForms();

        void RefocusUIForm(IUIForm uiForm);

        void RefocusUIForm(IUIForm uiForm, object userData);

        void SetUIFormInstanceLocked(object uiFormInstance, bool locked);

        void SetUIFormInstancePriority(object uiFormInstance, int priority);
    }
}
