
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ADMOB_ENABLED
using GoogleMobileAds.Api;
#endif
namespace CommandoRobot
{
    public class AdmobControl : MonoBehaviour
    {

#if ADMOB_ENABLED
        BannerView _bannerView;

        private string m_IntersitialZoneID = "ca-app-pub-3940256099942544/1033173712";
        private InterstitialAd interstitialAd;


        public static AdmobControl m_Current;
        [HideInInspector]
        public bool m_IsErrorInAdnetwork;

        public AdmobVideoObject[] m_VideoObjects;
        public Dictionary<string, AdmobVideoObject> m_VideoObjectsList;

        void Awake()
        {
            m_Current = this;
            m_VideoObjectsList = new Dictionary<string, AdmobVideoObject>();
            foreach (AdmobVideoObject obj in m_VideoObjects)
            {
                m_VideoObjectsList.Add(obj.gameObject.name, obj);
            }
        }
        // Start is called before the first frame update
        void Start()
        {

        }
        public void RequestInterstitialBanner()
        {
            // Clean up the old ad before loading a new one.
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }

            Debug.Log("Loading the interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();
            adRequest.Keywords.Add("unity-admob-sample");

            // send the request to load the ad.
            InterstitialAd.Load(m_IntersitialZoneID, adRequest,
                (InterstitialAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        Debug.LogError("interstitial ad failed to load an ad " +
                                       "with error : " + error);
                        return;
                    }

                    Debug.Log("Interstitial ad loaded with response : "
                              + ad.GetResponseInfo());

                    interstitialAd = ad;
                    RegisterReloadHandler(ad);
                });
        }

        public void ShowInterstitialBanner()
        {
            if (interstitialAd != null && interstitialAd.CanShowAd())
            {
                Debug.Log("Showing interstitial ad.");
                m_IsErrorInAdnetwork = false;
                interstitialAd.Show();
            }
            else
            {
                m_IsErrorInAdnetwork = true;
                Debug.LogError("Interstitial ad is not ready yet.");
            }
        }
        private void RegisterReloadHandler(InterstitialAd ad)
        {
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Interstitial Ad full screen content closed.");

                // Reload the ad so that we can show another as soon as possible.
                RequestInterstitialBanner();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogError("Interstitial ad failed to open full screen content " +
                               "with error : " + error);

                // Reload the ad so that we can show another as soon as possible.
                RequestInterstitialBanner();
            };
        }


#endif

    }
}
