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
	/// Detects non-deterministic API usage in ISimulatable.Simulate methods.
	/// Simulate methods must be deterministic for replay/networking.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class NonDeterministicSimulateAnalyzer : DiagnosticAnalyzer
	{
		// Non-deterministic Unity APIs
		private static readonly ImmutableHashSet<string> NonDeterministicTypes = ImmutableHashSet.Create(
			"Time", "Random");

		private static readonly ImmutableHashSet<string> NonDeterministicMethods = ImmutableHashSet.Create(
			"Find", "FindWithTag", "FindGameObjectWithTag", "FindGameObjectsWithTag",
			"FindObjectOfType", "FindObjectsOfType", "FindAnyObjectByType",
			"FindFirstObjectByType", "FindObjectsByType");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.NonDeterministicInSimulate);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
		{
			var method = (MethodDeclarationSyntax)context.Node;

			// Check if method is named "Simulate"
			if (method.Identifier.ValueText != "Simulate")
				return;

			// Also check if class implements ISimulatable (optional - method name is enough)
			var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (classDeclaration == null)
				return;

			// Check for non-deterministic member access (Time.deltaTime, Random.value, etc.)
			var memberAccesses = method.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
			foreach (var memberAccess in memberAccesses)
			{
				if (memberAccess.Expression is IdentifierNameSyntax identifier)
				{
					var typeName = identifier.Identifier.ValueText;
					if (NonDeterministicTypes.Contains(typeName))
					{
						// Verify it's actually UnityEngine.Time or UnityEngine.Random
						var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);
						var typeSymbol = symbolInfo.Symbol as INamedTypeSymbol;

						if (typeSymbol?.ContainingNamespace?.ToDisplayString() == "UnityEngine")
						{
							var fullAccess = $"{typeName}.{memberAccess.Name.Identifier.ValueText}";

							var diagnostic = Diagnostic.Create(
								DiagnosticDescriptors.NonDeterministicInSimulate,
								memberAccess.GetLocation(),
								fullAccess);

							context.ReportDiagnostic(diagnostic);
						}
					}
				}
			}

			// Check for non-deterministic method calls (Find*, etc.)
			var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
			foreach (var invocation in invocations)
			{
				var methodName = UnityHelpers.GetMethodName(invocation);
				if (methodName == null || !NonDeterministicMethods.Contains(methodName))
					continue;

				var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
				if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
					continue;

				var containingType = methodSymbol.ContainingType;
				if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine")
					continue;

				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.NonDeterministicInSimulate,
					invocation.GetLocation(),
					methodName);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
