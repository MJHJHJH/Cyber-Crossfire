using GamePlay;
using GamePlay.Data;
using UnityEngine;

namespace CommandoRobot
{
    public class WinUI : MonoBehaviour
    {
        public void Continue()
        {
            PlayerSave.AddCoin(50);
            ProcedureNavigator.EnterMainMenu();
        }
    }
}
