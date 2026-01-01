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

			var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
			if (methodSymbol == null)
			{
				return;
			}

			// For virtual methods: only skip if actually overridden somewhere
			// An empty virtual with no overrides is dead code
			if (method.Modifiers.Any(SyntaxKind.VirtualKeyword))
			{
				if (IsOverriddenAnywhere(methodSymbol, context.SemanticModel.Compilation))
				{
					return;
				}
				// Not overridden - fall through to flag it
			}

			// Skip methods that implement interface members (required by contract)
			if (ImplementsInterfaceMember(methodSymbol))
			{
				return;
			}

			// Check if the body contains any comments (or is just empty braces)
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

		/// <summary>
		/// Checks if a virtual method is overridden by any type in the compilation.
		/// </summary>
		private static bool IsOverriddenAnywhere(IMethodSymbol methodSymbol, Compilation compilation)
		{
			if (!methodSymbol.IsVirtual)
			{
				return false;
			}

			var containingType = methodSymbol.ContainingType;
			if (containingType == null)
			{
				return false;
			}

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
					{
						continue;
					}

					// Check if this type derives from the containing type
					if (!DerivesFrom(typeSymbol, containingType))
					{
						continue;
					}

					// Check if this type overrides the method
					foreach (var member in typeSymbol.GetMembers(methodSymbol.Name).OfType<IMethodSymbol>())
					{
						if (member.IsOverride && member.OverriddenMethod != null)
						{
							// Use OriginalDefinition to handle generic types
							// e.g., AbilityBehavior<T,TK>.OnCleanup vs AbilityBehavior<SomeT,SomeTK>.OnCleanup
							var overriddenOriginal = member.OverriddenMethod.OriginalDefinition;
							var methodOriginal = methodSymbol.OriginalDefinition;
							if (SymbolEqualityComparer.Default.Equals(overriddenOriginal, methodOriginal))
							{
								return true;
							}
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
				// e.g., AbilityBehavior<MeteorData, MeteorLevel> derives from AbilityBehavior<T, TK>
				if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, potentialBaseOriginal))
				{
					return true;
				}
				current = current.BaseType;
			}
			return false;
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
