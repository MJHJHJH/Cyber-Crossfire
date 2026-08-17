//------------------------------------------------------------
// ProcedureComponent — Refresh-aligned: type names + reflection
// Start wait uses UniTask (no coroutine)
//------------------------------------------------------------

using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using GameFramework.Procedure;
using System;
using System.Threading;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 流程组件：通过 Inspector 配置流程类型名，运行时反射创建并启动。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/Procedure")]
    public sealed class ProcedureComponent : GameFrameworkComponent
    {
        private IProcedureManager m_ProcedureManager = null;
        private ProcedureBase m_EntranceProcedure = null;
        private bool m_ProcedureInitialized = false;

        [SerializeField]
        private string[] m_AvailableProcedureTypeNames = null;

        [SerializeField]
        private string m_EntranceProcedureTypeName = null;

        /// <summary>
        /// 流程尚未 Initialize 时返回 null，避免 Inspector / 业务过早访问抛异常。
        /// </summary>
        public ProcedureBase CurrentProcedure =>
            m_ProcedureInitialized ? m_ProcedureManager.CurrentProcedure : null;

        public float CurrentProcedureTime =>
            m_ProcedureInitialized ? m_ProcedureManager.CurrentProcedureTime : 0f;

        protected override void Awake()
        {
            base.Awake();

            m_ProcedureManager = GameFrameworkEntry.GetModule<IProcedureManager>();
            if (m_ProcedureManager == null)
            {
                Debug.LogError("Procedure manager is invalid.");
            }
        }

        private void Start()
        {
            RunStartAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid RunStartAsync(CancellationToken cancellationToken)
        {
            if (m_AvailableProcedureTypeNames == null || m_AvailableProcedureTypeNames.Length == 0)
            {
                Debug.LogError("Available procedure type names is empty. Configure ProcedureComponent in Inspector.");
                return;
            }

            ProcedureBase[] procedures = new ProcedureBase[m_AvailableProcedureTypeNames.Length];
            for (int i = 0; i < m_AvailableProcedureTypeNames.Length; i++)
            {
                Type procedureType = Utility.Assembly.GetType(m_AvailableProcedureTypeNames[i]);
                if (procedureType == null)
                {
                    Debug.LogError($"Can not find procedure type '{m_AvailableProcedureTypeNames[i]}'.");
                    return;
                }

                procedures[i] = (ProcedureBase)Activator.CreateInstance(procedureType);
                if (procedures[i] == null)
                {
                    Debug.LogError($"Can not create procedure instance '{m_AvailableProcedureTypeNames[i]}'.");
                    return;
                }

                if (m_EntranceProcedureTypeName == m_AvailableProcedureTypeNames[i])
                {
                    m_EntranceProcedure = procedures[i];
                }
            }

            if (m_EntranceProcedure == null)
            {
                Debug.LogError("Entrance procedure is invalid.");
                return;
            }

            m_ProcedureManager.Initialize(GameFrameworkEntry.GetModule<IFsmManager>(), procedures);
            m_ProcedureInitialized = true;

            await UniTask.WaitForEndOfFrame(this, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            m_ProcedureManager.StartProcedure(m_EntranceProcedure.GetType());
        }

        public bool HasProcedure<T>() where T : ProcedureBase
        {
            return m_ProcedureManager.HasProcedure<T>();
        }

        public bool HasProcedure(Type procedureType)
        {
            return m_ProcedureManager.HasProcedure(procedureType);
        }

        public ProcedureBase GetProcedure<T>() where T : ProcedureBase
        {
            return m_ProcedureManager.GetProcedure<T>();
        }

        public ProcedureBase GetProcedure(Type procedureType)
        {
            return m_ProcedureManager.GetProcedure(procedureType);
        }

        /// <summary>
        /// 动态注册流程（创建后追加，用于热更程序集加载完成后的流程注册）。
        /// </summary>
        /// <param name="procedure">要注册的流程实例。</param>
        public void AddProcedure(ProcedureBase procedure)
        {
            m_ProcedureManager.AddProcedure(procedure);
        }

        /// <summary>
        /// 切换当前流程。
        /// </summary>
        /// <typeparam name="T">要切换到的流程类型。</typeparam>
        public void ChangeProcedure<T>() where T : ProcedureBase
        {
            m_ProcedureManager.ChangeProcedure<T>();
        }
    }
}
