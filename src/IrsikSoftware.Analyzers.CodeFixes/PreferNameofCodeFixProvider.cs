using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU0002: Prefer nameof().
	/// Replaces string literal with nameof(identifier).
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferNameofCodeFixProvider))]
	[Shared]
	public class PreferNameofCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.PreferNameof);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Use FindToken like ISU0004 for more reliable node finding
			var token = root.FindToken(diagnosticSpan.Start);
			var literalExpression = token.Parent as LiteralExpressionSyntax;
			if (literalExpression == null || !literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
				return;

			// Get the string value from the literal
			var stringValue = token.ValueText;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Use nameof({stringValue})",
					createChangedSolution: c => ReplaceWithNameofAsync(context.Document, diagnostic, stringValue, c),
					equivalenceKey: nameof(PreferNameofCodeFixProvider)),
				diagnostic);
		}

		private static async Task<Solution> ReplaceWithNameofAsync(
			Document document,
			Diagnostic diagnostic,
			string identifier,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document.Project.Solution;

			// Use FindToken like ISU0004 for more reliable node finding
			var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
			var literalExpression = token.Parent as LiteralExpressionSyntax;
			if (literalExpression == null || !literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
				return document.Project.Solution;

			// Create nameof(identifier) expression
			var nameofExpression = SyntaxFactory.InvocationExpression(
				SyntaxFactory.IdentifierName("nameof"),
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(
							SyntaxFactory.IdentifierName(identifier)))));

			// Preserve trivia from original expression
			var newExpression = nameofExpression
				.WithLeadingTrivia(literalExpression.GetLeadingTrivia())
				.WithTrailingTrivia(literalExpression.GetTrailingTrivia());

			var newRoot = root.ReplaceNode(literalExpression, newExpression);
			var newDocument = document.WithSyntaxRoot(newRoot);
			return newDocument.Project.Solution;
		}
	}
}
