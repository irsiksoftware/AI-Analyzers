using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Analyzer that detects methods containing only comments with no actual statements.
	/// These often indicate dead code or incomplete implementations left by AI agents.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class CommentOnlyMethodAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.CommentOnlyMethod);

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
					DiagnosticDescriptors.CommentOnlyMethod,
					method.Identifier.GetLocation(),
					method.Identifier.Text);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
