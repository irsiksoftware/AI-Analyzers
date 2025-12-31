using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects classes that use IObjectResolver.Instantiate() but have constructors
	/// or [Inject] methods that don't accept IObjectResolver as a parameter.
	/// This prevents null reference exceptions at runtime.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class MissingResolverInConstructorAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.MissingResolverInConstructor);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
		}

		private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
		{
			var classDeclaration = (ClassDeclarationSyntax)context.Node;
			var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

			if (classSymbol == null)
				return;

			// Find IObjectResolver fields/properties in the class
			var resolverMembers = GetResolverMembers(classSymbol);
			if (resolverMembers.Count == 0)
				return;

			// Check if any of these resolver members are used to call .Instantiate()
			if (!UsesResolverInstantiate(classDeclaration, resolverMembers, context.SemanticModel))
				return;

			// Check if this class uses [Inject] methods (MonoBehaviours or similar patterns)
			// or pure constructors for DI
			var isMonoBehaviour = IsMonoBehaviour(classSymbol);
			var hasInjectMethod = HasInjectMethodInHierarchy(classDeclaration, classSymbol, context);

			if (isMonoBehaviour || hasInjectMethod)
			{
				// Check [Inject] methods (in this class or base classes)
				CheckInjectMethods(classDeclaration, classSymbol, context);
			}
			else
			{
				// Check constructors
				CheckConstructors(classDeclaration, classSymbol, context);
			}
		}

		private static List<string> GetResolverMembers(INamedTypeSymbol classSymbol)
		{
			var resolverMembers = new List<string>();

			// Check this class and all base classes for resolver members
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

					// Check for IObjectResolver
					if (type.Name == "IObjectResolver" ||
					    type.AllInterfaces.Any(i => i.Name == "IObjectResolver"))
					{
						resolverMembers.Add(member.Name);
					}
				}
				currentClass = currentClass.BaseType;
			}

			return resolverMembers;
		}

		private static bool UsesResolverInstantiate(
			ClassDeclarationSyntax classDeclaration,
			List<string> resolverMembers,
			SemanticModel semanticModel)
		{
			var invocations = classDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>();

			foreach (var invocation in invocations)
			{
				// Check for pattern: resolver.Instantiate(...) or this.resolver.Instantiate(...)
				if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
				{
					var methodName = memberAccess.Name.Identifier.Text;
					if (methodName != "Instantiate")
						continue;

					// Get the expression being called on
					var targetExpression = memberAccess.Expression;
					string? targetName = null;

					if (targetExpression is IdentifierNameSyntax identifier)
					{
						targetName = identifier.Identifier.Text;
					}
					else if (targetExpression is MemberAccessExpressionSyntax nestedMember &&
					         nestedMember.Expression is ThisExpressionSyntax)
					{
						targetName = nestedMember.Name.Identifier.Text;
					}

					if (targetName != null && resolverMembers.Contains(targetName))
					{
						// Verify it's actually the VContainer Instantiate extension method
						var symbolInfo = semanticModel.GetSymbolInfo(invocation);
						if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
						{
							var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString();
							if (containingNamespace == "VContainer.Unity" ||
							    containingNamespace == "VContainer")
							{
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		private static bool IsMonoBehaviour(INamedTypeSymbol classSymbol)
		{
			var baseType = classSymbol.BaseType;
			while (baseType != null)
			{
				if (baseType.Name == "MonoBehaviour" ||
				    baseType.Name == "Component" ||
				    baseType.Name == "Behaviour")
				{
					return true;
				}
				baseType = baseType.BaseType;
			}
			return false;
		}

		private static bool HasInjectMethodInHierarchy(
			ClassDeclarationSyntax classDeclaration,
			INamedTypeSymbol classSymbol,
			SyntaxNodeAnalysisContext context)
		{
			// Check current class for [Inject] methods
			var hasInjectInClass = classDeclaration.Members
				.OfType<MethodDeclarationSyntax>()
				.Any(m => HasInjectAttribute(m));

			if (hasInjectInClass)
				return true;

			// Check base classes for [Inject] methods with IObjectResolver
			var baseType = classSymbol.BaseType;
			while (baseType != null)
			{
				var hasInjectInBase = baseType.GetMembers()
					.OfType<IMethodSymbol>()
					.Any(m => m.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute") &&
					          m.Parameters.Any(p => p.Type.Name == "IObjectResolver"));

				if (hasInjectInBase)
					return true;

				baseType = baseType.BaseType;
			}

			return false;
		}

		private static bool BaseClassHasInjectWithResolver(INamedTypeSymbol classSymbol)
		{
			var baseType = classSymbol.BaseType;
			while (baseType != null)
			{
				var hasInjectWithResolver = baseType.GetMembers()
					.OfType<IMethodSymbol>()
					.Any(m => m.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute") &&
					          m.Parameters.Any(p => p.Type.Name == "IObjectResolver"));

				if (hasInjectWithResolver)
					return true;

				baseType = baseType.BaseType;
			}

			return false;
		}

		private static void CheckConstructors(
			ClassDeclarationSyntax classDeclaration,
			INamedTypeSymbol classSymbol,
			SyntaxNodeAnalysisContext context)
		{
			var constructors = classDeclaration.Members
				.OfType<ConstructorDeclarationSyntax>()
				.ToList();

			// If no explicit constructors, there's an implicit parameterless one
			if (constructors.Count == 0)
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.MissingResolverInConstructor,
					classDeclaration.Identifier.GetLocation(),
					"Class",
					classSymbol.Name,
					"implicit default constructor");
				context.ReportDiagnostic(diagnostic);
				return;
			}

			foreach (var constructor in constructors)
			{
				if (!HasResolverParameter(constructor.ParameterList))
				{
					var parameterInfo = constructor.ParameterList.Parameters.Count == 0
						? "parameterless constructor"
						: $"constructor({GetParameterTypeList(constructor.ParameterList)})";

					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.MissingResolverInConstructor,
						constructor.GetLocation(),
						"Class",
						classSymbol.Name,
						parameterInfo);
					context.ReportDiagnostic(diagnostic);
				}
			}
		}

		private static void CheckInjectMethods(
			ClassDeclarationSyntax classDeclaration,
			INamedTypeSymbol classSymbol,
			SyntaxNodeAnalysisContext context)
		{
			// First check if base class has [Inject] method with IObjectResolver - if so, no issue
			if (BaseClassHasInjectWithResolver(classSymbol))
				return;

			var injectMethods = classDeclaration.Members
				.OfType<MethodDeclarationSyntax>()
				.Where(m => HasInjectAttribute(m))
				.ToList();

			if (injectMethods.Count == 0)
			{
				// Class with resolver usage but no [Inject] method in this class or base
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.MissingResolverInConstructor,
					classDeclaration.Identifier.GetLocation(),
					"Class",
					classSymbol.Name,
					"[Inject] method (none found)");
				context.ReportDiagnostic(diagnostic);
				return;
			}

			foreach (var method in injectMethods)
			{
				if (!HasResolverParameter(method.ParameterList))
				{
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.MissingResolverInConstructor,
						method.GetLocation(),
						"MonoBehaviour",
						classSymbol.Name,
						$"[Inject] method '{method.Identifier.Text}'");
					context.ReportDiagnostic(diagnostic);
				}
			}
		}

		private static bool HasInjectAttribute(MethodDeclarationSyntax method)
		{
			return method.AttributeLists
				.SelectMany(al => al.Attributes)
				.Any(a =>
				{
					var name = a.Name.ToString();
					return name == "Inject" || name == "InjectAttribute";
				});
		}

		private static bool HasResolverParameter(ParameterListSyntax parameterList)
		{
			return parameterList.Parameters.Any(p =>
			{
				var typeName = p.Type?.ToString() ?? "";
				return typeName == "IObjectResolver" ||
				       typeName == "VContainer.IObjectResolver" ||
				       typeName.EndsWith(".IObjectResolver");
			});
		}

		private static string GetParameterTypeList(ParameterListSyntax parameterList)
		{
			return string.Join(", ", parameterList.Parameters.Select(p => p.Type?.ToString() ?? "?"));
		}
	}
}
