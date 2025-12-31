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
	/// Code fix provider for ISU0001: Comment-only method.
	/// Removes the method entirely if safe to do so.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommentOnlyMethodCodeFixProvider))]
	[Shared]
	public class CommentOnlyMethodCodeFixProvider : CodeFixProvider
	{
		private const string Title = "Remove empty method";

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.CommentOnlyMethod);

		public sealed override FixAllProvider GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var methodDeclaration = root.FindToken(diagnosticSpan.Start)
				.Parent?
				.AncestorsAndSelf()
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault();

			if (methodDeclaration == null)
				return;

			// Safety check: skip methods that cannot be safely removed
			if (!CanSafelyRemoveMethod(methodDeclaration))
				return;

			// Additional safety check using semantic model
			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (semanticModel != null)
			{
				var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
				if (methodSymbol != null && ImplementsInterfaceMember(methodSymbol))
					return;
			}

			context.RegisterCodeFix(
				CodeAction.Create(
					title: Title,
					createChangedDocument: c => RemoveMethodAsync(context.Document, methodDeclaration, c),
					equivalenceKey: nameof(CommentOnlyMethodCodeFixProvider)),
				diagnostic);
		}

		private static bool CanSafelyRemoveMethod(MethodDeclarationSyntax method)
		{
			// Cannot remove virtual methods - derived classes may override them
			if (method.Modifiers.Any(SyntaxKind.VirtualKeyword))
				return false;

			// Cannot remove override methods - base class requires them
			if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
				return false;

			return true;
		}

		private static bool ImplementsInterfaceMember(IMethodSymbol methodSymbol)
		{
			if (methodSymbol.ContainingType == null)
				return false;

			// Check explicit interface implementations
			if (methodSymbol.ExplicitInterfaceImplementations.Length > 0)
				return true;

			// Check implicit interface implementations
			foreach (var iface in methodSymbol.ContainingType.AllInterfaces)
			{
				foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
				{
					var implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(member);
					if (SymbolEqualityComparer.Default.Equals(implementation, methodSymbol))
						return true;
				}
			}

			return false;
		}

		private static async Task<Document> RemoveMethodAsync(
			Document document,
			MethodDeclarationSyntax methodDeclaration,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			var newRoot = root.RemoveNode(methodDeclaration, SyntaxRemoveOptions.KeepNoTrivia);
			if (newRoot == null)
				return document;

			return document.WithSyntaxRoot(newRoot);
		}
	}
}
