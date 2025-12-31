using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Editing;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU3100: Use container.Instantiate.
	/// Replaces Object.Instantiate with resolver.Instantiate for DI support.
	/// If no IObjectResolver exists in the class, adds one to the constructor or [Inject] method.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VContainerInstantiateCodeFixProvider))]
	[Shared]
	public class VContainerInstantiateCodeFixProvider : CodeFixProvider
	{
		private const string ResolverFieldName = "resolver";
		private const string ResolverTypeName = "IObjectResolver";

		private static readonly string[] ResolverTypeNames =
		{
			"IObjectResolver",
			"IContainerBuilder",
			"LifetimeScope"
		};

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.UseContainerInstantiate);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var invocation = root.FindToken(diagnosticSpan.Start).Parent?
				.AncestorsAndSelf()
				.OfType<InvocationExpressionSyntax>()
				.FirstOrDefault();

			if (invocation == null)
				return;

			// Find the containing class
			var classDeclaration = invocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (classDeclaration == null)
				return;

			var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (semanticModel == null)
				return;

			var resolverName = FindResolverMemberName(classDeclaration, semanticModel);

			if (resolverName != null)
			{
				// Simple case: resolver already exists, just replace the call
				context.RegisterCodeFix(
					CodeAction.Create(
						title: $"Use {resolverName}.Instantiate",
						createChangedSolution: c => ReplaceWithContainerInstantiateAsync(context.Document, diagnostic, resolverName, c),
						equivalenceKey: nameof(VContainerInstantiateCodeFixProvider)),
					diagnostic);
			}
			else
			{
				// Complex case: need to add resolver field and parameter
				var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
				var isMonoBehaviour = IsMonoBehaviour(classSymbol);

				var title = isMonoBehaviour
					? "Add IObjectResolver to [Inject] method and use resolver.Instantiate"
					: "Add IObjectResolver to constructor and use resolver.Instantiate";

				context.RegisterCodeFix(
					CodeAction.Create(
						title: title,
						createChangedSolution: c => AddResolverAndReplaceAsync(context.Document, diagnostic, classDeclaration, isMonoBehaviour, c),
						equivalenceKey: nameof(VContainerInstantiateCodeFixProvider) + "_AddResolver"),
					diagnostic);
			}
		}

		private static string? FindResolverMemberName(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
		{
			var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
			if (classSymbol == null)
				return null;

			// Check current class and base classes for resolver members
			var currentClass = classSymbol;
			while (currentClass != null)
			{
				foreach (var member in currentClass.GetMembers())
				{
					ITypeSymbol? type = member switch
					{
						IFieldSymbol f => f.Type,
						IPropertySymbol p => p.Type,
						_ => null
					};

					if (type == null)
						continue;

					if (ResolverTypeNames.Contains(type.Name) ||
					    type.AllInterfaces.Any(i => ResolverTypeNames.Contains(i.Name)))
					{
						return member.Name;
					}
				}
				currentClass = currentClass.BaseType;
			}

			return null;
		}

		private static bool IsMonoBehaviour(INamedTypeSymbol? classSymbol)
		{
			if (classSymbol == null)
				return false;

			var baseType = classSymbol.BaseType;
			while (baseType != null)
			{
				if (baseType.Name == "MonoBehaviour" ||
				    baseType.Name == "Component" ||
				    baseType.Name == "Behaviour" ||
				    baseType.Name == "ScriptableObject")
				{
					return true;
				}
				baseType = baseType.BaseType;
			}
			return false;
		}

		private static async Task<Solution> ReplaceWithContainerInstantiateAsync(
			Document document,
			Diagnostic diagnostic,
			string resolverName,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document.Project.Solution;

			var invocation = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?
				.AncestorsAndSelf()
				.OfType<InvocationExpressionSyntax>()
				.FirstOrDefault();

			if (invocation == null)
				return document.Project.Solution;

			var newRoot = ReplaceInstantiateCall(root, invocation, resolverName);
			newRoot = AddUsingDirectiveIfMissing(newRoot, "VContainer.Unity");

			var newDocument = document.WithSyntaxRoot(newRoot);
			return newDocument.Project.Solution;
		}

		private static async Task<Solution> AddResolverAndReplaceAsync(
			Document document,
			Diagnostic diagnostic,
			ClassDeclarationSyntax classDeclaration,
			bool isMonoBehaviour,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document.Project.Solution;

			var invocation = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?
				.AncestorsAndSelf()
				.OfType<InvocationExpressionSyntax>()
				.FirstOrDefault();

			if (invocation == null)
				return document.Project.Solution;

			// Re-find the class declaration in the current root (it may have changed)
			classDeclaration = root.FindToken(classDeclaration.Identifier.SpanStart).Parent?
				.AncestorsAndSelf()
				.OfType<ClassDeclarationSyntax>()
				.FirstOrDefault() ?? classDeclaration;

			var newClassDeclaration = classDeclaration;

			// Step 1: Add the field if it doesn't exist
			if (!HasResolverField(classDeclaration))
			{
				newClassDeclaration = AddResolverField(newClassDeclaration, isMonoBehaviour);
			}

			// Step 2: Add resolver parameter to constructor or [Inject] method
			if (isMonoBehaviour)
			{
				newClassDeclaration = AddOrUpdateInjectMethod(newClassDeclaration);
			}
			else
			{
				newClassDeclaration = AddResolverToConstructors(newClassDeclaration);
			}

			// Step 3: Replace the Instantiate call
			// We need to find the invocation in the new class declaration
			var newInvocation = newClassDeclaration.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.FirstOrDefault(inv =>
				{
					var memberAccess = inv.Expression as MemberAccessExpressionSyntax;
					if (memberAccess != null)
					{
						return memberAccess.Name.Identifier.Text == "Instantiate" &&
						       inv.SpanStart >= invocation.SpanStart - 100 && // Approximate match due to tree changes
						       inv.SpanStart <= invocation.SpanStart + 100;
					}
					var identifier = inv.Expression as IdentifierNameSyntax;
					return identifier?.Identifier.Text == "Instantiate";
				});

			if (newInvocation != null)
			{
				var replacedInvocation = CreateResolverInstantiateCall(newInvocation, ResolverFieldName);
				newClassDeclaration = newClassDeclaration.ReplaceNode(newInvocation, replacedInvocation);
			}

			// Apply changes to root
			var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

			// Add using directives
			newRoot = AddUsingDirectiveIfMissing(newRoot, "VContainer");
			newRoot = AddUsingDirectiveIfMissing(newRoot, "VContainer.Unity");

			var newDocument = document.WithSyntaxRoot(newRoot);
			return newDocument.Project.Solution;
		}

		private static bool HasResolverField(ClassDeclarationSyntax classDeclaration)
		{
			return classDeclaration.Members
				.OfType<FieldDeclarationSyntax>()
				.Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == ResolverFieldName));
		}

		private static ClassDeclarationSyntax AddResolverField(ClassDeclarationSyntax classDeclaration, bool isMonoBehaviour)
		{
			// For MonoBehaviours: private IObjectResolver resolver;
			// For pure C# classes: private readonly IObjectResolver resolver;
			var modifiers = isMonoBehaviour
				? SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
				: SyntaxFactory.TokenList(
					SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
					SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

			var fieldDeclaration = SyntaxFactory.FieldDeclaration(
				SyntaxFactory.VariableDeclaration(
					SyntaxFactory.IdentifierName(ResolverTypeName))
				.WithVariables(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.VariableDeclarator(
							SyntaxFactory.Identifier(ResolverFieldName)))))
				.WithModifiers(modifiers)
				.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed, SyntaxFactory.Tab, SyntaxFactory.Tab)
				.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

			// Find insertion point - after existing fields, before methods
			var insertionIndex = 0;
			var members = classDeclaration.Members;

			for (int i = 0; i < members.Count; i++)
			{
				if (members[i] is FieldDeclarationSyntax)
				{
					insertionIndex = i + 1;
				}
				else if (members[i] is not FieldDeclarationSyntax)
				{
					break;
				}
			}

			return classDeclaration.WithMembers(members.Insert(insertionIndex, fieldDeclaration));
		}

		private static ClassDeclarationSyntax AddOrUpdateInjectMethod(ClassDeclarationSyntax classDeclaration)
		{
			// Find existing [Inject] method named "Construct"
			var existingInjectMethod = classDeclaration.Members
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(m =>
					m.AttributeLists.Any(al =>
						al.Attributes.Any(a =>
							a.Name.ToString() == "Inject" || a.Name.ToString() == "InjectAttribute")) ||
					m.Identifier.Text == "Construct");

			if (existingInjectMethod != null)
			{
				// Add parameter to existing method if not already present
				if (!HasResolverParameter(existingInjectMethod.ParameterList))
				{
					var newParameter = SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(ResolverFieldName))
						.WithType(SyntaxFactory.IdentifierName(ResolverTypeName));

					var newParameterList = existingInjectMethod.ParameterList.AddParameters(newParameter);

					// Add assignment to method body
					var assignment = CreateResolverAssignment();
					var newBody = existingInjectMethod.Body?.AddStatements(assignment) ??
						SyntaxFactory.Block(assignment);

					var newMethod = existingInjectMethod
						.WithParameterList(newParameterList)
						.WithBody(newBody);

					return classDeclaration.ReplaceNode(existingInjectMethod, newMethod);
				}
				return classDeclaration;
			}

			// Create new [Inject] Construct method
			var constructMethod = CreateInjectConstructMethod();

			// Find insertion point - after fields, before other methods
			var insertionIndex = 0;
			var members = classDeclaration.Members;

			for (int i = 0; i < members.Count; i++)
			{
				if (members[i] is FieldDeclarationSyntax)
				{
					insertionIndex = i + 1;
				}
			}

			return classDeclaration.WithMembers(members.Insert(insertionIndex, constructMethod));
		}

		private static MethodDeclarationSyntax CreateInjectConstructMethod()
		{
			var parameter = SyntaxFactory.Parameter(
				SyntaxFactory.Identifier(ResolverFieldName))
				.WithType(SyntaxFactory.IdentifierName(ResolverTypeName));

			var assignment = CreateResolverAssignment();

			return SyntaxFactory.MethodDeclaration(
				SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
				SyntaxFactory.Identifier("Construct"))
				.WithAttributeLists(
					SyntaxFactory.SingletonList(
						SyntaxFactory.AttributeList(
							SyntaxFactory.SingletonSeparatedList(
								SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Inject"))))))
				.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
				.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(parameter)))
				.WithBody(SyntaxFactory.Block(assignment))
				.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed, SyntaxFactory.Tab, SyntaxFactory.Tab)
				.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
		}

		private static ClassDeclarationSyntax AddResolverToConstructors(ClassDeclarationSyntax classDeclaration)
		{
			var constructors = classDeclaration.Members
				.OfType<ConstructorDeclarationSyntax>()
				.ToList();

			if (constructors.Count == 0)
			{
				// No constructor - create one
				var newConstructor = CreateConstructorWithResolver(classDeclaration.Identifier.Text);

				// Find insertion point - after fields
				var insertionIndex = 0;
				var members = classDeclaration.Members;

				for (int i = 0; i < members.Count; i++)
				{
					if (members[i] is FieldDeclarationSyntax)
					{
						insertionIndex = i + 1;
					}
				}

				return classDeclaration.WithMembers(members.Insert(insertionIndex, newConstructor));
			}

			// Add resolver parameter to all existing constructors
			var newClassDeclaration = classDeclaration;
			foreach (var constructor in constructors)
			{
				if (!HasResolverParameter(constructor.ParameterList))
				{
					var currentConstructor = newClassDeclaration.Members
						.OfType<ConstructorDeclarationSyntax>()
						.First(c => c.SpanStart == constructor.SpanStart ||
						           c.Identifier.Text == constructor.Identifier.Text &&
						           c.ParameterList.Parameters.Count == constructor.ParameterList.Parameters.Count);

					var newParameter = SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(ResolverFieldName))
						.WithType(SyntaxFactory.IdentifierName(ResolverTypeName));

					var newParameterList = currentConstructor.ParameterList.AddParameters(newParameter);

					// Add assignment to constructor body
					var assignment = CreateResolverAssignment();
					var existingStatements = currentConstructor.Body?.Statements ??
						SyntaxFactory.List<StatementSyntax>();
					var newBody = SyntaxFactory.Block(existingStatements.Insert(0, assignment));

					var newConstructor = currentConstructor
						.WithParameterList(newParameterList)
						.WithBody(newBody);

					newClassDeclaration = newClassDeclaration.ReplaceNode(currentConstructor, newConstructor);
				}
			}

			return newClassDeclaration;
		}

		private static ConstructorDeclarationSyntax CreateConstructorWithResolver(string className)
		{
			var parameter = SyntaxFactory.Parameter(
				SyntaxFactory.Identifier(ResolverFieldName))
				.WithType(SyntaxFactory.IdentifierName(ResolverTypeName));

			var assignment = CreateResolverAssignment();

			return SyntaxFactory.ConstructorDeclaration(className)
				.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
				.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(parameter)))
				.WithBody(SyntaxFactory.Block(assignment))
				.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed, SyntaxFactory.Tab, SyntaxFactory.Tab)
				.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
		}

		private static StatementSyntax CreateResolverAssignment()
		{
			return SyntaxFactory.ExpressionStatement(
				SyntaxFactory.AssignmentExpression(
					SyntaxKind.SimpleAssignmentExpression,
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						SyntaxFactory.ThisExpression(),
						SyntaxFactory.IdentifierName(ResolverFieldName)),
					SyntaxFactory.IdentifierName(ResolverFieldName)));
		}

		private static bool HasResolverParameter(ParameterListSyntax parameterList)
		{
			return parameterList.Parameters.Any(p =>
			{
				var typeName = p.Type?.ToString() ?? "";
				return typeName == ResolverTypeName ||
				       typeName == $"VContainer.{ResolverTypeName}" ||
				       typeName.EndsWith($".{ResolverTypeName}");
			});
		}

		private static SyntaxNode ReplaceInstantiateCall(SyntaxNode root, InvocationExpressionSyntax invocation, string resolverName)
		{
			var newInvocation = CreateResolverInstantiateCall(invocation, resolverName);
			return root.ReplaceNode(invocation, newInvocation);
		}

		private static InvocationExpressionSyntax CreateResolverInstantiateCall(InvocationExpressionSyntax invocation, string resolverName)
		{
			var arguments = invocation.ArgumentList;

			var newInvocation = SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					SyntaxFactory.IdentifierName(resolverName),
					SyntaxFactory.IdentifierName("Instantiate")),
				arguments);

			return newInvocation
				.WithLeadingTrivia(invocation.GetLeadingTrivia())
				.WithTrailingTrivia(invocation.GetTrailingTrivia());
		}

		private static SyntaxNode AddUsingDirectiveIfMissing(SyntaxNode root, string namespaceName)
		{
			var compilationUnit = root as CompilationUnitSyntax;
			if (compilationUnit == null)
				return root;

			// Check if using already exists
			var hasUsing = compilationUnit.Usings
				.Any(u => u.Name?.ToString() == namespaceName);

			if (hasUsing)
				return root;

			// Create the using directive
			var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
				.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

			// Find insertion point - after last VContainer using, or at end of usings
			var usings = compilationUnit.Usings;
			var lastVContainerIndex = -1;

			for (int i = 0; i < usings.Count; i++)
			{
				var usingName = usings[i].Name?.ToString() ?? "";
				if (usingName.StartsWith("VContainer"))
				{
					lastVContainerIndex = i;
				}
			}

			if (lastVContainerIndex >= 0)
			{
				// Insert after last VContainer using
				usings = usings.Insert(lastVContainerIndex + 1, usingDirective);
			}
			else
			{
				// Add at the end
				usings = usings.Add(usingDirective);
			}

			return compilationUnit.WithUsings(usings);
		}
	}
}
