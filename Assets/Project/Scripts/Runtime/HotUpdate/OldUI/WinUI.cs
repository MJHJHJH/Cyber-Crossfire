using CommandoRobot.ScriptableObjects;
using GamePlay;
using UnityEngine;

namespace CommandoRobot
{
    public class WinUI : MonoBehaviour
    {
        [SerializeField, Space]
        private GameplayData m_GameplayData;
        public DataStorage m_Storage;

        public void Continue()
        {
            m_Storage.Coin += 50;
            m_Storage.SaveData();
            ProcedureNavigator.EnterMainMenu();
        }
    }
}