using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Procedure;
using GameFramework.Sound;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 启动流程：注册 UI Provider，经 Resources 打开资源初始化界面（不读配表），再进入 AssetInit。
    /// </summary>
    public sealed class ProcedureLaunch : ProcedureBase
    {
        /// <summary>Resources 路径：Assets/Resources/YooAssetInitPanel.prefab</summary>
        public const string YooAssetInitLocation = "YooAssetInitPanel";

        private const string DefaultGroupName = "Default";

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
            if (GameFrameWork.UI == null)
            {
                Debug.LogError("[ProcedureLaunch] UI component is missing.");
                return;
            }
            // Provider 仅供后续读表 Open；本流程资源界面不走配表
            GameFrameWork.UI.SetUIFormPanelConfigProvider(new UIPanelConfigProvider());

            // 音效配表 Provider：按 ID 播放走 Luban Sound 表；声音组 / mixer 路由走 Luban SoundGroup 表
            if (GameFrameWork.Sound != null)
            {
                GameFrameWork.Sound.SetSoundConfigTable(new SoundConfigProvider());
                GameFrameWork.Sound.SetSoundGroupConfigTable(new SoundGroupConfigProvider());
            }

            // 已存在则 AddGroup 返回 false，可忽略
            GameFrameWork.UI.AddGroup(DefaultGroupName, 0);

            // location OpenAsync 固定走 ResourcesLoader，不依赖配表 / Yoo
            await GameFrameWork.UI.OpenAsync("YooAssetInitPanel", "Default", false, null, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            ChangeState<ProcedureAssetInit>(procedureOwner);
        }
    }
}
