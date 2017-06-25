using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace HypoSpray
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InjectFromField)), Shared]
    internal class InjectFromField : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            var interfaceType = node as IdentifierNameSyntax;
            if (interfaceType == null)
            {
                return;
            }
            if(!interfaceType.Identifier.ToString().StartsWith("I"))
            {
                return;
            }




            var interfaceName = interfaceType.Identifier.ToString();
            var plainName = interfaceName.Substring(1);
            var lowerPlainName = plainName.Substring(0, 1).ToLower() + plainName.Substring(1);
            var upperPlainName = plainName.Substring(0, 1).ToUpper() + plainName.Substring(1);
            var underscorePrefix = "_" + lowerPlainName;

            var namespaceNode = (NamespaceDeclarationSyntax) root.ChildNodes().First(n => n.Kind() == SyntaxKind.NamespaceDeclaration);
            var classNode = (ClassDeclarationSyntax) namespaceNode.ChildNodes().First(n => n.Kind() == SyntaxKind.ClassDeclaration);
            var constructorNode = (ConstructorDeclarationSyntax) classNode.ChildNodes().First(n => n.Kind() == SyntaxKind.ConstructorDeclaration);
            var existingDependencies = RoslynHelpers.InjectedInterfacesForConstuctor(constructorNode);

            if (existingDependencies.Contains(interfaceName))
            {
                return;
            }

            var action = CodeAction.Create(
                "Inject this as an interface '" + underscorePrefix + "'",
                ct => CreateFieldAsync(context, root, interfaceName, lowerPlainName, underscorePrefix, ct));

            context.RegisterRefactoring(action);
        }

        private async Task<Document> CreateFieldAsync(CodeRefactoringContext context, SyntaxNode root, string interfaceName,string paramName, string fieldName, CancellationToken cancellationToken)
        {

            var @namespace = (NamespaceDeclarationSyntax)
                root.ChildNodes().First(n => n.Kind() == SyntaxKind.NamespaceDeclaration);

            var @class = (ClassDeclarationSyntax)
                @namespace.ChildNodes().First(n => n.Kind() == SyntaxKind.ClassDeclaration);

            var constructor = (ConstructorDeclarationSyntax)
               @class.ChildNodes().First(n => n.Kind() == SyntaxKind.ConstructorDeclaration);

            var existingDependencies = RoslynHelpers.InjectedInterfacesForConstuctor(constructor);

            var oldConstructor = constructor;
            var newConstructor = oldConstructor.WithBody(oldConstructor.Body.AddStatements(
                 SyntaxFactory.ExpressionStatement(
                     SyntaxFactory.AssignmentExpression(
                         SyntaxKind.SimpleAssignmentExpression,
                         SyntaxFactory.IdentifierName(fieldName),
                         SyntaxFactory.IdentifierName(paramName)))));
            newConstructor = newConstructor.WithParameterList(newConstructor.ParameterList.AddParameters(
                SyntaxFactory.Parameter(
                    SyntaxFactory.Identifier(paramName))
                    .WithType(SyntaxFactory.ParseTypeName(interfaceName))
                    ));

            var oldClass = @class;
            var oldClassWithNewCtor = oldClass.ReplaceNode(oldConstructor, newConstructor);

            var fieldDeclaration = RoslynHelpers.CreateFieldDeclaration(interfaceName, fieldName);
            var newClass = oldClassWithNewCtor
                .WithMembers(oldClassWithNewCtor.Members.Insert(RoslynHelpers.CorrectIndexToAddNewField(oldClassWithNewCtor), fieldDeclaration))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var oldRoot = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(oldClass, newClass);

            return context.Document.WithSyntaxRoot(newRoot);
        }
    }
}