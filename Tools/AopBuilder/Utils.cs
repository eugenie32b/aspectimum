using AOP.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace AopBuilder
{
    public static class Utils
    {
        public static List<AopTemplate> GetAopTemplates(SyntaxList<AttributeListSyntax> attributeLists)
        {
            var templates = new List<AopTemplate>();

            foreach (AttributeListSyntax attrList in attributeLists)
            {
                foreach (AttributeSyntax attr in attrList.Attributes)
                {
                    if (BuilderSettings.Verbosity > 1)
                        Console.Out.WriteLine($"\tFound atribute {attr.Name}");

                    if (attr.Name.ToFullString() == "AopTemplate")
                    {
                        var aopTemplate = new AopTemplate();
                        templates.Add(aopTemplate);

                        int i = 0;

                        if (attr.ArgumentList != null)
                        {
                            foreach (AttributeArgumentSyntax arg in attr.ArgumentList.Arguments)
                            {
                                if (BuilderSettings.Verbosity > 1)
                                    Console.Out.WriteLine($"\tArgument {arg.ToFullString()}");

                                string argName;

                                if (arg.NameEquals != null)
                                    argName = arg.NameEquals.ToString().TrimEnd(':', '=', ' ').ToLower();
                                else if (arg.NameColon != null)
                                    argName = arg.NameColon.Name.ToString().TrimEnd(':', '=', ' ').ToLower();
                                else
                                    argName = _aopTemplateConstructor[i].ToLower();

                                UpdateTemplateFromArgument(argName, aopTemplate, arg.Expression);

                                i++;
                            }
                        }
                    }
                }
            }

            if (!String.IsNullOrEmpty(BuilderSettings.OnlyTemplate))
                return templates.Where(w => w.TemplateName == BuilderSettings.OnlyTemplate).ToList();

            return templates;
        }

        private static void UpdateTemplateFromArgument(string argName, AopTemplate aopTemplate, ExpressionSyntax expression)
        {
            string v = expression.ToString().Trim('"');

            switch (argName)
            {
                case "templatename":
                    aopTemplate.TemplateName = v;
                    break;

                case "advicepriority":
                    aopTemplate.AdvicePriority = Int32.Parse(v);
                    break;

                case "namefilter":
                    aopTemplate.NameFilter = v;
                    break;

                case "extratag":
                    aopTemplate.ExtraTag = v;
                    break;

                case "action":
                    v = v.Replace("AopTemplateAction.", "");
                    aopTemplate.Action = (AopTemplateAction)Enum.Parse(typeof(AopTemplateAction), v);
                    break;

                case "modifier":
                    v = v.Replace("AopTemplateModifier.", "");
                    aopTemplate.Modifier = (AopTemplateModifier)Enum.Parse(typeof(AopTemplateModifier), v);
                    break;

                default:
                    Console.Error.WriteLine($"AopTemplate unsupported attribute {argName}");
                    break;
            }
        }

        private static readonly Dictionary<int, string> _aopTemplateConstructor = new Dictionary<int, string>() {
            { 0, "templateName" },
            { 1, "aspectPriority" },
            { 2, "extraTag" },
            { 3, "nameFilter" },
            { 4, "action" },
            { 5, "modifier" },
        };

        public static AopTemplate GetAopTemplate(ArgumentListSyntax argumentList)
        {
            var aopTemplate = new AopTemplate();

            int i = 0;
            foreach (ArgumentSyntax arg in argumentList.Arguments)
            {
                if (BuilderSettings.Verbosity > 1)
                    Console.Out.WriteLine($"\tArgument {arg.ToFullString()}");

                string argName = (arg.NameColon == null ? _aopTemplateConstructor[i] : arg.NameColon.ToString().TrimEnd(':', '=', ' ')).ToLower();

                UpdateTemplateFromArgument(argName, aopTemplate, arg.Expression);

                i++;
            }

            return aopTemplate;
        }

        public static string GetSha256(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sha2 = new SHA256CryptoServiceProvider())
                {
                    var hash = sha2.ComputeHash(fs);
                    var hashStr = Convert.ToBase64String(hash);

                    return hashStr.TrimEnd('=');
                }
            }
        }

        public static T DeSerializeFromXml<T>(string xml)
        {
            using (var textReader = new StringReader(xml))
            {
                var deserializer = new XmlSerializer(typeof(T));
                T result = (T)deserializer.Deserialize(textReader);

                return result;
            }
        }
    }
}
