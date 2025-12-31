using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Analyzer that detects methods containing only comments with no actual statements.
	/// These often indicate dead code or incomplete implementations left by AI agents.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class CommentOnlyMethodAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.CommentOnlyMethod);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
		{
			var method = (MethodDeclarationSyntax)context.Node;

			// Skip methods without bodies (abstract, interface, expression-bodied)
			if (method.Body == null)
			{
				return;
			}

			// Skip if there are any actual statements
			if (method.Body.Statements.Count > 0)
			{
				return;
			}

			// Skip virtual methods - they may be overridden by derived classes
			if (method.Modifiers.Any(SyntaxKind.VirtualKeyword))
			{
				return;
			}

			// Skip override methods - base class requires the implementation
			if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
			{
				return;
			}

			// Skip methods that implement interface members
			var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
			if (methodSymbol != null && ImplementsInterfaceMember(methodSymbol))
			{
				return;
			}

			// Check if the body contains any comments
			var hasComments = method.Body.DescendantTrivia()
				.Any(trivia =>
					trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
					trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
					trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
					trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

			if (hasComments)
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.CommentOnlyMethod,
					method.Identifier.GetLocation(),
					method.Identifier.Text);

				context.ReportDiagnostic(diagnostic);
			}
		}

		private static bool ImplementsInterfaceMember(IMethodSymbol methodSymbol)
		{
			if (methodSymbol.ContainingType == null)
			{
				return false;
			}

			// Check explicit interface implementations
			if (methodSymbol.ExplicitInterfaceImplementations.Length > 0)
			{
				return true;
			}

			// Check implicit interface implementations
			foreach (var iface in methodSymbol.ContainingType.AllInterfaces)
			{
				foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
				{
					var implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(member);
					if (SymbolEqualityComparer.Default.Equals(implementation, methodSymbol))
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
