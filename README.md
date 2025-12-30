# IrsikSoftware Analyzers

Roslyn analyzers for catching AI-agent code smells and enforcing code quality standards in Unity projects.

## Projects

| Project | Description | Distribution |
|---------|-------------|--------------|
| `IrsikSoftware.Analyzers.Core` | Shared analyzers for all project types | NuGet |
| `IrsikSoftware.Analyzers.Unity` | Unity-specific analyzers (includes Core) | NuGet + UPM |

## Rules Overview

| ID | Category | Name | Severity | Code Fix |
|----|----------|------|----------|----------|
| [ISU0001](#isu0001) | Maintainability | Comment-only method | Warning | Yes |
| [ISU0002](#isu0002) | Maintainability | Prefer nameof() | Warning | Yes |
| [ISU0003](#isu0003) | Maintainability | Inject method naming | Suggestion | Yes |
| [ISU1000](#isu1000) | Performance | Camera.main in Update | Warning | Yes |
| [ISU1001](#isu1001) | Performance | Find in Update | Warning | Yes |
| [ISU1100](#isu1100) | Performance | Shader property string | Warning | Yes |
| [ISU1101](#isu1101) | Performance | Material property string | Warning | Yes |
| [ISU1102](#isu1102) | Performance | Animator string parameter | Warning | Yes |
| [ISU2000](#isu2000) | Reliability | UniTask missing CancellationToken | Warning | TBD |
| [ISU2001](#isu2001) | Reliability | UniTaskVoid without Forget() | Warning | Yes |
| [ISU2100](#isu2100) | Reliability | DOTween missing SetLink | Suggestion | Yes |
| [ISU3000](#isu3000) | Design | Debug.Log in production | Suggestion | TBD |
| [ISU3001](#isu3001) | Design | Direct Input access | Warning | TBD |
| [ISU3100](#isu3100) | Design | Use container.Instantiate | Warning | TBD |
| [ISU3101](#isu3101) | Design | Prefer ITickable | Suggestion | TBD |
| [ISU4000](#isu4000) | Determinism | Non-deterministic in Simulate | Error | TBD |
| [ISU4001](#isu4001) | Determinism | Animator magic integer | Warning | TBD |

---

## Maintainability Rules

### ISU0001

**Comment-only method body**

Methods containing only comments indicate dead code or incomplete implementation.

```csharp
// BAD - triggers ISU0001
protected virtual void OnCleanup()
{
    // TODO: implement cleanup
}

// GOOD - either implement or remove
protected virtual void OnCleanup()
{
    _subscription?.Dispose();
}
```

**Code Fix:** Remove the method.

---

### ISU0002

**Prefer nameof() for type/member string references**

String literals matching type or member names should use `nameof()` for refactor safety.

```csharp
// BAD - triggers ISU0002
public const string Player = "Player";
public static readonly string PoolName = "BulletPool";

// GOOD - refactor-safe
public const string Player = nameof(Player);
public static readonly string PoolName = nameof(BulletPool);
```

**Code Fix:** Replace string literal with `nameof(X)`.

---

### ISU0003

**Inject method naming convention**

Methods with `[Inject]` attribute should be named `Construct` for consistency.

```csharp
// BAD - triggers ISU0003
[Inject]
public void Initialize(IGameManager manager) { }

// GOOD - consistent naming
[Inject]
public void Construct(IGameManager manager) { }
```

**Code Fix:** Rename method to `Construct`.

---

## Performance Rules

### ISU1000

**Camera.main in Update methods**

`Camera.main` performs a `FindGameObjectWithTag` lookup each call. Cache it.

```csharp
// BAD - triggers ISU1000
void Update()
{
    transform.LookAt(Camera.main.transform);
}

// GOOD - cached reference
private Camera _mainCamera;

void Awake() => _mainCamera = Camera.main;

void Update()
{
    transform.LookAt(_mainCamera.transform);
}
```

**Code Fix:** Add cached field and initialize in Awake/Start.

---

### ISU1001

**Find methods in Update**

`Find*` methods are expensive. Cache results at initialization.

```csharp
// BAD - triggers ISU1001
void Update()
{
    var player = GameObject.FindWithTag("Player");
    FollowTarget(player.transform);
}

// GOOD - cached reference
private Transform _playerTransform;

void Start()
{
    _playerTransform = GameObject.FindWithTag("Player").transform;
}

void Update()
{
    FollowTarget(_playerTransform);
}
```

**Code Fix:** Add cached field and initialize in Awake/Start.

---

### ISU1100

**Shader.SetGlobalX with string parameter**

String-based shader property access is slow. Use `Shader.PropertyToID`.

```csharp
// BAD - triggers ISU1100
Shader.SetGlobalFloat("_GlobalTime", time);

// GOOD - cached property ID
private static readonly int GlobalTimeId = Shader.PropertyToID("_GlobalTime");

Shader.SetGlobalFloat(GlobalTimeId, time);
```

**Code Fix:** Add static readonly field with `Shader.PropertyToID`, replace call.

---

### ISU1101

**Material property access with string**

Material property string lookups are slow. Use cached property IDs.

```csharp
// BAD - triggers ISU1101
material.SetFloat("_Intensity", 1.5f);
material.SetColor("_TintColor", Color.red);

// GOOD - cached property IDs
private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

material.SetFloat(IntensityId, 1.5f);
material.SetColor(TintColorId, Color.red);
```

**Code Fix:** Add static readonly field with `Shader.PropertyToID`, replace call.

---

### ISU1102

**Animator methods with string parameter**

Animator string lookups are slow. Use `Animator.StringToHash`.

```csharp
// BAD - triggers ISU1102
animator.SetTrigger("Attack");
animator.Play("Idle");

// GOOD - cached hash
private static readonly int AttackHash = Animator.StringToHash("Attack");
private static readonly int IdleHash = Animator.StringToHash("Idle");

animator.SetTrigger(AttackHash);
animator.Play(IdleHash);
```

**Code Fix:** Add static readonly field with `Animator.StringToHash`, replace call.

---

## Reliability Rules

### ISU2000

**UniTask missing CancellationToken**

Async UniTask methods in MonoBehaviours need cancellation support to prevent execution after destruction.

```csharp
// BAD - triggers ISU2000
public async UniTask LoadDataAsync()
{
    await UniTask.Delay(1000);
    ProcessData();
}

// GOOD - cancellation-aware
public async UniTask LoadDataAsync(CancellationToken ct = default)
{
    await UniTask.Delay(1000, cancellationToken: ct);
    ProcessData();
}

// GOOD - auto-cancel on destroy
public async UniTaskVoid StartLoading()
{
    await LoadDataAsync(this.GetCancellationTokenOnDestroy());
}
```

**Code Fix:** TBD - Changes method signature and affects callers.

---

### ISU2001

**UniTaskVoid without Forget()**

`UniTaskVoid` return values must call `.Forget()` to prevent silent exceptions.

```csharp
// BAD - triggers ISU2001
FireAndForgetAsync();

// GOOD - explicit forget
FireAndForgetAsync().Forget();
```

**Code Fix:** Append `.Forget()` to the call.

---

### ISU2100

**DOTween missing SetLink**

DOTween sequences should use `.SetLink(gameObject)` to auto-kill on destruction.

```csharp
// BAD - triggers ISU2100 (can leak/error if object destroyed)
transform.DOMove(target, 1f);
DOTween.Sequence().Append(transform.DOScale(2f, 0.5f));

// GOOD - lifecycle-safe
transform.DOMove(target, 1f).SetLink(gameObject);
DOTween.Sequence()
    .Append(transform.DOScale(2f, 0.5f))
    .SetLink(gameObject);
```

**Code Fix:** Append `.SetLink(gameObject)`.

---

## Design Rules

### ISU3000

**Debug.Log in production code**

Debug logging should be conditionally compiled or use a logging abstraction.

```csharp
// BAD - triggers ISU3000
Debug.Log($"Player health: {health}");

// GOOD - conditional compilation
#if UNITY_EDITOR || DEVELOPMENT_BUILD
Debug.Log($"Player health: {health}");
#endif

// GOOD - conditional attribute
[Conditional("DEBUG")]
void LogHealth() => Debug.Log($"Player health: {health}");
```

**Code Fix:** TBD - Multiple valid approaches (conditional compilation, attribute, abstraction).

---

### ISU3001

**Direct UnityEngine.Input access**

Direct `Input` access reduces testability. Use an input abstraction.

```csharp
// BAD - triggers ISU3001
if (Input.GetKeyDown(KeyCode.Space))
    Jump();

// GOOD - abstracted input
public class PlayerController
{
    private readonly IInputManager _input;

    [Inject]
    public void Construct(IInputManager input) => _input = input;

    void Update()
    {
        if (_input.GetJumpPressed())
            Jump();
    }
}
```

**Code Fix:** TBD - Requires architectural change to input abstraction.

---

### ISU3100

**Use container.Instantiate with DI**

Classes with `IObjectResolver` should use container instantiation for proper DI.

```csharp
// BAD - triggers ISU3100 (skips DI injection)
public class EnemySpawner
{
    [Inject] private IObjectResolver _container;

    public void Spawn(GameObject prefab)
    {
        var enemy = Object.Instantiate(prefab);  // No injection!
    }
}

// GOOD - DI-aware instantiation
public class EnemySpawner
{
    [Inject] private IObjectResolver _container;

    public void Spawn(GameObject prefab)
    {
        var enemy = _container.Instantiate(prefab);  // Injected!
    }
}
```

**Code Fix:** TBD - Need to detect resolver field name.

---

### ISU3101

**Prefer ITickable for non-MonoBehaviour services**

Pure C# services with Update-like methods should implement `ITickable` for VContainer integration.

```csharp
// BAD - triggers ISU3101
public class GameTimer
{
    public void Update()  // Who calls this?
    {
        _elapsed += Time.deltaTime;
    }
}

// GOOD - VContainer-managed lifecycle
public class GameTimer : ITickable
{
    public void Tick()
    {
        _elapsed += Time.deltaTime;
    }
}
```

**Code Fix:** TBD - Add interface + rename method.

---

## Determinism Rules

### ISU4000

**Non-deterministic API in Simulate methods**

`ISimulatable.Simulate` methods must be deterministic for replay/networking.

```csharp
// BAD - triggers ISU4000
public void Simulate(float dt)
{
    // Non-deterministic!
    _position += Random.insideUnitCircle * Time.deltaTime;
    var target = GameObject.Find("Target");
}

// GOOD - deterministic
public void Simulate(float dt, IDeterministicRng rng)
{
    _position += rng.InsideUnitCircle() * dt;
    // Target should be injected, not found
}
```

**Code Fix:** TBD - Requires architectural change to deterministic patterns.

---

### ISU4001

**Animator.SetInteger with magic number**

Magic numbers in `SetInteger` should use enum casts for type safety.

```csharp
// BAD - triggers ISU4001
animator.SetInteger(StateHash, 2);  // What is 2?

// GOOD - enum-based
public enum MonsterState { Idle = 0, Walk = 1, Attack = 2 }

animator.SetInteger(StateHash, (int)MonsterState.Attack);
```

**Code Fix:** TBD - Requires enum definition.

---

## Installation

### Unity (UPM)

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.irsik.analyzers.unity": "https://github.com/irsiksoftware/AI-Analyzers.git?path=packages/com.irsik.analyzers.unity"
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

Configure rule severity in `.editorconfig`:

```ini
# Individual rules
dotnet_diagnostic.ISU0001.severity = error
dotnet_diagnostic.ISU3000.severity = none

# By category
dotnet_analyzer_diagnostic.category-IrsikSoftware.Performance.severity = warning
dotnet_analyzer_diagnostic.category-IrsikSoftware.Determinism.severity = error
```

## Building

```bash
dotnet build              # Debug build
dotnet build -c Release   # Release build + NuGet packages
```

## License

MIT
