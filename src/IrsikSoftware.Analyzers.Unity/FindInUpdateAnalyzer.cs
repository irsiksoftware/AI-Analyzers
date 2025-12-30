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
	/// Detects GameObject.Find* methods in Update/FixedUpdate/LateUpdate.
	/// These are expensive operations that should be cached.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class FindInUpdateAnalyzer : DiagnosticAnalyzer
	{
		private static readonly ImmutableHashSet<string> FindMethods = ImmutableHashSet.Create(
			"Find",
			"FindWithTag",
			"FindGameObjectWithTag",
			"FindGameObjectsWithTag",
			"FindObjectOfType",
			"FindObjectsOfType",
			"FindAnyObjectByType",
			"FindFirstObjectByType",
			"FindObjectsByType");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.FindInUpdate);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
		}

		private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
		{
			var classDeclaration = (ClassDeclarationSyntax)context.Node;
			var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

			if (!UnityHelpers.IsMonoBehaviour(classSymbol))
				return;

			foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
			{
				if (!UnityHelpers.IsUpdateMethod(method))
					continue;

				var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

				foreach (var invocation in invocations)
				{
					var methodName = UnityHelpers.GetMethodName(invocation);
					if (methodName == null || !FindMethods.Contains(methodName))
						continue;

					var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
					if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
						continue;

					var containingType = methodSymbol.ContainingType;
					var namespaceName = containingType?.ContainingNamespace?.ToDisplayString();

					// Check if it's a Unity Find method (GameObject, Object, Resources)
					if (namespaceName != "UnityEngine")
						continue;

					if (containingType?.Name != "GameObject" &&
					    containingType?.Name != "Object" &&
					    containingType?.Name != "Resources")
						continue;

					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.FindInUpdate,
						invocation.GetLocation(),
						methodName);

					context.ReportDiagnostic(diagnostic);
				}
			}
		}
	}
}
