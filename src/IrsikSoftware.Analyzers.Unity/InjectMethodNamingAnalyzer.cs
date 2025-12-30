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
	/// Detects VContainer [Inject] methods not named 'Construct'.
	/// Enforces consistent DI injection method naming across the codebase.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class InjectMethodNamingAnalyzer : DiagnosticAnalyzer
	{
		private const string ExpectedMethodName = "Construct";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.InjectMethodNaming);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
		{
			var method = (MethodDeclarationSyntax)context.Node;

			// Skip if already named correctly
			if (method.Identifier.ValueText == ExpectedMethodName)
				return;

			// Check for [Inject] attribute
			var hasInjectAttribute = method.AttributeLists
				.SelectMany(al => al.Attributes)
				.Any(attr =>
				{
					var name = attr.Name.ToString();
					return name == "Inject" || name == "InjectAttribute" ||
					       name.EndsWith(".Inject") || name.EndsWith(".InjectAttribute");
				});

			if (!hasInjectAttribute)
				return;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.InjectMethodNaming,
				method.Identifier.GetLocation(),
				method.Identifier.ValueText);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
