﻿using System.Threading;
using AD.Descriptor;
using AD.Model;
using Cysharp.Threading.Tasks;
using IronSource.Scripts;
using UnityEngine;

namespace AD.Provider
{
    //ERROR codes https://developers.is.com/ironsource-mobile/ios/supersonic-sdk-error-codes/
    public class IronSourceAdProvider : IADProvider
    {
        private UniTaskCompletionSource _taskCompletionSource;
        private UniTaskCompletionSource<ADResult> _adResult;
        private IronSourceDescriptor _ironSourceDescriptor;
        private bool _interstitialRequested;
        private bool _initialized;

        public async UniTask Init(ADDescriptor adDescriptor)
        {
            IronSourceEvents.onSdkInitializationCompletedEvent += OnSdkInitializationCompletedEvent;
            _ironSourceDescriptor = adDescriptor.IronSourceDescriptor;
            IronSourceService.Agent.validateIntegration();
            IronSourceService.Agent.init(_ironSourceDescriptor.Token);
            _taskCompletionSource = new UniTaskCompletionSource();
            await _taskCompletionSource.Task;
            SubscribeOnEvents();
        }

        private void SubscribeOnEvents()
        {
            IronSourceRewardedVideoEvents.onAdClosedEvent += OnRewardedVideoAdClosedEvent;
            IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoEvent;
            IronSourceRewardedVideoEvents.onAdShowFailedEvent += OnFailLoadEvent;


            IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;
            IronSourceInterstitialEvents.onAdLoadFailedEvent += OnInterstitialLoadFailed;
            IronSourceInterstitialEvents.onAdShowFailedEvent += OnInterstitialShowedFailed;
            IronSourceInterstitialEvents.onAdReadyEvent += OnInterstitialReady;
        }

        private void OnInterstitialReady(IronSourceAdInfo obj)
        {
            _interstitialRequested = false;
        }

        private void OnInterstitialShowedFailed(IronSourceError error, IronSourceAdInfo info)
        {
            if (error.getCode() == 520)
            {
                _adResult?.TrySetResult(ADResult.NetworkError);
            }else if (error.getCode() == 509)
            {
                _adResult?.TrySetResult(ADResult.FailLoad);

            }
            else
            {
                _adResult?.TrySetResult(ADResult.FailShow);
            }

            TryLoadInterstitial();
            _adResult = null;
        }

        private void OnInterstitialLoadFailed(IronSourceError obj)
        {
            TryLoadInterstitial();
        }

        private void OnInterstitialClosed(IronSourceAdInfo obj)
        {
            TryLoadInterstitial();
            _adResult?.TrySetResult(ADResult.AdClosed);
            _adResult = null;
        }

        private void TryLoadInterstitial()
        {
            if (IronSourceService.Agent.isInterstitialReady() || _interstitialRequested)
            {
                return;
            }
            IronSourceService.Agent.loadInterstitial();
            _interstitialRequested = true;
        }
        
        private void OnFailLoadEvent(IronSourceError error, IronSourceAdInfo info)
        {
            _adResult?.TrySetResult(ADResult.FailShow);
            _adResult = null;
        }

        private void OnRewardedVideoEvent(IronSourcePlacement placement, IronSourceAdInfo adInfo)
        {
            Debug.Log("Showed reward");
            _adResult?.TrySetResult(ADResult.Successfully);
            _adResult = null;
        }

        public async UniTask<ADResult> ShowAD(ADType adType, string placement)
        {
            Debug.Log("ShowAD");

            if (!_initialized)
            {
                return ADResult.NotInitialized;
            }
            
            _adResult?.TrySetCanceled(new CancellationToken(true));
            _adResult = new UniTaskCompletionSource<ADResult>();
            if (adType == ADType.Reward)
            {
                ShowRewardVideo(placement);
            }
            else
            {
                ShowInterstitial(placement);
            }

            ADResult adResultTask = await _adResult.Task;
            _adResult = null;
            return adResultTask;
        }

        private void ShowInterstitial(string placement)
        {
            if (IronSourceService.Agent.isInterstitialReady())
            {
                IronSourceService.Agent.showInterstitial(placement);
            }
            else
            {
                TryLoadInterstitial();
                _adResult?.TrySetResult(ADResult.AdNotReady);
            }
        }

        private void ShowRewardVideo(string placement)
        {
            IronSourceService.Agent.showRewardedVideo(placement);
        }

        private void OnSdkInitializationCompletedEvent()
        {
            Debug.Log("On AD SdkInitializationCompleted");
            _taskCompletionSource.TrySetResult();
            _initialized = true;
            if (_ironSourceDescriptor.PreInitInterstitial)
            {
                TryLoadInterstitial();
            }
        }

        public void Dispose()
        {
            IronSourceEvents.onSdkInitializationCompletedEvent -= OnSdkInitializationCompletedEvent;

            
            IronSourceRewardedVideoEvents.onAdClosedEvent -= OnRewardedVideoAdClosedEvent;
            IronSourceRewardedVideoEvents.onAdRewardedEvent -= OnRewardedVideoEvent;
            IronSourceRewardedVideoEvents.onAdShowFailedEvent -= OnFailLoadEvent;


            IronSourceInterstitialEvents.onAdClosedEvent -= OnInterstitialClosed;
            IronSourceInterstitialEvents.onAdLoadFailedEvent -= OnInterstitialLoadFailed;
            IronSourceInterstitialEvents.onAdShowFailedEvent -= OnInterstitialShowedFailed;
            IronSourceInterstitialEvents.onAdReadyEvent -= OnInterstitialReady;
        }

        private void OnRewardedVideoAdClosedEvent(IronSourceAdInfo info)
        {
            _adResult?.TrySetResult(ADResult.AdClosed);
            _adResult = null;
        }
    }
}