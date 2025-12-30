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
	/// Detects Camera.main usage in Update/FixedUpdate/LateUpdate methods.
	/// Camera.main performs a FindGameObjectsWithTag internally and should be cached.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class CameraMainAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.CameraMainInUpdate);

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

			// Check each update method
			foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
			{
				if (!UnityHelpers.IsUpdateMethod(method))
					continue;

				// Find all Camera.main accesses
				var memberAccesses = method.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

				foreach (var memberAccess in memberAccesses)
				{
					if (memberAccess.Name.Identifier.ValueText != "main")
						continue;

					var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
					var typeSymbol = symbolInfo.Symbol as INamedTypeSymbol;

					// Check if it's UnityEngine.Camera
					if (typeSymbol?.ContainingNamespace?.ToDisplayString() == "UnityEngine" &&
					    typeSymbol.Name == "Camera")
					{
						var diagnostic = Diagnostic.Create(
							DiagnosticDescriptors.CameraMainInUpdate,
							memberAccess.GetLocation(),
							method.Identifier.ValueText);

						context.ReportDiagnostic(diagnostic);
					}
				}
			}
		}
	}
}
