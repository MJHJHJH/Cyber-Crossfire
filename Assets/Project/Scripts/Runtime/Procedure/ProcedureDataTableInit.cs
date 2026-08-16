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
    /// 配表初始化：注入 Factory 并 LoadAsync，成功后进入 Main。不负责资源包就绪。
    /// </summary>
    public sealed class ProcedureDataTableInit : ProcedureBase
    {
        private CancellationTokenSource m_Cts;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_Cts = new CancellationTokenSource();
            EnterAsync(procedureOwner, m_Cts.Token).Forget();
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            if (m_Cts != null)
            {
                m_Cts.Cancel();
                m_Cts.Dispose();
                m_Cts = null;
            }

            base.OnLeave(procedureOwner, isShutdown);
        }

        private async UniTaskVoid EnterAsync(ProcedureOwner procedureOwner, CancellationToken cancellationToken)
        {
            if (GameFrameWork.DataTable == null)
            {
                Debug.LogError(
                    "[ProcedureDataTableInit] LubanConfigComponent is missing. Add it via Game Framework/Data Table.");
                return;
            }

            GameFrameWork.DataTable.SetTablesFactory(new LubanTablesFactory());

            try
            {
                await GameFrameWork.DataTable.LoadAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureDataTableInit] LoadAsync failed: {ex.Message}");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            // SoundGroup 表就绪，初始化声音组与大类 mixer 路由（Start 时表未加载，此处补建）
            GameFrameWork.Sound?.InitSoundGroupsFromTable();

            ChangeState<ProcedureMain>(procedureOwner);
        }
    }
}
