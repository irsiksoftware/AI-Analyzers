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
	/// Detects Object.Instantiate usage in projects using VContainer.
	/// If VContainer is present, GameObject/Component instantiation should use container.Instantiate instead.
	/// Exception: Editor code (in Editor folders or with UNITY_EDITOR defines).
	/// Exception: Non-injectable types like Material, Mesh, ScriptableObject, AnimationClip, etc.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class VContainerInstantiateAnalyzer : DiagnosticAnalyzer
	{
		// Types that don't need DI - they're just asset clones, not scene objects with components
		private static readonly string[] NonInjectableTypes =
		{
			"Material",
			"Mesh",
			"AnimationClip",
			"AudioClip",
			"Texture",
			"Texture2D",
			"Texture3D",
			"RenderTexture",
			"Sprite",
			"PhysicMaterial",
			"PhysicsMaterial2D",
			"Avatar",
			"AnimatorOverrideController",
			"RuntimeAnimatorController"
		};

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.UseContainerInstantiate);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterCompilationStartAction(OnCompilationStart);
		}

		private static void OnCompilationStart(CompilationStartAnalysisContext context)
		{
			// Check if VContainer is referenced in this compilation
			var hasVContainer = context.Compilation.ReferencedAssemblyNames
				.Any(a => a.Name == "VContainer");

			if (!hasVContainer)
				return;

			// VContainer is present - flag GameObject/Component instantiation in non-Editor code
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
		}

		private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
		{
			var invocation = (InvocationExpressionSyntax)context.Node;

			// Check if this is Object.Instantiate
			var methodName = UnityHelpers.GetMethodName(invocation);
			if (methodName != "Instantiate")
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			var containingType = methodSymbol.ContainingType;
			if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine")
				return;

			if (containingType.Name != "Object")
				return;

			// Skip Editor code
			if (IsEditorCode(context))
				return;

			// Skip non-injectable types (Materials, Meshes, etc.)
			if (IsNonInjectableType(methodSymbol))
				return;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.UseContainerInstantiate,
				invocation.GetLocation());

			context.ReportDiagnostic(diagnostic);
		}

		/// <summary>
		/// Checks if the Instantiate call is for a non-injectable type like Material or Mesh.
		/// These are asset clones that don't have MonoBehaviour components and don't need DI.
		/// </summary>
		private static bool IsNonInjectableType(IMethodSymbol methodSymbol)
		{
			// Get the return type of the Instantiate call
			var returnType = methodSymbol.ReturnType;

			// Check if it's one of the non-injectable types
			if (IsOrDerivesFromNonInjectable(returnType))
				return true;

			// For generic Instantiate<T>, check the type argument
			if (methodSymbol.IsGenericMethod && methodSymbol.TypeArguments.Length > 0)
			{
				var typeArg = methodSymbol.TypeArguments[0];
				if (IsOrDerivesFromNonInjectable(typeArg))
					return true;
			}

			return false;
		}

		private static bool IsOrDerivesFromNonInjectable(ITypeSymbol? type)
		{
			if (type == null)
				return false;

			// Check the type and all its base types
			var currentType = type;
			while (currentType != null)
			{
				if (NonInjectableTypes.Contains(currentType.Name))
					return true;

				currentType = currentType.BaseType;
			}

			return false;
		}

		private static bool IsEditorCode(SyntaxNodeAnalysisContext context)
		{
			// Check file path for Editor folder
			var filePath = context.Node.SyntaxTree.FilePath;
			if (!string.IsNullOrEmpty(filePath))
			{
				// Unity convention: Editor scripts are in /Editor/ folders
				if (filePath.Contains("\\Editor\\") || filePath.Contains("/Editor/"))
					return true;
			}

			// Check if inside #if UNITY_EDITOR block
			var node = context.Node;
			while (node != null)
			{
				var trivia = node.GetLeadingTrivia();
				foreach (var t in trivia)
				{
					if (t.IsKind(SyntaxKind.IfDirectiveTrivia))
					{
						var directive = t.ToString();
						if (directive.Contains("UNITY_EDITOR"))
							return true;
					}
				}
				node = node.Parent;
			}

			return false;
		}
	}
}
