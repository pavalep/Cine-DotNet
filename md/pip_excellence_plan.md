# PiP Excellence Plan — From "Sufficient" to "Excellent"

## Current Status

**15 identified gaps** — 3 critical, 4 major, 4 moderate, 4 minor.

This plan fixes all of them across 6 phases. Each phase is atomic — can be done independently, builds cleaner on the previous.

---

## Phase 1: Fix OnOpened/Opened Race (🔴#3)

**Risk:** Double registration attempt, fragile logic flow.

### Problem
`TryEnableMirrorNow()` + `StartMirrorRetry()` runs from **two** paths during window creation:
1. `PipWindow.OnOpened()` override (line 268)
2. `this.Opened += OnOpenedRetryMirror` event subscription (line 80)

Both trigger after `Show()` — guaranteed double evaluation, confusing to read.

### Fix
Remove the `Opened` event subscription entirely. The `OnOpened()` override is the **single** entry point. Also remove `StartMirrorRetry()` from `EnableDwmMirror()` — the retry timer only starts from `OnOpened()`.

#### `PipWindow.axaml.cs` — `EnableDwmMirror()` (lines 53-86)

```csharp
public void EnableDwmMirror(DwmThumbnailManager manager)
{
    if (_thumbnailId > 0)
    {
        Log.ForContext<PipWindow>().Info("EnableDwmMirror: already enabled, id={Id}", _thumbnailId);
        return;
    }
    _dwmManager = manager;

    // Don't retry here — OnOpened will handle it
    if (TryEnableMirrorNow())
        return;

    Log.ForContext<PipWindow>().Warning(
        "EnableDwmMirror: deferred (source=0x{Source:X}, handle=0x{Handle:X})",
        _dwmManager.SourceHwnd, TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
}
```

#### `PipWindow.axaml.cs` — `OnOpened()` (lines 262-273)

```csharp
protected override void OnOpened(EventArgs e)
{
    base.OnOpened(e);

    RestoreState();
    SetupHoverTimer();

    if (_aspectRatio > 0 && Width > 0 && Height > 0)
        ApplyAspectRatioConstraint();

    // Deferred mirror setup: the NativePlatformHandle may not be ready
    // right after Show(), so we retry with a timer.
    if (!TryEnableMirrorNow())
        StartMirrorRetry();
}
```

#### Remove `OnOpenedRetryMirror` entirely (delete lines 78-86)

---

## Phase 2: Replace Polling With Event-Driven Wait (🔴#1)

**Risk:** 50 UI thread wakeups polling `TryGetPlatformHandle()`.

### Problem
`DispatcherTimer` at 100ms × up to 50 attempts = 5 seconds of polling. The window `Handle` changes exactly once (when the native window is created) — we should **wait for that event**, not poll for it.

### Fix
Use **single-shot timer** with a longer interval, combined with `Activated` event which fires when the native window is fully ready.

#### `PipWindow.axaml.cs`

```csharp
private const int MirrorRetryMaxMs = 5000; // 5 seconds total
private Stopwatch? _mirrorRetryWatch;

private void StartMirrorRetry()
{
    if (_mirrorRetryTimer != null) return;

    _mirrorRetryWatch = Stopwatch.StartNew();
    _mirrorRetryTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(200) // 5× less frequent
    };
    _mirrorRetryTimer.Tick += OnMirrorRetryTick;
    _mirrorRetryTimer.Start();
    
    // Also listen for Activated — the handle is guaranteed ready by then
    this.Activated += OnActivatedRetryMirror;
}

private void StopMirrorRetry()
{
    if (_mirrorRetryTimer == null) return;
    _mirrorRetryTimer.Stop();
    _mirrorRetryTimer.Tick -= OnMirrorRetryTick;
    _mirrorRetryTimer = null;
    _mirrorRetryWatch = null;
    this.Activated -= OnActivatedRetryMirror;
    _mirrorRetryAttempts = 0;
}

private void OnActivatedRetryMirror(object? sender, EventArgs e)
{
    // Window is fully created and activated — handle should be ready
    this.Activated -= OnActivatedRetryMirror;
    if (TryEnableMirrorNow())
        StopMirrorRetry();
}

private void OnMirrorRetryTick(object? sender, EventArgs e)
{
    if (TryEnableMirrorNow())
    {
        StopMirrorRetry();
        return;
    }

    _mirrorRetryAttempts++;
    if (_mirrorRetryWatch != null && _mirrorRetryWatch.ElapsedMilliseconds >= MirrorRetryMaxMs)
    {
        Log.ForContext<PipWindow>().Warning(
            "Mirror retry exhausted after {Elapsed}ms (source=0x{Source:X}, handle=0x{Handle:X})",
            _mirrorRetryWatch.ElapsedMilliseconds,
            _dwmManager?.SourceHwnd ?? IntPtr.Zero,
            TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        StopMirrorRetry();
    }
}
```

