using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Detects cryptic abbreviations in identifiers.
	/// Suggests using full descriptive names instead.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class AvoidAbbreviationsAnalyzer : DiagnosticAnalyzer
	{
		// Known abbreviations and their preferred full names
		private static readonly ImmutableDictionary<string, string> Abbreviations =
			ImmutableDictionary<string, string>.Empty
				.Add("rng", "randomNumberGenerator")
				.Add("ctx", "context")
				.Add("cfg", "config")
				.Add("btn", "button")
				.Add("mgr", "manager")
				.Add("svc", "service")
				.Add("idx", "index")
				.Add("cnt", "count")
				.Add("msg", "message")
				.Add("req", "request")
				.Add("res", "response")
				.Add("src", "source")
				.Add("dst", "destination")
				.Add("tmp", "temp")
				.Add("ptr", "pointer")
				.Add("num", "number")
				.Add("val", "value")
				.Add("obj", "object")
				.Add("str", "string")
				.Add("len", "length")
				.Add("pos", "position")
				.Add("vel", "velocity")
				.Add("dir", "direction")
				.Add("rot", "rotation")
				.Add("xfrm", "transform")
				.Add("cb", "callback")
				.Add("evt", "event")
				.Add("args", "arguments")
				.Add("params", "parameters")
				.Add("impl", "implementation")
				.Add("init", "initialize")
				.Add("calc", "calculate")
				.Add("proc", "process");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(DiagnosticDescriptors.AvoidAbbreviations);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			// Check variable declarations
			context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration, SyntaxKind.VariableDeclarator);

			// Check parameters
			context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);

			// Check fields
			context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);

			// Check properties
			context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
		}

		private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
		{
			var variableDeclarator = (VariableDeclaratorSyntax)context.Node;
			CheckIdentifier(context, variableDeclarator.Identifier);
		}

		private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
		{
			var parameter = (ParameterSyntax)context.Node;
			CheckIdentifier(context, parameter.Identifier);
		}

		private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
		{
			var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
			foreach (var variable in fieldDeclaration.Declaration.Variables)
			{
				CheckIdentifier(context, variable.Identifier);
			}
		}

		private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
		{
			var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
			CheckIdentifier(context, propertyDeclaration.Identifier);
		}

		private static void CheckIdentifier(SyntaxNodeAnalysisContext context, SyntaxToken identifier)
		{
			var name = identifier.ValueText;

			// Strip leading underscore for field naming convention
			var checkName = name.TrimStart('_');

			// Check if the entire name (lowercase) matches an abbreviation
			var lowerName = checkName.ToLowerInvariant();

			if (Abbreviations.TryGetValue(lowerName, out var suggestion))
			{
				ReportDiagnostic(context, identifier, name);
				return;
			}

			// Also check if name starts with or contains the abbreviation as a word boundary
			// e.g., "rngService" or "_rng" should be flagged
			foreach (var abbr in Abbreviations.Keys)
			{
				// Check if it's the whole name or at a word boundary
				if (IsAbbreviationMatch(checkName, abbr))
				{
					ReportDiagnostic(context, identifier, name);
					return;
				}
			}
		}

		private static bool IsAbbreviationMatch(string name, string abbreviation)
		{
			var lowerName = name.ToLowerInvariant();
			var idx = lowerName.IndexOf(abbreviation);

			if (idx < 0)
				return false;

			// Check if it's at a word boundary
			// Start of string or after underscore
			var isStartBoundary = idx == 0 || name[idx - 1] == '_';

			// End of string or followed by uppercase (camelCase) or underscore
			var endIdx = idx + abbreviation.Length;
			var isEndBoundary = endIdx >= name.Length ||
			                    char.IsUpper(name[endIdx]) ||
			                    name[endIdx] == '_';

			return isStartBoundary && isEndBoundary;
		}

		private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, SyntaxToken identifier, string name)
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.AvoidAbbreviations,
				identifier.GetLocation(),
				name);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
