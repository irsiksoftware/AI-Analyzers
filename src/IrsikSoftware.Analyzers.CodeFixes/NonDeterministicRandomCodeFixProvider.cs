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
using Microsoft.CodeAnalysis.Formatting;

namespace IrsikSoftware.Analyzers.CodeFixes
{
	/// <summary>
	/// Code fix provider for ISU4000: Non-deterministic Random usage.
	/// Injects IDeterministicRng and replaces Random.* calls.
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NonDeterministicRandomCodeFixProvider))]
	[Shared]
	public class NonDeterministicRandomCodeFixProvider : CodeFixProvider
	{
		private const string Title = "Inject IDeterministicRng";
		private const string FieldName = "_randomNumberGenerator";
		private const string ParameterName = "randomNumberGenerator";
		private const string InterfaceName = "IDeterministicRng";
		private const string Namespace = "IrsikSoftware.Determinism";

		// Maps UnityEngine.Random members to IDeterministicRng methods
		private static readonly Dictionary<string, string> RandomMemberMappings = new()
		{
			{ "value", "Value()" },
			{ "insideUnitCircle", "InsideUnitCircle()" },
			{ "insideUnitSphere", "InsideUnitSphere()" },
			{ "onUnitSphere", "OnUnitSphere()" },
			{ "onUnitCircle", "OnUnitCircle()" }
		};

		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticIds.NonDeterministicInSimulate);

		public sealed override FixAllProvider? GetFixAllProvider() =>
			null; // Complex multi-step fix, not suitable for batch

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Only offer fix for Random.* usages, not Time.* or Find*
			var node = root.FindNode(diagnosticSpan);
			if (!IsRandomUsage(node))
				return;

			var classDeclaration = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDeclaration == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: Title,
					createChangedDocument: c => InjectDeterministicRngAsync(context.Document, classDeclaration, c),
					equivalenceKey: nameof(NonDeterministicRandomCodeFixProvider)),
				diagnostic);
		}

		private static bool IsRandomUsage(SyntaxNode node)
		{
			// Check if this is a Random.* member access
			if (node is MemberAccessExpressionSyntax memberAccess)
			{
				if (memberAccess.Expression is IdentifierNameSyntax identifier)
				{
					return identifier.Identifier.ValueText == "Random";
				}
			}

			// Check parent if we're on the identifier
			if (node.Parent is MemberAccessExpressionSyntax parentAccess)
			{
				if (parentAccess.Expression is IdentifierNameSyntax parentIdentifier)
				{
					return parentIdentifier.Identifier.ValueText == "Random";
				}
			}

			return false;
		}

		private static async Task<Document> InjectDeterministicRngAsync(
			Document document,
			ClassDeclarationSyntax classDeclaration,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

			if (root == null || semanticModel == null)
				return document;

			var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);
			if (classSymbol == null)
				return document;

			var isMonoBehaviour = IsMonoBehaviour(classSymbol);
			var newClassDeclaration = classDeclaration;

			// Step 1: Check if field already exists
			var hasField = classDeclaration.Members
				.OfType<FieldDeclarationSyntax>()
				.Any(f => f.Declaration.Variables
					.Any(v => v.Identifier.ValueText == FieldName));

			if (!hasField)
			{
				// Add field: private IDeterministicRng _randomNumberGenerator;
				var fieldDeclaration = CreateField();
				newClassDeclaration = newClassDeclaration.WithMembers(
					newClassDeclaration.Members.Insert(0, fieldDeclaration));
			}

			// Step 2: Add parameter to Construct (MonoBehaviour) or constructor (POCO)
			if (isMonoBehaviour)
			{
				newClassDeclaration = AddToConstructMethod(newClassDeclaration);
			}
			else
			{
				newClassDeclaration = AddToConstructor(newClassDeclaration);
			}

			// Step 3: Replace all Random.* usages in the class
			newClassDeclaration = ReplaceRandomUsages(newClassDeclaration);

			// Step 4: Apply changes
			var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

			// Step 5: Add using directive if needed
			newRoot = AddUsingDirective(newRoot);

			return document.WithSyntaxRoot(newRoot);
		}

		private static bool IsMonoBehaviour(INamedTypeSymbol classSymbol)
		{
			var current = classSymbol.BaseType;
			while (current != null)
			{
				if (current.Name == "MonoBehaviour" &&
				    current.ContainingNamespace?.ToDisplayString() == "UnityEngine")
				{
					return true;
				}
				current = current.BaseType;
			}
			return false;
		}

		private static FieldDeclarationSyntax CreateField()
		{
			return SyntaxFactory.FieldDeclaration(
				SyntaxFactory.VariableDeclaration(
					SyntaxFactory.IdentifierName(InterfaceName))
				.WithVariables(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.VariableDeclarator(
							SyntaxFactory.Identifier(FieldName)))))
				.WithModifiers(
					SyntaxFactory.TokenList(
						SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
				.WithAdditionalAnnotations(Formatter.Annotation);
		}

		private static ClassDeclarationSyntax AddToConstructMethod(ClassDeclarationSyntax classDeclaration)
		{
			// Find existing [Inject] Construct method
			var constructMethod = classDeclaration.Members
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(m =>
					m.Identifier.ValueText == "Construct" &&
					m.AttributeLists.Any(al =>
						al.Attributes.Any(a =>
							a.Name.ToString().Contains("Inject"))));

			if (constructMethod != null)
			{
				// Check if parameter already exists
				var hasParam = constructMethod.ParameterList.Parameters
					.Any(p => p.Type?.ToString() == InterfaceName);

				if (!hasParam)
				{
					// Add parameter
					var newParam = SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(ParameterName))
						.WithType(SyntaxFactory.IdentifierName(InterfaceName));

					var newParamList = constructMethod.ParameterList.AddParameters(newParam);
					var newConstructMethod = constructMethod.WithParameterList(newParamList);

					// Add assignment to body
					var assignment = SyntaxFactory.ExpressionStatement(
						SyntaxFactory.AssignmentExpression(
							SyntaxKind.SimpleAssignmentExpression,
							SyntaxFactory.IdentifierName(FieldName),
							SyntaxFactory.IdentifierName(ParameterName)));

					var newBody = constructMethod.Body?.AddStatements(assignment) ??
						SyntaxFactory.Block(assignment);
					newConstructMethod = newConstructMethod.WithBody(newBody);

					return classDeclaration.ReplaceNode(constructMethod, newConstructMethod);
				}
			}
			else
			{
				// Create new Construct method
				var newConstructMethod = SyntaxFactory.MethodDeclaration(
					SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
					"Construct")
					.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
					.WithAttributeLists(SyntaxFactory.SingletonList(
						SyntaxFactory.AttributeList(
							SyntaxFactory.SingletonSeparatedList(
								SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Inject"))))))
					.WithParameterList(SyntaxFactory.ParameterList(
						SyntaxFactory.SingletonSeparatedList(
							SyntaxFactory.Parameter(SyntaxFactory.Identifier(ParameterName))
								.WithType(SyntaxFactory.IdentifierName(InterfaceName)))))
					.WithBody(SyntaxFactory.Block(
						SyntaxFactory.ExpressionStatement(
							SyntaxFactory.AssignmentExpression(
								SyntaxKind.SimpleAssignmentExpression,
								SyntaxFactory.IdentifierName(FieldName),
								SyntaxFactory.IdentifierName(ParameterName)))))
					.WithAdditionalAnnotations(Formatter.Annotation);

				return classDeclaration.AddMembers(newConstructMethod);
			}

			return classDeclaration;
		}

		private static ClassDeclarationSyntax AddToConstructor(ClassDeclarationSyntax classDeclaration)
		{
			var constructor = classDeclaration.Members
				.OfType<ConstructorDeclarationSyntax>()
				.FirstOrDefault();

			if (constructor != null)
			{
				// Check if parameter already exists
				var hasParam = constructor.ParameterList.Parameters
					.Any(p => p.Type?.ToString() == InterfaceName);

				if (!hasParam)
				{
					// Add parameter
					var newParam = SyntaxFactory.Parameter(
						SyntaxFactory.Identifier(ParameterName))
						.WithType(SyntaxFactory.IdentifierName(InterfaceName));

					var newParamList = constructor.ParameterList.AddParameters(newParam);
					var newConstructor = constructor.WithParameterList(newParamList);

					// Add assignment to body
					var assignment = SyntaxFactory.ExpressionStatement(
						SyntaxFactory.AssignmentExpression(
							SyntaxKind.SimpleAssignmentExpression,
							SyntaxFactory.IdentifierName(FieldName),
							SyntaxFactory.IdentifierName(ParameterName)));

					var newBody = constructor.Body?.AddStatements(assignment) ??
						SyntaxFactory.Block(assignment);
					newConstructor = newConstructor.WithBody(newBody);

					return classDeclaration.ReplaceNode(constructor, newConstructor);
				}
			}
			else
			{
				// Create new constructor
				var className = classDeclaration.Identifier.ValueText;
				var newConstructor = SyntaxFactory.ConstructorDeclaration(className)
					.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
					.WithParameterList(SyntaxFactory.ParameterList(
						SyntaxFactory.SingletonSeparatedList(
							SyntaxFactory.Parameter(SyntaxFactory.Identifier(ParameterName))
								.WithType(SyntaxFactory.IdentifierName(InterfaceName)))))
					.WithBody(SyntaxFactory.Block(
						SyntaxFactory.ExpressionStatement(
							SyntaxFactory.AssignmentExpression(
								SyntaxKind.SimpleAssignmentExpression,
								SyntaxFactory.IdentifierName(FieldName),
								SyntaxFactory.IdentifierName(ParameterName)))))
					.WithAdditionalAnnotations(Formatter.Annotation);

				return classDeclaration.AddMembers(newConstructor);
			}

			return classDeclaration;
		}

		private static ClassDeclarationSyntax ReplaceRandomUsages(ClassDeclarationSyntax classDeclaration)
		{
			// Find all Random.* member accesses and replace them
			var randomAccesses = classDeclaration.DescendantNodes()
				.OfType<MemberAccessExpressionSyntax>()
				.Where(m => m.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "Random")
				.ToList();

			if (!randomAccesses.Any())
				return classDeclaration;

			return classDeclaration.ReplaceNodes(
				randomAccesses,
				(original, _) => ReplaceRandomMemberAccess(original));
		}

		private static SyntaxNode ReplaceRandomMemberAccess(MemberAccessExpressionSyntax memberAccess)
		{
			var memberName = memberAccess.Name.Identifier.ValueText;

			// Check for property access (value, insideUnitCircle, etc.)
			if (RandomMemberMappings.TryGetValue(memberName, out var replacement))
			{
				// Replace Random.value with _randomNumberGenerator.Value()
				var newExpression = SyntaxFactory.ParseExpression($"{FieldName}.{replacement}")
					.WithLeadingTrivia(memberAccess.GetLeadingTrivia())
					.WithTrailingTrivia(memberAccess.GetTrailingTrivia());
				return newExpression;
			}

			// For method calls like Random.Range, just replace Random with field name
			// The parent InvocationExpression will handle the full call
			if (memberName == "Range")
			{
				return SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					SyntaxFactory.IdentifierName(FieldName),
					memberAccess.Name)
					.WithLeadingTrivia(memberAccess.GetLeadingTrivia())
					.WithTrailingTrivia(memberAccess.GetTrailingTrivia());
			}

			// For unrecognized members, still replace Random with field but add TODO
			return SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				SyntaxFactory.IdentifierName(FieldName),
				memberAccess.Name)
				.WithLeadingTrivia(memberAccess.GetLeadingTrivia())
				.WithTrailingTrivia(memberAccess.GetTrailingTrivia());
		}

		private static SyntaxNode AddUsingDirective(SyntaxNode root)
		{
			if (root is not CompilationUnitSyntax compilationUnit)
				return root;

			// Check if using already exists
			var hasUsing = compilationUnit.Usings
				.Any(u => u.Name?.ToString() == Namespace);

			if (hasUsing)
				return root;

			var newUsing = SyntaxFactory.UsingDirective(
				SyntaxFactory.ParseName(Namespace))
				.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

			return compilationUnit.AddUsings(newUsing);
		}
	}
}
