#if LEVEL_PLAY_SDK

using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using System.Collections.Generic;
using Ad.Descriptor;
using Ad.Model;
using Ad.Service;
using CGK.Utils;

namespace Ad.Provider
{
    //ERROR codes https://developers.is.com/ironsource-mobile/ios/supersonic-sdk-error-codes/
    public class IronSourceAdProvider : IAdProvider
    {
        private static bool
            _initialized; // can't call IronSource.Agent.init(_ironSourceDescriptor.Token) twice in one App

        

        private readonly IronSourceDescriptor _ironSourceDescriptor;
        private readonly IAdAnalytics _adAnalytics;

        private UniTaskCompletionSource _taskCompletionSource;
        private UniTaskCompletionSource<AdResult> _adResult;
        private bool _interstitialRequested;
        private bool _rewardRequested;

        public IronSourceAdProvider(IronSourceDescriptor ironSourceDescriptor, IAdAnalytics adAnalytics)
        {
            _ironSourceDescriptor = ironSourceDescriptor;
            _adAnalytics = adAnalytics;
        }

        public async UniTask Init()
        {
            IronSourceEvents.onSdkInitializationCompletedEvent += OnSdkInitializationCompletedEvent;
            SubscribeOnEvents();

            if (_initialized)
            {
                await UniTask.CompletedTask;
                return;
            }

            if (_ironSourceDescriptor.TestSuitCase)
            {
                IronSource.Agent.setMetaData("is_test_suite", "enable");
            }

            IronSource.Agent.validateIntegration();
            IronSource.Agent.init(_ironSourceDescriptor.Token);
            _taskCompletionSource = new UniTaskCompletionSource();

            int indexCompletedTask =
                await UniTask.WhenAny(
                    UniTask.Delay(TimeSpan.FromSeconds(_ironSourceDescriptor.InitTimeOut)),
                    _taskCompletionSource.Task);
            if (indexCompletedTask == 0 && _ironSourceDescriptor.ThrowErrorInInit)
            {
                throw new TimeoutException("init timeout reached");
            }

            await Task.CompletedTask;
        }

        public void DestroyBanner()
        {
            IronSource.Agent.destroyBanner();
        }

        public async UniTask<AdResult> ShowAd(AdType adType, string placement)
        {
            Debug.Log($"ShowAD {adType} {placement}");
            if (!_initialized)
            {
                return AdResult.NotInitialized;
            }

            _adResult?.TrySetCanceled(new CancellationToken(true));
            _adResult = new UniTaskCompletionSource<AdResult>();

            switch (adType)
            {
                case AdType.Reward:
                    ShowRewardVideo(placement);
                    break;
                case AdType.Interstitial:
                    ShowInterstitial(placement);
                    break;
                case AdType.BannerBottom:
                    ShowBanner(IronSourceBannerPosition.BOTTOM, placement);
                    break;
                case AdType.BannerTop:
                    ShowBanner(IronSourceBannerPosition.TOP, placement);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(adType), adType, null);
            }

            AdResult adResultTask = await _adResult.Task;
            _adResult = null;
            return adResultTask;
        }

        private void ShowBanner(IronSourceBannerPosition bannerPosition, string placement)
        {
            IronSource.Agent.loadBanner(IronSourceBannerSize.BANNER, bannerPosition, placement);
        }

        private void SubscribeOnEvents()
        {
            IronSourceRewardedVideoEvents.onAdClosedEvent += OnRewardedVideoAdClosedEvent;
            IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoFinishedSuccessfully;
            IronSourceRewardedVideoEvents.onAdShowFailedEvent += OnFailLoadEvent;
            IronSourceRewardedVideoEvents.onAdLoadFailedEvent += LoadFailedEvent;
            IronSourceRewardedVideoEvents.onAdReadyEvent += OnLoadFinished;

            IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;
            IronSourceInterstitialEvents.onAdLoadFailedEvent += OnInterstitialLoadFailed;
            IronSourceInterstitialEvents.onAdShowFailedEvent += OnInterstitialShowedFailed;
            IronSourceInterstitialEvents.onAdReadyEvent += OnInterstitialReady;

            IronSourceEvents.onImpressionDataReadyEvent += ImpressionDataReadyEvent;
        }

        private void OnLoadFinished(IronSourceAdInfo obj)
        {
            Debug.LogWarning("LoadFailedEvent " + obj);
        }

        private void LoadFailedEvent(IronSourceError obj)
        {
            Debug.LogWarning("LoadFailedEvent " + obj);
        }

        private void OnInterstitialReady(IronSourceAdInfo obj)
        {
            _interstitialRequested = false;
        }

        private void OnInterstitialShowedFailed(IronSourceError error, IronSourceAdInfo info)
        {
            if (error.getCode() == 520)
            {
                _adResult?.TrySetResult(AdResult.NetworkError);
            }
            else if (error.getCode() == 509)
            {
                _adResult?.TrySetResult(AdResult.FailLoad);
            }
            else
            {
                _adResult?.TrySetResult(AdResult.FailShow);
            }

            TryLoadInterstitial();
            _adResult = null;
        }

