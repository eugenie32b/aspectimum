using AOP.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AopBuilder
{
    public class AopRewriter
    {
        private readonly Dictionary<string, List<AopTemplate>> _classTemplates;
        private readonly AopCsharpTemplateService _templateService;
        private readonly string _filePath;

        public AopRewriter(string filePath, AopCsharpTemplateService templateService, Dictionary<string, List<AopTemplate>> classTemplates)
        {
            _filePath = filePath;
            _templateService = templateService;
            _classTemplates = classTemplates;
        }

        public IEnumerable<AopTemplate> GetPropertyTemplates(PropertyDeclarationSyntax node)
        {
            ClassDeclarationSyntax classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            NamespaceDeclarationSyntax namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            var propertyTemplates = new List<AopTemplate>();

            var globalTemplate = _templateService.GetGlobalTemplates(_filePath, namespaceDeclaration?.Name?.ToString(), classDeclaration?.Identifier.ToString(), propertyName: node.Identifier.ToString());

            propertyTemplates.AddRange(globalTemplate.Where(w => w.Action == AopTemplateAction.All || w.Action == AopTemplateAction.Properties));

            AddTemplatesFromClass(propertyTemplates, AopTemplateAction.Properties, classDeclaration, node.Identifier.ToString(), node.Modifiers);
            AddTemplatesFromAttributes(propertyTemplates, AopTemplateAction.Properties, node.AttributeLists, AopTemplateAction.Properties);

            if (!String.IsNullOrEmpty(BuilderSettings.OnlyTemplate))
                return propertyTemplates.Where(w => w.TemplateName == BuilderSettings.OnlyTemplate);

            return propertyTemplates.OrderBy(o => o.AdvicePriority);
        }

        public SyntaxNode[] VisitPropertyDeclaration(PropertyDeclarationSyntax node, AopTemplate template)
        {
            var result = new List<SyntaxNode>();

            ClassDeclarationSyntax classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            string propertyName = node.Identifier.ToString();

            Console.Out.WriteLine("Property:  " + propertyName);

            if (BuilderSettings.Verbosity > 1)
                Console.Out.WriteLine(node.ToFullString());

            IEnumerable<SyntaxNode> resultNodes = ProcessTemplate(_templateService, template, node, classDeclaration);
            if (resultNodes != null)
                result.AddRange(resultNodes);

            return result.Count > 0 ? result.ToArray() : null;
        }

        public IEnumerable<AopTemplate> GetMethodTemplates(MethodDeclarationSyntax node)
        {
            ClassDeclarationSyntax classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            NamespaceDeclarationSyntax namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            var methodTemplates = new List<AopTemplate>();

            var globalTemplates = _templateService.GetGlobalTemplates(_filePath, namespaceDeclaration?.Name?.ToString(), classDeclaration?.Identifier.ToString(), methodName: node.Identifier.ToString());

            methodTemplates.AddRange(globalTemplates.Where(w => w.Action == AopTemplateAction.All || w.Action == AopTemplateAction.Methods));

            AddTemplatesFromClass(methodTemplates, AopTemplateAction.Methods, classDeclaration, node.Identifier.ToString(), node.Modifiers);

            AddTemplatesFromAttributes(methodTemplates, AopTemplateAction.Methods, node.AttributeLists, AopTemplateAction.Methods);

            if (!String.IsNullOrEmpty(BuilderSettings.OnlyTemplate))
                return methodTemplates.Where(w => w.TemplateName == BuilderSettings.OnlyTemplate);

            return methodTemplates.OrderBy(o => o.AdvicePriority);
        }

        public SyntaxNode[] VisitMethodDeclaration(MethodDeclarationSyntax node, AopTemplate template)
        {
            var result = new List<SyntaxNode>();

            ClassDeclarationSyntax classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            string methodName = node.Identifier.ToString();

            Console.Out.WriteLine("Method:  " + methodName);

            if (BuilderSettings.Verbosity > 2)
                Console.Out.WriteLine(node.ToFullString());

            IEnumerable<SyntaxNode> resultNodes = ProcessTemplate(_templateService, template, node, classDeclaration);
            if (resultNodes != null)
                result.AddRange(resultNodes);

            return result.Count > 0 ? result.ToArray() : null;
        }

        public static T ProcessTemplates<T>(AopCsharpTemplateService templateService, List<AopTemplate> templates, T node, ClassDeclarationSyntax classDeclaration) where T : SyntaxNode
        {
            T result = node;

            if (templates.Count == 0)
                return result;

            string startingWhitespace = "";
            if (node.HasLeadingTrivia)
                startingWhitespace = node.GetLeadingTrivia().ToFullString();

            string closingWhitespace = "";
            if (node.HasTrailingTrivia)
                closingWhitespace = node.GetTrailingTrivia().ToFullString();

            foreach (AopTemplate template in templates.OrderBy(o => o.AdvicePriority))
            {
                Console.Out.WriteLine($"\tProcessing template {template.TemplateName}");

                string sourceCode = templateService.ProcessTemplate(template.TemplateName, new Dictionary<string, object>() {
                    { "ClassNode", result is ClassDeclarationSyntax ? result as ClassDeclarationSyntax : classDeclaration },
                    { "MethodNode", result is MethodDeclarationSyntax ? result : null },
                    { "PropertyNode", result is PropertyDeclarationSyntax ? result : null },
                    { "StatementNode", null },
                    { "SyntaxNode", result },
                    { "AppliedTo", template.AppliedTo.ToString() },
                    { "ExtraTag", template.ExtraTag }
                });

                // if sourceCode is null, it means no changes were done to original code and we keep it as-is
                if (sourceCode == null)
                    continue;

                SyntaxNode compUnit = SyntaxFactory.ParseCompilationUnit(startingWhitespace + sourceCode.Trim(' ', '\r', '\n') + closingWhitespace);

                result = compUnit.DescendantNodes().OfType<T>().FirstOrDefault();

                if (result == null)
                {
                    throw (new Exception("Cannot parse generated source code as a " + typeof(T).Name));
                }
            }

            return result;
        }

        public static IEnumerable<SyntaxNode> ProcessTemplate(AopCsharpTemplateService templateService, AopTemplate template, SyntaxNode node, ClassDeclarationSyntax classDeclaration)
        {
            string startingWhitespace = "";
            if (node.HasLeadingTrivia)
                startingWhitespace = node.GetLeadingTrivia().ToFullString();

            string closingWhitespace = "";
            if (node.HasTrailingTrivia)
                closingWhitespace = node.GetTrailingTrivia().ToFullString();

            Console.Out.WriteLine($"\tProcessing template {template.TemplateName}");

            string sourceCode = templateService.ProcessTemplate(template.TemplateName, new Dictionary<string, object>() {
                    { "ClassNode", classDeclaration },
                    { "MethodNode", node is MethodDeclarationSyntax ? node : null },
                    { "PropertyNode", node is PropertyDeclarationSyntax ? node : null },
                    { "SyntaxNode", node },
                    { "AppliedTo", template.AppliedTo.ToString() },
                    { "StatementNode", null },
                    { "ExtraTag", template.ExtraTag }
                });

            // if sourceCode is null, it means no changes were done to original code and we keep it as-is
            if (sourceCode == null)
                return new List<SyntaxNode> { node };

            SyntaxNode compUnit = SyntaxFactory.ParseCompilationUnit(startingWhitespace + sourceCode.Trim(' ', '\r', '\n') + closingWhitespace);

            return compUnit.ChildNodes();
        }

        public static void AddTemplatesFromAttributes(List<AopTemplate> templates, AopTemplateAction propertyAction, SyntaxList<AttributeListSyntax> attributeLists, AopTemplateAction defaultAs)
        {
            foreach (var template in Utils.GetAopTemplates(attributeLists))
            {
                if (template.Action == AopTemplateAction.Default)
                    template.Action = defaultAs;

                if (template.Action == AopTemplateAction.IgnoreAll)
                {
                    templates.Clear();
                }
                else if (template.Action == AopTemplateAction.Ignore)
                {
                    templates.RemoveTemplateByName(template.TemplateName);
                }
                else if (template.Action == AopTemplateAction.All || template.Action == propertyAction)
                {
                    templates.UpdateTemplate(template);
                }
            }
        }

        private void AddTemplatesFromClass(List<AopTemplate> templates, AopTemplateAction templateAction, ClassDeclarationSyntax classDeclaration, string currentName, SyntaxTokenList modifiers)
        {
            if (!_classTemplates.TryGetValue(classDeclaration.Identifier.ToString(), out List<AopTemplate> classTemplates))
                return;

            foreach (var classTemplate in classTemplates)
            {
                if (classTemplate.Action == AopTemplateAction.IgnoreAll)
                {
                    templates.Clear();
                }
                else if (classTemplate.Action == AopTemplateAction.Ignore)
                {
                    templates.RemoveTemplateByName(classTemplate.TemplateName);
                }
                else if (classTemplate.Action == AopTemplateAction.All || classTemplate.Action == templateAction)
                {
                    if (String.IsNullOrEmpty(classTemplate.NameFilter) || Regex.IsMatch(currentName, classTemplate.NameFilter, RegexOptions.IgnoreCase))
                    {
                        if (classTemplate.Modifier == AopTemplateModifier.All
                            || (((classTemplate.Modifier & AopTemplateModifier.Public) > 0 || (classTemplate.Modifier == AopTemplateModifier.Default)) && modifiers.Any(w => w.Kind() == SyntaxKind.PublicKeyword))
                            || ((classTemplate.Modifier & AopTemplateModifier.Protected) > 0 && modifiers.Any(w => w.Kind() == SyntaxKind.ProtectedKeyword))
                            || ((classTemplate.Modifier & AopTemplateModifier.Private) > 0 && modifiers.Any(w => w.Kind() == SyntaxKind.PrivateKeyword)))
                        {
                            templates.UpdateTemplate(classTemplate);
                        }
                    }
                }
            }
        }
    }
}
