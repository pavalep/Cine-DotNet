# Phase 10.3 — `async void` Safety Audit

> **Goal**: Eliminate all `public async void` methods and wrap all event-handler `async void` with `ErrorBoundary.Run()`. **Estimated effort: 2 hours**.

---

## Current State

**11 `async void` methods** across **8 files**. By C# convention, `async void` should only be used for event handlers — any other usage risks unhandled exceptions crashing the process.

### ❌ Dangerous: `public async void` (not event handlers)

| Method | File | Line | Risk |
|--------|------|------|------|
| `OpenFile(string path)` | [`MainViewModel.Actions.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Actions.cs) | 108 | **High** — called from commands, session resume, CLI |
| `Show(double fadeMs)` | [`PauseOverlayControl.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/PauseOverlayControl.axaml.cs) | 18 | **Medium** — UI animation, crash = blank overlay |
| `Start()` | [`SpinnerOverlayControl.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/SpinnerOverlayControl.axaml.cs) | 20 | **Medium** — UI spinner, crash = no feedback |
| `ErrorBoundary.Run(Func<Task>)` | [`ErrorBoundary.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ErrorBoundary.cs) | 18 | **Low** — self-contained, but same pattern issue |

### ⚠️ Acceptable but Should Use ErrorBoundary: Event Handlers

| Method | File | Line |
|--------|------|------|
| `OnAddTrack(object?, RoutedEventArgs)` | [`SubtitleStyleFlyout.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleStyleFlyout.axaml.cs) | 306 |
| `OnSavePlaylistClick(object?, RoutedEventArgs)` | [`PlaylistDialog.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs) | 178 |
| `OnClearPlaylistClick(object?, RoutedEventArgs)` | [`PlaylistDialog.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs) | 389 |
| `AddFilesToQueue(MainViewModel)` | [`PlaylistDialog.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs) | 376 |
| `OnVolumeAutoDismiss(object?, EventArgs)` | [`ControlsBoxControl.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs) | 71 |
| `FadeHeaderAndControls(double)` | [`MainWindow.WindowControls.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.WindowControls.cs) | 283 |

---

## Implementation Steps

### Step 1: Convert `public async void` → `public async Task`

```csharp
// ❌ Before
public async void OpenFile(string path)

// ✅ After
public async Task OpenFile(string path)
```

**Callers must be updated**: Any fire-and-forget callers should use `_ = OpenFileAsync(path)` or wrap in `ErrorBoundary.Run()`.

### Step 2: Wrap Event Handlers with ErrorBoundary

```csharp
// ❌ Before
private async void OnAddTrack(object? sender, RoutedEventArgs e)
{
    // ... logic
}

// ✅ After
private async void OnAddTrack(object? sender, RoutedEventArgs e)
{
    await ErrorBoundary.TryAsync(async () =>
    {
        // ... logic
    });
}
```

### Step 3: Fix `PauseOverlayControl.Show()` and `SpinnerOverlayControl.Start()`

These are UI animation methods. Convert to `async Task`:

```csharp
// ✅ After
public async Task Show(double fadeDurationMs = 150)
public async Task Start()
```

Update callers to fire-and-forget explicitly: `_ = overlay.Show();`

---

## Policy

1. **No `public async void` methods** — ever. Use `async Task`.
2. **Event handlers** (`object sender, EventArgs e`) may use `async void` but must wrap body in `ErrorBoundary.Run()` or `ErrorBoundary.TryAsync()`.
3. **Fire-and-forget** call pattern: use `_ = MethodAsync()` with explicit discard.

---

## Success Criteria

- [ ] Zero `public async void` methods in the codebase
- [ ] All event handler `async void` methods wrapped with `ErrorBoundary`
- [ ] `OpenFile()` returns `Task` — all callers updated
- [ ] `PauseOverlayControl.Show()` and `SpinnerOverlayControl.Start()` return `Task`
