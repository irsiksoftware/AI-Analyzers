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
	/// Detects pure C# services with Update-like methods.
	/// Suggests implementing ITickable for VContainer integration.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class PreferITickableAnalyzer : DiagnosticAnalyzer
	{
		private static readonly ImmutableHashSet<string> UpdateLikeMethodNames = ImmutableHashSet.Create(
			"Update", "Tick", "OnUpdate", "DoUpdate", "ProcessUpdate");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.PreferITickable);

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

			// Skip if it's a MonoBehaviour or ScriptableObject
			if (UnityHelpers.IsMonoBehaviour(classSymbol) || UnityHelpers.IsScriptableObject(classSymbol))
				return;

			// Skip if already implements ITickable
			var implementsTickable = classSymbol.AllInterfaces
				.Any(i => i.Name == "ITickable" || i.Name == "IFixedTickable" || i.Name == "ILateTickable");

			if (implementsTickable)
				return;

			// Find Update-like methods
			foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
			{
				var methodName = method.Identifier.ValueText;

				if (!UpdateLikeMethodNames.Contains(methodName))
					continue;

				// Skip if method has parameters (likely not an update loop)
				if (method.ParameterList.Parameters.Count > 0)
					continue;

				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.PreferITickable,
					method.Identifier.GetLocation(),
					classSymbol.Name);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
