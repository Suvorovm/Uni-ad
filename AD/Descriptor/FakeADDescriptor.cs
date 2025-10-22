using System.Xml.Serialization;
using Ad.Model;

namespace Ad.Descriptor
{
    [XmlType("fakeAd")]
    public class FakeAdDescriptor : IProviderDescriptor
    {
        [XmlAttribute("timoOutLoadingAd")] 
        public float TimeOutLoadingAd { get; set; }
        
        [XmlAttribute("timeShowingReward")] 
        public float TimeShowingReward { get; set; }
        
        [XmlAttribute("resultShowingReward")] 
        public AdResult ResultShowingReward { get; set; }

        [XmlAttribute("resultShowingInterstitial")]
        public AdResult InterstitialInterstitial { get; set; }

        [XmlAttribute("timeShowingInterstitial")]
        public float TimeShowingInterstitial { get; set; }

        [XmlAttribute("pathToDialog")] 
        public string PathToDialog { get; set; }
        
        [XmlAttribute("pathToBanner")]
        public string PathToBanner { get; set; }

        [XmlAttribute("providerId")] 
        public string ProviderId { get; set; }
    }
}