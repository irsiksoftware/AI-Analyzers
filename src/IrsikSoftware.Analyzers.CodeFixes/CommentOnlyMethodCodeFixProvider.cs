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

			// Safety check: skip override methods - require manual review
			// (empty override might be intentionally suppressing base behavior, or just pointless)
			if (methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword))
				return;

			// Semantic safety checks
			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (semanticModel == null)
				return;

			var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
			if (methodSymbol == null)
				return;

			// Skip interface implementations (contract required)
			if (ImplementsInterfaceMember(methodSymbol))
				return;

			// For virtual methods: only allow removal if NOT overridden anywhere
			if (methodDeclaration.Modifiers.Any(SyntaxKind.VirtualKeyword))
			{
				if (IsOverriddenAnywhere(methodSymbol, semanticModel.Compilation))
					return; // Has overrides - cannot safely remove
				// No overrides found - safe to remove unused extension point
			}

			context.RegisterCodeFix(
				CodeAction.Create(
					title: Title,
					createChangedDocument: c => RemoveMethodAsync(context.Document, methodDeclaration, c),
					equivalenceKey: nameof(CommentOnlyMethodCodeFixProvider)),
				diagnostic);
		}

		/// <summary>
		/// Checks if a virtual method is overridden by any type in the compilation.
		/// </summary>
		private static bool IsOverriddenAnywhere(IMethodSymbol methodSymbol, Compilation compilation)
		{
			if (!methodSymbol.IsVirtual)
				return false;

			var containingType = methodSymbol.ContainingType;
			if (containingType == null)
				return false;

			// Check all types in the compilation for overrides
			foreach (var syntaxTree in compilation.SyntaxTrees)
			{
				var semanticModel = compilation.GetSemanticModel(syntaxTree);
				var root = syntaxTree.GetRoot();

				// Find all class/struct declarations
				foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
				{
					var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
					if (typeSymbol == null)
						continue;

					// Check if this type derives from the containing type
					if (!DerivesFrom(typeSymbol, containingType))
						continue;

					// Check if this type overrides the method
					foreach (var member in typeSymbol.GetMembers(methodSymbol.Name).OfType<IMethodSymbol>())
					{
						if (member.IsOverride && member.OverriddenMethod != null)
						{
							// Use OriginalDefinition to handle generic types
							var overriddenOriginal = member.OverriddenMethod.OriginalDefinition;
							var methodOriginal = methodSymbol.OriginalDefinition;
							if (SymbolEqualityComparer.Default.Equals(overriddenOriginal, methodOriginal))
								return true;
						}
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Checks if a type derives from (directly or indirectly) another type.
		/// Uses OriginalDefinition to handle generic types.
		/// </summary>
		private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol potentialBase)
		{
			var potentialBaseOriginal = potentialBase.OriginalDefinition;
			var current = type.BaseType;
			while (current != null)
			{
				// Compare using OriginalDefinition to handle generics
				if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, potentialBaseOriginal))
					return true;
				current = current.BaseType;
			}
			return false;
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
