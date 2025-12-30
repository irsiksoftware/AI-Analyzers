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
	/// Detects DOTween tween creation without .SetLink(gameObject).
	/// Missing SetLink can cause memory leaks when GameObjects are destroyed.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class DOTweenSetLinkAnalyzer : DiagnosticAnalyzer
	{
		private static readonly ImmutableHashSet<string> TweenCreationMethods = ImmutableHashSet.Create(
			// Transform
			"DOMove", "DOMoveX", "DOMoveY", "DOMoveZ",
			"DOLocalMove", "DOLocalMoveX", "DOLocalMoveY", "DOLocalMoveZ",
			"DORotate", "DORotateQuaternion", "DOLocalRotate", "DOLocalRotateQuaternion",
			"DOScale", "DOScaleX", "DOScaleY", "DOScaleZ",
			"DOPunchPosition", "DOPunchRotation", "DOPunchScale",
			"DOShakePosition", "DOShakeRotation", "DOShakeScale",
			"DOJump", "DOLocalJump",
			"DOPath", "DOLocalPath",
			"DOLookAt",
			// RectTransform
			"DOAnchorPos", "DOAnchorPosX", "DOAnchorPosY", "DOAnchorPos3D",
			"DOSizeDelta", "DOPivot", "DOPivotX", "DOPivotY",
			// SpriteRenderer / Image / CanvasGroup
			"DOFade", "DOColor",
			// Material
			"DOFloat", "DOVector", "DOOffset", "DOTiling",
			// Generic
			"DOValue", "To");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create(DiagnosticDescriptors.DOTweenMissingSetLink);

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
			if (methodName == null || !TweenCreationMethods.Contains(methodName))
				return;

			var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
			if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
				return;

			// Check if it's a DOTween method (return type is Tween or Tweener)
			var returnType = methodSymbol.ReturnType;
			var returnTypeName = returnType.Name;
			if (returnTypeName != "Tween" && returnTypeName != "Tweener" &&
			    returnTypeName != "Sequence" && !returnTypeName.StartsWith("Tween"))
				return;

			// Check namespace
			var ns = returnType.ContainingNamespace?.ToDisplayString();
			if (ns != "DG.Tweening" && ns != "DG.Tweening.Core")
				return;

			// Check if the chain includes SetLink
			if (HasSetLinkInChain(invocation))
				return;

			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.DOTweenMissingSetLink,
				invocation.GetLocation());

			context.ReportDiagnostic(diagnostic);
		}

		private static bool HasSetLinkInChain(InvocationExpressionSyntax invocation)
		{
			// Walk up the expression tree looking for method chains
			SyntaxNode? current = invocation;

			while (current != null)
			{
				// Check if parent is a member access that's part of a method chain
				if (current.Parent is MemberAccessExpressionSyntax memberAccess &&
				    memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
				{
					var methodName = memberAccess.Name.Identifier.ValueText;
					if (methodName == "SetLink")
						return true;

					current = chainedInvocation;
				}
				else
				{
					break;
				}
			}

			// Also check if we're inside a chain where SetLink comes later
			var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();
			if (statement != null)
			{
				var allInvocations = statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
				foreach (var inv in allInvocations)
				{
					if (inv.Expression is MemberAccessExpressionSyntax ma &&
					    ma.Name.Identifier.ValueText == "SetLink")
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
