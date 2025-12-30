using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Helper utilities for Unity-specific analysis.
	/// Ported and modernized from UnityEngineAnalyzer (MIT License).
	/// </summary>
	public static class UnityHelpers
	{
		/// <summary>
		/// High-frequency update methods where performance matters most.
		/// </summary>
		public static readonly ImmutableHashSet<string> UpdateMethods = ImmutableHashSet.Create(
			"Update",
			"FixedUpdate",
			"LateUpdate",
			"OnGUI");

		/// <summary>
		/// All MonoBehaviour lifecycle/message methods.
		/// </summary>
		public static readonly ImmutableHashSet<string> MonoBehaviourMethods = ImmutableHashSet.Create(
			"Awake",
			"Start",
			"Update",
			"FixedUpdate",
			"LateUpdate",
			"OnGUI",
			"OnEnable",
			"OnDisable",
			"OnDestroy",
			"OnValidate",
			"Reset",
			"OnAnimatorIK",
			"OnAnimatorMove",
			"OnApplicationFocus",
			"OnApplicationPause",
			"OnApplicationQuit",
			"OnAudioFilterRead",
			"OnBecameInvisible",
			"OnBecameVisible",
			"OnCollisionEnter",
			"OnCollisionEnter2D",
			"OnCollisionExit",
			"OnCollisionExit2D",
			"OnCollisionStay",
			"OnCollisionStay2D",
			"OnControllerColliderHit",
			"OnDrawGizmos",
			"OnDrawGizmosSelected",
			"OnJointBreak",
			"OnJointBreak2D",
			"OnMouseDown",
			"OnMouseDrag",
			"OnMouseEnter",
			"OnMouseExit",
			"OnMouseOver",
			"OnMouseUp",
			"OnMouseUpAsButton",
			"OnParticleCollision",
			"OnParticleSystemStopped",
			"OnParticleTrigger",
			"OnParticleUpdateJobScheduled",
			"OnPostRender",
			"OnPreCull",
			"OnPreRender",
			"OnRenderImage",
			"OnRenderObject",
			"OnTransformChildrenChanged",
			"OnTransformParentChanged",
			"OnTriggerEnter",
			"OnTriggerEnter2D",
			"OnTriggerExit",
			"OnTriggerExit2D",
			"OnTriggerStay",
			"OnTriggerStay2D",
			"OnWillRenderObject");

		/// <summary>
		/// Checks if a class inherits from UnityEngine.MonoBehaviour.
		/// </summary>
		public static bool IsMonoBehaviour(INamedTypeSymbol? classSymbol)
		{
			if (classSymbol == null)
				return false;

			var current = classSymbol.BaseType;
			while (current != null)
			{
				if (current.ContainingNamespace?.ToDisplayString() == "UnityEngine" &&
				    current.Name == "MonoBehaviour")
				{
					return true;
				}
				current = current.BaseType;
			}
			return false;
		}

		/// <summary>
		/// Checks if a class inherits from UnityEngine.ScriptableObject.
		/// </summary>
		public static bool IsScriptableObject(INamedTypeSymbol? classSymbol)
		{
			if (classSymbol == null)
				return false;

			var current = classSymbol.BaseType;
			while (current != null)
			{
				if (current.ContainingNamespace?.ToDisplayString() == "UnityEngine" &&
				    current.Name == "ScriptableObject")
				{
					return true;
				}
				current = current.BaseType;
			}
			return false;
		}

		/// <summary>
		/// Checks if a method is a high-frequency update method.
		/// </summary>
		public static bool IsUpdateMethod(MethodDeclarationSyntax method)
		{
			return UpdateMethods.Contains(method.Identifier.ValueText);
		}

		/// <summary>
		/// Checks if a method is any MonoBehaviour lifecycle method.
		/// </summary>
		public static bool IsMonoBehaviourMethod(MethodDeclarationSyntax method)
		{
			return MonoBehaviourMethods.Contains(method.Identifier.ValueText);
		}

		/// <summary>
		/// Gets the class symbol for a class declaration.
		/// </summary>
		public static INamedTypeSymbol? GetClassSymbol(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
		{
			return context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
		}

		/// <summary>
		/// Iterates over all update methods in a MonoBehaviour class.
		/// </summary>
		public static void ForEachUpdateMethod(
			SyntaxNodeAnalysisContext context,
			ClassDeclarationSyntax classDeclaration,
			Action<MethodDeclarationSyntax> callback)
		{
			var classSymbol = GetClassSymbol(context, classDeclaration);
			if (!IsMonoBehaviour(classSymbol))
				return;

			foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
			{
				if (IsUpdateMethod(method))
				{
					callback(method);
				}
			}
		}

		/// <summary>
		/// Gets the method name from an invocation expression.
		/// </summary>
		public static string? GetMethodName(InvocationExpressionSyntax invocation)
		{
			return invocation.Expression switch
			{
				MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
				IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
				_ => null
			};
		}

		/// <summary>
		/// Checks if an invocation is calling a method on a specific UnityEngine type with a string first parameter.
		/// </summary>
		public static bool IsUnityStringMethodCall(
			SyntaxNodeAnalysisContext context,
			InvocationExpressionSyntax invocation,
			string typeName,
			ImmutableHashSet<string> methodNames)
		{
			var methodName = GetMethodName(invocation);
			if (methodName == null || !methodNames.Contains(methodName))
				return false;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return false;

			var containingType = methodSymbol.ContainingType;
			if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine")
				return false;

			if (containingType.Name != typeName)
				return false;

			// Check if first parameter is string
			if (methodSymbol.Parameters.Length > 0 &&
			    methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String)
			{
				return true;
			}

			return false;
		}
	}
}
