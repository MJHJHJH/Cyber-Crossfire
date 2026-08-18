using GameFramework;
using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 热更侧流程导航：UI / 玩法在无 procedureOwner 时切换菜单与战斗。
    /// </summary>
    public static class ProcedureNavigator
    {
        public const string BattleEnterKey = "BattleEnter";

        /// <summary>当前战斗场景 location（进战后由 ProcedureBattle 写入，供重开）。</summary>
        public static string CurrentBattleLocation { get; internal set; }

        public static void EnterBattle(string sceneLocation)
        {
            if (string.IsNullOrEmpty(sceneLocation))
            {
                Debug.LogError("[ProcedureNavigator] Battle scene location is invalid.");
                return;
            }

            if (ProcedureSceneSwitch.IsSwitching)
            {
                Debug.LogWarning("[ProcedureNavigator] Scene switch already in progress, ignore EnterBattle.");
                return;
            }

            if (GameFrameWork.Fsm == null || GameFrameWork.Procedure == null)
            {
                Debug.LogError("[ProcedureNavigator] Fsm or Procedure component is missing.");
                return;
            }

            IFsm<IProcedureManager> fsm = GameFrameWork.Fsm.GetFsm<IProcedureManager>();
            if (fsm == null)
            {
                Debug.LogError("[ProcedureNavigator] Procedure FSM is missing.");
                return;
            }

            fsm.SetData(BattleEnterKey, VarString.Create(sceneLocation));
            GameFrameWork.Procedure.ChangeProcedure<ProcedureBattle>();
        }

        public static void EnterMainMenu()
        {
            Time.timeScale = 1f;

            if (ProcedureSceneSwitch.IsSwitching)
            {
                Debug.LogWarning("[ProcedureNavigator] Scene switch already in progress, ignore EnterMainMenu.");
                return;
            }

            if (GameFrameWork.Procedure == null)
            {
                Debug.LogError("[ProcedureNavigator] Procedure component is missing.");
                return;
            }

            GameFrameWork.Procedure.ChangeProcedure<ProcedureMain>();
        }

        public static void RestartBattle()
        {
            if (string.IsNullOrEmpty(CurrentBattleLocation))
            {
                Debug.LogError("[ProcedureNavigator] Current battle location is empty, cannot restart.");
                return;
            }

            EnterBattle(CurrentBattleLocation);
        }
    }
}
