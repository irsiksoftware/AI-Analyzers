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
	/// Code fix provider for ISU2100: DOTween missing SetLink.
	/// Appends .SetLink(gameObject) to the tween chain.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DOTweenSetLinkCodeFixProvider))]
	[Shared]
	public class DOTweenSetLinkCodeFixProvider : CodeFixProvider
	{
		private const string Title = "Add .SetLink(gameObject)";

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.DOTweenMissingSetLink);

		public sealed override FixAllProvider GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Find the complete expression to append to
			var node = root.FindNode(diagnosticSpan);
			var expression = FindTweenExpression(node);

			if (expression == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: Title,
					createChangedDocument: c => AddSetLinkAsync(context.Document, expression, c),
					equivalenceKey: nameof(DOTweenSetLinkCodeFixProvider)),
				diagnostic);
		}

		private static ExpressionSyntax? FindTweenExpression(SyntaxNode node)
		{
			// Walk up to find the full expression that should have SetLink appended
			// This could be an invocation like transform.DOMove(...)
			// or a member access chain like transform.DOMove(...).SetEase(...)
			var current = node;

			while (current != null)
			{
				if (current is InvocationExpressionSyntax invocation)
				{
					// Check if this invocation is part of a larger chain
					if (invocation.Parent is MemberAccessExpressionSyntax parentAccess &&
					    parentAccess.Expression == invocation)
					{
						// Keep walking up
						current = parentAccess.Parent;
						continue;
					}

					return invocation;
				}

				current = current.Parent;
			}

			return null;
		}

		private static async Task<Document> AddSetLinkAsync(
			Document document,
			ExpressionSyntax expression,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Create expression.SetLink(gameObject)
			var setLinkInvocation = SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					expression.WithoutTrailingTrivia(),
					SyntaxFactory.IdentifierName("SetLink")),
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(
							SyntaxFactory.IdentifierName("gameObject")))));

			// Preserve trailing trivia
			var newExpression = setLinkInvocation.WithTrailingTrivia(expression.GetTrailingTrivia());

			var newRoot = root.ReplaceNode(expression, newExpression);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
