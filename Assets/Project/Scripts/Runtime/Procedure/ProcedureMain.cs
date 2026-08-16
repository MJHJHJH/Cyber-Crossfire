using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Procedure;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 主流程：进入时加载场景组 AScene + BScene（激活 BScene）。
    /// </summary>
    public sealed class ProcedureMain : ProcedureBase
    {
        private static readonly string[] SceneGroup = { "AScene", "BScene" };
        private const string ActiveSceneLocation = "BScene";

        private CancellationTokenSource _cts;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _cts = new CancellationTokenSource();
            ProcedureSceneSwitch.SwitchEnd += SwitchSceneEnd;
            EnterAsync(_cts.Token).Forget();
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

        private async UniTaskVoid EnterAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ProcedureSceneSwitch.SwitchAsync(SceneGroup, ActiveSceneLocation, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError(Utility.Text.Format("[ProcedureMain] Switch scene group failed: {0}", ex.Message));
            }
        }

        private void SwitchSceneEnd()
        {
            GameFrameWork.UI.OpenAsync(1001);
        }
    }
}
