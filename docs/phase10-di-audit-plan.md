# Phase 10.2 — Dependency Injection Audit & Expansion

> **Goal**: Move all service instantiation into the DI container, eliminate `new Service()` patterns, and make every service testable. **Estimated effort: 2-3 hours**.

---

## Current State

The DI container in [`App.axaml.cs:285-290`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs#L285-L290) registers only **3 types**:

```csharp
services.AddSingleton<PlayerService>();
services.AddTransient<MainViewModel>();
services.AddTransient<MainWindow>();
```

Meanwhile, many services are instantiated with `new()` directly:

| Service | Where | Problem |
|---------|-------|---------|
| `SubtitleSettingsStore` | `PreferencesDialog.axaml.cs:13` | Can't mock/test |
| `AudioSettingsStore` | `PreferencesDialog.axaml.cs:14` | Can't mock/test |
| `PlaylistCoordinator` | Inside `MainViewModel` | Hidden dependency |
| `InputRoutingService` | `MainWindow` constructor | Can't mock/test |
| `AudioManager` | Various | Hidden dependency |
| `SubtitleManager` | Various | Hidden dependency |

---

## Target Lifetime Conventions

| Lifetime | When to use | Examples |
|----------|-------------|---------|
| `Singleton` | Stateless services, single instance | `PlayerService`, `InputRoutingService`, `ILogger` |
| `Transient` | ViewModels, Windows (Avalonia manages lifetime) | `MainViewModel`, `MainWindow`, all dialogs |

---

## Implementation Steps

### Step 1: Register All Services

```csharp
private static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    // Core infrastructure
    services.AddSingleton<ILogger>(_ => new FileLogger("Cine"));
    services.AddSingleton<InputRoutingService>();
    services.AddSingleton<PlayerService>();

    // Settings stores
    services.AddSingleton<SubtitleSettingsStore>();
    services.AddSingleton<AudioSettingsStore>();
    services.AddSingleton<PlaylistSettingsStore>();

    // Coordinators / Managers
    services.AddTransient<PlaylistCoordinator>();
    services.AddTransient<AudioManager>();
    services.AddTransient<SubtitleManager>();

    // ViewModels and Windows
    services.AddTransient<MainViewModel>();
    services.AddTransient<MainWindow>();
    services.AddTransient<PreferencesDialog>();
    services.AddTransient<PlaylistDialog>();

    return services.BuildServiceProvider();
}
```

### Step 2: Remove `new Service()` Patterns

```csharp
// ❌ Before
public partial class PreferencesDialog : Window
{
    private readonly SubtitleSettingsStore _subStore = new();
    private readonly AudioSettingsStore _audioStore = new();

// ✅ After — inject via constructor
public partial class PreferencesDialog : Window
{
    private readonly SubtitleSettingsStore _subStore;
    private readonly AudioSettingsStore _audioStore;

    public PreferencesDialog(SubtitleSettingsStore subStore, AudioSettingsStore audioStore)
    {
        _subStore = subStore;
        _audioStore = audioStore;
    }
```

### Step 3: Inject Coordinators into ViewModel

```csharp
// ✅ Inject PlaylistCoordinator instead of creating inside MainViewModel
public MainViewModel(PlaylistCoordinator playlistCoordinator, ...)
{
    _playlistCoordinator = playlistCoordinator;
}
```

### Step 4: Register Dialogs with DI Container

Dialogs created via `new()` should use DI:

```csharp
// ❌ Before
var dialog = new PreferencesDialog();

// ✅ After — resolve from DI
var dialog = _serviceProvider.GetRequiredService<PreferencesDialog>();
```

---

## Success Criteria

- [ ] All services are registered in the DI container
- [ ] No `new ServiceName()` patterns remain (except value objects/POCOs)
- [ ] All dialog windows accept constructor-injected dependencies
- [ ] `PlayerService` and `InputRoutingService` remain singleton
- [ ] ViewModels and Windows are transient
