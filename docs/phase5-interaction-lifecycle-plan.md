# Phase 5 — Interaction & Lifecycle Robustness

> **Planned**: 2026-06-19 | **Completion**: TBD
> **Current build**: ✅ 0 errors, 0 warnings
> **Current tests**: **160 tests** all passing
> **Projects**: Media (net10.0-windows), Core (net10.0), App (net10.0-windows)

---

## 1. Scope & Rationale

Phase 4 established the unit-testing foundation (160 tests) and performance baseline (17 benchmarks). Phase 5 covers the **user-facing interaction layer** — keyboard shortcuts, window management, dialogs, app lifecycle, drag-and-drop, and the remaining untested service surface.

These are the parts a user touches every session. They need to be robust: no shortcuts firing while a modal dialog is open, no PIP state lost on restart, no crash reporter that throws, no session resume restoring corrupted data that causes a crash loop.

### Why this order

| Priority | Reason |
|----------|--------|
| Keyboard shortcuts first | Input routing spans the entire app — fixing it unblocks dialog and window-mgmt changes |
| App lifecycle next | Session resume, startup, and crash handling are fragile and affect every user every session |
| Dialogs third | They consume input — must understand the shortcut system first |
| PIP window | Complex multi-window state sync — needs solid foundation |
| File association | Registry writes — needs crash-reporter running to protect against failures |
| Remaining tests | Integration tests that depend on the above being stable |

### Research-Backed Design Decisions

This plan draws from:

