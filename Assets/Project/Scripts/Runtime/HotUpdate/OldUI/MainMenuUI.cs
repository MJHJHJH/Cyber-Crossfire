using UnityEngine;
using UnityEngine.UI;
using CommandoRobot.ScriptableObjects;
using GamePlay;
using GamePlay.Data;

namespace CommandoRobot
{
    public class MainMenuUI : MonoBehaviour
    {
        public GameplayData m_GameplayData;

        public Text m_CointText;

        public Image m_LevelPanel;
        public Image m_ArmoryPanel;
        public Image m_MainPanel;
        public Image[] m_WeaponButtons;
        public Image[] m_WeaponImages;
        public Text[] m_WeaponPrices;

        void Start()
        {
            m_LevelPanel.gameObject.SetActive(false);
            m_ArmoryPanel.gameObject.SetActive(false);
            m_MainPanel.gameObject.SetActive(true);
            PlayerSave.EnsureLoaded();
        }

        void Update()
        {
            m_CointText.text = PlayerSave.Coin.ToString();

            bool[] unlocked = PlayerSave.WeaponsUnlocked;
            int count = unlocked != null ? unlocked.Length : 0;
            int selected = PlayerSave.SelectedWeapon;
            for (int i = 0; i < count && i < m_WeaponButtons.Length; i++)
            {
                m_WeaponButtons[i].color = i == selected ? Color.green : Color.white;

                if (unlocked[i])
                {
                    m_WeaponImages[i].color = Color.white;
                    m_WeaponPrices[i].gameObject.SetActive(false);
                }
                else
                {
                    m_WeaponPrices[i].gameObject.SetActive(true);
                    m_WeaponPrices[i].text = PlayerSave.GetWeaponPrice(i).ToString();
                    m_WeaponImages[i].color = new Color(.3f, .3f, .3f, .7f);
                }
            }
        }

        public void BtnPlay()
        {
            m_LevelPanel.gameObject.SetActive(true);
            m_MainPanel.gameObject.SetActive(false);
        }

        public void BtnArmory()
        {
            m_ArmoryPanel.gameObject.SetActive(true);
            m_MainPanel.gameObject.SetActive(false);
        }

        public void BtnBack()
        {
            m_ArmoryPanel.gameObject.SetActive(false);
            m_LevelPanel.gameObject.SetActive(false);
            m_MainPanel.gameObject.SetActive(true);
        }

        public void BtnWeapon(int num)
        {
            if (PlayerSave.IsWeaponUnlocked(num))
                PlayerSave.SelectWeapon(num);
            else
                PlayerSave.TryUnlockWeapon(num);
        }

        public void BtnLevel(int num)
        {
            m_GameplayData.LevelNumber = num;
            if (!LevelSceneLocations.TryGet(num, out string location))
            {
                Debug.LogError($"[MainMenuUI] Invalid level index: {num}");
                return;
            }

            ProcedureNavigator.EnterBattle(location);
        }

        public void BtnExit()
        {
            Application.Quit();
        }
    }
}
