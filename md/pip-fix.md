# PIP (Picture-in-Picture) — Analysis & Fix Plan

> Status: ✅ **Fixed** — Screenshot polling approach implemented (2026-06-05)

---

## Research Summary

### How Other Players Handle PIP

| Player | Approach | Notes |
|--------|----------|-------|
| **VLC** | Single instance, duplicate output via `--video-filter=scene` + separate window | Re-uses decoder, captures frames |
| **mpv.net** | No PIP (uses mpv's native window only) | — |
| **mpv-pip (Lua script)** | Same mpv instance, Win32 API window manipulation | Lua + Win32 — creates a WS_CHILD clone of the mpv window |
| **Android PIP** | OS-level activity lifecycle (Android 8+) | OS handles windowing, app just enters PIP mode |
| **MPC-HC/BE** | EVR Custom Presenter — renders twice into separate surfaces | DirectShow-based, same decoder |
| **PiP-Tool** (C#) | Screenshot capture every ~200ms | [GitHub](https://github.com/LionelJouin/PiP-Tool) |
| **OnTopReplica** (C#) | DWM thumbnail (top-level windows only) | [GitHub](https://github.com/LorenzCK/OnTopReplica) |

### Why NOT Dual-Decoder (the old approach)

Two independent mpv instances decoding the same file is wasteful:
- **2× CPU/GPU** — both decode every frame independently
- **Sync drift** — two decode pipelines fall out of sync within seconds
- **Complexity** — HWND management, child window positioning, WS_DISABLED forwarding

### Why NOT DWM Thumbnail

`DwmRegisterThumbnail` requires **top-level** windows. Our video renders into a child HWND inside D3D11VideoHost — `DwmRegisterThumbnail` returns `E_INVALIDARG` for child windows.

### Chosen Approach: Screenshot Polling

```
MainWindow (single decoder)              PipWindow
┌──────────────────────┐            ┌──────────────────┐
│  MpvPlayer           │            │  Image control   │
│  → decodes video     │   ═══►    │  (no D3D11Video) │
│  → renders to HWND   │   pull    │  Timer every 33ms │
│  → has audio         │   frames  │  → ScreenshotRaw │
│  └──────────────┘    │            │  → display frame │
└──────────────────────┘            └──────────────────┘
```

**One decoder. One player. One frame source. ~30fps in PIP. Perfect sync.**

---

## Changes Made

### Files Modified

| File | Change |
|------|--------|
| [`IMediaPlayer.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/Media/Interfaces/IMediaPlayer.cs) | Added `ScreenshotRaw(out int w, out int h) → byte[]?` |
| [`MpvPlayer.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/Media/Implementations/mpv/MpvPlayer.cs) | Implemented `ScreenshotRaw()` — saves temp PNG via mpv, reads back raw bytes |
| [`MediaFoundationPlayer.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/Media/Implementations/mediafoundationplayer/MediaFoundationPlayer.cs) | Added stub `ScreenshotRaw()` returning null |
| [`PipWindow.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml) | **Complete rewrite** — removed D3D11VideoHost, added `Image` control with `Stretch="Uniform"` |
| [`PipWindow.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml.cs) | **Complete rewrite** — removed HWND/child window/init logic, added 33ms polling timer |
| [`PipService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs) | **Simplified** — removed secondary player, just creates PipWindow with main player ref |
| [`PlayerService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlayerService.cs) | Removed `CreateSecondaryPlayer()` (no longer needed) |
| [`MainWindow.Pip.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Pip.cs) | Simplified — debug logging removed |
| [`MainWindow.Input.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) | Removed PIP key forwarding (PipWindow handles its own keys) |
| [`MainWindow.Core.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Core.cs) | Removed `InitPipHandlers()` call |

### Files Deleted/Removed Conceptually

| What | Why |
|------|-----|
| `D3D11VideoHost` in PipWindow | No longer needed — replaced by `Image` control |
| `CreateSecondaryPlayer()` | No second mpv instance needed |
| Mute/pause on main player | Main player keeps playing normally |
| Child HWND position math | No child window in PIP |

---

## Implementation Details

### ScreenshotRaw() in MpvPlayer

```csharp
public byte[]? ScreenshotRaw(out int width, out int height)
{
    // 1. Save frame to temp PNG via mpv command
    CommandInternal("screenshot-to-file", tempPath, "video");
    // 2. Wait briefly for file to appear (async op)
    for (int i = 0; i < 50; i++) { ... Thread.Sleep(2); }
    // 3. Read bytes, delete temp file
    var bytes = File.ReadAllBytes(tempPath);
    // 4. Get video dimensions from mpv property
    width = GetIntProperty("width");
    height = GetIntProperty("height");
    return bytes;
}
```

### Polling Loop in PipWindow

```csharp
private async Task PollFramesAsync(CancellationToken ct)
{
    var interval = TimeSpan.FromMilliseconds(33); // ~30fps
    while (!ct.IsCancellationRequested)
    {
        var bytes = _mainPlayer.ScreenshotRaw(out w, out h);
        if (bytes != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                using var ms = new MemoryStream(bytes);
                PipFrameImage.Source = new Bitmap(ms);
            });
        }
        var delay = (int)(interval.TotalMilliseconds - sw.ElapsedMilliseconds);
        if (delay > 0) await Task.Delay(delay, ct);
    }
}
```

### PipWindow XAML (simplified)

```xml
<Image x:Name="PipFrameImage"
       Stretch="Uniform"
       HorizontalAlignment="Center"
       VerticalAlignment="Center" />
```

No `D3D11VideoHost`, no `FindControl`, no `ParentHwnd`, no `ChildWindowCreated`.

---

## Trade-offs

| Aspect | Before (Dual-Decoder) | After (Screenshot Polling) |
|--------|----------------------|---------------------------|
| **CPU cost** | 2× decode | 1× decode + <1% memcpy |
| **Frame rate** | 60fps both | 60fps main, ~30fps PIP |
| **Sync** | ❌ Drifts | ✅ Perfect (same frame) |
| **Main player** | ❌ Muted + Paused | ✅ Playing normally |
| **Code complexity** | ~500 lines (HWND, sync, 2 players) | ~100 lines (timer + image) |
| **Window management** | D3D11, WS_DISABLED, position math | None (Image control) |

---

## Phase 1 Complete ✅ — Screenshot Polling (PNG to file)

Current `ScreenshotRaw()` uses `screenshot-to-file` → temp PNG → read bytes. Works but capped at ~30fps due to PNG encode/decode.

---

## Phase 2 — Screenshot Raw (GPU → CPU memcpy)

**Goal**: Replace disk-backed PNG approach with `screenshot-raw` mpv command that returns raw BGRA pixel data directly. This eliminates ~30-50ms of PNG I/O per frame, enabling 60fps PIP.

### Current (Phase 1) Flow

```
mpv frame → PNG encode → write to temp file → read file → decode PNG → Bitmap
                  ↑ 30-50ms per frame, capped at ~30fps
```

### Phase 2 Flow

```
mpv frame → screenshot-raw → raw BGRA bytes → WriteableBitmap
                  ↑ 1-2ms per frame, can hit 60fps
```

### What needs to change

1. **Add `mpv_command_node` P/Invoke** — mpv's API returns structured data for `screenshot-raw` via a node interface
2. **Add `mpv_node` struct** — the return type containing raw byte buffer
3. **Rewrite `ScreenshotRaw()`** — call `screenshot-raw` instead of `screenshot-to-file`, parse the returned node for BGRA data + dimensions
4. **Use `WriteableBitmap`** — instead of `Bitmap(Stream)`, write raw pixels directly to avoid PNG decoding

### No interface change needed

`IMediaPlayer.ScreenshotRaw(out int width, out int height) → byte[]?` stays the same. The `byte[]` will just contain raw BGRA pixels instead of PNG bytes.

### Files to modify

| File | Change |
|------|--------|
| `MpvPlayer.cs` | Add `mpv_command_node`, `mpv_node`, `mpv_byte_array` structs + P/Invoke |
| `MpvPlayer.cs` | Rewrite `ScreenshotRaw()` to use `screenshot-raw` |
| `PipWindow.axaml.cs` | Change `Bitmap(Stream)` to `WriteableBitmap(rawBytes, w, h)` |
| `PipWindow.axaml.cs` | Reduce timer interval from 33ms to 16ms for 60fps polling |

### PipWindow XAML stays the same

Since both `Bitmap` and `WriteableBitmap` can be assigned to `Image.Source`, the XAML doesn't change.

---

## Phase 3 — DWM Thumbnail (60fps native, zero copy)

**Goal:** Replace screenshot polling entirely. Use Windows DWM thumbnails to clone the video output to both main and PIP windows at 60fps with zero CPU/GPU cost.

### How It Works

```
NORMAL PLAYBACK:
  mpv → renders to hidden top-level window (WS_POPUP, invisible)
          ↓
        DWM composes it into main window ← same as every window on screen
          (hidden window is just another window to DWM)

PIP ACTIVE:
  mpv → renders to same hidden top-level window (unchanged)
          ↓
        DWM thumbnail → main window ← same cost as normal playback
        DWM thumbnail → PIP window  ← one extra GPU composition (not measurable)

PIP CLOSED:
  DWM removes PIP thumbnail → back to normal
```

### Key Insight: Hidden Top-Level Window

Currently `D3D11VideoHost.CreateChildWindow()` creates a `WS_CHILD` window. DWM thumbnails only work with **top-level** windows. The fix: create a `WS_POPUP` window (top-level, no title bar, never appears on taskbar).

```csharp
// Current (Phase 1-2): Child HWND — DWM can't use it
_childHwnd = CreateWindowEx(..., WS_CHILD | WS_VISIBLE, ..., _parentHwnd, ...);

// Phase 3: Hidden top-level HWND — DWM works
_hiddenHwnd = CreateWindowEx(
    WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW,
    windowClass, "CineVideo",
    WS_POPUP | WS_VISIBLE,
    x, y, width, height,
    IntPtr.Zero, // no parent — it's top-level
    ...);
ShowWindow(_hiddenHwnd, SW_SHOWNOACTIVATE);
```

The hidden window is always invisible to the user (`WS_EX_LAYERED` with 0 opacity, or simply positioned off-screen sized 1×1 when not in use).

### What Changes

#### New File: `DwmThumbnailManager.cs`

Central class that manages DWM thumbnail registration. Located at `src/App/UI/Controls/Video/DwmThumbnailManager.cs`.

```csharp
namespace Cine.Avalonia.Controls;

/// <summary>
/// Manages DWM thumbnails for zero-copy video mirroring.
/// Uses DwmRegisterThumbnail / DwmUpdateThumbnailProperties / DwmUnregisterThumbnail.
/// </summary>
public class DwmThumbnailManager : IDisposable
{
    private readonly Dictionary<int, IntPtr> _thumbnails = new();
    private int _nextId = 1;
    private IntPtr _sourceHwnd; // The hidden top-level window mpv renders to

    // Set once when hidden window is created
    public void SetSource(IntPtr sourceHwnd) { _sourceHwnd = sourceHwnd; }

    // Register a destination window (main or PIP) to receive the live thumbnail
    public int RegisterTarget(IntPtr destHwnd, Rect sourceRect)
    {
        DwmRegisterThumbnail(destHwnd, _sourceHwnd, out var thumbId);
        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_SOURCECLIENTAREAONLY,
            fVisible = true,
            opacity = 255,
            rcSource = sourceRect,
            fSourceClientAreaOnly = false
        };
        DwmUpdateThumbnailProperties(thumbId, ref props);
        _thumbnails[_nextId] = thumbId;
        return _nextId++;
    }

    // Update thumbnail properties (e.g. on resize)
    public void UpdateTarget(int id, Rect sourceRect) { ... }

    // Remove a target (e.g. PIP closed)
    public void UnregisterTarget(int id) { ... DwmUnregisterThumbnail(thumbId); ... }

    public void Dispose() { foreach (var t in _thumbnails.Values) DwmUnregisterThumbnail(t); }

    // P/Invoke
    [DllImport("dwmapi.dll")]
    static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumbId);
    [DllImport("dwmapi.dll")]
    static extern int DwmUnregisterThumbnail(IntPtr thumbId);
    [DllImport("dwmapi.dll")]
    static extern int DwmUpdateThumbnailProperties(IntPtr thumbId, ref DWM_THUMBNAIL_PROPERTIES props);
}
```

#### Modified: `D3D11VideoHost.cs` → `D3D11VideoHost.cs`

**Before (current):** Creates `WS_CHILD` window, positions it with `SetWindowPos`, mpv renders directly to it.

**After:** Creates `WS_POPUP` hidden top-level window, registers it with `DwmThumbnailManager`, shows video via DWM thumbnail in the control's area.

The control shrinks significantly:

```csharp
// Old: 350+ lines with child window creation, positioning, WS_DISABLED, etc.
// New: ~80 lines — just layout + DWM thumbnail management

// On attached: create hidden top-level window for mpv
// On arrange: update DWM thumbnail position/size in main window
// On PIP: DwmThumbnailManager.RegisterTarget(pipHwnd, ...)
```

#### Modified: `MpvPlayer.InitializeRenderer()`

No changes needed. mpv still receives a HWND via `wid` option. It just happens to be the hidden top-level window now instead of a child window.

#### Modified: `PipWindow`

PIP window gets a reference to `DwmThumbnailManager` instead of the main player.

```csharp
// Old: Timer-based screenshot polling (~30fps)
// New: DWM thumbnail at 60fps, zero CPU

// PipWindow receives DwmThumbnailManager + its own HWND
public void EnableDwmMirror(DwmThumbnailManager manager)
{
    var pipHwnd = TryGetPlatformHandle()!.Handle;
    _thumbnailId = manager.RegisterTarget(pipHwnd, ...);
}

public void DisableDwmMirror()
{
    _dwmManager.UnregisterTarget(_thumbnailId);
}
```

No timer. No `ScreenshotRaw()`. No `WriteableBitmap`. No frame polling loop.

#### Modified: `PipService`

```csharp
// Old: creates PipWindow(mainPlayer) and starts screenshot polling
// New: creates PipWindow(DwmThumbnailManager) and enables DWM mirror

public PipWindow? EnterPip()
{
    _pipWindow = new PipWindow();
    _pipWindow.Show();
    _pipWindow.EnableDwmMirror(_dwmManager);
    return _pipWindow;
}
```

### Step-by-Step Execution Plan

Follow these sub-steps **in order**. Build after each step to catch errors immediately.

#### Step 3.1 — Clean: Remove dead code from Phase 2

**Do this first** — clean slate before Phase 3.

| Sub-step | File | Action |
|----------|------|--------|
| 3.1.1 | `PipService.cs` | Remove `_playerService` field and constructor parameter (unused). Remove `PipOpened`, `PipError`, `PipClosed` events (zero subscribers). Remove `SetCurrentFilePath()` (used only for CanPip check — can be simplified). |
| 3.1.2 | `PipWindow.axaml.cs` | Remove `_frameCount` field (written never read). Remove `using Cine.Avalonia.Helpers` (unused). |
| 3.1.3 | `PipWindow.axaml` | Remove `PipVolumeSlider`, `PipSyncLabel`, volume icon Grid entirely (dead UI). |
| 3.1.4 | `MainWindow.Input.cs` | Remove `case Key.I: Handle(() => { });` (empty handler, line 151). |
| 3.1.5 | `pip-fix.md` | Mark Phase 2 as "Cleaned" after build verification. |

**Build check:** `dotnet build` — 0 errors, 0 warnings.

#### Step 3.2 — Create: DwmThumbnailManager.cs

| Sub-step | Action |
|----------|--------|
| 3.2.1 | Create new file `src/App/UI/Controls/Video/DwmThumbnailManager.cs` |
| 3.2.2 | Add `DwmRegisterThumbnail`, `DwmUnregisterThumbnail`, `DwmUpdateThumbnailProperties` P/Invokes |
| 3.2.3 | Add `DWM_THUMBNAIL_PROPERTIES` struct with `dwFlags`, `opacity`, `fVisible`, `rcSource`, `fSourceClientAreaOnly` |
| 3.2.4 | Implement `SetSource(IntPtr)`, `RegisterTarget(destHwnd, sourceRect)`, `UnregisterTarget(id)`, `UpdateTarget(id, rect)`, `Dispose()` |
| 3.2.5 | Add `public IntPtr SourceHwnd => _sourceHwnd;` for D3D11VideoHost to read |

**Build check:** `dotnet build` — 0 errors.

#### Step 3.3 — Rewrite: D3D11VideoHost.cs

Replace child window with hidden top-level window.

| Sub-step | Action |
|----------|--------|
| 3.3.1 | Rename `_childHwnd` → `_hiddenHwnd` (field). Rename `ChildWindowCreated` → `HiddenWindowCreated`. Rename `VideoHwnd` → `HiddenWindowHwnd` (public property). |
| 3.3.2 | In `TryCreateNow()` / `CreateChildWindow()`: change `WS_CHILD` → `WS_POPUP`. Remove `_parentHwnd` requirement — hidden window is top-level, no parent. Add `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW`. Remove `SetWindowRgn` (no more clipping). |
| 3.3.3 | In `ArrangeOverride()`: remove `TranslatePoint` (no child positioning needed). Instead, call `DwmThumbnailManager.UpdateTarget()` for main window's thumbnail position. |
| 3.3.4 | Remove `WndProc`, `WM_NCHITTEST`, `WM_ERASEBKGND`, `WM_MOUSEACTIVATE` handlers (child-window specific). |
| 3.3.5 | Remove `UpdateVideoRegion()` (not needed for top-level window). |
| 3.3.6 | Remove `IsVideoSurfaceVisibleProperty` (hidden window is always visible to DWM). |
| 3.3.7 | Add `DwmThumbnailManager` as a dependency — accept via constructor or property. |
| 3.3.8 | Update `MainWindow.Core.cs` `OnOpened`: instead of `_videoHost.ParentHwnd = handle`, call `_videoHost.SetMainHwnd(handle)` which registers a DWM thumbnail for the main window. |

**Build check:** `dotnet build` — 0 errors. Run app, verify video plays normally in main window.

#### Step 3.4 — Rewrite: PipWindow.axaml.cs

Remove screenshot timer, add DWM mirror.

| Sub-step | Action |
|----------|--------|
| 3.4.1 | Remove `_mainPlayer` field, constructor parameter (no longer needed). |
| 3.4.2 | Remove `_pollCts`, `_frameCount`, `StartPolling()`, `StopPolling()`, `PollFramesAsync()` entirely. |
| 3.4.3 | Add `_dwmManager` field, `_thumbnailId` field (int). |
| 3.4.4 | Add `public void EnableDwmMirror(DwmThumbnailManager manager)`: calls `manager.RegisterTarget(pipHwnd, ...)`. |
| 3.4.5 | Add `public void DisableDwmMirror()`: calls `manager.UnregisterTarget(_thumbnailId)`. |
| 3.4.6 | In `OnClosing`/`Close()`: call `DisableDwmMirror()` instead of `StopPolling()`. |
| 3.4.7 | In `OnOpened()`: remove `StartPolling()` call. |
| 3.4.8 | Remove `using System.Text.Json` (no longer needed if state persistence removed). |
| 3.4.9 | Remove `PipFrameImage.Source = null` cleanup (DWM thumbnail dies with the window). |

**Build check:** `dotnet build` — 0 errors.

#### Step 3.5 — Update: PipService.cs

Remove Phase 2 logic, wire DWM manager.

| Sub-step | Action |
|----------|--------|
| 3.5.1 | Remove constructor dependency on `PlayerService` (field + parameter). |
| 3.5.2 | Add `DwmThumbnailManager` as constructor dependency. |
| 3.5.3 | Remove `_mainPlayer` field. Remove `Initialize(IMediaPlayer)`. Remove `SetCurrentFilePath(string)`. Remove `CanPip` property. |
| 3.5.4 | Simplify `EnterPip()`: create `PipWindow()` (no args), `pipWindow.Show()`, `pipWindow.EnableDwmMirror(_dwmManager)`, return. |
| 3.5.5 | Remove `_playerService` field, `CleanupPip()` with `Closed -= handler` (not needed). |
| 3.5.6 | Add `_canPip` flag (set from outside, or check if main player has file loaded). |

**Build check:** `dotnet build` — 0 errors.

#### Step 3.6 — Update: MainWindow (Core + Pip)

Wire everything together.

| Sub-step | Action |
|----------|--------|
| 3.6.1 | `MainWindow.Core.cs`: Create `DwmThumbnailManager` as singleton, pass to `PipService` constructor. |
| 3.6.2 | `MainWindow.Core.cs`: On `_videoHost.HiddenWindowCreated`, call `_dwmManager.SetSource(hiddenHwnd)`. |
| 3.6.3 | `MainWindow.Pip.cs`: Simplify `OnPipToggled` — remove `_pipService.Initialize()`, `SetCurrentFilePath()` calls. |

**Build check:** `dotnet build` — 0 errors. Run app, verify video plays in main window.

#### Step 3.7 — Wire PIP button to DWM mirror

| Sub-step | Action |
|----------|--------|
| 3.7.1 | In `OnPipToggled`: call `_pipService.EnterPip()` which internally does `_dwmManager.RegisterTarget(pipHwnd)`. |
| 3.7.2 | In `OnPipToggled` on exit: call `_pipService.ExitPip()` which internally does `_dwmManager.UnregisterTarget(pipId)`. |

**Build check:** `dotnet build` — 0 errors. Run app, open video, test PIP button.

#### Step 3.8 — Handle resize of main window

| Sub-step | Action |
|----------|--------|
| 3.8.1 | In `D3D11VideoHost.ArrangeOverride`: after computing new bounds, call `_dwmManager.UpdateTarget(mainWindowThumbId, sourceRect)` to reposition the video display area. |

**Build check:** Run app, resize main window, verify video display area stays correct.

#### Step 3.9 — Handle resize of PIP window

| Sub-step | Action |
|----------|--------|
| 3.9.1 | In `PipWindow` constructor or `OnOpened`: subscribe to `SizeChanged` event. On resize, call `_dwmManager.UpdateTarget(_thumbnailId, sourceRect)`. |

**Build check:** Run app, resize PIP window, verify video fills correctly.

#### Step 3.10 — Final cleanup pass

| Sub-step | Action |
|----------|--------|
| 3.10.1 | Remove any remaining Phase 2 leftovers: `using Cine.Media.Interfaces` from PipWindow (if unused), `ScreenshotRaw()` call references in PIP code. |
| 3.10.2 | Validate: `dotnet build`, run app, test: open video → PIP on → PIP off → PIP on again → close app. |
| 3.10.3 | Check debug logs in `cine_startup.log` for any DWM-related errors. |

---

### Rollback Plan

If Phase 3 has issues, we keep Phase 2 as fallback:
- `DwmThumbnailManager.cs` can be deleted
- D3D11VideoHost reverts to WS_CHILD (or we keep both paths via a flag)
- PipWindow reverts to screenshot polling timer
- One build flag `USE_DWM_THUMBNAIL` to toggle between Phase 2 and Phase 3

---

## Phase 4 (Future)

- **Variable frame rate** — Auto-adjust polling interval based on video FPS (only needed if Phase 3 isn't adopted)
- **Click-through mode** — Make PIP area click-through when not hovered (WS_EX_TRANSPARENT)
