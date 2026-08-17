using CommandoRobot.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CommandoRobot
{
    public class WinUI : MonoBehaviour
    {
        [SerializeField, Space]
        private GameplayData m_GameplayData;
        public DataStorage m_Storage;
        void Start()
        {

        }

        void Update()
        {
        }

        public void Continue()
        {
            m_Storage.Coin += 50;
            m_Storage.SaveData();
            SceneManager.LoadScene("MainMenu");
        }
    }

}