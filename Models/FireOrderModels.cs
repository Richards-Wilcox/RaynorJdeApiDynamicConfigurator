using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace RaynorJdeApi.Models
{
    [DataContract]
    [XmlRoot("fireOrderReturn")]
    public class FireOrderReturn
    {
        [DataMember]
        [XmlElement(ElementName = "success", Order = 0)]
        public string success { get; set; }
        [DataMember]
        [XmlElement(ElementName = "key", Order = 1)]
        public string key { get; set; }
        [DataMember]
        [XmlElement(ElementName = "message", Order = 2)]
        public string message { get; set; }
    }
}
