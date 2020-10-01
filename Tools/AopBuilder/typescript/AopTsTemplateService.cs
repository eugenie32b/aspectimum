using Microsoft.VisualStudio.TextTemplating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AopBuilder
{
    public class AopTsTemplateService : AopBaseTemplateService
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
            var regex = new Regex("^\\s*//\\s*##\\s*aspect=(\"[^\"]+\"|[^\"][^ \\n]+)( .*)?$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            string s = regex.Replace(sourceCode.Replace("\r", ""), (m) => m.Value + " guid=" + System.Guid.NewGuid().ToString("N"));

            regex = new Regex("^\\s*//\\s*##\\s*aspect=(\"[^\"]+\"|[^\"][^ \\n]+)( .*)? guid=([0-9A-Fa-f]{32,32})$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            s = regex.Replace(s, (m) =>
            {
                string result;

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
    }
}
