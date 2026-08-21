using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Analytics;
#if ADMOB_ENABLED
using GoogleMobileAds.Api;
#endif
namespace CommandoRobot
{
    public class AdmobVideoObject : MonoBehaviour
    {
#if ADMOB_ENABLED
        [HideInInspector]
        public RewardedAd rewardedAd;
#endif
        [HideInInspector]
        public bool m_AdRequested = false;

        public string m_VideoZoneID;

        private void Awake()
        {
            m_AdRequested = false;
        }

        // Start is called before the first frame update
        void Start()
        {

            OnHandleReward += HandleReward;
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void RequestVideoAd()
        {
            if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork ||
                Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
            {
#if ADMOB_ENABLED
                // Clean up the old ad before loading a new one.
                if (rewardedAd != null)
                {
                    rewardedAd.Destroy();
                    rewardedAd = null;
                }

                Debug.Log("Loading the rewarded ad.");

                // create our request used to load the ad.
                var adRequest = new AdRequest();
                m_AdRequested = true;
                //adRequest.Keywords.Add("unity-admob-sample");

                // send the request to load the ad.
                RewardedAd.Load(m_VideoZoneID, adRequest,
                    (RewardedAd ad, LoadAdError error) =>
                    {
                        // if error is not null, the load request failed.
                        if (error != null || ad == null)
                        {

                            Debug.LogError("Rewarded ad failed to load an ad " +
                                       "with error : " + error);

                            return;
                        }


                        Debug.Log("Rewarded ad loaded with response : "
                                  + ad.GetResponseInfo());

                        rewardedAd = ad;
                        RegisterEventHandlers(ad);
                    });
#endif
            }
        }

        public void ShowVideoAd()
        {
#if ADMOB_ENABLED
            const string rewardMsg =
            "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                rewardedAd.Show((Reward reward) =>
                {
                    // TODO: Reward the user.
                    Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));
                    OnHandleReward();
                });
            }
#endif
        }
        public bool AdmobError_OK()
        {
            return true;
        }
#if ADMOB_ENABLED
        private void RegisterEventHandlers(RewardedAd ad)
        {
            // Raised when the ad is estimated to have earned money.
            ad.OnAdPaid += (AdValue adValue) =>
            {
                Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
            };
            // Raised when an impression is recorded for an ad.
            ad.OnAdImpressionRecorded += () =>
            {
                Debug.Log("Rewarded ad recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                Debug.Log("Rewarded ad was clicked.");
            };
            // Raised when the ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("Rewarded ad full screen content opened.");
                // m_AdCompleteSeen = false;
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Rewarded ad full screen content closed.");
                RequestVideoAd();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogError("Rewarded ad failed to open full screen content with error : "
                    + error);
                RequestVideoAd();
            };

        }
#endif
        public void HandleReward()
        {

        }

        //public event EventHandler OnHandleReward;

        public delegate void RewardHandler();
        public event RewardHandler OnHandleReward;

        public void CallOnHandleReward()
        {
            OnHandleReward();
        }
    }
}