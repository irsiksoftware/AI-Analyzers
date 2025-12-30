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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU0004: Avoid abbreviations.
	/// Renames identifier to use full name.
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
			null; // Rename operations don't work well with batch fixing

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
					createChangedSolution: c => RenameIdentifierAsync(context.Document, token, newName, c),
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

		private static async Task<Solution> RenameIdentifierAsync(
			Document document,
			SyntaxToken token,
			string newName,
			CancellationToken cancellationToken)
		{
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
			if (semanticModel == null)
				return document.Project.Solution;

			// Find the symbol for this identifier
			var symbol = semanticModel.GetDeclaredSymbol(token.Parent!, cancellationToken);

			// If not a declaration, try to get symbol info
			if (symbol == null)
			{
				var symbolInfo = semanticModel.GetSymbolInfo(token.Parent!, cancellationToken);
				symbol = symbolInfo.Symbol;
			}

			if (symbol == null)
				return document.Project.Solution;

			// Use Roslyn's rename functionality
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
