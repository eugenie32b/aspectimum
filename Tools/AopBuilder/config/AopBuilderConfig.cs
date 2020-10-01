using System.Xml.Serialization;

namespace AopBuilder
{
    [XmlRoot("config")]
    public class AopBuilderConfig
    {
        [XmlArray("aspects")]
        [XmlArrayItem("aspect")]
        public AspectConfig[] Aspects { get; set; } = new AspectConfig[0];
    }
}
