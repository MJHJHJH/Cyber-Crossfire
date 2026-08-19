using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 热更主流程：由 AOT 侧动态注册进 Procedure Fsm，OnEnter 时加载场景组。
    /// </summary>
    public sealed class ProcedureMain : GameFramework.Procedure.ProcedureBase
    {
        private static readonly string[] SceneGroup = { "MainMenu" };
        private const string ActiveSceneLocation = "MainMenu";
        private const int MainPanelId = 1001; // 临时写死ID，后续考虑统一放进枚举

        private CancellationTokenSource _cts;
        private IUIForm _mainForm;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            GameDiInstaller.EnsureInitialized();

            _cts = new CancellationTokenSource();
            SwitchSceneGroupAsync(_cts.Token).Forget();
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            CloseMainForm();
            base.OnLeave(procedureOwner, isShutdown);
        }

        private async UniTaskVoid SwitchSceneGroupAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ProcedureSceneSwitch.SwitchAsync(SceneGroup, ActiveSceneLocation, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureMain] Switch scene group failed: {ex.Message}");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            await OnSceneReadyAsync(cancellationToken);
        }

        private async UniTask OnSceneReadyAsync(CancellationToken cancellationToken)
        {
            if (GameFrameWork.UI == null)
            {
                Debug.LogError("[ProcedureMain] UI component is missing.");
                return;
            }

            try
            {
                _mainForm = await GameFrameWork.UI.OpenAsync(MainPanelId, null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureMain] Open UI panel {MainPanelId} failed: {ex.Message}");
            }
        }

        private void CloseMainForm()
        {
            IUIForm form = _mainForm;
            _mainForm = null;
            if (form == null)
                return;

            GameFrameWork.UI?.Close(form);
        }
    }
}
