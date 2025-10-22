using System.Xml.Serialization;

namespace Ad.Descriptor
{
    [XmlRoot("adConfig")]
    public class AdDescriptor
    {
        [XmlAttribute("adProviderId")]
        public string AdProvider { get; set; }
        
        [XmlElement("fakeAd")]
        public FakeAdDescriptor FakeADDescriptor { get; set; }
        
        [XmlElement("ironSource")]
        public IronSourceDescriptor IronSourceDescriptor { get; set; }
        
        [XmlElement("cleverAd")]
        public CleverAdDescriptor CleverAdDescriptor { get; set; }
        
    }
}