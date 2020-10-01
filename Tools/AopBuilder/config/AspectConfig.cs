using AOP.Common;
using System.Xml.Serialization;

namespace AopBuilder
{
    public class AspectConfig
    {
        [XmlArray("filters")]
        [XmlArrayItem("filter")]
        public FilterConfig[] Filters { get; set; } = new FilterConfig[0];

        [XmlElement("advice")]
        public AopTemplate[] Templates { get; set; } = new AopTemplate[0];
    }
}
