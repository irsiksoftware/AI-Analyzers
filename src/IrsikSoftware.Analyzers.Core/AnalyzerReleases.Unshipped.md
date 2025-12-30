; Unshipped analyzer changes
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ISU0001 | IrsikSoftware.Maintainability | Warning | Method body contains only comments
ISU0002 | IrsikSoftware.Maintainability | Warning | Prefer nameof() for type references
ISU0003 | IrsikSoftware.Maintainability | Info | Inject method naming convention
ISU0004 | IrsikSoftware.Maintainability | Warning | Avoid cryptic abbreviations
ISU0005 | IrsikSoftware.Maintainability | Warning | State enum should be in /Scripts/Enums/
ISU1000 | IrsikSoftware.Performance | Warning | Camera.main in Update methods
ISU1001 | IrsikSoftware.Performance | Warning | Find methods in Update
ISU1002 | IrsikSoftware.Performance | Warning | GetComponent in Update
ISU1100 | IrsikSoftware.Performance | Warning | Shader string property names
ISU1101 | IrsikSoftware.Performance | Warning | Material string property names
ISU1102 | IrsikSoftware.Performance | Warning | Animator string state names
ISU2000 | IrsikSoftware.Reliability | Warning | UniTask missing CancellationToken
ISU2001 | IrsikSoftware.Reliability | Warning | UniTaskVoid missing Forget()
ISU2100 | IrsikSoftware.Reliability | Info | DOTween missing SetLink
ISU3000 | IrsikSoftware.Design | Info | Debug.Log in production
ISU3001 | IrsikSoftware.Design | Info | Direct Input access
ISU3100 | IrsikSoftware.Design | Warning | Use container.Instantiate
ISU3101 | IrsikSoftware.Design | Info | Prefer ITickable
ISU4000 | IrsikSoftware.Reliability | Error | Non-deterministic in Simulate
ISU4001 | IrsikSoftware.Maintainability | Warning | Animator magic integers
