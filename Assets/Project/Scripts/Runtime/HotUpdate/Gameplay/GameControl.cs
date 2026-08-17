using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CommandoRobot.ScriptableObjects;
using UnityEngine.UI;

namespace CommandoRobot
{
    public class GameControl : MonoBehaviour
    {
        public static GameControl m_Current;

        public LevelBase m_Level;

        public DataStorage m_DataStorage;
        public GameplayData m_GameplayData;
        public Contents m_Contents;

        public GameObject m_DeathEff;
        [HideInInspector]
        public bool m_Pausesd = false;

        [HideInInspector]
        public int m_TotalEnemyCount = 0;
        [HideInInspector]
        public int m_EnemyKilledCount = 0;

        public const int State_Start = 0;
        public const int State_Gameplay = 1;
        public const int State_Lose = 2;
        public const int State_Win = 3;

        public GameObject m_Boss;
        [HideInInspector]
        public bool m_IsBossFight = false;

        [HideInInspector]
        public int m_GameState = 0;
        void Awake()
        {
            m_Current = this;
            m_TotalEnemyCount = 0;
            m_EnemyKilledCount = 0;
        }
        // Start is called before the first frame update
        void Start()
        {
            int levelNum = m_GameplayData.LevelNumber;

            //if (m_Contents.m_LevelPrefabs[levelNum] != null)
            //{
            //    GameObject levelObj = Instantiate(m_Contents.m_LevelPrefabs[levelNum]);
            //    m_Level = levelObj.GetComponent<LevelBase>();
            //}

            StartCoroutine(Co_Start());
            m_GameState = State_Start;

            if (InputControl.m_Main.m_MobileControl)
                UISystem.ShowUI("JoystickUI");
        }

        IEnumerator Co_Start()
        {
            if (m_Level != null)
            {
                PlayerControl.m_Main.Respawn();
            }
            m_GameState = State_Start;
            yield return new WaitForSeconds(1f);
            m_GameState = State_Gameplay;
        }

        // Update is called once per frame
        void Update()
        {
            switch (m_GameState)
            {
                case State_Start:
                    break;
                case State_Gameplay:
                    if (m_IsBossFight && m_Boss == null)
                    {
                        m_IsBossFight = false;
                        EndLevel(4);
                    }
                    //if (Time.time > 1)
                    //{
                    //    if (m_EnemyKilledCount >= m_TotalEnemyCount)
                    //    {
                    //        HandleWin();
                    //    }
                    //}
                    break;
            }


            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!m_Pausesd)
                {
                    PauseGame();
                }
                else
                {
                    ResumeGame();
                }
            }
        }

        void FixedUpdate()
        {



        }

        public void HandlePlayerDeath()
        {
            StartCoroutine(Co_HandleGameOver());
        }

        IEnumerator Co_HandleGameOver()
        {
            //m_DeathEff.SetActive(true);
            m_GameState = State_Lose;
            CameraControl.m_Current.StartShake(.4f, .3f);
            yield return new WaitForSeconds(2);
            UISystem.ShowUI("lose-ui");
            //FadeControl.m_Current.StartFadeOut();
            //yield return new WaitForSeconds(2);
            //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void HandleWin()
        {
            m_GameState = State_Win;
            UISystem.ShowUI("win-ui");
        }
        public void PauseGame()
        {
            m_Pausesd = true;
            Time.timeScale = 0;
            UISystem.ShowUI("pause-ui");
            UI_HUD.m_Main.gameObject.SetActive(false);
        }
        public void ResumeGame()
        {
            m_Pausesd = false;
            Time.timeScale = 1;
            UISystem.RemoveUI("pause-ui");
            UI_HUD.m_Main.gameObject.SetActive(true);
        }
        public void ExitGame()
        {
            m_Pausesd = false;
            Time.timeScale = 1;
            SceneManager.LoadScene("MainMenu");
        }

        public void EndLevel(int waitTime = 0)
        {
            if (m_GameState != State_Gameplay)
                return;

            StartCoroutine(Co_EndLevel(waitTime));
        }

        IEnumerator Co_EndLevel(int waitTime = 0)
        {
            m_GameState = State_Win;
            ShowMessage("Mission Complete");
            yield return new WaitForSeconds(1);
            yield return new WaitForSeconds(waitTime);
            //FadeControl.m_Current.StartFadeOut();
            yield return new WaitForSeconds(2);
            UISystem.ShowUI("win-ui");
        }

        public void StartBossFight()
        {
            if (m_Boss != null)
            {
                m_IsBossFight = true;
                m_Boss.GetComponent<AIControlBase>().StartAlert();
                UI_HUD.m_Main.ShowBossHealth(m_Boss.GetComponent<DamageControl>());
            }
        }

        public void ShowMessage(string message)
        {
            StartCoroutine(CO_ShowMessage(message));
        }

        IEnumerator CO_ShowMessage(string message)
        {
            GameObject msgObj = UISystem.ShowUI("message-1");
            if (msgObj != null)
            {
                Text text = msgObj.GetComponentInChildren<Text>();
                text.text = message;
            }

            yield return new WaitForSeconds(2);

            UISystem.RemoveUI(msgObj);
        }
    }
}
