using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 热更战斗流程：读取进战 location，经 ProcedureSceneSwitch 加载战斗场景后打开战斗 HUD。
    /// </summary>
    public sealed class ProcedureBattle : GameFramework.Procedure.ProcedureBase
    {
        private const int BattleHudPanelId = 1002; // UIPanel：GameUI_PC

        private CancellationTokenSource _cts;
        private string _battleLocation;
        private IUIForm _battleHudForm;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            VarString enterArgs = procedureOwner.GetData<VarString>(ProcedureNavigator.BattleEnterKey);
            string location = enterArgs?.Value;
            procedureOwner.RemoveData(ProcedureNavigator.BattleEnterKey);

            if (string.IsNullOrEmpty(location))
            {
                Debug.LogError("[ProcedureBattle] Battle enter location is missing.");
                return;
            }

            _battleLocation = location;
            ProcedureNavigator.CurrentBattleLocation = location;

            _cts = new CancellationTokenSource();
            SwitchBattleSceneAsync(_cts.Token).Forget();
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            CloseBattleHud();
            // 局内捡币等只标脏，离开战斗统一落盘
            GamePlay.Data.PlayerSave.Save();
            base.OnLeave(procedureOwner, isShutdown);
        }

        private async UniTaskVoid SwitchBattleSceneAsync(CancellationToken cancellationToken)
        {
            string location = _battleLocation;
            try
            {
                await ProcedureSceneSwitch.SwitchAsync(
                    new[] { location },
                    location,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureBattle] Switch battle scene failed: {ex.Message}");
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
                Debug.LogError("[ProcedureBattle] UI component is missing.");
                return;
            }

            try
            {
                _battleHudForm = await GameFrameWork.UI.OpenAsync(BattleHudPanelId, null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureBattle] Open UI panel {BattleHudPanelId} failed: {ex.Message}");
            }
        }

        private void CloseBattleHud()
        {
            IUIForm form = _battleHudForm;
            _battleHudForm = null;
            if (form == null)
                return;

            GameFrameWork.UI?.Close(form);
        }
    }
}
