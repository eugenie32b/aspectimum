using System;
using System.Xml.Serialization;

namespace AOP.Common
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class AopTemplate : System.Attribute, IDisposable
    {
        [XmlAttribute("template_name")]
        public string TemplateName { get; set; }
        
        [XmlAttribute("priority")]
        public int AdvicePriority { get; set; }
        
        [XmlAttribute("extra_tag")]
        public string ExtraTag { get; set; }
        
        [XmlAttribute("filter_by_name")]
        public string NameFilter { get; set; }
        
        [XmlAttribute("action")]
        public AopTemplateAction Action { get; set; }

        [XmlAttribute("modifier")]
        public AopTemplateModifier Modifier { get; set; }

        [XmlAttribute("AppliedTo")]
        public AopTemplateApplied AppliedTo { get; set; }

        public AopTemplate()
        {
            Action = AopTemplateAction.Default;
            Modifier = AopTemplateModifier.Public;
        }

        public AopTemplate(string templateName, 
                            int aspectPriority = 0, 
                            string extraTag = null, 
                            string nameFilter = null, 
                            AopTemplateAction action = AopTemplateAction.Default, 
                            AopTemplateModifier modifier = AopTemplateModifier.Default)
        {
            TemplateName = templateName;
            AdvicePriority = aspectPriority;
            ExtraTag = extraTag;
            Action = action;
            Modifier = modifier;
            NameFilter = nameFilter;
        }

        public void Dispose()
        {
            // ignore, this interface is used for purpose of being able to include AopTemplate into "using" block.
        }
    }

    [Flags]
    public enum AopTemplateAction
    {
        Ignore = 1,
        IgnoreAll = 2,
        Properties = 4,
        Fields = 8,
        LocalVariables = 16,
        LocalFunctions = 32,
        Methods = 64,
        Classes = 128,
        PostProcessingClasses = 256,
        Namespace = 512,
        Comments = 1024,
        All = 708, // Properties + Methods + Classes + Namespace
        Any = 2048,
        Default = 4096
    }

    [Flags]
    public enum AopTemplateApplied
    {
        Unknown = 0,
        Property = 4,
        Field = 8,
        Method = 16,
        Class = 32,
        Namespace = 64,
        Comment = 128,
    }

    [Flags]
    public enum AopTemplateModifier
    {
        Public = 1,
        Protected = 2,
        Private = 4,
        Virtual = 8,
        Overriden = 16,
        Static = 32,
        Abstract = 64,
        Readonly = 64,
        All = 255,
        Default = 512
    }
}
