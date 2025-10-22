using System.Xml.Serialization;

namespace Ad.Descriptor
{
    [XmlType("cleverAd")]
    public class CleverAdDescriptor : IProviderDescriptor
    {
        [XmlAttribute("providerId")]
        public string ProviderId { get; set; }
        
        [XmlAttribute("tokenId")]
        public string TokenId { get; set; }
        
        [XmlAttribute("testDevice")]
        public bool TestDevice { get; set; }
        
        [XmlAttribute("testDeviceId")]
        public string TestDeviceId { get; set; }
        
        [XmlAttribute("tenjinKey")]
        public string TenjinKey { get; set; }
    }
}