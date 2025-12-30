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
	/// Detects Object.Instantiate usage in classes that have IObjectResolver.
	/// Suggests using container.Instantiate for proper DI support.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class VContainerInstantiateAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.UseContainerInstantiate);

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

			if (classSymbol == null)
				return;

			// Check if class has IObjectResolver field/property
			var hasObjectResolver = classSymbol.GetMembers()
				.Any(m =>
				{
					ITypeSymbol? type = m switch
					{
						IFieldSymbol f => f.Type,
						IPropertySymbol p => p.Type,
						_ => null
					};

					if (type == null)
						return false;

					// Check for IObjectResolver or IContainerBuilder
					var typeName = type.Name;
					return typeName == "IObjectResolver" ||
					       typeName == "IContainerBuilder" ||
					       typeName == "LifetimeScope";
				});

			if (!hasObjectResolver)
				return;

			// Find all Object.Instantiate calls
			var invocations = classDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>();

			foreach (var invocation in invocations)
			{
				var methodName = UnityHelpers.GetMethodName(invocation);
				if (methodName != "Instantiate")
					continue;

				var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
				if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
					continue;

				var containingType = methodSymbol.ContainingType;
				if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine")
					continue;

				if (containingType.Name != "Object")
					continue;

				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.UseContainerInstantiate,
					invocation.GetLocation());

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
