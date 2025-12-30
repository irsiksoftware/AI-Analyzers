using System.Collections.Immutable;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects Animator methods using string state/parameter names instead of StringToHash.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class AnimatorStringAnalyzer : DiagnosticAnalyzer
	{
		private static readonly ImmutableHashSet<string> AnimatorStringMethods = ImmutableHashSet.Create(
			"CrossFade", "CrossFadeInFixedTime",
			"GetBool", "GetFloat", "GetInteger",
			"IsParameterControlledByCurve",
			"Play", "PlayInFixedTime",
			"ResetTrigger",
			"SetBool", "SetFloat", "SetInteger", "SetTrigger");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.AnimatorStringState);

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
			if (methodName == null || !AnimatorStringMethods.Contains(methodName))
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			var containingType = methodSymbol.ContainingType;
			if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine" ||
			    containingType.Name != "Animator")
				return;

			// Check if first parameter is string
			if (methodSymbol.Parameters.Length == 0 ||
			    methodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_String)
				return;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.AnimatorStringState,
				invocation.GetLocation(),
				methodName);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