---

## Phase 3: User Feedback During Retry (🔴#2)

**Risk:** Dead UI — user presses PiP button, nothing visible for up to 5 seconds.

### Problem
`PipService.EnterPip()` creates the window offscreen, calls `Show()`, and returns. If the mirror retry is running, the window is **visible but blank** — nothing tells the user "I'm working on it."

### Fix
Add a **transient "loading" overlay** to the PiP window that shows during retry, then auto-hides when mirror succeeds.

#### `PipWindow.axaml` — Add to the video area (layer 0, behind HoverOverlay)

```xml
<!-- Layer 0: DWM video surface -->
<Border x:Name="VideoArea"
        Background="#0D000000" ClipToBounds="True"
        CornerRadius="0,0,10,10" />

<!-- Layer 0.5: Loading indicator (visible during mirror retry) -->
<Border x:Name="LoadingOverlay"
        Background="#40000000"
        IsVisible="False"
        CornerRadius="0,0,10,10"
        IsHitTestVisible="False">
    <TextBlock Text="Starting Picture-in-Picture…"
               Foreground="White"
               FontSize="13"
               VerticalAlignment="Center"
               HorizontalAlignment="Center" />
</Border>
```

#### `PipWindow.axaml.cs` — Show/hide loading overlay

```csharp
private void StartMirrorRetry()
{
    // Show loading indicator
    if (LoadingOverlay != null) LoadingOverlay.IsVisible = true;
    // ... rest of existing StartMirrorRetry()
}

private void StopMirrorRetry()
{
    // Hide loading indicator
    if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
    // ... rest of existing StopMirrorRetry()
}
```

---

## Phase 4: Resize Handling & Layout Sync (🟠#5, #8, #11)

**Risk:** Video doesn't follow window resize; titlebar height changes break positioning.

### Problem
`SyncThumbnailRect()` is called only once after `RegisterTarget`. No `SizeChanged` handler, no `LayoutUpdated` handler.

### Fix
Hook `SizeChanged` to call `SyncThumbnailRect()` whenever the PiP window changes size. Also listen to `TitleBar` bounds changes for dynamic offset.

#### `PipWindow.axaml.cs` — In constructor

```csharp
public PipWindow()
{
    InitializeComponent();
    TitleBar.PointerPressed += OnTitleBarPointerPressed;
    KeyDown += OnKeyDown;

    PipSeekSlider.PropertyChanged += OnSeekSliderChanged;
    
    // ── New: Sync DWM thumbnail on resize ──
    this.SizeChanged += (_, _) => SyncThumbnailRect();
    
    // ── New: Detect titlebar height changes (DPI, system font scale) ──
    TitleBar.PropertyChanged += OnTitleBarPropertyChanged;
}

// ── New ──
private void OnTitleBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == Layoutable.BoundsProperty)
        SyncThumbnailRect();
}
```

#### `PipWindow.axaml.cs` — Fix `SyncThumbnailRect()` (replace magic number)

