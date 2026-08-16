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
