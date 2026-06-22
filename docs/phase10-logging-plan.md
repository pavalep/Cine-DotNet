# Phase 10.1 — Logging Infrastructure Formalization

> **Goal**: Unify all logging paths into a single pipeline, remove ad-hoc logging methods, and add log rotation and level configuration. **Estimated effort: 2 hours**.

---

## Current Anti-Patterns

| Issue | Details | Severity |
|-------|---------|----------|
| **Three separate log paths** | `FileLogger` → `%LOCALAPPDATA%\Cine\logs\`, `MainViewModel.Log()` → `%LOCALAPPDATA%\Cine\cine_startup.log`, `CrashReporter` → separate file | 🔴 |
| **No log level configuration** | `FileLogger` has no concept of minimum level — all levels go through | 🟡 |
| **No log rotation** | Log files grow unboundedly — no archive/delete policy | 🟡 |
| **Duplicate `Log()` helpers** | `MainViewModel.Log()` and `App.Log()` both replicate `FileLogger` functionality | 🔴 |

---

## Files Affected

| File | What to change |
|------|----------------|
| [`src/Core/Services/FileLogger.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/Core/Services/FileLogger.cs) | Add log levels with minimum-level filtering, add rotation |
| [`src/App/Application/ViewModels/MainViewModel.cs:32-45`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs#L32-L45) | Remove private `Log()` method, redirect to `FileLogger` |
| [`src/App/App.axaml.cs:82-85`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs#L82-L85) | Remove private `Log()` method |
| [`src/App/Application/Services/CrashReporter.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/CrashReporter.cs) | Integrate with `FileLogger` instead of separate dump |
| `App.axaml.cs` (Main entry) | Bootstrap logger and inject into DI |

---

## Implementation Steps

### Step 1: Enhance `FileLogger` with Levels & Rotation

```csharp
// Add to FileLogger.cs
public enum LogLevel { Trace, Debug, Info, Warning, Error, Critical }

public class FileLogger : ILogger, IDisposable
{
    private LogLevel _minimumLevel = LogLevel.Trace;

    public void SetMinimumLevel(LogLevel level) => _minimumLevel = level;

    private bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    // Add rotation: delete files older than 7 days on startup
    private static void RotateLogs(string logDir, int maxDays = 7)
    {
        try
        {
            foreach (var file in Directory.GetFiles(logDir, "Cine_*.log"))
            {
                var age = DateTime.Now - File.GetLastWriteTime(file);
                if (age.TotalDays > maxDays)
                    File.Delete(file);
            }
        }
        catch { /* best-effort */ }
    }
}
```

### Step 2: Bootstrap Logger in `App.axaml.cs`

```csharp
public static void Main(string[] args)
{
    var logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "logs");
    var logger = new FileLogger("Cine", logDir);
    logger.Info("=== Cine.Avalonia starting ===");

    // Remove duplicate: App.Log(), MainViewModel.Log() → use this logger
}
```

### Step 3: Remove Duplicate Log Methods

Delete `MainViewModel.Log()` — replace all calls with `Log.ForContext<MainViewModel>()`.

Delete `App.Log()` — replace with `FileLogger` instance or `Log.ForContext<App>()`.

### Step 4: Route CrashReporter Through FileLogger

```csharp
// CrashReporter.cs — instead of writing its own file
public static void Dump(Exception ex, string context)
{
    Log.ForContext("CrashReporter").Error(ex, "Dump: {Context}", context);
}
```

---

## Success Criteria

- [ ] Only one log directory: `%LOCALAPPDATA%\Cine\logs\`
- [ ] Files older than 7 days are cleaned up on startup
- [ ] Log level can be configured (via config file or env var)
- [ ] `MainViewModel.Log()` and `App.Log()` are removed
- [ ] All `Log.ForContext()` calls use the same `ILogger` interface
