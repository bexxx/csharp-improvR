using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpImprovR
{
    [ExportCodeFixProvider("CSharpImprovRCodeFixProvider", LanguageNames.CSharp), Shared]
    public class CSharpImprovRCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> GetFixableDiagnosticIds()
        {
            return ImmutableArray.Create(NameOfAnalyzer.DiagnosticId);
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task ComputeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var stringLiteral = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ExpressionSyntax>().First();

            if (!stringLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return;
            }

            // Register a code action that will invoke the fix.
            context.RegisterFix(
                CodeAction.Create("Replace string with nameof expression", c => ReplaceWithNameOfExpression(context, stringLiteral, c)),
                diagnostic);
        }

        private async Task<Document> ReplaceWithNameOfExpression(CodeFixContext context, ExpressionSyntax stringLiteral, CancellationToken cancellationToken)
        {
            var document = context.Document;

            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var stringValue = (string)semanticModel.GetConstantValue(stringLiteral).Value;

            ExpressionSyntax newMessageExpression = null;
            var newExpression = stringLiteral.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>(null, ascendOutOfTrivia: true);
            var oldNewExpression = newExpression;
            if (newExpression.ArgumentList.Arguments.Count == 2 && 
                newExpression.ArgumentList.Arguments[1].Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var message = (string)semanticModel.GetConstantValue(newExpression.ArgumentList.Arguments[1].Expression, cancellationToken).Value;
                if (message != null && message.Contains(stringValue) && !message.Contains("\"")) // do not touch strings with double quotes, or you get straight into hell.
                {
                    // string.Format("foo {0} baz", nameof(param))
                    var newString = ReplaceWord(message, stringValue, "{0}");

                    newMessageExpression = SyntaxFactory.InvocationExpression(

                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.IdentifierName("Format")),

                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(
                            new[] {
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Token(default(SyntaxTriviaList), SyntaxKind.StringLiteralToken, "\"" + newString + "\"", newString, default(SyntaxTriviaList)))),
                                    SyntaxFactory.Argument(SyntaxFactory.NameOfExpression(SyntaxFactory.IdentifierName("nameof"), SyntaxFactory.IdentifierName(stringValue)).WithLeadingTrivia(SyntaxFactory.Whitespace(" ")))
                            }))).WithLeadingTrivia(newExpression.ArgumentList.Arguments[1].Expression.GetLeadingTrivia()).WithTrailingTrivia(newExpression.ArgumentList.Arguments[1].Expression.GetTrailingTrivia());

                    newExpression = newExpression.ReplaceNode(newExpression.ArgumentList.Arguments[1].Expression, newMessageExpression);
                }
            }

            newExpression = newExpression.ReplaceNode(newExpression.ArgumentList.Arguments[0].Expression, SyntaxFactory.NameOfExpression(SyntaxFactory.IdentifierName("nameof"), SyntaxFactory.IdentifierName(stringValue)));

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var originalTree = stringLiteral.SyntaxTree;
            var oldRoot = await originalTree.GetRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(oldNewExpression, newExpression);

            // Return the new solution with the now-uppercase type name.
            return document.WithSyntaxRoot(newRoot);
        }

        private string ReplaceWord(string oldString, string oldWord, string newWord)
        {
            var words = oldString.Split(' ');
            return string.Join(" ", words.Select(s => s.Replace(oldWord, newWord)));
        }
    }
}