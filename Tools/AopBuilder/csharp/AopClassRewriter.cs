using AOP.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AopBuilder
{
    public class AopClassRewriter : CSharpSyntaxRewriter
    {
        private readonly AopCsharpTemplateService _templateService;
        private readonly string _filePath;

        public AopTemplateAction Action { get; set; } = AopTemplateAction.Classes;

        public AopClassRewriter(string filePath, AopCsharpTemplateService templateService)
        {
            _filePath = filePath;
            _templateService = templateService;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            NamespaceDeclarationSyntax namespaceDeclaration = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

            var classTemplates = new List<AopTemplate>();

            var globalTemplates = _templateService.GetGlobalTemplates(_filePath, namespaceDeclaration?.Name?.ToString(), node.Identifier.ToString());

            classTemplates.AddRange(globalTemplates.Where(w => w.Action == Action));
            classTemplates.ForEach(s => s.AppliedTo = AopTemplateApplied.Class);

            string className = node.Identifier.ToString();

            Console.Out.WriteLine("Class:  " + className);

            if (BuilderSettings.Verbosity > 2)
            {
                Console.Out.WriteLine("Old code:");
                Console.Out.WriteLine(node.ToFullString());
            }

            AopRewriter.AddTemplatesFromAttributes(classTemplates, Action, node.AttributeLists, AopTemplateAction.Methods);

            ClassDeclarationSyntax result = AopRewriter.ProcessTemplates<ClassDeclarationSyntax>(_templateService, classTemplates, node, node);

            return result;
        }
    }
}