        private void OnInterstitialLoadFailed(IronSourceError obj)
        {
            Debug.LogWarning("OnInterstitialLoadFailed " + obj);
            _interstitialRequested = false;
        }

        private void OnInterstitialClosed(IronSourceAdInfo obj)
        {
            TryLoadInterstitial();
            _adResult?.TrySetResult(AdResult.Successfully);
            _adResult = null;
        }

        private void TryLoadInterstitial()
        {
            if (IronSource.Agent.isInterstitialReady() || _interstitialRequested)
            {
                return;
            }

            _interstitialRequested = true;
            IronSource.Agent.loadInterstitial();
        }

        private void OnFailLoadEvent(IronSourceError error, IronSourceAdInfo info)
        {
            Debug.LogWarning("OnFailLoadEvent " + error + " " + info);
            _adResult?.TrySetResult(AdResult.FailShow);
            _adResult = null;
        }

        private void OnRewardedVideoFinishedSuccessfully(IronSourcePlacement placement, IronSourceAdInfo adInfo)
        {
            _adResult?.TrySetResult(AdResult.Successfully);
            _adResult = null;
        }

        private void ShowInterstitial(string placement)
        {
            if (IronSource.Agent.isInterstitialReady())
            {
                IronSource.Agent.showInterstitial(placement);
            }
            else
            {
                TryLoadInterstitial();
                _adResult?.TrySetResult(AdResult.AdNotReady);
            }
        }

        private void ShowRewardVideo(string placement)
        {
            if (IronSource.Agent.isRewardedVideoAvailable())
            {
                IronSource.Agent.showRewardedVideo(placement);
            }
            else
            {
                _adResult.TrySetResult(AdResult.AdNotReady);
            }
        }

        private void OnSdkInitializationCompletedEvent()
        {
            TryLoadInterstitial();
            _taskCompletionSource?.TrySetResult();
            _initialized = true;
        }

        private void OnRewardedVideoAdClosedEvent(IronSourceAdInfo info)
        {
            _adResult?.TrySetResult(AdResult.AdClosed);
            _adResult = null;
        }

        private void ImpressionDataReadyEvent(IronSourceImpressionData impressionData)
        {
            if (impressionData == null)
            {
                return;
            }

            double impressionDataRevenue = impressionData.revenue ?? 0;
            string cpmEncrypted = impressionData.encryptedCPM.IsNullOrEmpty() ? "none" : impressionData.encryptedCPM;
            Dictionary<string, object> parameters = new Dictionary<string, object>()
            {
                { IronSourceAdConst.IRON_SOURCE_AD_SOURCE, impressionData.adNetwork },
                { IronSourceAdConst.IRON_SOURCE_AD_FORMAT, impressionData.adUnit },
                { IronSourceAdConst.IRON_SOURCE_AD_INSTANCE_NAME, impressionData.instanceName },
                { IronSourceAdConst.IRON_SOURCE_AD_PLACEMENT, impressionData.placement },
                { IronSourceAdConst.IRON_SOURCE_AD_COUNTRY, impressionData.country },
                { IronSourceAdConst.IRON_SOURCE_AD_PRECISION, impressionData.precision },
                { IronSourceAdConst.IRON_SOURCE_AD_LIFETIME_REVENUE, impressionData.lifetimeRevenue ?? 0},
                { IronSourceAdConst.IRON_SOURCE_AD_ENCRYPTED_CPM, cpmEncrypted },
                { IronSourceAdConst.IRON_SOURCE_AD_REVENUE, impressionDataRevenue },
                { IronSourceAdConst.IRON_SOURCE_AD_DATA, impressionData}
            };
            _adAnalytics.AdRevenue(parameters);
        }

        public void Dispose()
        {
            IronSourceEvents.onSdkInitializationCompletedEvent -= OnSdkInitializationCompletedEvent;


            IronSourceRewardedVideoEvents.onAdClosedEvent -= OnRewardedVideoAdClosedEvent;
            IronSourceRewardedVideoEvents.onAdRewardedEvent -= OnRewardedVideoFinishedSuccessfully;
            IronSourceRewardedVideoEvents.onAdShowFailedEvent -= OnFailLoadEvent;
            IronSourceRewardedVideoEvents.onAdLoadFailedEvent -= LoadFailedEvent;
            IronSourceRewardedVideoEvents.onAdReadyEvent -= OnLoadFinished;

            IronSourceInterstitialEvents.onAdClosedEvent -= OnInterstitialClosed;
            IronSourceInterstitialEvents.onAdLoadFailedEvent -= OnInterstitialLoadFailed;
            IronSourceInterstitialEvents.onAdShowFailedEvent -= OnInterstitialShowedFailed;
            IronSourceInterstitialEvents.onAdReadyEvent -= OnInterstitialReady;


            IronSourceEvents.onImpressionDataReadyEvent -= ImpressionDataReadyEvent;
        }
    }
}
#endif