```csharp
private const int TitleBarHeightDefault = 28; // Only used as last resort
private const int TitleBarHeightMin = 20;     // Sanity check

/// <summary>Constrains DWM thumbnail to video area (below titlebar).</summary>
private void SyncThumbnailRect()
{
    if (_dwmManager == null || _thumbnailId <= 0 || _isClosing) return;

    double scale = RenderScaling;
    int w = Math.Max(1, (int)(Width * scale));
    int h = Math.Max(1, (int)(Height * scale));

    // Get actual titlebar height, with fallback and sanity clamp
    double titleH = TitleBar?.Bounds.Height ?? TitleBarHeightDefault;
    titleH = Math.Max(TitleBarHeightMin, titleH);
    int top = (int)(titleH * scale);

    _dwmManager.UpdateTarget(_thumbnailId, opacity: 255, visible: true,
        destLeft: 0, destTop: top,
        destRight: w, destBottom: h);
}
```

---

## Phase 5: Safety & Robustness (🟠#4, #6, #7, 🟡#9)

### 5a: Protect shared DwmThumbnailManager (🟠#4)

**Problem:** PipWindow holds a reference to the shared `_dwmManager` and could accidentally call `Dispose()`, breaking MainWindow's thumbnail.

**Fix:** Remove `_dwmManager = null` in `DisableDwmMirror()` — the manager is owned by MainWindow, not by PipWindow. PipWindow only calls `RegisterTarget` / `UnregisterTarget`.

```csharp
public void DisableDwmMirror()
{
    StopMirrorRetry();
    if (_dwmManager != null && _thumbnailId > 0)
        _dwmManager.UnregisterTarget(_thumbnailId);
    _thumbnailId = 0;
    // DO NOT set _dwmManager = null — it's owned by MainWindow
}
```

### 5b: Crash recovery for `_isActive` (🟠#6)

**Problem:** If the OS kills the PipWindow (OOM, process crash), `OnPipWindowClosed` never fires → `_isActive` stuck at `true`.

**Fix:** Add a **sentinel check** — if user initiates PiP and `_isActive == true` but `_pipWindow` is null or disposed, reset the state first.

```csharp
public PipWindow? EnterPip()
{
    // ...
    if (_isActive)
    {
        // Verify the window is still alive
        if (_pipWindow == null || _pipWindow.IsClosed)
        {
            Log.ForContext<PipService>().Warning("EnterPip: stale _isActive=true, resetting");
            _isActive = false;
            _pipWindow = null;
        }
        else
        {
            return _pipWindow;
        }
    }
    // ... proceed to create
}
```

> **Note:** `IsClosed` is not a built-in Avalonia property. You'd need a flag:
> ```csharp
> // In PipWindow:
> internal bool IsClosed { get; private set; }
> protected override void OnClosed(EventArgs e) { IsClosed = true; base.OnClosed(e); }
> ```

### 5c: Clean DWM on DetachedFromVisualTree (🟠#7)

**Problem:** `D3D11VideoHost.DetachedFromVisualTree` only hides video but doesn't unregister the DWM thumbnail.

**Fix:**

```csharp
// In D3D11VideoHost:
this.DetachedFromVisualTree += (_, _) =>
{
    VideoHostLog("DetachedFromVisualTree — cleanup");
    IsVideoSurfaceVisible = false;
    // Unregister main window thumbnail to prevent leak
    if (_dwmManager != null && _mainThumbnailId > 0)
    {
        _dwmManager.UnregisterTarget(_mainThumbnailId);
        _mainThumbnailId = 0;
    }
};
```

### 5d: Simplify `TryEnableMirrorNow()` return semantics (🟡#9)

**Problem:** Returns `true` for both "already registered" and "just registered" — same value, different meaning.

**Fix:** Rename to `bool TryRegisterMirror()` and document the semantics clearly.

