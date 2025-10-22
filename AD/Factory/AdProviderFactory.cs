using System;
using System.Collections.Generic;
using System.Reflection;
using Ad.Descriptor;
using Ad.Provider;
using Ad.Service;

namespace Ad.Factory
{
    public class AdProviderFactory
    {
        private static readonly
            Dictionary<Type, Func<IProviderDescriptor, IAdAnalytics, IAdConsentService, IAdProvider>>
            _providerRegistry =
                new Dictionary<Type, Func<IProviderDescriptor, IAdAnalytics, IAdConsentService, IAdProvider>>()
                {
#if LEVEL_PLAY_SDK
                    {
                        typeof(IronSourceDescriptor),
                        (descriptor, adAnalytics, adConsentService) =>
                            new IronSourceAdProvider(descriptor as IronSourceDescriptor, adAnalytics)
                    },
#endif

                    {
                        typeof(FakeAdDescriptor),
                        (descriptor, adAnalytics, adConsentService) =>
                            new FakeAdProvider(descriptor as FakeAdDescriptor)
                    },
#if CLEVER_SDK
                    {
                        typeof(CleverAdDescriptor),
                        (descriptor, adAnalytics, adConsentService) =>
                            new CleverAdProvider((CleverAdDescriptor) descriptor, adAnalytics, adConsentService)
                    },
#endif
                    // new providers can be here
                };

        public static IAdProvider CreateProvider(AdDescriptor adDescriptor, IAdAnalytics adAnalytics,
            IAdConsentService adConsentService)
        {
            foreach (PropertyInfo property in typeof(AdDescriptor).GetProperties())
            {
                if (typeof(IProviderDescriptor).IsAssignableFrom(property.PropertyType))
                {
                    IProviderDescriptor descriptor = property.GetValue(adDescriptor) as IProviderDescriptor;
                    if (descriptor != null && descriptor.ProviderId == adDescriptor.AdProvider)
                    {
                        if (_providerRegistry.TryGetValue(property.PropertyType, out var factory))
                        {
                            return factory(descriptor, adAnalytics, adConsentService);
                        }
                    }
                }
            }

            throw new NotSupportedException("No valid ad provider configuration found.");
        }

        public static void RegisterProvider<TDescriptor>(
            Func<IProviderDescriptor, IAdAnalytics, IAdConsentService, IAdProvider> factory)
            where TDescriptor : IProviderDescriptor
        {
            _providerRegistry[typeof(TDescriptor)] = factory;
        }
    }
}