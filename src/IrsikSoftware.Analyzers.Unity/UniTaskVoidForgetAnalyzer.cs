using System.Collections.Immutable;
using System.Linq;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects calls to async UniTaskVoid methods without .Forget().
	/// UniTaskVoid methods should explicitly call .Forget() to indicate fire-and-forget intent.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class UniTaskVoidForgetAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.UniTaskVoidForget);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
		}

		private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
		{
			var invocation = (InvocationExpressionSyntax)context.Node;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			// Check if return type is UniTaskVoid
			var returnType = methodSymbol.ReturnType;
			if (returnType.Name != "UniTaskVoid")
				return;

			if (returnType.ContainingNamespace?.ToDisplayString() != "Cysharp.Threading.Tasks")
				return;

			// Check if this invocation is followed by .Forget()
			if (invocation.Parent is MemberAccessExpressionSyntax memberAccess &&
			    memberAccess.Name.Identifier.ValueText == "Forget")
			{
				return; // Already has .Forget()
			}

			// Check if result is being discarded with _ =
			if (invocation.Parent is AssignmentExpressionSyntax assignment &&
			    assignment.Left is IdentifierNameSyntax identifier &&
			    identifier.Identifier.ValueText == "_")
			{
				return; // Explicitly discarded
			}

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.UniTaskVoidForget,
				invocation.GetLocation(),
				methodSymbol.Name);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
