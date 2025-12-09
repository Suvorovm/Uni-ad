#if CLEVER_SDK
using System;
using System.Collections.Generic;
using Ad.Descriptor;
using Ad.Model;
using Ad.Service;
using CAS;
using CGK.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using AdType = Ad.Model.AdType;

namespace Ad.Provider
{
    public class CleverAdProvider : IAdProvider
    {
        private readonly CleverAdDescriptor _cleverAdDescriptor;
        private readonly IAdAnalytics _adAnalytics;
        private readonly IAdConsentService _adConsentService;
        private readonly Stack<string> _placementShowQueue = new Stack<string>();

        private IMediationManager _mediationManager;
        private UniTaskCompletionSource _taskCompletionSource;
        private UniTaskCompletionSource<AdResult> _adResult;
        private bool _inited;
        private IAdView _currentBanner;

        public CleverAdProvider(
            CleverAdDescriptor cleverAdDescriptor,
            IAdAnalytics adAnalytics,
            IAdConsentService adConsentService)
        {
            _cleverAdDescriptor = cleverAdDescriptor;
            _adAnalytics = adAnalytics;
            _adConsentService = adConsentService;
        }

        public async UniTask Init()
        {
            if (_cleverAdDescriptor.TestDevice)
            {
                CAS.MobileAds.settings.SetTestDeviceIds(new[]
                {
                    _cleverAdDescriptor.TestDeviceId
                });
            }

            _taskCompletionSource = new UniTaskCompletionSource();
            _mediationManager = GetAdManager();
            await _taskCompletionSource.Task;
        }

        private IMediationManager GetAdManager()
        {
            var builder = MobileAds.BuildManager();
            builder.WithConsentFlow(new ConsentFlow(false));

            if (!_cleverAdDescriptor.TenjinKey.IsNullOrEmpty())
            {
                builder.WithMediationExtras("tenjin_key", _cleverAdDescriptor.TenjinKey);
            }

            return builder
                .WithCompletionListener((config) =>
                {
                    string initErrorOrNull = config.error;
                    string userCountryISO2OrNull = config.countryCode;
                    IMediationManager manager = config.manager;
                    bool protectionApplied = config.isConsentRequired;
                    ConsentFlow.Status consentFlowStatus = config.consentFlowStatus;

                    Debug.Log($"{initErrorOrNull} {userCountryISO2OrNull} {protectionApplied} {consentFlowStatus}");

                    if (initErrorOrNull != null)
                    {
                        _taskCompletionSource.TrySetException(new Exception(initErrorOrNull));
                        return;
                    }

                    _inited = true;
                    _taskCompletionSource.TrySetResult();
                    SubscribeOnEvents();
                })
                .Build();
        }

        public async UniTask<AdResult> ShowAd(AdType adType, string placement = "")
        {
            if (!_inited)
            {
                return AdResult.NotInitialized;
            }

            if (!_placementShowQueue.Contains(placement))
            {
                _placementShowQueue.Push(placement);
            }
            switch (adType)
            {
                case AdType.Reward:
                    TryShowAd(CAS.AdType.Rewarded, placement);
                    break;
                case AdType.Interstitial:
                    TryShowAd(CAS.AdType.Interstitial, placement);
                    break;
                case AdType.BannerBottom:
                    TryShowBottomBanner();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(adType), adType, null);
            }

            AdResult adResultTask = await _adResult.Task;
            _adResult = null;
            return adResultTask;
        }

        private void TryShowBottomBanner()
        {
            DestroyBanner();
            _adResult = new UniTaskCompletionSource<AdResult>();
            if (_currentBanner == null)
            {
                _currentBanner = _mediationManager.GetAdView(AdSize.Banner);
            }

            _currentBanner?.SetActive(true);
            _currentBanner?.SetPosition(0, 0, AdPosition.BottomCenter);
            _adResult?.TrySetResult(AdResult.Successfully);
        }

        private void TryShowAd(CAS.AdType adType, string placement)
        {
            _adResult?.TrySetCanceled();
            _adResult = new UniTaskCompletionSource<AdResult>();

            if (_mediationManager == null || !_inited)
            {
                _adResult.TrySetResult(AdResult.NotInitialized);
                return;
            }

            if (_mediationManager.IsReadyAd(adType))
            {
                _mediationManager.ShowAd(adType);
            }
            else
            {
                _adResult.TrySetResult(AdResult.AdNotReady);
            }
        }

        public void DestroyBanner()
        {
            if (_inited && _currentBanner != null)
            {
                _currentBanner?.SetActive(false);
            }
        }

