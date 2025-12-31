namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Diagnostic IDs for all IrsikSoftware analyzers.
	/// Format: ISU#### where ISU = IrsikSoftware, #### = 4-digit rule number.
	/// </summary>
	/// <remarks>
	/// ID Ranges:
	/// - 0001-0099: Maintainability - Code Quality (dead code, comments, naming)
	/// - 0100-0199: Maintainability - Patterns (nameof, DI conventions)
	/// - 1000-1099: Performance - Unity Core (Camera.main, Find, GetComponent)
	/// - 1100-1199: Performance - Materials/Shaders (PropertyToID)
	/// - 1200-1299: Performance - Physics
	/// - 2000-2099: Reliability - Async (CancellationToken, UniTask)
	/// - 2100-2199: Reliability - Lifecycle (DOTween, memory)
	/// - 3000-3099: Design - Unity Patterns (Input abstraction, Debug.Log)
	/// - 3100-3199: Design - DI Patterns (VContainer)
	/// - 4000-4099: Determinism - Simulation safety
	/// </remarks>
	public static class DiagnosticIds
	{
		// ===========================================
		// Maintainability - Code Quality (0001-0099)
		// ===========================================

		/// <summary>Method body contains only comments - likely dead code or incomplete.</summary>
		public const string CommentOnlyMethod = "ISU0001";

		/// <summary>Prefer nameof() over string literals for type/member references.</summary>
		public const string PreferNameof = "ISU0002";

		/// <summary>VContainer [Inject] method should be named 'Construct'.</summary>
		public const string InjectMethodNaming = "ISU0003";

		/// <summary>Avoid abbreviations like 'rng' - use full name 'randomNumberGenerator'.</summary>
		public const string AvoidAbbreviations = "ISU0004";

		/// <summary>State enum should follow {ClassName}State naming and be in /Scripts/Enums/.</summary>
		public const string StateEnumConvention = "ISU0005";

		// ===========================================
		// Performance - Unity Core (1000-1099)
		// ===========================================

		/// <summary>Camera.main is slow in Update - cache it.</summary>
		public const string CameraMainInUpdate = "ISU1000";

		/// <summary>Find methods are slow in Update - cache references.</summary>
		public const string FindInUpdate = "ISU1001";

		/// <summary>GetComponent in Update - cache the reference.</summary>
		public const string GetComponentInUpdate = "ISU1002";

		// ===========================================
		// Performance - Materials/Shaders (1100-1199)
		// ===========================================

		/// <summary>Use Shader.PropertyToID instead of string property names.</summary>
		public const string ShaderPropertyString = "ISU1100";

		/// <summary>Use Material property IDs instead of string property names.</summary>
		public const string MaterialPropertyString = "ISU1101";

		/// <summary>Use Animator.StringToHash instead of string state names.</summary>
		public const string AnimatorStringState = "ISU1102";

		// ===========================================
		// Reliability - Async (2000-2099)
		// ===========================================

		/// <summary>async UniTask in MonoBehaviour should use CancellationToken.</summary>
		public const string UniTaskMissingCancellation = "ISU2000";

		/// <summary>UniTaskVoid should call .Forget() to suppress warnings.</summary>
		public const string UniTaskVoidForget = "ISU2001";

		// ===========================================
		// Reliability - Lifecycle (2100-2199)
		// ===========================================

		/// <summary>DOTween should use .SetLink(gameObject) to prevent leaks.</summary>
		public const string DOTweenMissingSetLink = "ISU2100";

		// ===========================================
		// Design - Unity Patterns (3000-3099)
		// ===========================================

		/// <summary>Debug.Log should be wrapped in #if UNITY_EDITOR or DEVELOPMENT_BUILD.</summary>
		public const string DebugLogInProduction = "ISU3000";

		/// <summary>Direct UnityEngine.Input usage - prefer IInputManager abstraction.</summary>
		public const string DirectInputAccess = "ISU3001";

		// ===========================================
		// Design - DI Patterns (3100-3199)
		// ===========================================

		/// <summary>Use container.Instantiate instead of Object.Instantiate for DI support.</summary>
		public const string UseContainerInstantiate = "ISU3100";

		/// <summary>Pure C# service should use ITickable instead of Update pattern.</summary>
		public const string PreferITickable = "ISU3101";

		/// <summary>Class using resolver.Instantiate must have IObjectResolver in all constructors/[Inject] methods.</summary>
		public const string MissingResolverInConstructor = "ISU3102";

		// ===========================================
		// Determinism - Simulation (4000-4099)
		// ===========================================

		/// <summary>Non-deterministic API in Simulate method - use injected services.</summary>
		public const string NonDeterministicInSimulate = "ISU4000";

		/// <summary>Use enum for animator state instead of magic integers.</summary>
		public const string AnimatorMagicInteger = "ISU4001";
	}
}
