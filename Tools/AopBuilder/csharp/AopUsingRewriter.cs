using AOP.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AopBuilder
{
    public class AopUsingRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitUsingStatement(UsingStatementSyntax node)
        {
            ClassDeclarationSyntax classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            var newObjectCreation = node.Expression.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();

            if (newObjectCreation == null || !newObjectCreation.Type.ToString().EndsWith("AopTemplate") || !(node.Statement is BlockSyntax))
                return node;

            var blockTemplates = new List<AopTemplate>();

            AopTemplate aopTemplate = Utils.GetAopTemplate(newObjectCreation.ArgumentList);

            if (!String.IsNullOrEmpty(BuilderSettings.OnlyTemplate) && BuilderSettings.OnlyTemplate != aopTemplate.TemplateName)
                return node.Statement;

            blockTemplates.Add(aopTemplate);

            SyntaxNode result = ProcessTemplates(blockTemplates, node.Statement, classDeclaration);
            return result;
        }

        private static SyntaxNode ProcessTemplates(List<AopTemplate> templates, StatementSyntax node, ClassDeclarationSyntax classDeclaration)
        {
            SyntaxNode result = node;

            string startingWhitespace = "";
            if (node.HasLeadingTrivia)
                startingWhitespace = node.GetLeadingTrivia().ToFullString();

            string closingWhitespace = "";
            if (node.HasTrailingTrivia)
                closingWhitespace = node.GetTrailingTrivia().ToFullString();

            var aopCsharpTemplateService = new AopCsharpTemplateService();

            foreach (AopTemplate template in templates.OrderBy(o => o.AdvicePriority))
            {
                Console.Out.WriteLine($"\tProcessing template {template.TemplateName}");

                string sourceCode = aopCsharpTemplateService.ProcessTemplate(template.TemplateName, new Dictionary<string, object>() {
                    { "ClassNode", classDeclaration },
                    { "MethodNode", result is MethodDeclarationSyntax ? result : null },
                    { "PropertyNode", result is PropertyDeclarationSyntax ? result : null },
                    { "StatementNode", result is StatementSyntax ? result : null },
                    { "ExtraTag", template.ExtraTag }
                });

                // if sourceCode is null, it means no changes were done to original code and we keep it as-is
                if (sourceCode == null)
                    continue;

                sourceCode = sourceCode.Trim(' ', '\r', '\n');

                if (!Regex.IsMatch(sourceCode, "^\\s*\\{.*\\}\\s*$", RegexOptions.Singleline))
                {
                    sourceCode = (new StringBuilder()).AppendLine("{").AppendLine(sourceCode).AppendLine(startingWhitespace + "}").ToString();
                }

                result = SyntaxFactory.ParseStatement(startingWhitespace + sourceCode + closingWhitespace);
            }

            return result;
        }
    }
}
