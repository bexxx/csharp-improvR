using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CSharpImprovR
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NameOfAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CSharpImprovR";
        internal const string Title = "String literal can be replace with nameof expression";
        internal const string MessageFormat = "String literal '{0}' can be replaced with nameof({0}) expression";
        internal const string Category = "Readability";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
        }

        private static void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
        {
            var node = (ThrowStatementSyntax)context.Node;

            // check if throw has a new T(...) with at least one argument
            if (node.Expression?.CSharpKind() != SyntaxKind.ObjectCreationExpression ||
                !((ObjectCreationExpressionSyntax)node.Expression).ArgumentList.Arguments.Any())
            {
                return;
            }

            // check if first argument is a string literal (potential parameter name)
            var argument = ((ObjectCreationExpressionSyntax)node.Expression).ArgumentList.Arguments.First();
            if (argument.Expression.CSharpKind() == SyntaxKind.StringLiteralExpression)
            {
                var stringValue = context.SemanticModel.GetConstantValue(argument.Expression);
                if (!stringValue.HasValue || string.IsNullOrWhiteSpace(((string)stringValue.Value)))
                {
                    return;
                }

                var parameterName = (string)stringValue.Value;
                
                // now check if this throw is in a if statement that checks for null (only right side, no checks for left side)
                var enclosingIf = node.FirstAncestorOrSelf<IfStatementSyntax>(null, ascendOutOfTrivia: true);
                if (enclosingIf == null || !enclosingIf.Condition.IsKind(SyntaxKind.EqualsExpression))
                {
                    return;
                }

                // if it's not in a if that compares an identifier of the same name to null
                var equalsExpression = ((BinaryExpressionSyntax)enclosingIf.Condition);
                if (!((equalsExpression.Left.IsKind(SyntaxKind.IdentifierName) &&
                    ((IdentifierNameSyntax)equalsExpression.Left).Identifier.Text == parameterName &&
                    equalsExpression.Right.IsKind(SyntaxKind.NullLiteralExpression)) ||
                    (equalsExpression.Right.IsKind(SyntaxKind.IdentifierName) &&
                    ((IdentifierNameSyntax)equalsExpression.Right).Identifier.Text == parameterName &&
                    equalsExpression.Left.IsKind(SyntaxKind.NullLiteralExpression))))
                {
                    return;
                }

                var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>(null, ascendOutOfTrivia: true);
                if (!methodDeclaration.ParameterList.Parameters.Any(p => p.Identifier.ValueText == parameterName))
                {
                    return;
                }

                var typeSymbol = context.SemanticModel.GetTypeInfo(node.Expression);
                if (typeSymbol.Type?.Name != "ArgumentNullException" || typeSymbol.Type.ContainingNamespace.Name != "System")
                {
                    return;
                }

                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, argument.Expression.GetLocation(), parameterName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
