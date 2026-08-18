using GamePlay;
using UnityEngine;

namespace CommandoRobot
{
    public class LoseUI : MonoBehaviour
    {
        public void BtnRestart()
        {
            ProcedureNavigator.RestartBattle();
        }

        public void BtnExit()
        {
            ProcedureNavigator.EnterMainMenu();
        }
    }
}