using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GamePlay.AssetPatch;
using UnityEngine;
using YooAsset;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 资源初始化流程：按 Catalog 串行补丁各 Package，完成后注入主包 / UI Package。
    /// </summary>
    public sealed class ProcedureAssetInit : ProcedureBase
    {
        private static readonly List<string> s_PackageQueue = new List<string>(4);

        private AssetPatchContext m_Context;
        private IFsm<AssetPatchContext> m_PatchFsm;
        private ProcedureOwner m_ProcedureOwner;
        private bool m_Completed;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_ProcedureOwner = procedureOwner;
            m_Completed = false;

            if (GameFrameWork.Base == null)
            {
                Debug.LogError("[ProcedureAssetInit] BaseComponent is missing.");
                return;
            }

            if (GameFrameWork.Event == null)
            {
                Debug.LogError("[ProcedureAssetInit] EventComponent is missing.");
                return;
            }

            if (GameFrameWork.Fsm == null)
            {
                Debug.LogError("[ProcedureAssetInit] FsmComponent is missing.");
                return;
            }

            GameFrameWork.Base.ResolveYooPackageQueue(s_PackageQueue, out string defaultPackageName);
            // Debug.Log(
            //     $"[ProcedureAssetInit] Package queue ({s_PackageQueue.Count}), default={defaultPackageName}: " +
            //     string.Join(", ", s_PackageQueue));

            m_Context = new AssetPatchContext(
                s_PackageQueue,
                defaultPackageName,
                GameFrameWork.Base.YooPlayMode,
                GameFrameWork.Base.HostServerIP,
                GameFrameWork.Base.FallbackHostServerIP,
                GameFrameWork.Base.HostAppVersion,
                GameFrameWork.Event,
                OnPatchSucceeded);

            m_Context.SubscribeUserEvents();

            m_PatchFsm = GameFrameWork.Fsm.CreateFsm(
                AssetPatchContext.FsmName,
                m_Context,
                new InitializePackageState(),
                new RequestPackageVersionState(),
                new UpdatePackageManifestState(),
                new CreateDownloaderState(),
                new ConfirmDownloadState(),
                new DownloadPackageFilesState(),
                new ClearCacheBundleState(),
                new PatchDoneState());

            m_Context.BindFsm(m_PatchFsm);
            m_PatchFsm.Start<InitializePackageState>();
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            TearDownPatchFsm();
            m_ProcedureOwner = null;
            base.OnLeave(procedureOwner, isShutdown);
        }

        private void OnPatchSucceeded()
        {
            if (m_Completed || m_Context == null)
                return;

            m_Completed = true;

            ResourcePackage package = YooAssets.GetPackage(m_Context.DefaultPackageName);

            GameFrameWork.UI?.SetYooAssetPackage(package);
            GameFrameWork.Scene?.SetYooAssetPackage(package);
            GameFrameWork.Sound?.SetYooAssetPackage(package);

            // 盖屏期预热着色器变体：把首进场景的编译成本前移到启动阶段
            WarmupAndContinueAsync(package).Forget();
        }

        private static void CloseYooAssetInitPanel()
        {
            IUIManager uiManager = GameFrameworkEntry.GetModule<IUIManager>();
            IUIForm form = uiManager?.GetUIForm(ProcedureLaunch.YooAssetInitLocation);
            if (form == null)
                return;

            if (GameFrameWork.UI != null)
                GameFrameWork.UI.Close(form);
            else
                uiManager.CloseUIForm(form);
        }

        private async UniTaskVoid WarmupAndContinueAsync(ResourcePackage package)
        {
            // 先预热再关面板：面板保持盖屏，预热耗时（含首次驱动编译）对玩家不可见
            await ShaderVariantWarmup.WarmupAsync(package);
            if (m_ProcedureOwner == null)
                return;

            CloseYooAssetInitPanel();
            await UniTask.Yield();
            if (m_ProcedureOwner == null)
                return;

            ChangeState<ProcedureHotUpdateInit>(m_ProcedureOwner);
        }

        private void TearDownPatchFsm()
        {
            if (m_Context != null)
            {
                m_Context.UnsubscribeUserEvents();
                m_Context.Cancel();
                m_Context = null;
            }

            if (m_PatchFsm != null)
            {
                GameFrameWork.Fsm?.DestroyFsm(m_PatchFsm);
                m_PatchFsm = null;
            }
        }
    }
}
