using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace HypoSpray
{
    public static class RoslynHelpers
    {
        public static List<string> InjectedInterfacesForConstuctor(ConstructorDeclarationSyntax thisConstuctor)
        {
            return thisConstuctor.ParameterList
                .ChildNodes().Where(x => x.Kind() == SyntaxKind.Parameter).Select(x => (ParameterSyntax)x)
                .Select(x => x.Type)
                .Select(x => x.ToString())
                .Where(x => x.StartsWith("I"))
                .Distinct()
                .ToList()
                ;
            
        }

        public static int CorrectIndexToAddNewField(ClassDeclarationSyntax thisClass)
        {

            for(int i =0; i< thisClass.Members.Count; i++)
            {
                if(! (thisClass.Members[i] is FieldDeclarationSyntax))
                {
                    return i;
                }
            }
            return 0;
        }

        public static bool VariableExists(SyntaxNode root, params string[] variableNames)
        {
            return root
                .DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                .SelectMany(ps => ps.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) && variableNames.Contains(t.ValueText)))
                .Any();
        }

        public static string GetParameterType(ParameterSyntax parameter)
        {
            return parameter
                .DescendantNodes()
                .First(node => node is PredefinedTypeSyntax || node is IdentifierNameSyntax)
                .GetFirstToken()
                .ValueText;
        }

        public static string GetParameterName(ParameterSyntax parameter)
        {
            return parameter
                .DescendantTokens()
                .Where(t => t.IsKind(SyntaxKind.IdentifierToken))
                .Last().ValueText;
        }

        public static string GetVariableType(VariableDeclarationSyntax variable)
        {
            return variable
                .DescendantNodes()
                .First(node => node is PredefinedTypeSyntax || node is IdentifierNameSyntax)
                .GetFirstToken()
                .ValueText;
        }

        public static bool IsVariableAnInterface(VariableDeclarationSyntax variable)
        {
            return GetVariableType(variable).StartsWith("I");
        }

        public static FieldDeclarationSyntax CreateFieldDeclaration(string type, string name)
        {
            return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(type))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name)))))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
        }
    }
}
