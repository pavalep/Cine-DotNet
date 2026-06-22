# Phase 10.8 — Memory Leak & Event Handler Audit

> **Goal**: Ensure every `+=` has a corresponding `-=`, and every `IDisposable` properly cleans up subscriptions. **Estimated effort: 2-3 hours**.

---

## Current State

`MainViewModel` implements `IDisposable` with a [`Dispose()`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs#L434) method, but it's unclear if all event handlers are properly unsubscribed. Several patterns in the codebase are leak-prone.

---

## Leak Patterns Identified

### Pattern 1: Anonymous Lambda Event Handlers (🔴 High Risk)

```csharp
// ❌ Anonymous handler — CANNOT be unsubscribed
_player.MediaOpened += (_, _) => OnMediaOpened();
_player.PositionChanged += (_, _) => OnPositionChanged();
```

These subscribe to a long-lived `_player` singleton. Every time a new ViewModel is created, a new lambda is attached with no way to remove it.

**Fix**: Convert to method handlers and unsubscribe in `Dispose()`:

```csharp
// ✅ After
public MainViewModel()
{
    _player.MediaOpened += OnPlayerMediaOpened;
    _player.PositionChanged += OnPlayerPositionChanged;
}

private void OnPlayerMediaOpened(object? sender, EventArgs e) => OnMediaOpened();
private void OnPlayerPositionChanged(object? sender, EventArgs e) => OnPositionChanged();

public void Dispose()
{
    _player.MediaOpened -= OnPlayerMediaOpened;
    _player.PositionChanged -= OnPlayerPositionChanged;
    // ...
}
```

### Pattern 2: Static Event Subscriptions (🟢 Low Risk)

```csharp
// Static event — process lifetime, no leak
AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
```

These are fine — they live for the process lifetime. But should still be wrapped in try-catch to prevent secondary crashes.

### Pattern 3: `CollectionChanged` on ViewModel from View (🟡 Medium Risk)

```csharp
// StartPage.axaml.cs — currently handled correctly ✅
vm.RecentFiles.CollectionChanged += OnRecentFilesChanged;
// Unsubscribed in DataContextChanged
_previousVm.RecentFiles.CollectionChanged -= OnRecentFilesChanged;
```

**Audit needed**: Verify the same pattern is used everywhere.

### Pattern 4: `DispatcherTimer` Subscriptions (🟡 Medium Risk)

```csharp
private DispatcherTimer? _autoHideTimer;

// Verify Stop() + null in Dispose()
public void Dispose()
{
    _autoHideTimer?.Stop();
    _autoHideTimer = null;
}
```

---

## Implementation Steps

### Step 1: Audit All `+=` Operators

Search the codebase for every event subscription:

```powershell
Select-String -Path src\App -Recurse -Pattern '\+=' | 
    Where-Object { $_ -notmatch '=\s*\+=' }  # exclude compound assignment
```

Categorize each:

| Category | Action | Count |
|----------|--------|-------|
| Anonymous lambda on long-lived object | Convert to method + unsubscribe | ~5-10 |
| Named method on long-lived object | Verify `-=` exists in `Dispose()` | ~5-10 |
| Static event | Keep (process lifetime) | ~3 |
| View-to-ViewModel subscription | Verify unsubscribe on `DataContextChanged` | ~5 |

### Step 2: Fix Anonymous Lambdas in MainViewModel

```csharp
// File: MainViewModel.cs — audit all _player subscriptions
// Convert from:
_player.MediaOpened += (_, _) => OnMediaOpened();
// To:
_player.MediaOpened += OnPlayerMediaOpened;
// With unsubscribe in Dispose()
```

### Step 3: Add `-=` to All `IDisposable.Dispose()` Methods

Ensure `MainViewModel.Dispose()` unsubscribes everything:

```csharp
public void Dispose()
{
    // Unsubscribe from player events
    _player.MediaOpened -= OnPlayerMediaOpened;
    _player.PositionChanged -= OnPlayerPositionChanged;
    _player.MediaEnded -= OnPlayerMediaEnded;
    _player.PlaybackStateChanged -= OnPlaybackStateChanged;

    // Stop timers
    _autoHideTimer?.Stop();
    _autoHideTimer = null;

    // Dispose owned disposables
    Subtitles?.Dispose();
    Audio.Dispose();

    if (_player is IDisposable disposable)
        disposable.Dispose();
}
```

### Step 4: Verify View-ViewModel Unsubscribe Pattern

Check all views that subscribe to ViewModel events:

- `StartPage.axaml.cs` — already done ✅
- `HeaderBarControl.axaml.cs` — verify
- `ControlsBoxControl.axaml.cs` — verify
- All dialog windows — verify

---

## Success Criteria

- [ ] Every `+=` has a matching `-=` (or documented reason)
- [ ] No anonymous lambda event handlers on long-lived objects
- [ ] `MainViewModel.Dispose()` unsubscribes all player events
- [ ] All `DispatcherTimer` instances stopped in `Dispose()`
- [ ] View-ViewModel subscriptions cleaned up on detach
