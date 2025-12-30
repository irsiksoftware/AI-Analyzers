# IrsikSoftware Analyzers

Roslyn analyzers for catching AI-agent code smells and enforcing code quality standards.

## Projects

| Project | Description | Distribution |
|---------|-------------|--------------|
| `IrsikSoftware.Analyzers.Core` | Shared analyzers for all project types | NuGet |
| `IrsikSoftware.Analyzers.Unity` | Unity-specific analyzers (includes Core) | NuGet + UPM |

## Rules

### ISU0001 - Method body contains only comments

Flags methods that have no statements but contain comments - often indicates dead code or incomplete implementation.

```csharp
// Triggers ISU0001
protected virtual void Update()
{
    // TODO: implement this later
}
```

## Installation

### Unity (UPM)

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.irsik.analyzers.unity": "https://github.com/irsik/AI-Analyzers.git?path=packages/com.irsik.analyzers.unity"
  }
}
```

### Unity (Manual)

1. Build: `dotnet build -c Release`
2. Copy DLLs from `src/IrsikSoftware.Analyzers.Unity/bin/Release/netstandard2.0/` to `Assets/Plugins/Analyzers/`
3. Add `RoslynAnalyzer` label to the `.meta` files

### .NET Projects (NuGet)

```bash
dotnet add package IrsikSoftware.Analyzers.Unity
# or for non-Unity projects:
dotnet add package IrsikSoftware.Analyzers.Core
```

## Configuration

Enable/disable rules in `.editorconfig`:

```ini
# Enable (default)
dotnet_diagnostic.ISU0001.severity = warning

# Disable
dotnet_diagnostic.ISU0001.severity = none
```

## Building

```bash
dotnet build              # Debug build
dotnet build -c Release   # Release build + NuGet packages
```

NuGet packages output to `src/*/bin/Release/*.nupkg`

## Structure

```
AI-Analyzers/
├── src/
│   ├── IrsikSoftware.Analyzers.Core/      # Shared analyzers
│   └── IrsikSoftware.Analyzers.Unity/     # Unity-specific (refs Core)
├── packages/
│   └── com.irsik.analyzers.unity/         # UPM package
├── IrsikSoftware.Analyzers.slnx
└── README.md
```

## License

MIT
