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
		private const string Title = "Use nameof()";

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.PreferNameof);

		public sealed override FixAllProvider GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var literalExpression = root.FindNode(diagnosticSpan) as LiteralExpressionSyntax;
			if (literalExpression == null)
				return;

			// Get the string value from the literal
			var stringValue = literalExpression.Token.ValueText;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Use nameof({stringValue})",
					createChangedDocument: c => ReplaceWithNameofAsync(context.Document, literalExpression, stringValue, c),
					equivalenceKey: nameof(PreferNameofCodeFixProvider)),
				diagnostic);
		}

		private static async Task<Document> ReplaceWithNameofAsync(
			Document document,
			LiteralExpressionSyntax literalExpression,
			string identifier,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

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
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
