using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GamePlay.Data;
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

            // 主菜单购买/选武器等先刷盘，再进战
            PlayerSave.Save();
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

            // 离开战斗时 ProcedureBattle.OnLeave 会 Save，此处只需切流程
            GameFrameWork.Procedure.ChangeProcedure<ProcedureMain>();
        }

        public static void RestartBattle()
        {
            if (string.IsNullOrEmpty(CurrentBattleLocation))
            {
                Debug.LogError("[ProcedureNavigator] Current battle location is empty, cannot restart.");
                return;
            }

            if (ProcedureSceneSwitch.IsSwitching)
            {
                Debug.LogWarning("[ProcedureNavigator] Scene switch already in progress, ignore RestartBattle.");
                return;
            }

            RestartBattleAsync().Forget();
        }

        /// <summary>
        /// 重开：先 YooAsset 卸当前战斗场景，再 EnterBattle 走完整 Load（避免 duplicate Load / IsSameGroup 短路）。
        /// </summary>
        private static async UniTaskVoid RestartBattleAsync()
        {
            string location = CurrentBattleLocation;
            Time.timeScale = 1f;

            if (GameFrameWork.Scene == null)
            {
                Debug.LogError("[ProcedureNavigator] Scene component is missing.");
                return;
            }

            if (GameFrameWork.Scene.SceneIsLoaded(location))
            {
                try
                {
                    await GameFrameWork.Scene.UnloadSceneAsync(location);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ProcedureNavigator] Unload battle scene '{location}' failed: {ex.Message}");
                    return;
                }
            }

            EnterBattle(location);
        }
    }
}
