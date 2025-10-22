using System.Collections.Generic;
using Ad.Model;

namespace Ad.Service
{
    public interface IAdAnalytics
    {
        void SendAdEvent(string placement, AdResult result, AdType adType);

        void SendEvent(string eventName, Dictionary<string, object> analyticsParams);

        void AdRevenue(Dictionary<string, object> analyticsParams);
        
    }
}