        private void SubscribeOnEvents()
        {
            _mediationManager.GetAdView(AdSize.Banner).OnImpression += OnImpressionBanner;

            _mediationManager.OnInterstitialAdLoaded += AdLoaded;
            _mediationManager.OnInterstitialAdFailedToLoad += AdFailedToLoad;
            _mediationManager.OnInterstitialAdShown += AdShown;
            _mediationManager.OnInterstitialAdFailedToShow += AdFailedToShow;
            _mediationManager.OnInterstitialAdClicked += AdClicked;
            _mediationManager.OnInterstitialAdClosed += AdClosed;
            _mediationManager.OnInterstitialAdImpression += InterstitialAdImpression;

            _mediationManager.OnRewardedAdImpression += RewardedAdImpression;
            _mediationManager.OnRewardedAdCompleted += AdCompleted;
            _mediationManager.OnRewardedAdLoaded += AdLoaded;
            _mediationManager.OnRewardedAdFailedToLoad += AdFailedToLoad;
            _mediationManager.OnRewardedAdShown += AdShown;
            _mediationManager.OnRewardedAdFailedToShow += AdFailedToShow;
            _mediationManager.OnRewardedAdClicked += AdClicked;
            _mediationManager.OnRewardedAdClosed += AdClosed;

        }
        private void OnImpressionBanner(IAdView view, AdMetaData data)
        {
            string placement = "none";
            if (_placementShowQueue.Count != 0)
            {
                placement = _placementShowQueue.Peek();
            }
            SendAnalytics(data, placement);
        }
        private void RewardedAdImpression(AdMetaData impression)
        {
            string placement = "none";
            if (_placementShowQueue.Count != 0)
            {
                placement = _placementShowQueue.Pop();
            }

            SendAnalytics(impression, placement);
        }
        private void InterstitialAdImpression(AdMetaData impression)
        {
            string placement = "none";
            if (_placementShowQueue.Count != 0)
            {
                placement = _placementShowQueue.Pop();
            }

            SendAnalytics(impression, placement);
        }
        private void SendAnalytics(AdMetaData impression, string placement)
        {
            Dictionary<string, object> adImpression = new Dictionary<string, object>()
            {
                { CleverSourceAdConst.AD_PLATFORM, "CAS" },
                { CleverSourceAdConst.AD_SOURCE, impression.network.ToString() },
                { CleverSourceAdConst.AD_UNIT_NAME, impression.identifier },
                { CleverSourceAdConst.AD_FORMAT, impression.type.ToString() },
                { CleverSourceAdConst.CURRENCY, "USD" },
                { CleverSourceAdConst.VALUE, impression.revenue },
                { CleverSourceAdConst.AD_LIFE_TIME_REVENUE, impression.lifetimeRevenue },
                { CleverSourceAdConst.AD_PRECISION, impression.priceAccuracy },
                { CleverSourceAdConst.SOURCE_UNIT_ID, impression.sourceUnitId },
                { CleverSourceAdConst.PLACEMENT, placement }
            };
            _adAnalytics.AdRevenue(adImpression);
        }

        private void AdCompleted()
        {
            Debug.Log("AdCompleted");
            _adResult?.TrySetResult(AdResult.Successfully);
            _adResult = null;
        }

        private void AdLoaded()
        {
            // можно повесить аналитику
        }

        private void AdFailedToLoad(AdError error)
        {
            Debug.LogError($"AdFailedToLoad {error}");
        }

        private void AdShown()
        {
            Debug.Log("AdShown");
            // Больше не завершаем _adResult здесь!
        }

        private void AdFailedToShow(string error)
        {
            Debug.Log("AdFailedToShow");
            _adResult?.TrySetResult(AdResult.FailShow);
            _adResult = null;
        }

        private void AdClicked()
        {
            Debug.Log("AdClicked");
        }

        private void AdClosed()
        {
            Debug.Log("AdClosed");
            if (_adResult != null)
            {
                _adResult.TrySetResult(AdResult.AdClosed);
                _adResult = null;
            }
        }

        public void Dispose()
        {
            _currentBanner?.SetActive(false);

            _mediationManager.GetAdView(AdSize.Banner).OnImpression -= OnImpressionBanner;


            _mediationManager.OnInterstitialAdLoaded -= AdLoaded;
            _mediationManager.OnInterstitialAdFailedToLoad -= AdFailedToLoad;
            _mediationManager.OnInterstitialAdShown -= AdShown;
            _mediationManager.OnInterstitialAdFailedToShow -= AdFailedToShow;
            _mediationManager.OnInterstitialAdClicked -= AdClicked;
            _mediationManager.OnInterstitialAdClosed -= AdClosed;
            _mediationManager.OnInterstitialAdImpression -= InterstitialAdImpression;

            _mediationManager.OnRewardedAdImpression -= RewardedAdImpression;
            _mediationManager.OnRewardedAdCompleted -= AdCompleted;
            _mediationManager.OnRewardedAdLoaded -= AdLoaded;
            _mediationManager.OnRewardedAdFailedToLoad -= AdFailedToLoad;
            _mediationManager.OnRewardedAdShown -= AdShown;
            _mediationManager.OnRewardedAdFailedToShow -= AdFailedToShow;
            _mediationManager.OnRewardedAdClicked -= AdClicked;
            _mediationManager.OnRewardedAdClosed -= AdClosed;
        }
    }
}
#endif