```csharp
/// <summary>
/// Attempts DWM thumbnail registration if all conditions are met.
/// Returns true if registration is complete (pre-existing or just succeeded).
/// </summary>
private bool TryRegisterMirror()
{
    if (_thumbnailId > 0) return true;       // Already registered
    if (_dwmManager == null || _isClosing) return false;

    var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    if (handle == IntPtr.Zero || _dwmManager.SourceHwnd == IntPtr.Zero)
        return false;

    DoEnableMirror(handle);
    return _thumbnailId > 0;
}
```

---

## Phase 6: Code Hygiene (🟡#10, #12, 🔵#13-15)

### 6a: Fix namespace (🟡#10)

Move `PipService` from `Cine.Avalonia.ViewModels` to `Cine.Avalonia.Services`.

- Move file: `src/App/Application/Services/PipService.cs` (already in correct folder, just fix namespace)
- Update `using` statements referencing it

```csharp
// Old
namespace Cine.Avalonia.ViewModels;

// New
namespace Cine.Avalonia.Services;
```

### 6b: Protect against recursive layout in aspect ratio (🟡#12)

Guard the aspect ratio constraint with a **re-entrancy flag**.

```csharp
private bool _isApplyingAspectRatio;

private void ApplyAspectRatioConstraint()
{
    if (_isApplyingAspectRatio) return; // Prevent recursion
    if (_aspectRatio <= 0 || Width <= 0 || Height <= 0) return;

    var currentRatio = Width / Height;
    const double tolerance = 0.01;

    if (Math.Abs(currentRatio - _aspectRatio) > tolerance)
    {
        _isApplyingAspectRatio = true;
        Width = Height * _aspectRatio;
        _isApplyingAspectRatio = false;
    }
}
```

### 6c: Buffer logging writes (🔵#13)

Replace per-call `File.AppendAllText` with a `StringBuilder` dump at the end.

```csharp
public PipWindow? EnterPip()
{
    var log = new StringBuilder();
    log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EnterPip start");
    // ... append to log instead of writing files
    // At the end (or on failure):
    try { File.AppendAllText(PipLogPath, log.ToString()); } catch { }
}
```

### 6d: Set `WindowStartupLocation = Manual` (🔵#15)

In `PipWindow` constructor or XAML:

```csharp
// PipWindow.axaml.cs constructor
WindowStartupLocation = WindowStartupLocation.Manual;
```

### 6e: Fix exception swallowing (🔵#14)

Replace `catch { }` with at least a **conditional Debug.WriteLine** or trace.

```csharp
try { File.AppendAllText(path, content); }
catch (Exception ex) when (Debugger.IsAttached)
{
    Debug.WriteLine($"PiP log write failed: {ex.Message}");
    // Release builds: silently drop log lines (acceptable)
}
```

---

## Phase Ordering

```
Phase 1 ── Fix OnOpened/Opened race ─── Prevent double-register bug
   │
Phase 2 ── Event-driven wait ────────── Replace polling (performance)
   │
Phase 3 ── User feedback ────────────── Loading overlay (UX)
   │
Phase 4 ── Resize handling ──────────── Sync video to window (visual)
   │
Phase 5 ── Safety & robustness ──────── Crash recovery, leak fix (stability)
   │
Phase 6 ── Code hygiene ─────────────── Namespace, logging, etc (cleanup)
```

Phases 1-3 fix the 3 🔴 critical issues.
Phase 4 fixes major visual bugs.
Phase 5 fixes robustness.
Phase 6 is cleanup.

---

## Files to Modify

| File | Phases | Lines Affected |
|------|--------|----------------|
| `src/App/UI/Screens/Dialogs/PipWindow.axaml.cs` | 1, 2, 3, 4, 5a, 5d, 6b, 6d | ~100 lines changed |
| `src/App/UI/Screens/Dialogs/PipWindow.axaml` | 3 | ~10 lines added |
| `src/App/Application/Services/PipService.cs` | 5b, 6a, 6c, 6e | ~30 lines changed |
| `src/App/UI/Controls/Video/D3D11VideoHost.cs` | 5c | ~8 lines added |

**Total: ~150 lines changed, zero new files.**
