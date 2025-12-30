using System.Collections.Immutable;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects direct UnityEngine.Input usage.
	/// Suggests using an IInputManager abstraction for testability.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class DirectInputAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.DirectInputAccess);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
		}

		private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
		{
			var memberAccess = (MemberAccessExpressionSyntax)context.Node;

			// Check if accessing Input.something
			if (memberAccess.Expression is not IdentifierNameSyntax identifier)
				return;

			if (identifier.Identifier.ValueText != "Input")
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
			var typeSymbol = symbolInfo.Symbol as INamedTypeSymbol;

			// Verify it's UnityEngine.Input
			if (typeSymbol?.ContainingNamespace?.ToDisplayString() != "UnityEngine" ||
			    typeSymbol.Name != "Input")
				return;

			var memberName = memberAccess.Name.Identifier.ValueText;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.DirectInputAccess,
				memberAccess.GetLocation(),
				memberName);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
