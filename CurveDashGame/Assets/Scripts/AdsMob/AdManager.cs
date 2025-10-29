using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace STG.CurveDash.AdsMob
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class AdManager : IAdManager
    {
#if UNITY_ANDROID
        // ReSharper disable once InconsistentNaming
        private const string BANNER_AD_UNIT_ID = "ca-app-pub-3940256099942544/6300978111";
        // private const string BANNER_AD_UNIT_ID = "ca-app-pub-3601008096580983/9155124929";

        // ReSharper disable once InconsistentNaming
        private const string INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-3940256099942544/1033173712";
        // private const string INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-3601008096580983/6101763781";

        // ReSharper disable once InconsistentNaming
        private const string REWARDED_AD_UNIT_ID = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
        private const string BANNER_AD_UNIT_ID = "ca-app-pub-3940256099942544/2934735716";
        private const string INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-3940256099942544/4411468910";
        private const string REWARDED_AD_UNIT_ID = "ca-app-pub-3940256099942544/1712485313";
#else
        private const string BANNER_AD_UNIT_ID = "unused";
        private const string INTERSTITIAL_AD_UNIT_ID = "unused";
        private const string REWARDED_AD_UNIT_ID = "unused";
#endif

        private BannerView bannerView;
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;

        private bool isInitialized = false;

        public void Initialize()
        {
            MobileAds.Initialize(initStatus => { isInitialized = true; });
            RequestBanner();
            RequestInterstitial();
            // RequestRewardedAd();
        }

        public void ShowBanner()
        {
            if (!isInitialized)
            {
                return;
            }

            bannerView?.Show();
        }

        public void ShowInterstitial(Action onAdClosed = null)
        {
            if (isInitialized && interstitialAd.CanShowAd())
            {
                interstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    onAdClosed?.Invoke();
                    RequestInterstitial();
                };
                interstitialAd.Show();
            }
            else
            {
                onAdClosed?.Invoke();
            }
        }

        public void ShowRewardedAd(Action onAdClosed = null)
        {
            throw new NotImplementedException();
        }

        private void RequestBanner()
        {
            bannerView = new BannerView(BANNER_AD_UNIT_ID, AdSize.Banner, AdPosition.Bottom);
            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
        }

        private void RequestInterstitial()
        {
            var adRequest = new AdRequest();

            InterstitialAd.Load(INTERSTITIAL_AD_UNIT_ID, adRequest, (InterstitialAd ad, LoadAdError error) =>
            {
                // If the operation failed for a reason.
                if (error != null)
                {
                    Debug.LogError("Interstitial ad failed to load an ad with error : " + error);
                    return;
                }

                // If the operation failed for unknown reasons.
                // This is an unexpected error, please report this bug if it happens.
                if (ad == null)
                {
                    Debug.LogError("Unexpected error: Interstitial load event fired with null ad and null error.");
                    return;
                }

                // The operation completed successfully.
                Debug.Log("Interstitial ad loaded with response : " + ad.GetResponseInfo());
                interstitialAd = ad;
            });
        }

        private void RequestRewardedAd()
        {
            throw new System.NotImplementedException();
        }
    }
}