using System.Xml.Serialization;

namespace AopBuilder
{
    public class FilterConfig
    {
        [XmlAttribute("pattern")]
        public string Pattern { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("action")]
        public string Action { get; set; }
    }
}
