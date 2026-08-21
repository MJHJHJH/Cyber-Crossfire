using System;
using System.Collections;
using CommandoRobot.ScriptableObjects;
using Cysharp.Threading.Tasks;
using GameFramework;
using GamePlay;
using UnityEngine;

namespace CommandoRobot
{
    public class GameControl : MonoBehaviour
    {
        private const int PausePanelId = 1005;
        private const int LosePanelId = 1006;
        private const int WinPanelId = 1007;
        private const int MessagePanelId = 1009;

        public static GameControl m_Current;

        public LevelBase m_Level;

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

        private IUIForm _pauseForm;

        void Awake()
        {
            m_Current = this;
            m_TotalEnemyCount = 0;
            m_EnemyKilledCount = 0;
        }

        void OnDestroy()
        {
            if (m_Current == this)
                m_Current = null;
        }

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
        }

        IEnumerator Co_Start()
        {
            if (m_Level != null && PlayerControl.m_Main != null)
                PlayerControl.m_Main.Respawn();

            m_GameState = State_Start;
            yield return new WaitForSeconds(1f);
            m_GameState = State_Gameplay;
        }

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
            OpenPanelAsync(LosePanelId).Forget();
            //FadeControl.m_Current.StartFadeOut();
            //yield return new WaitForSeconds(2);
            //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void HandleWin()
        {
            m_GameState = State_Win;
            OpenPanelAsync(WinPanelId).Forget();
        }

        public void PauseGame()
        {
            m_Pausesd = true;
            Time.timeScale = 0;
            OpenPauseAsync().Forget();
            if (GameUI_PCUIFormLogic.Current != null)
                GameUI_PCUIFormLogic.Current.gameObject.SetActive(false);
        }

        public void ResumeGame()
        {
            m_Pausesd = false;
            Time.timeScale = 1;
            if (_pauseForm != null)
            {
                GameFrameWork.UI?.Close(_pauseForm);
                _pauseForm = null;
            }

            if (GameUI_PCUIFormLogic.Current != null)
                GameUI_PCUIFormLogic.Current.gameObject.SetActive(true);
        }

        public void ExitGame()
        {
            m_Pausesd = false;
            Time.timeScale = 1;
            _pauseForm = null;
            ProcedureNavigator.EnterMainMenu();
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
            OpenPanelAsync(WinPanelId).Forget();
        }

        public void StartBossFight()
        {
            if (m_Boss != null)
            {
                m_IsBossFight = true;
                m_Boss.GetComponent<AIControlBase>().StartAlert();
                if (GameUI_PCUIFormLogic.Current != null)
                    GameUI_PCUIFormLogic.Current.ShowBossHealth(m_Boss.GetComponent<DamageControl>());
            }
        }

        public void ShowMessage(string message)
        {
            OpenPanelAsync(MessagePanelId, message).Forget();
        }

        private async UniTaskVoid OpenPauseAsync()
        {
            if (GameFrameWork.UI == null)
            {
                Debug.LogError("[GameControl] UI component is missing.");
                return;
            }

            try
            {
                _pauseForm = await GameFrameWork.UI.OpenAsync(PausePanelId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameControl] Open pause UI failed: {ex.Message}");
            }
        }

        private async UniTaskVoid OpenPanelAsync(int panelId, object userData = null)
        {
            if (GameFrameWork.UI == null)
            {
                Debug.LogError("[GameControl] UI component is missing.");
                return;
            }

            try
            {
                await GameFrameWork.UI.OpenAsync(panelId, userData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameControl] Open UI panel {panelId} failed: {ex.Message}");
            }
        }
    }
}