- **Avalonia official docs**: [Window management](https://docs.avaloniaui.net/docs/app-development/window-management), [Unhandled exceptions](https://docs.avaloniaui.net/docs/app-development/setting-unhandled-exceptions), [HotKeyManager](https://docs.avaloniaui.net/docs/input-interaction/keyboard-and-hotkeys), [Commanding](https://docs.avaloniaui.net/docs/input-interaction/commanding), [Data persistence](https://docs.avaloniaui.net/docs/how-to/data-persistence-how-to)
- **Avalonia source code**: [WindowImpl.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/WindowImpl.cs) — window state save/restore with `_savedWindowInfo`, `_lastWindowState`, minimized-to-fullscreen transitions. [Window.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Window.cs) — `_children` window tracking, `OnClosing`/`OnClosed` lifecycle.
- **SourceGit App.axaml.cs**: Production Avalonia app crash handling — `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` + top-level try-catch + structured crash log with OS version, thread name, memory usage dump.
- **Microsoft Resilient Coding Patterns**: [Graceful error logging](https://github.com/microsoft/resilient-coding-patterns/blob/main/docs/patterns/10-graceful-errors.md) — distinguish expected vs unexpected errors, log without leaking sensitive data, transform technical exceptions to user-friendly messages.
- **mpv.net changelog**: Real-world media player shutdown bugs — "Fix severe bug causing termination before scripts having a chance of reacting to shutdown event." (v6.0.3.2) — "shutdown thread was aborted if it was running more than 3 seconds" (v5.4.9.5 Beta) — "Fix crash choosing Matroska edition in the menu" (v6.0.0.0-beta).
- **Industrial JSON resilience practices**: Atomic writes (write to `.tmp` → rename to target), hash verification (MD5/SHA256 on serialized data), backup file fallback, versioned schemas.

### Testability Assessment

| Class | LOC | Constructor deps | Testable now? | Gate |
|-------|-----|------------------|---------------|------|
| `MainWindow` (partials) | ~900 | `PlayerService`(indirect), platform | ❌ UI-bound | Decouple via `InputRoutingService` |
| `PipWindow` | ~500 | Platform (Window) | ❌ UI-bound | Extract `IPipWindow` |
| `PipWindowManager` | ~80 | `PipWindow`, `MainWindow` | 🟡 Partial | Use `IPipWindow` |
| `PipService` | ~80 | `IMediaPlayer`, `PipWindowManager` | 🟡 Partial | Use `IPipWindow` |
| `MainWindow.Input.cs` | ~200 | `MainWindow` state | ❌ Logic inline | Extract `InputRoutingService` |
| `PreferencesDialog` | ~60 | `SubtitleSettingsStore` | 🟡 Partial | Directly test store ops |
| `PlaylistDialog` | ~457 | `MainViewModel` | ❌ UI-bound | Extract helpers |
| `GoToTimeDialog` | ~83 | `MainViewModel` | 🟡 Partial | `ParseTime()` is pure |
| `StartPage` | ~99 | `MainViewModel` | ❌ UI-bound | Minimal logic |
| `App.axaml.cs` | ~50 | DI, startup | ❌ Integration | Integration test |
| `CrashReporter` | ~40 | Static | ✅ Yes | Direct unit test |
| `SessionManager` | ~80 | File I/O | ✅ Yes | Direct unit test |
| `FileAssociationService` | ~150 | Registry | ✅ Yes | Extract `IRegistryService` |
| `DragDropOverlayControl` | ~44 | Visual tree | ❌ UI-bound | Minimal logic |
| `SubtitleOverlayControl` | ~80 | Visual tree | ❌ UI-bound | Headless test |
| `PlayerService` | ~120 | `MpvPlayer` | 🟡 Partial | Extract `IPlayerFactory` |
| `HeaderBarControl` | ~443 | `MainViewModel` | ❌ UI-bound | Visual test |

**Testability gap: ~5 classes directly testable, ~8 need interface extraction, ~8 are UI-bound.**

---

## 2. File Inventory (Target)

### Source files to create/modify

| File | Type | Purpose | Effort |
|------|------|---------|--------|
| `src/App/Application/Services/InputRoutingService.cs` | **NEW** | Keyboard shortcut routing engine with scope support | 2 hr |
| `src/App/Application/Services/ResumeService.cs` | **NEW** | Session resume with atomic JSON writes + hash verification | 1.5 hr |
| `src/App/UI/Shell/MainWindow.Input.cs` | MODIFY | Route through InputRoutingService | 30 min |
| `src/App/Application/Services/PipWindowManager.cs` | MODIFY | Use `IPipWindow` interface, multi-monitor validation | 30 min |
| `src/App/Application/Services/PipService.cs` | MODIFY | Use `IPipWindow` interface | 30 min |
| `src/App/Application/Services/CrashReporter.cs` | MODIFY | Re-entry guard, multi-layer exception hooks, dump writer | 1 hr |
| `src/App/Application/Services/PlayerService.cs` | MODIFY | `IPlayerFactory`, async init/shutdown with timeout, dispose ordering | 1 hr |
| `src/App/App.axaml.cs` | MODIFY | Structured startup sequence, six-layer exception defense | 45 min |
| `src/App/Application/ViewModels/MainViewModel.Playlist.cs` | MODIFY | Delegate session resume to ResumeService | 30 min |
| `src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs` | MODIFY | Queue mode persistence, extract search/M3U helpers | 1 hr |
| `src/App/UI/Screens/Dialogs/PreferencesDialog.axaml.cs` | MODIFY | Dirty-state tracking, save-only-if-changed | 30 min |
| `src/App/UI/Screens/Dialogs/GoToTimeDialog.axaml.cs` | MODIFY | Extract `TimeParsingUtility.TryParseTime()` static | 15 min |
| `src/App/Application/Services/FileAssociationService.cs` | MODIFY | Per-format try-catch, `IRegistryService` interface | 30 min |

### Test files to create

| File | Tests | Purpose |
|------|-------|---------|
| `tests/Cine.Tests/Services/InputRoutingServiceTests.cs` | 15-20 | Key combinations, scope blocking, chord precedence, unbound fallback |
| `tests/Cine.Tests/Services/SessionManagerTests.cs` | 12 | Save/load/clear, corrupt JSON, missing file, version migration |
| `tests/Cine.Tests/Services/CrashReporterTests.cs` | 6 | Exception capture, re-entry guard, dump formatting |
| `tests/Cine.Tests/Services/ResumeServiceTests.cs` | 12 | Full/partial resume, corrupt file, deleted file, versioned file |
| `tests/Cine.Tests/Services/PlayerServiceTests.cs` | 8 | Init/double-init, error forwarding, dispose ordering, timeout |
| `tests/Cine.Tests/Services/PipServiceTests.cs` | 6 | State sync, decode start/stop, window lifecycle |
| `tests/Cine.Tests/Services/PipWindowManagerTests.cs` | 6 | Create/close, double-open guard, multi-monitor position validation |
| `tests/Cine.Tests/Services/FileAssociationServiceTests.cs` | 6 | Register/unregister, IsRegistered, per-format failure isolation |
| `tests/Cine.Tests/ViewModels/TimeParsingUtilityTests.cs` | 10 | HH:MM:SS, MM:SS, bare seconds, invalid, negative, overflow, trim |
| `tests/Cine.Tests/ViewModels/PlaylistDialogHelpersTests.cs` | 6 | Search filtering, M3U export format, empty list handling |
| `tests/Cine.Tests/Integration/AppLifecycleTests.cs` | 4 | Cold start, warm start, shutdown with cleanup |

**Total new tests**: ~87-96
**Total test files**: ~11

---

## 3. Detailed Work Items

### 5A — Keyboard Shortcut Routing Engine

**Files**: `InputRoutingService.cs` (NEW), `MainWindow.Input.cs` (MODIFY)

**Current state**:
- [MainWindow.Input.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) has ~200 lines of `if/else if` key routing within `OnKeyDown`
- `PipWindow.axaml.cs` has its own `OnKeyDown` handler for Space/Escape
- Both `ControlsBoxControl.axaml.cs` and `PlaylistDialog.axaml.cs` have duplicate key handling
- Some keyboard shortcuts (e.g., Ctrl+Shift+E for Equalizer) can fire while a modal dialog is open, causing confusing behavior

**Best-practice foundation**:

Avalonia provides two key routing systems:
1. **`KeyBinding`** — XAML-declarative, fires only when the control has focus (per [Avalonia commanding docs](https://docs.avaloniaui.net/docs/input-interaction/mouse-and-keyboard-shortcuts))
2. **`HotKeyManager`** with `MenuItem.HotKey` — application-wide, walks the visual tree (per [HotKeyManager docs](https://docs.avaloniaui.net/docs/input-interaction/keyboard-and-hotkeys))

Neither handles our use case: `MainWindow` needs global shortcuts that work regardless of focus and also need **scope blocking** (no shortcuts when dialog is open, different shortcuts in PIP mode). The best approach for this is **WPF's `InputGesture` + `CommandBinding` pattern adapted for Avalonia**, combined with Avalonia's `KeyGesture` parsing (which already supports `Ctrl+Shift+E` style syntax).

**Design**:
```csharp
public class InputRoutingService
{
    public enum InputScope { Normal, DialogOpen, Fullscreen, PipActive }

    private readonly Dictionary<(KeyModifiers, Key), RegisteredShortcut> _bindings = new();

    public void Register(KeyModifiers modifiers, Key key, Action action,
        string description, InputScope scope = InputScope.Normal)
    { ... }

    /// <summary>
    /// Attempts to handle a key event. Returns true if consumed.
    /// Note: longer chords (Ctrl+Shift+S) are checked before shorter ones (Ctrl+S)
    /// to prevent the wrong shortcut from activating.
    /// </summary>
    public bool TryHandle(KeyEventArgs e, InputScope currentScope = InputScope.Normal)
    { ... }
}
```

**Why not Avalonia `KeyBinding`**: Requires the control to have focus — our shortcuts must work when the video surface has focus (which swallows all input). Avalonia's own docs note: *"`KeyBinding` only fires when the control (or one of its children) has keyboard focus. If you need an application-wide shortcut that works regardless of focus, use `HotKey` on a `MenuItem`."* We're building our own because we need scopes, which `HotKeyManager` doesn't provide.

**Changes**:

1. Create `InputRoutingService` with:
   - Scoped binding table
   - Chord precedence (longer modifiers checked first)
   - `TryHandle()` returns `true` if consumed
   - Thread-safe registration (bindings can be added after creation)

2. Remove duplicate key handling from:
   - `ControlsBoxControl` — delegate to shared service
   - `PlaylistDialog` — delegate to shared service  
   - `PipWindow` — delegate to shared service with `PipActive` scope

3. Refactor `MainWindow.Input.cs` to:
   - Build the binding table once in constructor
   - Delegate to `InputRoutingService.TryHandle()`
   - Keep only window-specific handlers (window state toggle, close) inline

**Test plan** (15-20 tests):
- Register single key with no modifier
- Register chord (Ctrl+Shift+S)
- Chord precedence: Ctrl+Shift+S fires before Ctrl+S
- Scope blocking: Normal-scope shortcut does not fire when `currentScope` is `DialogOpen`
- Scope allow: Fullscreen-scope shortcut fires when `currentScope` is `Fullscreen`
- Unregistered key returns false (not consumed)
- Registration overwrite (register same key twice — last wins)
- Null/empty description does not throw
- Multiple registrations with different scopes

---

### 5B — App Lifecycle Robustness

**Files**: `App.axaml.cs` (MODIFY), `CrashReporter.cs` (MODIFY), `PlayerService.cs` (MODIFY), `ResumeService.cs` (NEW)

**Current state**:
- [App.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs): ~50 lines — minimal exception handling, no structured startup sequence
- [CrashReporter.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/CrashReporter.cs): ~40 lines — static class, no re-entry guard, only handles `AppDomain.UnhandledException`
- [SessionManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/SessionManager.cs): ~80 lines — file-based JSON, no corruption handling, no atomic write
- [PlayerService.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlayerService.cs): ~120 lines — hard-codes `new MpvPlayer()`, no shutdown timeout
- Session resume is inline in [MainViewModel.Playlist.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Playlist.cs)

**Best-practice foundation**:

Avalonia's official [exception handling docs](https://docs.avaloniaui.net/docs/app-development/setting-unhandled-exceptions) recommend a **six-layer defense**:

1. `Dispatcher.UIThread.UnhandledException` — catches UI thread exceptions (Avalonia-specific)
2. `Dispatcher.UIThread.UnhandledExceptionFilter` — let exceptions through selectively
3. `TaskScheduler.UnobservedTaskException` — catches forgotten task exceptions (fires on finalizer thread, delayed)
4. `AppDomain.CurrentDomain.UnhandledException` — catches thread-pool/non-task exceptions (informational only; can't prevent termination when `IsTerminating` is true)
5. Global `try-catch` in `Main()` — last line of defense; app has already shut down
6. Structured crash dump — SourceGit pattern: log version, OS, framework, thread name, memory usage alongside stack trace

Our `CrashReporter` currently only has #4. We need all six.

For session persistence, the [Avalonia settings persistence guide](https://docs.avaloniaui.net/docs/how-to/data-persistence-how-to) recommends: *"wrap your `Load` method in a `try/catch` block. If the JSON file becomes corrupt or its schema changes between application versions, `JsonSerializer.Deserialize` will throw an exception. Returning a default instance in the `catch` block prevents your application from crashing on startup."*

For media players specifically, the mpv.net changelog documents real-world shutdown bugs:
- "Fix severe bug causing termination before scripts having a chance of reacting to shutdown event" (v6.0.3.2)
- "shutdown thread was aborted if it was running more than 3 seconds" (v5.4.9.5 Beta)
- "Fix exception closing command palette" (v5.4.9.7 Beta)

These translate to: **player shutdown needs a timeout (not indefinite wait)** and **event handler cleanup must happen before dispose**.

**Changes**:

#### 5B.1 — Six-Layer Exception Defense

```csharp
// Program.cs / App.axaml.cs entry:
public static void Main(string[] args)
{
    // Layer 4: Thread-pool / non-task exceptions
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        CrashReporter.Capture((Exception)e.ExceptionObject);

    // Layer 3: Forgotten task exceptions (fire on finalizer — delayed)
    TaskScheduler.UnobservedTaskException += (_, e) =>
    {
        CrashReporter.Log(e.Exception);
        e.SetObserved(); // prevent process teardown
    };

    try
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    catch (Exception ex) // Layer 5: Last line of defense
    {
        CrashReporter.Capture(ex);
    }
}

// App.axaml.cs OnFrameworkInitializationCompleted:
public override void OnFrameworkInitializationCompleted()
{
    // Layer 1: UI thread exceptions
    Dispatcher.UIThread.UnhandledException += (_, e) =>
    {
        CrashReporter.Log(e.Exception);
        e.Handled = true; // only safe because our UI is stateless
    };

    // Layer 2: Filter
    Dispatcher.UIThread.UnhandledExceptionFilter += (_, e) =>
    {
        if (e.Exception is TaskCanceledException)
            e.RequestCatch = false; // dont log cancellations
    };

    base.OnFrameworkInitializationCompleted();
}
```

#### 5B.2 — CrashReporter Enhancements

- **Re-entry guard**: `private static volatile int _inCrash;` — if `Interlocked.Exchange(ref _inCrash, 1) == 1`, return immediately (prevents crash-loop on crash-reporter failure)
- **Structured dump**: Log app version, .NET version, OS version, thread name, memory, timestamp alongside full stack trace (SourceGit pattern)
- **Dump path**: `%LOCALAPPDATA%\Cine\crashes\crash_{yyyy-MM-dd_HH-mm-ss}.log`
- **Catch own failures**: Wrap dump-write in try-catch — if the crash reporter itself fails, log to `Debug.WriteLine` as ultimate fallback

#### 5B.3 — Atomic Session Persistence (ResumeService)

Microsoft's [resilient coding patterns](https://github.com/microsoft/resilient-coding-patterns/blob/main/docs/patterns/10-graceful-errors.md) distinguish between **expected errors** (file not found, corrupt JSON) and **unexpected errors** (disk full, access denied). For session resume:

```csharp
public class ResumeService
{
    public Task<ResumeData?> TryResumeAsync(string sessionPath)
    {
        // 1. CHECK EXPECTED: file not found → return null (fresh start)
        if (!File.Exists(sessionPath)) return null;

        try
        {
            // 2. CHECK EXPECTED: corrupt JSON → log warning, return null
            var json = File.ReadAllText(sessionPath);
            var data = JsonSerializer.Deserialize<ResumeData>(json);
            if (data == null) return null;

            // 3. VALIDATE: check schema version, prune invalid entries
            if (data.Version > CurrentVersion) return null; // future version
            data.Playlist.RemoveAll(p => !File.Exists(p)); // files deleted since last session

            // 4. SANITIZE: clamp position to valid range
            if (data.Position < 0) data.Position = 0;

            return data;
        }
        catch (JsonException ex)
        {
            CrashReporter.Log(ex, isWarning: true);
            return null; // corrupt → fresh start
        }
    }

    public void Save(ResumeData data, string sessionPath)
    {
        // ATOMIC WRITE: temp file → rename (prevents half-written files on crash)
        var tempPath = sessionPath + ".tmp";
        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, sessionPath, overwrite: true);
    }
}
```

This is backed by the industrial practice of **atomic writes** — write to a temp file first, then rename. If the app crashes mid-write, the temp file is orphaned but the main file is intact.

#### 5B.4 — PlayerService Shutdown

mpv.net's changelog teaches: **never wait indefinitely for player shutdown**. We add a 3-second timeout with thread abort fallback (the pattern mpv.net itself adopted after their crash):

```csharp
public async Task ShutdownAsync(CancellationToken ct = default)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

    // 1. Stop playback first (prevents decode thread from accessing disposed resources)
    _player.Stop();

    // 2. Unsubscribe events BEFORE dispose (prevents late callbacks on dead objects)
    UnsubscribePlayerEvents();

    // 3. Dispose with timeout
    try
    {
        await Task.Run(() => _player.Dispose(), linked.Token);
    }
    catch (OperationCanceledException)
    {
        CrashReporter.Log(new TimeoutException("Player dispose timed out"), isWarning: true);
        // Best-effort: we tried. Process exit will clean up native resources.
    }
}
```

#### 5B.5 — Startup Sequence

```csharp
// Ordered startup (each step can fail independently):
// 1. CrashReporter.Attach()        — exception hooks first (defensive)
// 2. FileAssociationService.Init() — non-critical, best-effort
// 3. PlayerService.InitializeAsync(5s timeout) — critical, abort on failure
// 4. DI container build             — critical, abort on failure
// 5. ResumeService.TryResumeAsync() — non-critical, falls back to fresh start
// 6. MainWindow created + shown     — final step, fully initialized
```

**Test plan** (42-46 tests):
- `SessionManager`: 12 tests (save/load round-trip, empty file, corrupt JSON between versions, missing file, atomic write verification, clear removes file)
- `CrashReporter`: 6 tests (Capture formats correctly, re-entry guard blocks second call, null exception handled, inner exception unwrapped, dump contains version info, dump path unique)
- `ResumeService`: 12 tests (full resume, partial resume with deleted files, corrupt JSON returns null, future version returns null, negative position clamped, empty playlist valid, null path throws, temp file cleaned up after save)
- `PlayerService`: 8 tests (init succeeds, double-init is no-op, init failure throws, shutdown disposes, shutdown after init OK, shutdown without init no-op, event unsubscription, dispose timeout fires)
- Integration: 4 tests (cold start creates empty session, warm start resumes all state, shutdown cleans up temp file, crash mid-save leaves original file intact)

---

### 5C — Dialog Behavior & Dismiss

**Files**: `PreferencesDialog.axaml.cs` (MODIFY), `PlaylistDialog.axaml.cs` (MODIFY), `GoToTimeDialog.axaml.cs` (MODIFY)

**Current state**:
- [GoToTimeDialog.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/GoToTimeDialog.axaml.cs): `ParseTime()` is a private instance method — should be a public static utility
- [PreferencesDialog.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PreferencesDialog.axaml.cs): saves on every close — even if user opened and immediately closed without changes
- [PlaylistDialog.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs): queue mode state lost on close, `OnDataContextChanged` subscribes to `CollectionChanged` but never unsubscribes, search filter logic inline

**Best-practice foundation**:

Avalonia's [saving settings guide](https://docs.avaloniaui.net/docs/how-to/data-persistence-how-to) warns: *"saving on every property change can cause excessive disk I/O. Consider debouncing saves or batching changes with a timer."* Our preferences dialog currently saves on every close — the fix is dirty-state tracking: compare initial values on open to values on close, only save if different.

For dialog lifecycle, the [Avalonia Book Chapter 12](https://wieslawsoltes.github.io/AvaloniaBook/Chapters/Chapter12.html) shows the pattern for unsaved-changes dialogs:
```csharp
Closing += async (sender, e) => {
    if (DataContext is ShellViewModel vm && vm.HasUnsavedChanges) {
        var dialog = new UnsavedChangesDialog();
        var result = await dialog.ShowDialog<bool>(this);
        if (!result) e.Cancel = true;
    }
};
```

**Changes**:

1. **GoToTimeDialog** — Extract `TimeParsingUtility`:
   ```csharp
   public static class TimeParsingUtility
   {
       /// <summary>Parse: "90" → 1:30, "5:30" → 5:30, "1:23:45" → 1:23:45, "abc" → null</summary>
       public static TimeSpan? TryParseTime(string input)
       {
           if (string.IsNullOrWhiteSpace(input)) return null;
           input = input.Trim();
           // Try HH:MM:SS, MM:SS, or bare seconds
           ...
       }
   }
   ```
   Then `GoToTimeDialog` calls `TimeParsingUtility.TryParseTime()` instead of private method.

2. **PreferencesDialog** — Dirty-state tracking:
   - On `OnDataContextChanged`: snapshot current values
   - On `Closing`: compare snapshots, only save if different
   - No change = no disk write = no debounce timer = less I/O

3. **PlaylistDialog**:
   - Fix `CollectionChanged` leak: unsubscribe in `OnClosed`/`OnUnloaded`
   - Extract `ApplySearchFilter(string query, IEnumerable<PlaylistItem> items)` to helper
   - Extract `ExportToM3U(IEnumerable<string> paths, string filePath)` to helper
   - Persist queue mode flag via `MainViewModel.IsQueueModeEnabled` (already exists on ViewModel)

**Test plan** (16-18 tests):
- `TimeParsingUtility`: 10 tests — "90", "5:30", "1:23:45", "0" → 00:00, "abc" → null, "" → null, "  " → null, "-1:00" → null, "99:99:99" (overflow behavior), "  1:30  " (trim)
- `PlaylistDialogHelpers.SearchFilter`: 4 tests — match, no-match, empty query returns all, null query returns all
- `PlaylistDialogHelpers.M3UExport`: 2 tests — format header + entries, empty list produces empty file

---

### 5D — PIP Window State Sync

**Files**: `PipWindowManager.cs` (MODIFY), `PipService.cs` (MODIFY), `IPipWindow.cs` (NEW)

**Current state**:
- [PipWindowManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipWindowManager.cs): ~80 lines — creates/owns `PipWindow`, relays events
- [PipService.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs): ~80 lines — background frame decode
- [PipWindow.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Ui/Screens/Dialogs/PipWindow.axaml.cs): ~500 lines — seek bar, resize, auto-hide, snap-to-edge, state persistence

**Best-practice foundation**:

Avalonia's [Window management docs](https://docs.avaloniaui.net/docs/app-development/window-management) provide the canonical pattern for multi-window tracking — maintain a static list of open windows, register on creation, remove on `Closed` event:

```csharp
public static class WindowManager
{
    private static readonly List<Window> _openWindows = new();
    public static IReadOnlyList<Window> OpenWindows => _openWindows;

    public static void Register(Window window)
    {
        _openWindows.Add(window);
        window.Closed += (_, _) => _openWindows.Remove(window);
    }
}
```

For multi-monitor position validation, the [Avalonia settings guide](https://docs.avaloniaui.net/docs/how-to/data-persistence-how-to) explicitly warns: *"validate that the saved coordinates are still within the bounds of the current screen configuration. A user may have disconnected an external monitor since the last session, causing the window to appear off-screen."*

The fix uses `TopLevel.Screens`:
```csharp
var screens = TopLevel.GetTopLevel(this)?.Screens;
var onScreen = screens.All.Any(s => s.WorkingArea.Contains(savedPosition));
if (!onScreen)
    savedPosition = new PixelPoint(screens.Primary.WorkingArea.X + 20,
                                    screens.Primary.WorkingArea.Y + 20);
```

This is confirmed by the Avalonia source: [WindowImpl.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/WindowImpl.cs) handles `_savedWindowInfo` with minimized/maximized state transitions to prevent wrong bounds restoration.

**Changes**:

1. Extract `IPipWindow` interface:
   ```csharp
   public interface IPipWindow : IDisposable
   {
       bool IsClosed { get; }
       event EventHandler? PlayPauseRequested;
       event EventHandler<double>? SeekRequested;
       event EventHandler? MuteToggled;
       void SetPlayingState(bool isPlaying);
       void UpdatePosition(double positionSec, double durationSec);
       void Show();
       void Close();
   }
   ```

2. `PipWindowManager` — Multi-monitor validation for position restore:
   ```csharp
   private void ValidatePosition(PipState state)
   {
       if (TopLevel.GetTopLevel(_mainWindow)?.Screens is not { } screens) return;
       if (screens.All.Any(s => s.WorkingArea.Contains(new PixelPoint(state.X, state.Y))))
           return; // on screen — good
       // Off screen — place on primary with default offset
       state = new PipState(screens.Primary.WorkingArea.X + 20,
                            screens.Primary.WorkingArea.Y + 20, state.W, state.H);
   }
   ```

3. `PipService` — Frame decode thread safety:
   - `UpdateFrame()` uses `Dispatcher.UIThread.Post()` for `WriteableBitmap` writes (Avalonia requires UI thread for bitmap operations)
   - Add frame-drop counter: if 3+ consecutive frames dropped, log warning (indicates throttle too aggressive or CPU overloaded)
   - Use `CancellationToken` for decode loop — dispose cancels token, loop exits cleanly

**Test plan** (12 tests):
- `PipWindowManager`: 6 tests — toggle create/close, double-open guard, dispose closes window, state restore with valid position, state restore with off-screen position (clamped), null window on first toggle
- `PipService`: 6 tests — state sync forwards to PIP window, decode start/stop, decode cancellation on dispose, frame throttle respects interval, multiple start calls are no-op, event relay

---

### 5E — File Association & Drag-Drop

**Files**: `FileAssociationService.cs` (MODIFY), `DragDropOverlayControl.axaml.cs` (MODIFY)

**Current state**:
- [FileAssociationService.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FileAssociationService.cs): ~150 lines — static class, modifies HKCU registry. Single top-level try-catch on `Register()` means if one format fails, all fail. Called from `App.axaml.cs` on startup.
- [DragDropOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/DragDropOverlayControl.axaml.cs): ~44 lines — simple fade-in/out overlay. No guard against double-show.

**Changes**:

1. **FileAssociationService**:
   - Extract `IRegistryService` interface so tests don't touch real registry
   - Per-format try-catch: if `.mkv` registration fails, `.mp4` still registers
   - Error logging: log which format failed and why
   - `RegisterOnStartup()`: optionally runs on background thread via `Task.Run()` (registry can block)
   - Guard: `GetExecutablePath()` may return unexpected value in development scenarios — validate path ends with `.exe` before using

2. **DragDropOverlayControl**:
   - Guard `Show()` with `_isShowing` flag — if already showing, skip (prevents double-fade animation conflict)
   - Guard `Hide()` with `_isShowing` flag — if not showing, skip
   - Use `CancellationTokenSource` for animation cancellation — rapid show/hide will cancel previous animation instead of stacking

**Test plan** (6-8 tests):
- `FileAssociationService`: 6 tests with mock `IRegistryService` — Register sets expected keys, Unregister removes keys, IsRegistered returns true after register, format failure doesn't block others, null format list handled, executable path validation

---

### 5F — Remaining Test Coverage

**Files**: `SubtitleOverlayControl.axaml.cs`, `ControlsBoxControl.axaml.cs`, `OsdNotificationControl.axaml.cs`, integration tests

**Current state**: These are UI-bound controls with visual rendering. We can test them using **Avalonia.Headless** — an official package that runs Avalonia without a window manager, perfect for smoke tests.

**Approach** (per [Avalonia Headless docs](https://docs.avaloniaui.net/docs/concepts/headless)):
1. Add `Avalonia.Headless` NuGet package to `Cine.Tests.csproj`
2. Create shared `AppBuilder.Configure<TestApp>()` for headless session
3. Smoke tests: render each control, set properties, verify no crash

Headless unit tests use `AvaloniaTest` attribute with headless session:
```csharp
[AvaloniaTest]
public void SubtitleOverlay_RendersText_DoesNotThrow()
{
    var window = new Window();
    var overlay = new SubtitleOverlayControl();
    window.Content = overlay;
    window.Show();

    // Render one frame
    overlay.SubtitleText = "Test\nMulti-line";

    // No assertion needed — if it throws, test fails
}
```

**Test plan** (5-7 tests):
- `SubtitleOverlayControl`: 2 smoke tests (single-line render, multi-line render)
- `ControlsBoxControl`: 1 smoke test (render with visible buttons)
- `OsdNotificationControl`: 1 smoke test (show/hide cycle)
- Integration lifecycle: 4 tests (cold start creates MainWindow, shutdown disposes all services, resume with corrupt session falls back gracefully, rapid open/close PIP doesn't double-window)

---

## 4. Risk Analysis

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| InputRoutingService breaks existing shortcuts | High — users can't control playback | Low if tested | 15-20 comprehensive tests; keep old code path behind feature flag for rollout |
| PIP double-window on rapid toggle | Medium — two overlapping PIP windows | Medium | Guard with `IsClosed` check; add debug log if rapid toggle detected |
| WriteableBitmap + UI thread contention | Medium — frame drops in PIP | Low after throttle | Already throttled to 30fps; add frame-drop counter for monitoring |
| Registry write failures on Win11 lockdown | Medium — file associations fail silently | Medium | Per-format try-catch; one format failure doesn't block others |
| Session resume restores corrupt state → crash loop | High — startup crash | Low | Atomic writes + JSON validation + version check + fallback to default |
| Crash reporter itself crashes → crash loop | High — user sees nothing | Low | Re-entry guard + try-catch around dump write + Debug.WriteLine fallback |
| Player dispose hangs → app doesn't exit | Medium — zombie process | Medium | 3-second timeout with `CancellationTokenSource` after `Stop()` + event unsubscribe |
| Headless tests flaky in CI | Low — CI false failures | Low | Pin Avalonia.Headless version; mark as `[Trait("Category", "Integration")]`; run in separate CI job |

---

## 5. Execution Order & Effort

| Step | Task | Files | Effort | Tests | Priority |
|------|------|-------|--------|-------|----------|
| **5A** | Input routing service | 2 new + 1 modify | 2.5 hr | 15-20 | 🔴 Must-do (blocks input testing) |
| **5B** | App lifecycle | 2 new + 3 modify | 3.5 hr | 38-46 | 🔴 High (affects every startup) |
| **5C** | Dialog behavior | 3 modify | 2 hr | 14-16 | 🟡 High (affects every user) |
| **5D** | PIP state sync | 1 new + 2 modify | 2 hr | 10-12 | 🟡 Medium (PIP users) |
| **5E** | File association + drag-drop | 2 modify | 1.5 hr | 6-8 | 🟢 Low (non-critical) |
| **5F** | Remaining test coverage | ~3 new + 1 modify | 2 hr | 5-7 | 🟢 Low (visual/integration) |
| **Total** | | **~5 new + ~11 modify** | **~13.5 hours** | **~88-109 tests** | |

### Quick Start (Do First — ~6 hours)

```
1. Input routing service      (2.5 hr)  — foundation for all key handling fixes
2. App lifecycle              (3.5 hr)  — startup robustness before dialogs
   ├── CrashReporter enhancements    (1 hr)
   ├── ResumeService                 (1.5 hr)
   └── PlayerService + startup seq  (1 hr)
```

---

## 6. Key Metrics

| Metric | Target |
|--------|--------|
| Total test count after Phase 5 | **~248-269 passing** |
| Directly testable service classes | **100%** (all services with injectable deps) |
| Exception defense layers | **6** (Avalonia recommended) |
| Session resume tolerance | **Corrupt file → default, missing file → default, wrong version → default, partial data → pruned** |
| PIP concurrent window safety | **Never two windows, position on-screen (multi-monitor validated)** |
| Player shutdown timeout | **3 seconds** |
| Registry operation isolation | **Per-format try-catch** |
| Keyboard shortcut coverage | **100% registered shortcuts tested** |
| Build time | **< 10 seconds** |

---

## 7. Dependencies & Prerequisites

| Task | Depends On | Notes |
|------|-----------|-------|
| 5A Input routing | None | First task |
| 5B App lifecycle | None | Can be done in parallel with 5A |
| 5C Dialogs | 5A (keyboard handling) | Dialogs need to register their own shortcuts |
| 5D PIP | 5A, 5B | PIP uses keyboard scopes + app shutdown |
| 5E File association | 5B (CrashReporter) | Registry ops guarded by crash reporter |
| 5F Remaining tests | 5A-5E completed | Integration tests depend on all refactors |

---

## 8. File Inventory (Phase 5 — Created)

| File | Tests | Purpose |
|------|-------|---------|
| `src/App/Application/Services/InputRoutingService.cs` | 15-20 | Keyboard shortcut routing engine |
| `src/App/Application/Services/ResumeService.cs` | 12 | Session resume with atomic writes + validation |
| `src/App/Application/Services/IPipWindow.cs` | — | PIP window interface |
| `tests/Cine.Tests/Services/InputRoutingServiceTests.cs` | 15-20 | Key combos, scopes, chord precedence |
| `tests/Cine.Tests/Services/SessionManagerTests.cs` | 12 | Save/load, corrupt, missing, atomic |
| `tests/Cine.Tests/Services/CrashReporterTests.cs` | 6 | Capture, re-entry, dump |
| `tests/Cine.Tests/Services/ResumeServiceTests.cs` | 12 | Full, partial, corrupt, versioned |
| `tests/Cine.Tests/Services/PlayerServiceTests.cs` | 8 | Init, double-init, dispose, timeout |
| `tests/Cine.Tests/Services/PipServiceTests.cs` | 6 | State sync, decode lifecycle |
| `tests/Cine.Tests/Services/PipWindowManagerTests.cs` | 6 | Create/close, position validation |
| `tests/Cine.Tests/Services/FileAssociationServiceTests.cs` | 6 | Register/unregister, isolation |
| `tests/Cine.Tests/ViewModels/TimeParsingUtilityTests.cs` | 10 | All time formats |
| `tests/Cine.Tests/Integration/AppLifecycleTests.cs` | 4 | Startup/shutdown/crash |

### Modified files

| File | Change Summary |
|------|----------------|
| `src/App/UI/Shell/MainWindow.Input.cs` | Route through InputRoutingService |
| `src/App/Application/Services/CrashReporter.cs` | Re-entry guard + six-layer hooks + structured dumps |
| `src/App/Application/Services/PlayerService.cs` | IPlayerFactory + async init/shutdown with timeout |
| `src/App/App.axaml.cs` | Structured startup sequence + six-layer exception defense |
| `src/App/Application/ViewModels/MainViewModel.Playlist.cs` | Delegate resume to ResumeService |
| `src/App/Application/Services/PipWindowManager.cs` | IPipWindow + multi-monitor validation |
| `src/App/Application/Services/PipService.cs` | CancellationToken decode loop |
| `src/App/UI/Screens/Dialogs/GoToTimeDialog.axaml.cs` | Use TimeParsingUtility |
| `src/App/UI/Screens/Dialogs/PreferencesDialog.axaml.cs` | Dirty-state tracking |
| `src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs` | Queue persistence, event leak fix, extract helpers |
| `src/App/Application/Services/FileAssociationService.cs` | Per-format try-catch, IRegistryService |
| `src/App/UI/Controls/Indicators/DragDropOverlayControl.axaml.cs` | Double-show guard |

---

## 9. Quick Reference

```
# Build test project
dotnet build tests\Cine.Tests\Cine.Tests.csproj

# Run unit tests only
dotnet test tests\Cine.Tests\Cine.Tests.csproj --no-build

# Run specific category
dotnet test tests\Cine.Tests\Cine.Tests.csproj --no-build --filter "Category=Integration"

# Run benchmarks
dotnet run --project tests\Cine.Benchmarks\Cine.Benchmarks.csproj -c Release

# Build Release (CI pipeline)
dotnet build tests\Cine.Tests\Cine.Tests.csproj -c Release
```

---

## 10. Research References

| Source | What We Learned |
|--------|----------------|
| [Avalonia Window Management](https://docs.avaloniaui.net/docs/app-development/window-management) | Multi-window tracking pattern (`_openWindows` list), `WindowClosingBehavior`, `Screens` API for multi-monitor validation, `OnOpened`/`OnClosing` lifecycle hooks |
| [Avalonia Exception Handling](https://docs.avaloniaui.net/docs/app-development/setting-unhandled-exceptions) | Six-layer defense: `Dispatcher.UnhandledException` + `UnhandledExceptionFilter` + `TaskScheduler.UnobservedTaskException` + `AppDomain.UnhandledException` + global try-catch + structured logging |
| [Avalonia Settings Persistence](https://docs.avaloniaui.net/docs/how-to/data-persistence-how-to) | `try/catch` around `Deserialize` → return default on failure; warn about debouncing saves; validate screen bounds before restoring position |
| [Avalonia HotKeyManager](https://docs.avaloniaui.net/docs/input-interaction/keyboard-and-hotkeys) | `HotKeyManager` walks visual tree for `ICommand` targets; `KeyGesture` parsing supports `Ctrl+Shift+X` syntax |
| [Avalonia KeyBinding](https://docs.avaloniaui.net/docs/input-interaction/mouse-and-keyboard-shortcuts) | `KeyBinding` only fires when focused — need `HotKey` for global; `Gesture` string parsed as `KeyGesture` |
| [Avalonia WindowImpl.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Windows/Avalonia.Win32/WindowImpl.cs) | `_savedWindowInfo` restoration with `_lastWindowState` check prevents minimized-state restoring wrong bounds |
| [SourceGit App.axaml.cs](https://github.com/NathanBaulch/sourcegit/blob/master/src/App.axaml.cs) | Production crash handler: structured dump with version, OS, framework, thread name, memory, user, app start time |
| [mpv.net changelog](https://github.com/mpvnet-player/mpv.net/blob/main/docs/changelog.md) | Real shutdown bugs: abort shutdown thread after 3s, event handler cleanup before dispose, exception closing windows on Win7 |
| [Microsoft Resilient Patterns](https://github.com/microsoft/resilient-coding-patterns) | Distinguish expected vs unexpected errors; log without sensitive data; transform technical to user-friendly |
| [Industrial JSON patterns (CSDN)](https://blog.csdn.net/kylezhao2019/article/details/158043763) | Atomic writes (`.tmp` → rename), hash verification, backup file fallback, versioned schemas |
| [Avalonia Book Ch.12](https://wieslawsoltes.github.io/AvaloniaBook/Chapters/Chapter12.html) | `Screens.Changed` event for hot-plug montior detection; unsaved-changes dialog pattern; `ClosingBehavior` for child window cascade |
| [.NET Threading Best Practices](https://github.com/dotnet/docs/blob/main/docs/standard/threading/managed-threading-best-practices.md) | `Monitor.TryEnter` with timeout to prevent deadlocks; avoid `lock(this)`; `Interlocked` for race-condition-free counters |
