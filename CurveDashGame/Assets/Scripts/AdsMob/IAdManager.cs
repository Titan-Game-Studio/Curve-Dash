namespace STG.CurveDash.AdsMob
{
    public interface IAdManager
    {
        void Initialize();
        void ShowBanner();
        void ShowInterstitial(System.Action onAdClosed = null);
        void ShowRewardedAd(System.Action onAdClosed = null);
    }
}
