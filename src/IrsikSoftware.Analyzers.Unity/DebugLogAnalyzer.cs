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
	/// Detects Debug.Log/LogWarning/LogError calls not wrapped in conditional compilation.
	/// Debug logging in production builds impacts performance.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class DebugLogAnalyzer : DiagnosticAnalyzer
	{
		private static readonly ImmutableHashSet<string> DebugMethods = ImmutableHashSet.Create(
			"Log", "LogWarning", "LogError", "LogException", "LogAssertion",
			"LogFormat", "LogWarningFormat", "LogErrorFormat", "LogAssertionFormat");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.DebugLogInProduction);

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
			if (methodName == null || !DebugMethods.Contains(methodName))
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			var containingType = methodSymbol.ContainingType;
			if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine" ||
			    containingType.Name != "Debug")
				return;

			// Check if already inside a conditional directive
			if (IsInsideConditionalDirective(invocation))
				return;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.DebugLogInProduction,
				invocation.GetLocation(),
				methodName);

			context.ReportDiagnostic(diagnostic);
		}

		private static bool IsInsideConditionalDirective(SyntaxNode node)
		{
			// Walk up the tree looking for #if directives
			var current = node;
			while (current != null)
			{
				// Check leading trivia for #if
				var leadingTrivia = current.GetLeadingTrivia();
				foreach (var trivia in leadingTrivia)
				{
					if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
					{
						var ifDirective = trivia.GetStructure() as IfDirectiveTriviaSyntax;
						if (ifDirective != null && ContainsEditorOrDevelopmentBuild(ifDirective.Condition))
						{
							return true;
						}
					}
				}

				// Also check if parent statement has conditional trivia
				if (current.Parent is StatementSyntax parentStatement)
				{
					var parentTrivia = parentStatement.GetLeadingTrivia();
					foreach (var trivia in parentTrivia)
					{
						if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
						{
							var ifDirective = trivia.GetStructure() as IfDirectiveTriviaSyntax;
							if (ifDirective != null && ContainsEditorOrDevelopmentBuild(ifDirective.Condition))
							{
								return true;
							}
						}
					}
				}

				current = current.Parent;
			}

			return false;
		}

		private static bool ContainsEditorOrDevelopmentBuild(ExpressionSyntax? condition)
		{
			if (condition == null)
				return false;

			var conditionText = condition.ToString();
			return conditionText.Contains("UNITY_EDITOR") ||
			       conditionText.Contains("DEVELOPMENT_BUILD") ||
			       conditionText.Contains("DEBUG");
		}
	}
}
