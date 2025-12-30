namespace IrsikSoftware.Analyzers.Core
{
	/// <summary>
	/// Categories for IrsikSoftware analyzer diagnostics.
	/// Uses compound names for .editorconfig category-level control:
	/// <code>dotnet_analyzer_diagnostic.category-IrsikSoftware.Maintainability.severity = warning</code>
	/// </summary>
	public static class DiagnosticCategories
	{
		private const string Prefix = "IrsikSoftware";

		/// <summary>Dead code, complexity, readability, comments.</summary>
		public const string Maintainability = Prefix + ".Maintainability";

		/// <summary>Allocations, inefficient patterns, caching.</summary>
		public const string Performance = Prefix + ".Performance";

		/// <summary>API design, patterns, architecture.</summary>
		public const string Design = Prefix + ".Design";

		/// <summary>Null handling, error handling, state management.</summary>
		public const string Reliability = Prefix + ".Reliability";

		/// <summary>Input validation, injection, secrets.</summary>
		public const string Security = Prefix + ".Security";

		/// <summary>Unity-specific patterns and lifecycle.</summary>
		public const string Unity = Prefix + ".Unity";
	}
}
