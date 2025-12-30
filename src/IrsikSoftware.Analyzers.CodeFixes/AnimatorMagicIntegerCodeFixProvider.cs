using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU4001: Animator magic integers.
	/// Creates or appends to a {ClassName}State enum in /Scripts/Enums/.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AnimatorMagicIntegerCodeFixProvider))]
	[Shared]
	public class AnimatorMagicIntegerCodeFixProvider : CodeFixProvider
	{
		private const string Title = "Create state enum value";
		private const string EnumsFolderPath = "Scripts/Enums";

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.AnimatorMagicInteger);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			null; // Complex multi-file fix, not suitable for batch

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var node = root.FindNode(diagnosticSpan);

			// Find the containing class
			var classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDeclaration == null)
				return;

			// Get the magic number value
			if (node is not LiteralExpressionSyntax literal)
				return;

			if (!int.TryParse(literal.Token.ValueText, out var magicValue))
				return;

			var className = classDeclaration.Identifier.ValueText;
			var enumName = $"{className}State";
			var enumMemberName = $"State_{magicValue}";

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Create '{enumName}.{enumMemberName} = {magicValue}'",
					createChangedSolution: c => CreateOrAppendEnumAsync(
						context.Document, classDeclaration, enumName, enumMemberName, magicValue, literal, c),
					equivalenceKey: nameof(AnimatorMagicIntegerCodeFixProvider)),
				diagnostic);
		}

		private static async Task<Solution> CreateOrAppendEnumAsync(
			Document document,
			ClassDeclarationSyntax classDeclaration,
			string enumName,
			string enumMemberName,
			int enumValue,
			LiteralExpressionSyntax literalToReplace,
			CancellationToken cancellationToken)
		{
			var solution = document.Project.Solution;

			// Try to find existing enum file in /Scripts/Enums/
			var existingEnumDocument = FindEnumDocument(solution, enumName);

			if (existingEnumDocument != null)
			{
				// Append to existing enum and replace usage
				solution = await AppendToExistingEnumAsync(
					solution, existingEnumDocument, document, enumName, enumMemberName, enumValue, literalToReplace, cancellationToken);
			}
			else
			{
				// Create new enum file and replace usage
				solution = await CreateNewEnumFileAsync(
					solution, document, enumName, enumMemberName, enumValue, literalToReplace, cancellationToken);
			}

			return solution;
		}

		private static Document? FindEnumDocument(Solution solution, string enumName)
		{
			// Look for a file named {enumName}.cs in any project
			foreach (var project in solution.Projects)
			{
				foreach (var doc in project.Documents)
				{
					if (doc.Name == $"{enumName}.cs")
					{
						var path = doc.FilePath ?? doc.Name;
						var normalizedPath = path.Replace('\\', '/');
						if (normalizedPath.Contains($"/{EnumsFolderPath}/") ||
						    normalizedPath.EndsWith($"/{EnumsFolderPath}/{enumName}.cs"))
						{
							return doc;
						}
					}
				}
			}
			return null;
		}

		private static async Task<Solution> AppendToExistingEnumAsync(
			Solution solution,
			Document enumDocument,
			Document usageDocument,
			string enumName,
			string enumMemberName,
			int enumValue,
			LiteralExpressionSyntax literalToReplace,
			CancellationToken cancellationToken)
		{
			var enumRoot = await enumDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (enumRoot == null)
				return solution;

			// Find the enum declaration
			var enumDeclaration = enumRoot.DescendantNodes()
				.OfType<EnumDeclarationSyntax>()
				.FirstOrDefault(e => e.Identifier.ValueText == enumName);

			if (enumDeclaration == null)
				return solution;

			// Check if value already exists
			var existingMember = enumDeclaration.Members
				.FirstOrDefault(m =>
				{
					if (m.EqualsValue?.Value is LiteralExpressionSyntax lit)
					{
						return lit.Token.ValueText == enumValue.ToString();
					}
					return false;
				});

			string actualMemberName;
			if (existingMember != null)
			{
				// Use existing member name
				actualMemberName = existingMember.Identifier.ValueText;
			}
			else
			{
				// Add new member
				actualMemberName = enumMemberName;
				var newMember = SyntaxFactory.EnumMemberDeclaration(enumMemberName)
					.WithEqualsValue(SyntaxFactory.EqualsValueClause(
						SyntaxFactory.LiteralExpression(
							SyntaxKind.NumericLiteralExpression,
							SyntaxFactory.Literal(enumValue))))
					.WithAdditionalAnnotations(Formatter.Annotation);

				var newEnumDeclaration = enumDeclaration.AddMembers(newMember);
				var newEnumRoot = enumRoot.ReplaceNode(enumDeclaration, newEnumDeclaration);
				solution = solution.WithDocumentSyntaxRoot(enumDocument.Id, newEnumRoot);
			}

			// Replace the literal with enum cast in usage document
			solution = await ReplaceLiteralWithEnumCastAsync(
				solution, usageDocument, enumName, actualMemberName, literalToReplace, cancellationToken);

			return solution;
		}

		private static async Task<Solution> CreateNewEnumFileAsync(
			Solution solution,
			Document usageDocument,
			string enumName,
			string enumMemberName,
			int enumValue,
			LiteralExpressionSyntax literalToReplace,
			CancellationToken cancellationToken)
		{
			// Determine namespace from usage document
			var usageRoot = await usageDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			var namespaceDecl = usageRoot?.DescendantNodes()
				.OfType<BaseNamespaceDeclarationSyntax>()
				.FirstOrDefault();

			var namespaceName = namespaceDecl?.Name.ToString() ?? "GameEnums";

			// Create enum source
			var enumSource = $@"namespace {namespaceName}
{{
	/// <summary>
	/// State enum for animator state machine.
	/// TODO: Rename enum members to match animation state names.
	/// </summary>
	public enum {enumName}
	{{
		{enumMemberName} = {enumValue}
	}}
}}
";

			// Try to find Scripts/Enums folder path
			var documentPath = usageDocument.FilePath;
			string enumFilePath;

			if (documentPath != null && documentPath.Length > 0)
			{
				// Find project root by looking for Scripts folder
				var normalizedPath = documentPath.Replace('\\', '/');
				var scriptsIndex = normalizedPath.IndexOf("/Scripts/");
				if (scriptsIndex >= 0)
				{
					var projectRoot = normalizedPath.Substring(0, scriptsIndex);
					enumFilePath = Path.Combine(projectRoot, "Scripts", "Enums", $"{enumName}.cs");
				}
				else
				{
					// Fallback: create relative to document
					var docDir = Path.GetDirectoryName(documentPath) ?? "";
					enumFilePath = Path.Combine(docDir, "..", "Enums", $"{enumName}.cs");
				}
			}
			else
			{
				enumFilePath = $"Scripts/Enums/{enumName}.cs";
			}

			// Add document to project
			var newDocument = usageDocument.Project.AddDocument(
				$"{enumName}.cs",
				SourceText.From(enumSource),
				folders: new[] { "Scripts", "Enums" },
				filePath: enumFilePath);

			solution = newDocument.Project.Solution;

			// Replace the literal with enum cast in usage document
			solution = await ReplaceLiteralWithEnumCastAsync(
				solution, solution.GetDocument(usageDocument.Id)!, enumName, enumMemberName, literalToReplace, cancellationToken);

			return solution;
		}

		private static async Task<Solution> ReplaceLiteralWithEnumCastAsync(
			Solution solution,
			Document document,
			string enumName,
			string memberName,
			LiteralExpressionSyntax literalToReplace,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return solution;

			// Find the literal in the current tree (it may have moved)
			var literal = root.FindNode(literalToReplace.Span) as LiteralExpressionSyntax;
			if (literal == null)
			{
				// Try to find by text span
				literal = root.DescendantNodes()
					.OfType<LiteralExpressionSyntax>()
					.FirstOrDefault(l => l.Span == literalToReplace.Span);
			}

			if (literal == null)
				return solution;

			// Create cast expression: (int)EnumName.MemberName
			var enumMemberAccess = SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				SyntaxFactory.IdentifierName(enumName),
				SyntaxFactory.IdentifierName(memberName));

			var castExpression = SyntaxFactory.CastExpression(
				SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
				enumMemberAccess)
				.WithLeadingTrivia(literal.GetLeadingTrivia())
				.WithTrailingTrivia(literal.GetTrailingTrivia());

			var newRoot = root.ReplaceNode(literal, castExpression);
			return solution.WithDocumentSyntaxRoot(document.Id, newRoot);
		}
	}
}
