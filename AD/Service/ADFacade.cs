using Ad.Descriptor;
using Ad.Model;
using Ad.Provider;
using CGK.Descriptor.Service;
using Cysharp.Threading.Tasks;
using Ad.Factory;

namespace Ad.Service
{
    public class AdFacade : IAdProvider
    {
        private readonly IAdProvider _adProvider;
        private readonly AdDescriptor _adDescriptor;
        private readonly IAdAnalytics _adAnalytics;
        
        public AdFacade(DescriptorHolder descriptorHolder, IAdAnalytics adAnalytics, IAdConsentService adConsentService)
        {
            _adAnalytics = adAnalytics;
            _adDescriptor = descriptorHolder.GetDescriptor<AdDescriptor>();
            _adProvider = AdProviderFactory.CreateProvider(_adDescriptor, adAnalytics, adConsentService);
        }

        public UniTask Init()
        {
            return _adProvider.Init();
        }

        public async UniTask<AdResult> ShowAd(AdType adType, string placement)
        {
            AdResult adResult = await _adProvider.ShowAd(adType, placement);
            _adAnalytics?.SendAdEvent(placement, adResult, adType);
            return adResult;
        }

        public void DestroyBanner()
        {
            _adProvider.DestroyBanner();
        }

        public void Dispose()
        {
            _adProvider?.Dispose();
        }
    }
}