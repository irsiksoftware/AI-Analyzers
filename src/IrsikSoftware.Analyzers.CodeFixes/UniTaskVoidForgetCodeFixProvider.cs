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
	/// Code fix provider for ISU2001: UniTaskVoid without Forget().
	/// Appends .Forget() to the invocation.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UniTaskVoidForgetCodeFixProvider))]
	[Shared]
	public class UniTaskVoidForgetCodeFixProvider : CodeFixProvider
	{
		private const string Title = "Add .Forget()";

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.UniTaskVoidForget);

		public sealed override FixAllProvider GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var invocation = root.FindNode(diagnosticSpan)
				.AncestorsAndSelf()
				.OfType<InvocationExpressionSyntax>()
				.FirstOrDefault();

			if (invocation == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: Title,
					createChangedDocument: c => AddForgetAsync(context.Document, invocation, c),
					equivalenceKey: nameof(UniTaskVoidForgetCodeFixProvider)),
				diagnostic);
		}

		private static async Task<Document> AddForgetAsync(
			Document document,
			InvocationExpressionSyntax invocation,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Create invocation.Forget()
			var forgetInvocation = SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					invocation.WithoutTrailingTrivia(),
					SyntaxFactory.IdentifierName("Forget")),
				SyntaxFactory.ArgumentList());

			// Preserve trailing trivia
			var newExpression = forgetInvocation.WithTrailingTrivia(invocation.GetTrailingTrivia());

			var newRoot = root.ReplaceNode(invocation, newExpression);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
