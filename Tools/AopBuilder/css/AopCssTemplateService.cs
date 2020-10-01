using Microsoft.VisualStudio.TextTemplating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AopBuilder
{
    public class AopCssTemplateService : AopBaseTemplateService
    {
        protected override string InternalProcessFile(string sourcePath, string sourceCode)
        {
            // step 1. process comments
            string newSourceCode = ProcessAspectComments(sourceCode);

            return newSourceCode;
        }

        protected override void SetStartSession(string sourcePath, string sourceSha256)
        {
            ITextTemplatingSession session = SetGeneralSessionValues(sourcePath, sourceSha256);

            session["Source"] = File.ReadAllText(sourcePath);
        }

        private string ProcessAspectComments(string sourceCode)
        {
            var regex = new Regex("^\\s*/\\*\\s*##\\s*aspect=(\"[^\"]+\"|[^\"][^ \\n]+)( .*)?\\s*\\*\\/\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            string s = regex.Replace(sourceCode.Replace("\r", ""), (m) =>
            {
                string result = "";

                try
                {
                    var sessionData = new Dictionary<string, object>() {
                        { "ExtraTag", m.Groups[2].Value.Trim() },
                        { "GlobalTag", BuilderSettings.GlobalTag  }
                    };

                    string templateName = m.Groups[1].Value.Trim('"');

                    result = ProcessTemplate(templateName, sessionData);

                    if (result != null)
                        result = result.Trim(' ', '\r', '\n');
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine(ex);
                    throw;
                }

                return result;
            });

            return s;
        }

        public override bool WasSourceOrTemplateChanged(string destinationFilePath, string sourceCodeSha256)
        {
            if (!File.Exists(destinationFilePath))
                return true;

            string oldGeneratedSourceCode = File.ReadAllText(destinationFilePath).Replace("\r", "");

            Match m = Regex.Match(oldGeneratedSourceCode, "^\\s*\\/\\/\\s*##sha256:\\s*([^ \\n]+)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!m.Success || sourceCodeSha256 != m.Groups[1].Value)
                return true;

            foreach (Match tm in Regex.Matches(oldGeneratedSourceCode, "^\\s*\\/\\/\\s*##template=([^ ]+)\\s+sha256=([^ \\n]+)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                string templateSha256 = GetTemplateSha256(tm.Groups[1].Value);

                if (String.IsNullOrEmpty(templateSha256) || templateSha256 != tm.Groups[2].Value)
                    return true;
            }

            return false;
        }

        protected override string GetTemplateHashes()
        {
            var templateHashes = new StringBuilder();

            templateHashes.AppendLine().AppendLine();

            foreach (string key in _usedTemplates.Keys)
            {
                templateHashes.AppendLine($"/* ##template={key} sha256={_usedTemplates[key]} */");
            }

            return templateHashes.ToString();
        }
    }
}
