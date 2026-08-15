

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CommandoRobot.ScriptableObjects;
using System;
using UnityEngine.SceneManagement;

namespace CommandoRobot
{
    public class RewardedAdsButton : MonoBehaviour
    {
        public string m_VideoObjectName;
        AdmobVideoObject m_TargetVideoObj;



        [SerializeField]
        private DataStorage m_DataStorage;
        [SerializeField, Space]


        public VideoRewardData m_Reward;
        public Func<bool> f_VideoComplete;



        public float m_RequestDelay = .1f;

        [Space]
        public Image m_ImgLoading;

        public Image m_ImgVideo;
        public Image m_ImgIcon;
        public Text m_TextLoading;

        // Start is called before the first frame update
        void Start()
        {
#if ADMOB_ENABLED

            m_TargetVideoObj = AdmobControl.m_Current.m_VideoObjectsList[m_VideoObjectName];


            Invoke("RequestVideo", m_RequestDelay);

            m_TargetVideoObj.OnHandleReward += HandleReward;
#endif
        }

        public void RequestVideo()
        {
            m_TargetVideoObj.RequestVideoAd();
        }

        // Update is called once per frame
        void Update()
        {

#if ADMOB_ENABLED
            if (m_TargetVideoObj.m_AdRequested)
            {
                if (m_TargetVideoObj.rewardedAd != null && m_TargetVideoObj.rewardedAd.CanShowAd())
                {
                    m_ImgLoading.gameObject.SetActive(false);


                    m_TextLoading.text = "Watch Ad";



                    m_ImgVideo.gameObject.SetActive(true);
                    GetComponent<Button>().interactable = true;
                }
                else
                {
                    m_ImgVideo.gameObject.SetActive(false);
                    m_ImgLoading.gameObject.SetActive(true);


                    m_TextLoading.text = "Finding video...";



                    GetComponent<Button>().interactable = false;
                }
            }
            else
            {
                m_ImgVideo.gameObject.SetActive(false);
                m_ImgLoading.gameObject.SetActive(true);


                m_TextLoading.text = "Finding video...";



                GetComponent<Button>().interactable = false;
            }
#endif
        }

        private void OnDestroy()
        {
            if (m_TargetVideoObj != null)
            {
                m_TargetVideoObj.OnHandleReward -= HandleReward;
            }
        }

        public void OnClick()
        {
#if ADMOB_ENABLED
            if (m_TargetVideoObj.rewardedAd != null && m_TargetVideoObj.rewardedAd.CanShowAd())
            {
                m_TargetVideoObj.ShowVideoAd();
            }
#endif
        }

        public void HandleReward()
        {

            if (m_Reward != null)
            {

                switch (m_Reward.m_Type)
                {
                    case "FreeGem":


                        m_DataStorage.Coin += 50;
                        m_DataStorage.SaveData();
                        UISystem.ShowUI("GemRewardUI");
                        break;


                }

                m_DataStorage.SaveData();
            }

        }
    }
}
