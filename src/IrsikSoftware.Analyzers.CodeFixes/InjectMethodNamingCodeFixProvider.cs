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
using Microsoft.CodeAnalysis.Rename;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU0003: Inject method naming.
	/// Renames [Inject] method to 'Construct'.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectMethodNamingCodeFixProvider))]
	[Shared]
	public class InjectMethodNamingCodeFixProvider : CodeFixProvider
	{
		private const string Title = "Rename to 'Construct'";

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.InjectMethodNaming);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			null; // Rename operations don't work well with batch fixing

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

			context.RegisterCodeFix(
				CodeAction.Create(
					title: Title,
					createChangedSolution: c => RenameMethodAsync(context.Document, methodDeclaration, c),
					equivalenceKey: nameof(InjectMethodNamingCodeFixProvider)),
				diagnostic);
		}

		private static async Task<Solution> RenameMethodAsync(
			Document document,
			MethodDeclarationSyntax methodDeclaration,
			CancellationToken cancellationToken)
		{
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
			if (semanticModel == null)
				return document.Project.Solution;

			var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);
			if (methodSymbol == null)
				return document.Project.Solution;

			// Use Roslyn's rename functionality to rename the method and all references
			var newSolution = await Renamer.RenameSymbolAsync(
				document.Project.Solution,
				methodSymbol,
				new SymbolRenameOptions(),
				"Construct",
				cancellationToken).ConfigureAwait(false);

			return newSolution;
		}
	}
}
