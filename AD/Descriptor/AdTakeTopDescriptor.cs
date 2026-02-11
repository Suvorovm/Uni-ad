using System;
using System.Xml.Serialization;
using Ad.Descriptor;

namespace Ad.Descriptor
{
    [Serializable]
    [XmlType("takeTop")]
    public class AdTakeTopDescriptor : IProviderDescriptor
    {
        [XmlAttribute("providerId")]
        public string ProviderId { get; set; }
    }
}