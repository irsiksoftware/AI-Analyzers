using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU1100, ISU1101, ISU1102: Property/hash string usage.
	/// Adds a static readonly field with cached ID and replaces the string argument.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertyIdCodeFixProvider))]
	[Shared]
	public class PropertyIdCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(
				DiagnosticIds.ShaderPropertyString,
				DiagnosticIds.MaterialPropertyString,
				DiagnosticIds.AnimatorStringState);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			null; // Adding fields is complex for batch fixing

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

			var stringValue = literalExpression.Token.ValueText;
			var isAnimator = diagnostic.Id == DiagnosticIds.AnimatorStringState;
			var fieldName = GenerateFieldName(stringValue, isAnimator);

			var title = isAnimator
				? $"Cache as Animator.StringToHash ({fieldName})"
				: $"Cache as Shader.PropertyToID ({fieldName})";

			context.RegisterCodeFix(
				CodeAction.Create(
					title: title,
					createChangedDocument: c => CachePropertyIdAsync(
						context.Document, literalExpression, stringValue, fieldName, isAnimator, c),
					equivalenceKey: nameof(PropertyIdCodeFixProvider) + diagnostic.Id),
				diagnostic);
		}

		private static string GenerateFieldName(string propertyName, bool isAnimator)
		{
			// Remove leading underscore for shader properties
			var baseName = propertyName.TrimStart('_');

			// Convert to PascalCase if needed
			if (baseName.Length > 0 && char.IsLower(baseName[0]))
			{
				baseName = char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
			}

			// Add appropriate suffix
			return isAnimator ? baseName + "Hash" : baseName + "Id";
		}

		private static async Task<Document> CachePropertyIdAsync(
			Document document,
			LiteralExpressionSyntax literalExpression,
			string propertyName,
			string fieldName,
			bool isAnimator,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Find the containing class
			var classDeclaration = literalExpression.Ancestors()
				.OfType<ClassDeclarationSyntax>()
				.FirstOrDefault();

			if (classDeclaration == null)
				return document;

			// Check if field already exists
			var existingField = classDeclaration.Members
				.OfType<FieldDeclarationSyntax>()
				.FirstOrDefault(f => f.Declaration.Variables
					.Any(v => v.Identifier.ValueText == fieldName));

			SyntaxNode newRoot;

			if (existingField != null)
			{
				// Field exists, just replace the literal with field reference
				newRoot = root.ReplaceNode(
					literalExpression,
					SyntaxFactory.IdentifierName(fieldName)
						.WithLeadingTrivia(literalExpression.GetLeadingTrivia())
						.WithTrailingTrivia(literalExpression.GetTrailingTrivia()));
			}
			else
			{
				// Create the static readonly field
				var hashMethod = isAnimator ? "Animator.StringToHash" : "Shader.PropertyToID";

				var fieldDeclaration = SyntaxFactory.FieldDeclaration(
					SyntaxFactory.VariableDeclaration(
						SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)))
					.WithVariables(
						SyntaxFactory.SingletonSeparatedList(
							SyntaxFactory.VariableDeclarator(
								SyntaxFactory.Identifier(fieldName))
							.WithInitializer(
								SyntaxFactory.EqualsValueClause(
									SyntaxFactory.InvocationExpression(
										SyntaxFactory.ParseExpression(hashMethod),
										SyntaxFactory.ArgumentList(
											SyntaxFactory.SingletonSeparatedList(
												SyntaxFactory.Argument(
													SyntaxFactory.LiteralExpression(
														SyntaxKind.StringLiteralExpression,
														SyntaxFactory.Literal(propertyName)))))))))))
					.WithModifiers(
						SyntaxFactory.TokenList(
							SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
							SyntaxFactory.Token(SyntaxKind.StaticKeyword),
							SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
					.WithAdditionalAnnotations(Formatter.Annotation);

				// Find insertion point - after existing static readonly int fields, or at start
				var insertIndex = 0;
				for (var i = 0; i < classDeclaration.Members.Count; i++)
				{
					if (classDeclaration.Members[i] is FieldDeclarationSyntax existingFieldDecl &&
					    existingFieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword) &&
					    existingFieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
					{
						insertIndex = i + 1;
					}
				}

				var newClassDeclaration = classDeclaration.WithMembers(
					classDeclaration.Members.Insert(insertIndex, fieldDeclaration));

				// Replace both the class and the literal
				newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

				// Now find and replace the literal in the updated tree
				var updatedLiteral = newRoot.DescendantNodes()
					.OfType<LiteralExpressionSyntax>()
					.FirstOrDefault(l =>
						l.Token.ValueText == propertyName &&
						l.SpanStart >= literalExpression.SpanStart - 100 &&
						l.SpanStart <= literalExpression.SpanStart + 100);

				if (updatedLiteral != null)
				{
					newRoot = newRoot.ReplaceNode(
						updatedLiteral,
						SyntaxFactory.IdentifierName(fieldName)
							.WithLeadingTrivia(updatedLiteral.GetLeadingTrivia())
							.WithTrailingTrivia(updatedLiteral.GetTrailingTrivia()));
				}
			}

			return document.WithSyntaxRoot(newRoot);
		}
	}
}
