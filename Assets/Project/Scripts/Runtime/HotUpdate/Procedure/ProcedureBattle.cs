using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 热更战斗流程：读取进战 location，经 ProcedureSceneSwitch 加载战斗场景。
    /// </summary>
    public sealed class ProcedureBattle : GameFramework.Procedure.ProcedureBase
    {
        private CancellationTokenSource _cts;
        private string _battleLocation;

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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureBattle] Switch battle scene failed: {ex.Message}");
            }
        }
    }
}
