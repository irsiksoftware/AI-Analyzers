using System.Collections.Immutable;
using System.IO;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects state enums that don't follow the {ClassName}State naming convention
	/// or are not located in the /Scripts/Enums/ folder.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class StateEnumConventionAnalyzer : DiagnosticAnalyzer
	{
		private const string StateSuffix = "State";
		private const string EnumsFolderPath = "Scripts/Enums";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.StateEnumConvention);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeEnum, SyntaxKind.EnumDeclaration);
		}

		private static void AnalyzeEnum(SyntaxNodeAnalysisContext context)
		{
			var enumDeclaration = (EnumDeclarationSyntax)context.Node;
			var enumName = enumDeclaration.Identifier.ValueText;

			// Only analyze enums that end with "State"
			if (!enumName.EndsWith(StateSuffix))
				return;

			// Check if the file is in /Scripts/Enums/ folder
			var filePath = context.Node.SyntaxTree.FilePath;
			if (string.IsNullOrEmpty(filePath))
				return;

			// Normalize path separators
			var normalizedPath = filePath.Replace('\\', '/');

			// Check if path contains /Scripts/Enums/
			var isInEnumsFolder = normalizedPath.Contains($"/{EnumsFolderPath}/") ||
			                      normalizedPath.Contains($"/{EnumsFolderPath}\\");

			if (isInEnumsFolder)
				return;

			// Suggest the proper location
			var suggestedName = enumName;
			var suggestedLocation = $"/Scripts/Enums/{enumName}.cs";

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.StateEnumConvention,
				enumDeclaration.Identifier.GetLocation(),
				enumName,
				suggestedLocation);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
