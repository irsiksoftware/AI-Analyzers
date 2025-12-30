using System.Collections.Immutable;
using IrsikSoftware.Analyzers.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IrsikSoftware.Analyzers.Unity
{
	/// <summary>
	/// Detects Material and Shader methods using string property names instead of PropertyToID.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class MaterialPropertyStringAnalyzer : DiagnosticAnalyzer
	{
		private static readonly ImmutableHashSet<string> MaterialMethods = ImmutableHashSet.Create(
			"GetColor", "GetColorArray", "GetFloat", "GetFloatArray", "GetInt", "GetInteger",
			"GetMatrix", "GetMatrixArray", "GetTexture", "GetTextureOffset", "GetTextureScale",
			"GetVector", "GetVectorArray", "HasColor", "HasFloat", "HasInt", "HasInteger",
			"HasMatrix", "HasProperty", "HasTexture", "HasVector",
			"SetBuffer", "SetColor", "SetColorArray", "SetFloat", "SetFloatArray",
			"SetInt", "SetInteger", "SetMatrix", "SetMatrixArray", "SetOverrideTag",
			"SetTexture", "SetTextureOffset", "SetTextureScale", "SetVector", "SetVectorArray");

		private static readonly ImmutableHashSet<string> ShaderMethods = ImmutableHashSet.Create(
			"GetGlobalColor", "GetGlobalFloat", "GetGlobalFloatArray", "GetGlobalInt", "GetGlobalInteger",
			"GetGlobalMatrix", "GetGlobalMatrixArray", "GetGlobalTexture", "GetGlobalVector",
			"GetGlobalVectorArray", "SetGlobalBuffer", "SetGlobalColor", "SetGlobalFloat",
			"SetGlobalFloatArray", "SetGlobalInt", "SetGlobalInteger", "SetGlobalMatrix",
			"SetGlobalMatrixArray", "SetGlobalTexture", "SetGlobalVector", "SetGlobalVectorArray");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(
				DiagnosticDescriptors.MaterialPropertyString,
				DiagnosticDescriptors.ShaderPropertyString);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
		}

		private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
		{
			var invocation = (InvocationExpressionSyntax)context.Node;

			var methodName = UnityHelpers.GetMethodName(invocation);
			if (methodName == null)
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			var containingType = methodSymbol.ContainingType;
			if (containingType?.ContainingNamespace?.ToDisplayString() != "UnityEngine")
				return;

			// Check if first parameter is string
			if (methodSymbol.Parameters.Length == 0 ||
			    methodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_String)
				return;

			// Check Material methods
			if (containingType.Name == "Material" && MaterialMethods.Contains(methodName))
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.MaterialPropertyString,
					invocation.GetLocation(),
					methodName);

				context.ReportDiagnostic(diagnostic);
			}
			// Check Shader methods
			else if (containingType.Name == "Shader" && ShaderMethods.Contains(methodName))
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.ShaderPropertyString,
					invocation.GetLocation(),
					methodName);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
