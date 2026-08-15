using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CommandoRobot.ScriptableObjects;
namespace CommandoRobot
{
    public class MainMenuUI : MonoBehaviour
    {
        public DataStorage m_Storage;
        public GameplayData m_GameplayData;

        public Text m_CointText;

        public Image m_LevelPanel;
        public Image m_ArmoryPanel;
        public Image m_MainPanel;
        public Image[] m_WeaponButtons;
        public Image[] m_WeaponImages;
        public Text[] m_WeaponPrices;
        // Start is called before the first frame update
        void Start()
        {
            m_LevelPanel.gameObject.SetActive(false);
            m_ArmoryPanel.gameObject.SetActive(false);
            m_MainPanel.gameObject.SetActive(true);
            m_Storage.LoadData();
        }

        // Update is called once per frame
        void Update()
        {
            m_CointText.text = m_Storage.Coin.ToString();

            for (int i = 0;i<6;i++)
            {
                if (i==m_Storage.m_SelectedWeapon) 
                {
                    m_WeaponButtons[i].color = Color.green;
                }
                else
                {
                    m_WeaponButtons[i].color = Color.white;
                }

                if (m_Storage.m_WeaponsUnlocked[i])
                {
                    m_WeaponImages[i].color=Color.white;
                    m_WeaponPrices[i].gameObject.SetActive(false);
                }
                else
                {
                    m_WeaponPrices[i].gameObject.SetActive(true);
                    m_WeaponPrices[i].text = m_Storage.m_WeaponsPrice[i].ToString();
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
            if (m_Storage.m_WeaponsUnlocked[num])
            {
                m_Storage.m_SelectedWeapon = num;
            }
            else
            {
                if (m_Storage.m_WeaponsPrice[num]<= m_Storage.Coin)
                {
                    m_Storage.Coin -= m_Storage.m_WeaponsPrice[num];
                    m_Storage.m_WeaponsUnlocked[num] = true;
                    m_Storage.m_SelectedWeapon = num;
                }
            }
        }

        public void BtnLevel(int num)
        {
            m_GameplayData.LevelNumber = num;
            switch (num)
            {
                case 0:
                    SceneManager.LoadScene("Level 1");
                    break;
                case 1:
                    SceneManager.LoadScene("Level 2");
                    break;
                case 2:
                    SceneManager.LoadScene("Level 3");
                    break;
                case 3:
                    SceneManager.LoadScene("Level 4");
                    break;
                case 4:
                    SceneManager.LoadScene("Level 5");
                    break;
            }
        }

        public void BtnExit()
        {
            Application.Quit();
        }
    }
}