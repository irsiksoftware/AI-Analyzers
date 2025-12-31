using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Rename;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU0004: Avoid abbreviations.
	/// Renames identifier to use full name across all references.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidAbbreviationsCodeFixProvider))]
	[Shared]
	public class AvoidAbbreviationsCodeFixProvider : CodeFixProvider
	{
		// Known abbreviations and their preferred full names
		private static readonly Dictionary<string, string> Abbreviations = new()
		{
			{ "rng", "randomNumberGenerator" },
			{ "ctx", "context" },
			{ "cfg", "config" },
			{ "btn", "button" },
			{ "mgr", "manager" },
			{ "svc", "service" },
			{ "idx", "index" },
			{ "cnt", "count" },
			{ "msg", "message" },
			{ "req", "request" },
			{ "res", "response" },
			{ "src", "source" },
			{ "dst", "destination" },
			{ "tmp", "temp" },
			{ "ptr", "pointer" },
			{ "val", "value" },
			{ "len", "length" },
			{ "pos", "position" },
			{ "vel", "velocity" },
			{ "dir", "direction" },
			{ "rot", "rotation" },
			{ "xfrm", "transform" },
			{ "cb", "callback" },
			{ "evt", "event" },
			{ "impl", "implementation" },
			{ "calc", "calculate" },
			{ "proc", "process" }
		};

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.AvoidAbbreviations);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var token = root.FindToken(diagnosticSpan.Start);

			var newName = GetSuggestedName(token.ValueText);
			if (newName == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Rename to '{newName}'",
					createChangedSolution: c => RenameSymbolAsync(context.Document, diagnostic, newName, c),
					equivalenceKey: nameof(AvoidAbbreviationsCodeFixProvider)),
				diagnostic);
		}

		private static string? GetSuggestedName(string originalName)
		{
			var prefix = "";
			var workingName = originalName;

			// Handle underscore prefix
			if (workingName.StartsWith("_"))
			{
				prefix = "_";
				workingName = workingName.Substring(1);
			}

			// Check for exact match (case insensitive)
			var lowerName = workingName.ToLowerInvariant();
			if (Abbreviations.TryGetValue(lowerName, out var fullName))
			{
				// Preserve original casing style
				if (char.IsUpper(workingName[0]))
				{
					fullName = char.ToUpperInvariant(fullName[0]) + fullName.Substring(1);
				}
				return prefix + fullName;
			}

			// Try to find and replace abbreviation within the name
			foreach (var kvp in Abbreviations)
			{
				var pattern = $@"(?i)^({kvp.Key})([A-Z_]|$)";
				var match = Regex.Match(workingName, pattern);

				if (match.Success)
				{
					var replacement = kvp.Value;
					// Match case of original
					if (char.IsUpper(match.Groups[1].Value[0]))
					{
						replacement = char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
					}

					var newName = Regex.Replace(workingName, pattern, replacement + "$2");
					return prefix + newName;
				}
			}

			return null;
		}

		private static async Task<Solution> RenameSymbolAsync(
			Document document,
			Diagnostic diagnostic,
			string newName,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document.Project.Solution;

			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
			if (semanticModel == null)
				return document.Project.Solution;

			var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
			var node = token.Parent;
			if (node == null)
				return document.Project.Solution;

			// Get the symbol - try declaration first, then symbol info
			var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken)
				?? semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;

			if (symbol == null)
				return document.Project.Solution;

			// Use Renamer to rename all references
			var newSolution = await Renamer.RenameSymbolAsync(
				document.Project.Solution,
				symbol,
				new SymbolRenameOptions(),
				newName,
				cancellationToken).ConfigureAwait(false);

			return newSolution;
		}
	}
}
