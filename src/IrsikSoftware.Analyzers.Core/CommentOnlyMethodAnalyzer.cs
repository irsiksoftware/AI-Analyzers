using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Core
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class CommentOnlyMethodAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "ISU0001";

		private static readonly LocalizableString Title =
			"Method body contains only comments";

		private static readonly LocalizableString MessageFormat =
			"Method '{0}' contains only comments - consider removing or implementing";

		private static readonly LocalizableString Description =
			"Methods that contain only comments are effectively empty and may indicate dead code or incomplete implementation.";

		private const string Category = "Maintainability";

		private static readonly DiagnosticDescriptor Rule = new(
			DiagnosticId,
			Title,
			MessageFormat,
			Category,
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: Description);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
		{
			var method = (MethodDeclarationSyntax)context.Node;

			// Skip methods without bodies (abstract, interface, expression-bodied)
			if (method.Body == null)
			{
				return;
			}

			// Skip if there are any actual statements
			if (method.Body.Statements.Count > 0)
			{
				return;
			}

			// Check if the body contains any comments
			var hasComments = method.Body.DescendantTrivia()
				.Any(trivia =>
					trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
					trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
					trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
					trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

			if (hasComments)
			{
				var diagnostic = Diagnostic.Create(
					Rule,
					method.Identifier.GetLocation(),
					method.Identifier.Text);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
