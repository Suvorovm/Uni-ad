using System;
using System.Xml.Serialization;

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