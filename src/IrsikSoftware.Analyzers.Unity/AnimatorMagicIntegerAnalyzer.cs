using System.Collections.Immutable;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects Animator.SetInteger with magic number literals.
	/// Suggests using enum casts for type safety.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class AnimatorMagicIntegerAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.AnimatorMagicInteger);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
		}

		private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
		{
			var invocation = (InvocationExpressionSyntax)context.Node;

			var methodName = UnityHelpers.GetMethodName(invocation);
			if (methodName != "SetInteger")
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			var containingType = methodSymbol.ContainingType;
			if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine" ||
			    containingType.Name != "Animator")
				return;

			// Check arguments - we're looking for SetInteger(hash, value) with literal value
			var arguments = invocation.ArgumentList.Arguments;
			if (arguments.Count < 2)
				return;

			// Check if the second argument (the value) is a literal integer
			var valueArg = arguments[1].Expression;

			if (valueArg is LiteralExpressionSyntax literal &&
			    literal.IsKind(SyntaxKind.NumericLiteralExpression))
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.AnimatorMagicInteger,
					literal.GetLocation(),
					literal.Token.ValueText);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
