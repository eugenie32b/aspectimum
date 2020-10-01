using AOP.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TextTemplating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AopBuilder
{
    public class AopCsharpTemplateService : AopBaseTemplateService
    {
        private readonly Regex _objBin = new Regex(@"[/\\](bin|obj)[/\\]", RegexOptions.IgnoreCase);
        private List<string> _requiredUsings;

        public override bool NeedSkipFile(string sourcePath)
        {
            // don't process bin and obj folders
            if (_objBin.IsMatch(sourcePath))
            {
                if (BuilderSettings.Verbosity > 1)
                    Console.Out.WriteLine($"Ignoring file {sourcePath}");

                return true;
            }

            return false;
        }

        protected override string InternalProcessFile(string sourcePath, string sourceCode)
        {
            // step 1. process comments
            string newSourceCode = ProcessAspectComments(sourceCode);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(newSourceCode);

            AopWalker walker = GetClassTemplates(tree);

            // step 2. rewrite source code for classes
            {
                var classRewriter = new AopClassRewriter(sourcePath, this);

                SyntaxNode newNode = classRewriter.Visit(tree.GetRoot());
                newSourceCode = newNode.ToFullString();

                tree = CSharpSyntaxTree.ParseText(newSourceCode);
            }

            // step 3. rewrite source code for methods and properties
            {
                var rewriter = new AopRewriter(sourcePath, this, walker.ClassTemplates);

                SyntaxNode newNode = tree.GetRoot();

                var methodNames = newNode.DescendantNodes().OfType<MethodDeclarationSyntax>().Select(s => s.Identifier.ValueText);

                foreach (string methodName in methodNames)
                {
                    MethodDeclarationSyntax method = newNode.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(w => w.Identifier.ValueText == methodName);
                    if (method == null)
                        continue;

                    foreach (AopTemplate template in rewriter.GetMethodTemplates(method))
                    {
                        method = newNode.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(w => w.Identifier.ValueText == methodName);
                        if (method == null)
                            continue;

                        template.AppliedTo = AopTemplateApplied.Method;
                        SyntaxNode[] result = rewriter.VisitMethodDeclaration(method, template);

                        newNode = newNode.ReplaceNode(method, result);
                    }
                }

                var propertyNames = newNode.DescendantNodes().OfType<PropertyDeclarationSyntax>().Select(s => s.Identifier.ValueText);

                foreach (string propertyName in propertyNames)
                {
                    PropertyDeclarationSyntax property = newNode.DescendantNodes().OfType<PropertyDeclarationSyntax>().FirstOrDefault(w => w.Identifier.ValueText == propertyName);
                    if (property == null)
                        continue;

                    foreach (AopTemplate template in rewriter.GetPropertyTemplates(property))
                    {
                        property = newNode.DescendantNodes().OfType<PropertyDeclarationSyntax>().FirstOrDefault(w => w.Identifier.ValueText == propertyName);
                        if (property == null)
                            continue;

                        template.AppliedTo = AopTemplateApplied.Property;
                        SyntaxNode[] result = rewriter.VisitPropertyDeclaration(property, template);

                        newNode = newNode.ReplaceNode(property, result);
                    }
                }

                newSourceCode = newNode.ToFullString();


                tree = CSharpSyntaxTree.ParseText(newSourceCode);
            }

            // step 4. rewrite source code for using blocks
            {
                var rewriter = new AopUsingRewriter();

                SyntaxNode newNode = rewriter.Visit(tree.GetRoot());

                newSourceCode = newNode.ToFullString();

                tree = CSharpSyntaxTree.ParseText(newSourceCode);
            }

            // step 5. rewrite source code for postprocessing classes templates
            {
                var classRewriter = new AopClassRewriter(sourcePath, this)
                {
                    Action = AopTemplateAction.PostProcessingClasses
                };

                SyntaxNode newNode = classRewriter.Visit(tree.GetRoot());
                newSourceCode = newNode.ToFullString();
            }

            // step 6: cleanup AopTemplate attributes and using
            {
                tree = CSharpSyntaxTree.ParseText(newSourceCode);

                SyntaxNode node = tree.GetRoot();

                {
                    AttributeListSyntax oldNode;
                    while ((oldNode = node.DescendantNodes().OfType<AttributeListSyntax>().FirstOrDefault(w => w.Attributes.Any(a => a.Name.ToFullString() == "AopTemplate"))) != null)
                    {
                        node = node.RemoveNode(oldNode, SyntaxRemoveOptions.KeepLeadingTrivia);
                    }
                }

                {
                    UsingDirectiveSyntax oldNode;
                    while ((oldNode = node.DescendantNodes().OfType<UsingDirectiveSyntax>().FirstOrDefault(w => w.Name.ToFullString() == "AOP.Common")) != null)
                    {
                        node = node.RemoveNode(oldNode, SyntaxRemoveOptions.KeepLeadingTrivia);
                    }
                }

                // make sure the class has all required usings 
                if (_requiredUsings.Count > 0)
                {
                    for (int i = 0; i < _requiredUsings.Count; i++)
                    {
                        _requiredUsings[i] = _requiredUsings[i].Trim(';');
                    }

                    foreach (UsingDirectiveSyntax usingNode in node.DescendantNodes().OfType<UsingDirectiveSyntax>())
                    {
                        string usingName = usingNode.Name.ToFullString();
                        if (_requiredUsings.Contains(usingName))
                            _requiredUsings.Remove(usingName);
                    }

                    SyntaxNode insertNode = node.ChildNodes().LastOrDefault(w => w.Kind() == SyntaxKind.UsingDirective);
                    bool insertAfter = true;

                    if (insertNode == null)
                    {
                        insertNode = node.ChildNodes().First();
                        insertAfter = false;
                    }

                    foreach (string requiredUsing in _requiredUsings)
                    {
                        var usingNode = new List<SyntaxNode>() { SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(requiredUsing)) };

                        if (insertAfter)
                            node = node.InsertNodesAfter(insertNode, usingNode);
                        else
                            node = node.InsertNodesBefore(insertNode, usingNode);
                    }
                }

                newSourceCode = node.NormalizeWhitespace().ToFullString();
            }

            return newSourceCode;
        }

        protected override void SetStartSession(string sourcePath, string sourceSha256)
        {
            ITextTemplatingSession session = SetGeneralSessionValues(sourcePath, sourceSha256);

            session["ClassNode"] = null;
            session["MethodNode"] = null;
            session["PropertyNode"] = null;

            _requiredUsings = new List<string>();
            session["RequiredUsing"] = _requiredUsings;
        }

        public IEnumerable<AopTemplate> GetGlobalTemplates(string filePath, string namespaceName, string className, string methodName = null, string propertyName = null)
        {
            var templates = new List<AopTemplate>();

            if (BuilderSettings.Config == null || BuilderSettings.Config.Aspects == null) 
                return templates;

            foreach (var aspectConfig in BuilderSettings.Config.Aspects)
            {
                if (aspectConfig.Templates != null && IsIncluded(aspectConfig.Filters, filePath, namespaceName, className, methodName, propertyName))
                {
                    foreach (var templateToAdd in aspectConfig.Templates)
                    {
                        templates.UpdateTemplate(templateToAdd);
                    }
                }
            }

            return templates;
        }

        private bool IsIncluded(FilterConfig[] filters, string filePath, string namespaceName, string className, string methodName, string propertyName)
        {
            if (filters == null || filters.Length == 0)
                return true;

            foreach (FilterConfig filter in filters.Where(w => !String.IsNullOrEmpty(w.Pattern)))
            {
                if(Regex.IsMatch(filePath, filter.Pattern, RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }

        private AopWalker GetClassTemplates(SyntaxTree tree)
        {
            var walker = new AopWalker();

            walker.Visit(tree.GetRoot());

            return walker;
        }

        private string ProcessAspectComments(string sourceCode)
        {
            var regex = new Regex("^\\s*//\\s*##\\s*aspect=(\"[^\"]+\"|[^\"][^ \\n]+)( .*)?$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            string s = regex.Replace(sourceCode.Replace("\r", ""), (m) => m.Value + " guid=" + System.Guid.NewGuid().ToString("N"));

            regex = new Regex("^\\s*//\\s*##\\s*aspect=(\"[^\"]+\"|[^\"][^ \\n]+)( .*)? guid=([0-9A-Fa-f]{32,32})$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(s);

            var declarations = new Dictionary<string, Tuple<ClassDeclarationSyntax, MethodDeclarationSyntax, PropertyDeclarationSyntax>>();

            foreach (SyntaxTrivia trivia in tree.GetRoot().DescendantTrivia().Where(w => w.IsKind(SyntaxKind.SingleLineCommentTrivia)))
            {
                Match m = regex.Match(trivia.ToFullString());
                if (m.Success && trivia.Token != null && trivia.Token.Parent != null)
                {
                    var classDeclaration = trivia.Token.Parent.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    var methodDeclaration = trivia.Token.Parent.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    var propertyDeclaration = trivia.Token.Parent.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();

                    declarations[m.Groups[3].Value] = new Tuple<ClassDeclarationSyntax, MethodDeclarationSyntax, PropertyDeclarationSyntax>(classDeclaration, methodDeclaration, propertyDeclaration);
                }
            }

            s = regex.Replace(s, (m) =>
            {
                string result = "";

                try
                {
                    var sessionData = new Dictionary<string, object>() {
                        { "ExtraTag", m.Groups[2].Value.Trim() },
                        { "GlobalTag", BuilderSettings.GlobalTag  }
                    };

                    if (declarations.TryGetValue(m.Groups[3].Value, out Tuple<ClassDeclarationSyntax, MethodDeclarationSyntax, PropertyDeclarationSyntax> declaration))
                    {
                        sessionData["ClassNode"] = declaration.Item1;
                        sessionData["MethodNode"] = declaration.Item2;
                        sessionData["PropertyNode"] = declaration.Item3;
                    }

                    string templateName = m.Groups[1].Value.Trim('"');


                    if (!String.IsNullOrEmpty(BuilderSettings.OnlyTemplate) && templateName != BuilderSettings.OnlyTemplate)
                        return "";

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

    public static class AopCsharpTemplateExt
    {
        public static void UpdateTemplate(this List<AopTemplate> templates, AopTemplate template)
        {
            if (templates == null || template == null)
                return;

            templates.RemoveTemplateByName(template.TemplateName);
            templates.Add(template);
        }

        public static void RemoveTemplateByName(this List<AopTemplate> templates, string templateName)
        {
            if (templates == null)
                return;

            AopTemplate templateToRemove = templates.FirstOrDefault(w => w.TemplateName == templateName);
            if (templateToRemove != null)
                templates.Remove(templateToRemove);
        }
    }
}

