using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Detects string literals that match type or member names where nameof() should be used.
	/// Prevents strings from becoming stale during refactoring.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class PreferNameofAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.PreferNameof);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
		}

		private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
		{
			var literal = (LiteralExpressionSyntax)context.Node;
			var literalValue = literal.Token.ValueText;

			// Skip empty or whitespace strings
			if (string.IsNullOrWhiteSpace(literalValue))
				return;

			// Skip if it contains spaces (likely not a type/member name)
			if (literalValue.Contains(" "))
				return;

			// Skip very short strings (unlikely to be meaningful names)
			if (literalValue.Length < 3)
				return;

			// Get the containing type
			var containingType = literal.FirstAncestorOrSelf<TypeDeclarationSyntax>();
			if (containingType == null)
				return;

			var semanticModel = context.SemanticModel;
			var typeSymbol = semanticModel.GetDeclaredSymbol(containingType) as INamedTypeSymbol;
			if (typeSymbol == null)
				return;

			// Check if string matches containing type name
			if (literalValue == typeSymbol.Name)
			{
				ReportDiagnostic(context, literal, literalValue);
				return;
			}

			// Check if string matches any member name in the containing type
			var members = typeSymbol.GetMembers();
			if (members.Any(m => m.Name == literalValue))
			{
				ReportDiagnostic(context, literal, literalValue);
				return;
			}

			// Check if string matches any parameter in the containing method
			var containingMethod = literal.FirstAncestorOrSelf<MethodDeclarationSyntax>();
			if (containingMethod != null)
			{
				var parameters = containingMethod.ParameterList.Parameters;
				if (parameters.Any(p => p.Identifier.ValueText == literalValue))
				{
					ReportDiagnostic(context, literal, literalValue);
					return;
				}
			}

			// Check if string matches any local variable in scope
			var containingBlock = literal.FirstAncestorOrSelf<BlockSyntax>();
			if (containingBlock != null)
			{
				var localDeclarations = containingBlock.DescendantNodes()
					.OfType<VariableDeclaratorSyntax>()
					.Where(v => v.SpanStart < literal.SpanStart);

				if (localDeclarations.Any(v => v.Identifier.ValueText == literalValue))
				{
					ReportDiagnostic(context, literal, literalValue);
				}
			}
		}

		private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal, string literalValue)
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.PreferNameof,
				literal.GetLocation(),
				literalValue);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
