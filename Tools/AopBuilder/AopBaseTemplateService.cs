using Microsoft.VisualStudio.TextTemplating;
using Mono.TextTemplating;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AopBuilder
{
    public interface IAopTemplateService
    {
        bool NeedSkipFile(string sourcePath);
        bool WasSourceOrTemplateChanged(string destinationFilePath, string sourceCodeSha256);
        void ProcessFile(string sourcePath, string destinationFilePath, string sourceSha256);
    }

    public abstract class AopBaseTemplateService : IAopTemplateService
    {
        protected Dictionary<string, string> _usedTemplates = new Dictionary<string, string>();

        protected TemplateGenerator _t4Generator;
        protected TemplateGenerator T4Generator
        {
            get
            {
                if (_t4Generator != null)
                    return _t4Generator;

                _t4Generator = new TemplateGenerator();
                _t4Generator.IncludePaths.Add(BuilderSettings.TemplatePath);

                return _t4Generator;
            }
        }

        protected Dictionary<string, AopTemplateInfo> _templates = new Dictionary<string, AopTemplateInfo>();
        protected Dictionary<string, string> _templateHashes = new Dictionary<string, string>();

        public string ProcessTemplate(string name, Dictionary<string, object> sessionParams)
        {
            CompiledTemplate t4gen = GetTemplate(name, sessionParams);

            string result = t4gen.Process();

            ShowErrors(name);

            if (BuilderSettings.Verbosity > 1)
            {
                Console.Out.WriteLine("Generated source code:");
                Console.Out.WriteLine(result == null ? "[No code was generated]" : result);
            }

            return result;
        }

        private CompiledTemplate GetTemplate(string name, Dictionary<string, object> sessionParams)
        {
            var session = T4Generator.GetOrCreateSession();

            if (sessionParams != null && sessionParams.Count > 0)
            {
                foreach (string key in sessionParams.Keys)
                {
                    session[key] = sessionParams[key];
                }
            }

            foreach (string key in session.Keys.ToArray())
            {
                if (session[key] == null)
                    session.Remove(key);
            }

            name = Path.GetFileNameWithoutExtension(name);

            if (!_templates.TryGetValue(name, out AopTemplateInfo template))
            {
                string fileName = GetTemplateFileName(name);

                string templ = File.ReadAllText(fileName);
                _templateHashes[name] = Utils.GetSha256(fileName);

                template = new AopTemplateInfo();

                template.CompiledTemplate = T4Generator.CompileTemplate(templ);

                ShowErrors();

                _templates[name] = template;

                // find all dependent templates
                var regEx = new Regex("<\\#@\\s+include\\s+file=\"([^\"]+)\"\\s+\\#>");

                foreach (Match match in regEx.Matches(templ))
                {
                    string t = match.Groups[1].Value;
                    template.DependentOnTemplates.Add(t);
                    if (!_templateHashes.ContainsKey(t))
                        _templateHashes[t] = Utils.GetSha256(GetTemplateFileName(t));
                }
            }

            _usedTemplates[name] = _templateHashes[name];
            template.DependentOnTemplates.ForEach(s => _usedTemplates[s] = _templateHashes[s]);

            return template.CompiledTemplate;
        }

        private bool ShowErrors(string templateName = null)
        {
            if (T4Generator.Errors.Count <= 0)
                return false;

            foreach (var error in T4Generator.Errors.OfType<CompilerError>())
            {
                Console.Out.WriteLine("error: " + error.ErrorText);
            }

            if (BuilderSettings.Verbosity > 1 && !String.IsNullOrEmpty(templateName))
            {
                string templateSourceCode = GetTemplateSourceCode(templateName);
                Console.Out.WriteLine(templateSourceCode);
            }

            return true;
        }

        public string GetTemplateSourceCode(string name)
        {
            string sourceCode = File.ReadAllText(GetTemplateFileName(name));
            var host = T4Generator as ITextTemplatingEngineHost;

            ParsedTemplate pt = ParsedTemplate.FromText(sourceCode, T4Generator);
            if (pt.Errors.HasErrors)
            {
                host.LogErrors(pt.Errors);
                return null;
            }

            TemplateSettings settings = TemplatingEngine.GetSettings(T4Generator, pt);

            var ccu = TemplatingEngine.GenerateCompileUnit(host, sourceCode, pt, settings);

            var opts = new CodeGeneratorOptions();
            using (var writer = new StringWriter())
            {
                settings.Provider.GenerateCodeFromCompileUnit(ccu, writer, opts);
                return writer.ToString();
            }
        }

        protected string GetTemplateFileName(string name)
        {
            string fileName = BuilderSettings.TemplatePath + $"/{name}";
            if (!Regex.IsMatch(name, @"\.[a-z0-9]+$", RegexOptions.IgnoreCase))
                fileName += ".t4";

            return fileName;
        }

        public string GetTemplateSha256(string name)
        {
            if (_templateHashes.TryGetValue(name, out string sha256))
                return sha256;

            string fileName = BuilderSettings.TemplatePath + $"/{name}";
            if (!Regex.IsMatch(name, @"\.[a-z0-9]+$", RegexOptions.IgnoreCase))
                fileName += ".t4";

            if (File.Exists(fileName))
            {
                sha256 = Utils.GetSha256(fileName);

                _templateHashes[name] = sha256;

                return sha256;
            }

            return null;
        }

        public ITextTemplatingSession SetGeneralSessionValues(string sourcePath, string sourceSha256)
        {
            _usedTemplates.Clear();

            ITextTemplatingSession session = T4Generator.GetOrCreateSession();

            session["FileName"] = Path.GetFileName(sourcePath);
            session["FilePath"] = sourcePath;
            session["User"] = Environment.UserName;
            session["FileSha256"] = sourceSha256;
            session["Now"] = DateTime.Now.ToString("o");
            session["MachineName"] = Environment.MachineName;

            session["GlobalTag"] = BuilderSettings.GlobalTag;

            Action<string> log = (s) => Console.Out.WriteLine(s);
            session["Log"] = log;

            Action<string> logError = (s) => Console.Error.WriteLine(s);
            session["LogError"] = logError;

            Action<string> logDebug = (s) =>
            {
                if (BuilderSettings.Verbosity > 1)
                    Console.Out.WriteLine(s);
            };

            session["LogDebug"] = logDebug;


            session["ExtraTag"] = null;

            return session;
        }

        public virtual bool WasSourceOrTemplateChanged(string destinationFilePath, string sourceCodeSha256)
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

        protected virtual string GetTemplateHashes()
        {
            var templateHashes = new StringBuilder();

            templateHashes.AppendLine().AppendLine();

            foreach (string key in _usedTemplates.Keys)
            {
                templateHashes.AppendLine($"// ##template={key} sha256={_usedTemplates[key]}");
            }

            return templateHashes.ToString();
        }

        public virtual void ProcessFile(string sourcePath, string destinationFilePath, string sourceSha256)
        {
            string sourceCode = File.ReadAllText(sourcePath);

            string newSourceCode = null;

            try
            {
                SetStartSession(sourcePath, sourceSha256);

                // step 1. process comments
                newSourceCode = InternalProcessFile(sourcePath, sourceCode);

                if (BuilderSettings.Verbosity > 2)
                {
                    Console.WriteLine("New source code:");
                    Console.WriteLine(newSourceCode);
                }

                newSourceCode += GetTemplateHashes();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in file: {sourcePath}: {ex.Message}");
                if (BuilderSettings.Verbosity > 1)
                    Console.Error.WriteLine(ex.StackTrace);
            }

            File.WriteAllText(destinationFilePath, newSourceCode);
        }

        public virtual bool NeedSkipFile(string sourcePath)
        {
            return false;
        }

        protected abstract string InternalProcessFile(string sourcePath, string sourceCode);
        protected abstract void SetStartSession(string sourcePath, string sourceSha256);

        public class AopTemplateInfo
        {
            public CompiledTemplate CompiledTemplate { get; set; }
            public List<string> DependentOnTemplates { get; set; } = new List<string>();
        }
    }
}
