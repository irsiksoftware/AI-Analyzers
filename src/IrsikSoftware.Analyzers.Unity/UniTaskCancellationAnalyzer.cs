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
	/// Detects async UniTask methods in MonoBehaviour that don't accept CancellationToken.
	/// Missing cancellation tokens can cause orphaned tasks when GameObjects are destroyed.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class UniTaskCancellationAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.UniTaskMissingCancellation);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
		{
			var method = (MethodDeclarationSyntax)context.Node;

			// Must be async
			if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
				return;

			// Check return type is UniTask
			var returnType = method.ReturnType.ToString();
			if (!returnType.Contains("UniTask"))
				return;

			// Skip if return type is UniTaskVoid (fire-and-forget pattern)
			if (returnType.Contains("UniTaskVoid"))
				return;

			// Check if method is in a MonoBehaviour
			var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (classDeclaration == null)
				return;

			var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
			if (!UnityHelpers.IsMonoBehaviour(classSymbol))
				return;

			// Check if method already has a CancellationToken parameter
			var hasCancellationToken = method.ParameterList.Parameters
				.Any(p => p.Type?.ToString().Contains("CancellationToken") == true);

			if (hasCancellationToken)
				return;

			// Check if the method uses GetCancellationTokenOnDestroy internally
			var usesInternalToken = method.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.Any(inv => UnityHelpers.GetMethodName(inv)?.Contains("GetCancellationTokenOnDestroy") == true);

			if (usesInternalToken)
				return;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.UniTaskMissingCancellation,
				method.Identifier.GetLocation(),
				method.Identifier.ValueText);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
