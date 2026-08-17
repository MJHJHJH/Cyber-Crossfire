using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CommandoRobot.ScriptableObjects;

namespace CommandoRobot
{
    public class UI_HUD : MonoBehaviour
    {
        public Text m_CoinCountText;
        public Text m_GunNameText;
        public Text m_GrenadeText;
        public Image m_TargetLockImage;

        public RectTransform m_MainCanvas;

        public Image m_PlayerHealth;

        [Space]
        public Image m_BossHealthBase;
        public Image m_BossHealth;
        [HideInInspector]
        public DamageControl m_BossDamageControl;

        public static UI_HUD m_Main;

        public DataStorage m_DataStorage;

        void Awake()
        {
            m_Main = this;
        }
        // Start is called before the first frame update
        void Start()
        {
            m_BossHealthBase.gameObject.SetActive(false);

        }

        // Update is called once per frame
        void Update()
        {

            if (InputControl.m_Main.m_MouseAim)
            {
                m_TargetLockImage.gameObject.SetActive(true);
                m_TargetLockImage.rectTransform.anchoredPosition = MousePosToUI();
            }
            else
            {
                if (PlayerCharacter.m_Current.m_BestTargetObject != null)
                {
                    m_TargetLockImage.gameObject.SetActive(true);
                    m_TargetLockImage.rectTransform.anchoredPosition = WorldPointToUI(PlayerCharacter.m_Current.m_BestTargetObject.m_TargetCenter.position);
                }
                else
                {
                    m_TargetLockImage.gameObject.SetActive(false);
                }
            }
            //--------

            m_CoinCountText.text = m_DataStorage.Coin.ToString();


            if (PlayerCharacter.m_Current.m_CurrentWeapon != null)
            {
                m_GunNameText.text = PlayerCharacter.m_Current.m_CurrentWeapon.Title;
            }

            m_GrenadeText.text = "Grenade : " + PlayerCharacter.m_Current.m_GrenadeCount.ToString();



            DamageControl damage = PlayerCharacter.m_Current.GetComponent<DamageControl>();
            m_PlayerHealth.fillAmount = damage.Damage / damage.MaxDamage;

            if (m_BossDamageControl != null)
            {
                m_BossHealth.fillAmount = m_BossDamageControl.Damage / m_BossDamageControl.MaxDamage;
            }
        }

        public Vector2 WorldPointToUI(Vector3 pos)
        {
            Vector3 v = CameraControl.m_Current.m_MyCamera.WorldToScreenPoint(pos);
            v.x = v.x / (float)Screen.width;
            v.y = v.y / (float)Screen.height;

            v.x = m_MainCanvas.sizeDelta.x * v.x;
            v.y = m_MainCanvas.sizeDelta.y * v.y;

            return Helper.ToVector2(v);
        }

        public Vector2 MousePosToUI()
        {
            Vector3 v = Input.mousePosition;
            v.x = v.x / (float)Screen.width;
            v.y = v.y / (float)Screen.height;

            v.x = m_MainCanvas.sizeDelta.x * v.x;
            v.y = m_MainCanvas.sizeDelta.y * v.y;

            return Helper.ToVector2(v);
        }

        public void BtnPause()
        {
            GameControl.m_Current.PauseGame();
        }

        public void ShowBossHealth(DamageControl bossDamage)
        {
            m_BossDamageControl = bossDamage;
            m_BossHealthBase.gameObject.SetActive(true);
        }

    }
}
