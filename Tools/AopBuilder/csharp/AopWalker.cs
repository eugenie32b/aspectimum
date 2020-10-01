using AOP.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AopBuilder
{
    public class AopWalker : CSharpSyntaxWalker
    {
        public Dictionary<string, List<AopTemplate>> ClassTemplates = new Dictionary<string, List<AopTemplate>>();

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            base.VisitClassDeclaration(node);

            ClassTemplates[node.Identifier.Text] = Utils.GetAopTemplates(node.AttributeLists);
        }
    }
}
