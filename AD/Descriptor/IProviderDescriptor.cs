using System.Xml.Serialization;

namespace Ad.Descriptor
{
    public interface IProviderDescriptor
    {
        [XmlAttribute("providerId")]
        string ProviderId { get; }
    }
}