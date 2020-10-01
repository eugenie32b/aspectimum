using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace AopBuilder
{
    abstract class BuilderSettings
    {
        public static AopBuilderConfig Config { get; set; } = new AopBuilderConfig();

        public static List<string> FileSearchPatterns = new List<string>();
        public static List<string> ExcludeSearchPatterns = new List<string>();

        public static Dictionary<string, string> GlobalTag = new Dictionary<string, string>();

        public static int Verbosity = 1;

        public static bool ForceRebuild;

        public static string Source;
        public static string Destination;
        public static string TemplatePath;

        public static string OnlyTemplate;

        public static bool DoShowHelp;

        private static OptionSet OptionParams;

        protected static bool ProcessOptions(string[] args)
        {
            OptionParams = new OptionSet() {
                                        { "s|source=", "path of a source project",  v => Source = new FileInfo(v).FullName },
                                        { "d|destination=", "path of a destination project", v => Destination = new FileInfo(v).FullName },
                                        { "t|template=", "path of a template folder", v => TemplatePath = new FileInfo(v).FullName },
                                        { "f|force", "force rebuild all sources", v => ForceRebuild = true },
                                        { "c|config=", "load configuration from a file", v => LoadConfig(v) },
                                        { "o|only=", "ignore all templates except specified in the option (for debug purposes)", v => OnlyTemplate = v },
                                        { "g|global_tag=", "add global tag to all processed templates", v => AddGlobalTag(v) },
                                        { "x|exclude_pattern=", "add search pattern for files to be excluded from processing", v => ExcludeSearchPatterns.Add(v) },
                                        { "v|verbose", "increase debug message verbosity", v => { if (v != null) ++Verbosity; } },
                                        { "h|help",  "show this message and exit", v => DoShowHelp = v != null },
                                    };

            try
            {
                FileSearchPatterns.AddRange(OptionParams.Parse(args));
            }
            catch (OptionException e)
            {
                Console.Out.Write("AopBuilder: ");
                Console.Out.WriteLine(e.Message);
                Console.Out.WriteLine("Try 'AopBuilder --help' for more information.");

                return false;
            }

            if (FileSearchPatterns.Count == 0)
                FileSearchPatterns.Add("*.cs");

            return true;
        }

        private static void AddGlobalTag(string s)
        {
            if (String.IsNullOrEmpty(s))
                return;

            int p = s.IndexOf('=');
            if (p < 0)
                GlobalTag[s] = "";
            else
                GlobalTag[s.Substring(0, p)] = s.Substring(p + 1);
        }

        private static void LoadConfig(string configFilePath)
        {
            if (Verbosity > 2)
            {
                Console.Out.WriteLine("Config:");
                Console.Out.WriteLine(File.ReadAllText(configFilePath));
            }

            Config = Utils.DeSerializeFromXml<AopBuilderConfig>(File.ReadAllText(configFilePath));
        }

        protected static void ShowHelp()
        {
            Console.Out.WriteLine("Usage: AopBuilder [-s=source_path] [-d=destination_path] [-t=template_path] [search pattern]");
            Console.Out.WriteLine("");

            foreach (var v in OptionParams)
            {
                Console.Out.WriteLine($"{v.Prototype.TrimEnd('=')}: {v.Description}");
            }

            Console.Out.WriteLine("");
            Console.Out.WriteLine("[search pattern]: patterns to include file(s) for processing, if not specified assume *.cs");
        }
    }
}
