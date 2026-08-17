using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        private CancellationTokenSource _cts;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            _cts = new CancellationTokenSource();
            SwitchSceneGroupAsync(procedureOwner, _cts.Token).Forget();
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

        private async UniTaskVoid SwitchSceneGroupAsync(ProcedureOwner procedureOwner, CancellationToken cancellationToken)
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
                Debug.LogError($"[ProcedureMain] Switch scene group failed: {ex.Message}");
            }
        }
    }
}
