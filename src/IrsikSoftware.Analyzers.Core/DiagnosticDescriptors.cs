using Microsoft.CodeAnalysis;

namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Centralized diagnostic descriptors for all IrsikSoftware analyzers.
	/// </summary>
	public static class DiagnosticDescriptors
	{
		private const string HelpLinkBase = "https://github.com/irsiksoftware/AI-Analyzers/blob/main/docs/rules/";

		private static string GetHelpLink(string id) => $"{HelpLinkBase}{id}.md";

		// ===========================================
		// Maintainability - Code Quality (ISU0001-ISU0099)
		// ===========================================

		public static readonly DiagnosticDescriptor CommentOnlyMethod = new(
			DiagnosticIds.CommentOnlyMethod,
			title: "Method body contains only comments",
			messageFormat: "Method '{0}' contains only comments - consider removing or implementing",
			category: DiagnosticCategories.Maintainability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Methods that contain only comments are effectively empty and may indicate dead code or incomplete implementation.",
			helpLinkUri: GetHelpLink(DiagnosticIds.CommentOnlyMethod));

		public static readonly DiagnosticDescriptor PreferNameof = new(
			DiagnosticIds.PreferNameof,
			title: "Prefer nameof() for type references",
			messageFormat: "String literal '{0}' matches type/member name - use nameof({0}) instead",
			category: DiagnosticCategories.Maintainability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Using nameof() instead of string literals prevents strings from becoming stale during refactoring.",
			helpLinkUri: GetHelpLink(DiagnosticIds.PreferNameof));

		public static readonly DiagnosticDescriptor InjectMethodNaming = new(
			DiagnosticIds.InjectMethodNaming,
			title: "Inject method should be named 'Construct'",
			messageFormat: "Method '{0}' with [Inject] attribute should be named 'Construct' for consistency",
			category: DiagnosticCategories.Maintainability,
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description: "Standardizes the DI injection method naming across the codebase.",
			helpLinkUri: GetHelpLink(DiagnosticIds.InjectMethodNaming));

		public static readonly DiagnosticDescriptor AvoidAbbreviations = new(
			DiagnosticIds.AvoidAbbreviations,
			title: "Avoid cryptic abbreviations",
			messageFormat: "Identifier '{0}' uses abbreviation - prefer full name (e.g., 'randomNumberGenerator' instead of 'rng')",
			category: DiagnosticCategories.Maintainability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Abbreviations like 'rng' are not self-documenting. Use descriptive names like 'randomNumberGenerator'.",
			helpLinkUri: GetHelpLink(DiagnosticIds.AvoidAbbreviations));

		public static readonly DiagnosticDescriptor StateEnumConvention = new(
			DiagnosticIds.StateEnumConvention,
			title: "State enum should be in /Scripts/Enums/",
			messageFormat: "State enum '{0}' should be moved to '{1}'",
			category: DiagnosticCategories.Maintainability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "State enums should be located in the /Scripts/Enums/ folder for consistency.",
			helpLinkUri: GetHelpLink(DiagnosticIds.StateEnumConvention));

		// ===========================================
		// Performance - Unity Core (ISU1000-ISU1099)
		// ===========================================

		public static readonly DiagnosticDescriptor CameraMainInUpdate = new(
			DiagnosticIds.CameraMainInUpdate,
			title: "Camera.main is slow in Update",
			messageFormat: "Camera.main in '{0}' performs a FindGameObjectsWithTag - cache it in Awake/Start",
			category: DiagnosticCategories.Performance,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Camera.main has CPU overhead comparable to GetComponent. Cache it for high-frequency methods.",
			helpLinkUri: GetHelpLink(DiagnosticIds.CameraMainInUpdate));

		public static readonly DiagnosticDescriptor FindInUpdate = new(
			DiagnosticIds.FindInUpdate,
			title: "Find methods are slow in Update",
			messageFormat: "'{0}' in Update method is expensive - cache the reference in Awake/Start",
			category: DiagnosticCategories.Performance,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Find methods search the entire scene hierarchy. Cache references during initialization.",
			helpLinkUri: GetHelpLink(DiagnosticIds.FindInUpdate));

		public static readonly DiagnosticDescriptor GetComponentInUpdate = new(
			DiagnosticIds.GetComponentInUpdate,
			title: "GetComponent in Update",
			messageFormat: "GetComponent<{0}>() in Update - cache the reference in Awake/Start",
			category: DiagnosticCategories.Performance,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "GetComponent has CPU overhead. Cache component references during initialization.",
			helpLinkUri: GetHelpLink(DiagnosticIds.GetComponentInUpdate));

		// ===========================================
		// Performance - Materials/Shaders (ISU1100-ISU1199)
		// ===========================================

		public static readonly DiagnosticDescriptor ShaderPropertyString = new(
			DiagnosticIds.ShaderPropertyString,
			title: "Use Shader.PropertyToID",
			messageFormat: "Shader.{0} with string parameter - use Shader.PropertyToID for better performance",
			category: DiagnosticCategories.Performance,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "String-based shader property access is slower than using cached property IDs.",
			helpLinkUri: GetHelpLink(DiagnosticIds.ShaderPropertyString));

		public static readonly DiagnosticDescriptor MaterialPropertyString = new(
			DiagnosticIds.MaterialPropertyString,
			title: "Use Material property IDs",
			messageFormat: "Material.{0} with string parameter - use Shader.PropertyToID for better performance",
			category: DiagnosticCategories.Performance,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "String-based material property access is slower than using cached property IDs.",
			helpLinkUri: GetHelpLink(DiagnosticIds.MaterialPropertyString));

		public static readonly DiagnosticDescriptor AnimatorStringState = new(
			DiagnosticIds.AnimatorStringState,
			title: "Use Animator.StringToHash",
			messageFormat: "Animator.{0} with string parameter - use Animator.StringToHash for better performance",
			category: DiagnosticCategories.Performance,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "String-based animator state access is slower than using cached hash IDs.",
			helpLinkUri: GetHelpLink(DiagnosticIds.AnimatorStringState));

		// ===========================================
		// Reliability - Async (ISU2000-ISU2099)
		// ===========================================

		public static readonly DiagnosticDescriptor UniTaskMissingCancellation = new(
			DiagnosticIds.UniTaskMissingCancellation,
			title: "UniTask missing CancellationToken",
			messageFormat: "async UniTask method '{0}' should accept CancellationToken (use GetCancellationTokenOnDestroy())",
			category: DiagnosticCategories.Reliability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "UniTask methods in MonoBehaviour should use CancellationToken to prevent orphaned tasks when objects are destroyed.",
			helpLinkUri: GetHelpLink(DiagnosticIds.UniTaskMissingCancellation));

		public static readonly DiagnosticDescriptor UniTaskVoidForget = new(
			DiagnosticIds.UniTaskVoidForget,
			title: "UniTaskVoid should call Forget()",
			messageFormat: "UniTaskVoid method '{0}' should be called with .Forget() to suppress warnings",
			category: DiagnosticCategories.Reliability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "UniTaskVoid methods should explicitly call .Forget() to indicate fire-and-forget intent.",
			helpLinkUri: GetHelpLink(DiagnosticIds.UniTaskVoidForget));

		// ===========================================
		// Reliability - Lifecycle (ISU2100-ISU2199)
		// ===========================================

		public static readonly DiagnosticDescriptor DOTweenMissingSetLink = new(
			DiagnosticIds.DOTweenMissingSetLink,
			title: "DOTween missing SetLink",
			messageFormat: "DOTween tween should call .SetLink(gameObject) to prevent memory leaks",
			category: DiagnosticCategories.Reliability,
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description: "DOTween tweens should be linked to a GameObject to automatically kill them when the object is destroyed.",
			helpLinkUri: GetHelpLink(DiagnosticIds.DOTweenMissingSetLink));

		// ===========================================
		// Design - Unity Patterns (ISU3000-ISU3099)
		// ===========================================

		public static readonly DiagnosticDescriptor DebugLogInProduction = new(
			DiagnosticIds.DebugLogInProduction,
			title: "Debug.Log in production code",
			messageFormat: "Debug.{0} should be wrapped in #if UNITY_EDITOR || DEVELOPMENT_BUILD",
			category: DiagnosticCategories.Design,
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description: "Debug logging in production builds impacts performance. Wrap in conditional compilation.",
			helpLinkUri: GetHelpLink(DiagnosticIds.DebugLogInProduction));

		public static readonly DiagnosticDescriptor DirectInputAccess = new(
			DiagnosticIds.DirectInputAccess,
			title: "Direct Input access",
			messageFormat: "UnityEngine.Input.{0} - consider using an IInputManager abstraction for testability",
			category: DiagnosticCategories.Design,
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description: "Direct Input access makes code harder to test. Consider abstracting behind an interface.",
			helpLinkUri: GetHelpLink(DiagnosticIds.DirectInputAccess));

		// ===========================================
		// Design - DI Patterns (ISU3100-ISU3199)
		// ===========================================

		public static readonly DiagnosticDescriptor UseContainerInstantiate = new(
			DiagnosticIds.UseContainerInstantiate,
			title: "Use container.Instantiate",
			messageFormat: "Object.Instantiate in class with IObjectResolver - use container.Instantiate for DI support",
			category: DiagnosticCategories.Design,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "When using VContainer, instantiate through the container to enable dependency injection.",
			helpLinkUri: GetHelpLink(DiagnosticIds.UseContainerInstantiate));

		public static readonly DiagnosticDescriptor PreferITickable = new(
			DiagnosticIds.PreferITickable,
			title: "Prefer ITickable over Update",
			messageFormat: "Pure C# service '{0}' with Update-like method - consider implementing ITickable",
			category: DiagnosticCategories.Design,
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description: "Pure C# services should use VContainer's ITickable interface instead of MonoBehaviour Update patterns.",
			helpLinkUri: GetHelpLink(DiagnosticIds.PreferITickable));

		// ===========================================
		// Determinism - Simulation (ISU4000-ISU4099)
		// ===========================================

		public static readonly DiagnosticDescriptor NonDeterministicInSimulate = new(
			DiagnosticIds.NonDeterministicInSimulate,
			title: "Non-deterministic API in Simulate",
			messageFormat: "'{0}' is non-deterministic - use passed deltaTime or injected deterministic services",
			category: DiagnosticCategories.Reliability,
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Simulate methods must be deterministic. Use injected services instead of Time.*, Random.*, or Find methods.",
			helpLinkUri: GetHelpLink(DiagnosticIds.NonDeterministicInSimulate));

		public static readonly DiagnosticDescriptor AnimatorMagicInteger = new(
			DiagnosticIds.AnimatorMagicInteger,
			title: "Magic integer in animator state",
			messageFormat: "Animator.SetInteger with magic number {0} - use enum cast (e.g., (int)MonsterState.Walk)",
			category: DiagnosticCategories.Maintainability,
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Using magic integers for animator states is error-prone. Use an enum for type safety.",
			helpLinkUri: GetHelpLink(DiagnosticIds.AnimatorMagicInteger));
	}
}
