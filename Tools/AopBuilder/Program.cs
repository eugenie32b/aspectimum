using System;
using System.Collections.Generic;
using System.IO;

namespace AopBuilder
{
    class Program : BuilderSettings
    {
        static void Main(string[] args)
        {
            if (!ProcessOptions(args))
                return;

            if (DoShowHelp || String.IsNullOrEmpty(Source) || String.IsNullOrEmpty(Destination) || String.IsNullOrEmpty(TemplatePath))
            {
                ShowHelp();

                return;
            }

            var processServices = new Dictionary<string, IAopTemplateService>() {
                { ".cs", new AopCsharpTemplateService() },
                { ".js", new AopJsTemplateService() },
                { ".ts", new AopTsTemplateService() },
                { ".sql", new AopSqlTemplateService() },
                { ".css", new AopCssTemplateService() }
            };

            foreach (string sourcePath in GetFilesToProcess())
            {
                if (Verbosity > 1)
                    Console.Out.WriteLine($"Processing file: {sourcePath}");

                string sourceSha256 = Utils.GetSha256(sourcePath);

                string onlyDirectory = Path.GetDirectoryName(sourcePath);
                string extraPath = Source.Length < onlyDirectory.Length ? onlyDirectory.Substring(Source.Length) : "";
                string destinationFilePath = $"{Destination}/{extraPath}/{Path.GetFileName(sourcePath)}";

                if (processServices.TryGetValue(Path.GetExtension(sourcePath).ToLower(), out IAopTemplateService aopTemplateService))
                {
                    if (aopTemplateService.NeedSkipFile(sourcePath))
                        continue;

                    if (!BuilderSettings.ForceRebuild && !aopTemplateService.WasSourceOrTemplateChanged(destinationFilePath, sourceSha256))
                    {
                        Console.Out.WriteLine($"File or templates for {sourcePath} has not changed");
                        return;
                    }

                    aopTemplateService.ProcessFile(sourcePath, destinationFilePath, sourceSha256);
                }
                else
                {
                    Console.Error.WriteLine($"File {sourcePath}: not supported file type: {Path.GetExtension(sourcePath).ToLower()}");
                }
            }
        }

        private static IEnumerable<string> GetFilesToProcess()
        {
            var excludedFiles = new List<string>();

            // find all files that must be excluded
            foreach (string excludeSearchPattern in ExcludeSearchPatterns)
            {
                foreach (string sourcePath in Directory.EnumerateFiles(Source, excludeSearchPattern, SearchOption.AllDirectories))
                {
                    excludedFiles.Add(sourcePath);
                }
            }

            foreach (string fileSearchPattern in FileSearchPatterns)
            {
                foreach (string sourcePath in Directory.EnumerateFiles(Source, fileSearchPattern, SearchOption.AllDirectories))
                {
                    if (Verbosity > 2)
                        Console.Out.WriteLine("Found file: " + sourcePath);

                    // skip if excluded
                    if (excludedFiles.Contains(sourcePath))
                    {
                        if (Verbosity > 1)
                            Console.Out.WriteLine("Skipping excluded file: " + sourcePath);

                        continue;
                    }

                    yield return sourcePath;
                }
            }
        }
    }
}
