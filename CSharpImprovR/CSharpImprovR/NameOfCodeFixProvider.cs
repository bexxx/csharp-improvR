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
                CodeAction.Create("Replace string with nameof expression", c => ReplaceWithNameOfExpression(context.Document, stringLiteral, c)),
                diagnostic);
        }

        private async Task<Document> ReplaceWithNameOfExpression(Document document, ExpressionSyntax stringLiteral, CancellationToken cancellationToken)
        {
            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var stringValue = (string)semanticModel.GetConstantValue(stringLiteral).Value;

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var originalTree = stringLiteral.SyntaxTree;
            var oldRoot = await originalTree.GetRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(stringLiteral, SyntaxFactory.NameOfExpression(SyntaxFactory.IdentifierName("nameof"), SyntaxFactory.IdentifierName(stringValue)));

            // Return the new solution with the now-uppercase type name.
            return document.WithSyntaxRoot(newRoot);
        }
    }
}