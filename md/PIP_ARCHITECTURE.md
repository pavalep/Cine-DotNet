# PIP (Picture-in-Picture) Architecture & Implementation Plan

> Status: **Gold Standard — all features implemented**  
> Backend: mpv/libmpv via `MpvPlayer` wrapping libmpv-2.dll  
> UI Framework: Avalonia (cross-platform WPF-like, not WPF)  
> Rendering: D3D11 via native Win32 child HWND embedded in `D3D11VideoHost`

---

## 1. How PIP Is Done Architecturally (Industry Best Practices)

### 1.1 The Core Problem

PIP requires showing the **same video content** in a **second window** simultaneously. There are three fundamental architectural approaches:

| Approach | Description | Pros | Cons |
|---|---|---|---|
| **A. Separate Decoder Instance** | Second independent player instance, same file, synced via position polling/IPC | Simple, decoupled | Double decode cost, sync jitter, risk of audio desync |
| **B. Frame Buffer Capture** | Grab rendered frames from main player, copy to PIP window | Single decode, perfect sync | High GPU→CPU→GPU copy cost, latency |
| **C. Shared GPU Texture** | Both windows read from same D3D11 texture / shared surface | Single decode, zero copy, perfect sync | Complex GPU resource sharing, API-dependent |

### 1.2 What Major Players Use

| Player | Approach | Details |
|---|---|---|
| **VLC (desktop)** | **Approach A** | Separate `libvlc_media_player` instances; sync via position set. Widest compatibility. |
| **mpv (standalone)** | Single-context | mpv doesn't natively support multi-window output from one context. `--lavfi-complex` can composite two videos side-by-side in one window, but that's not PIP. |
| **VLC (Android)** | OS-native | Uses Android's `PictureInPictureParams` API — the OS handles the overlayed surface. |
| **Chrome/Edge PIP** | **Approach C** | Uses the browser's compositor to share video frames to a system PIP window via `DocumentPictureInPicture` API. |
| **PotPlayer / MPC-HC** | **Approach A** | Separate filter graph instances synced via IPC. |
| **david-j-lee/picture-in-picture (WPF)** | **Approach A** | Two `MediaElement` controls in separate windows, `ProcessesService` manages capture. |

### 1.3 Thread Association to Views

In WPF/Avalonia desktop apps, each `Window` has thread affinity:

- **UI Thread**: Each `Window` belongs to the thread that created it. In WPF, all windows share the **same** UI thread by default (STA model). Creating a window on a separate thread requires `Thread.SetApartmentState(ApartmentState.STA)` and a dedicated `Dispatcher`.
- **Rendering Thread**: WPF/Avalonia use a separate **composition/render thread** that talks to DirectX. This thread is shared across all windows in the same process.
- **mpv/libmpv thread model**: mpv runs its own internal threads for demuxing, decoding, and rendering. The `InitializeRenderer(hwnd)` call sets up the GPU output target. Once set, mpv handles frame delivery to that HWND internally.

**Key rule**: Both the main window and PIP window run on the **same UI thread**. The second `MpvPlayer` instance runs its event loop on a separate `Task` thread (via `Task.Run`), but all UI updates (seek bar, time labels) must be marshalled via `Dispatcher.UIThread.Post`.

### 1.4 How Many Threads Are Needed

For the **dual-instance approach** (Approach A) that Cine uses:

| Thread | Purpose |
|---|---|
| **1 UI Thread** (STA) | MainWindow + PipWindow, all Avalonia controls, event handlers |
| **1 Render Thread** | Avalonia compositor → D3D11 swap chain (shared) |
| **2 mpv Event Loop Threads** | One per `MpvPlayer` instance (for property observation, event dispatch) |
| **N mpv Internal Threads** | mpv's own demuxer/decoder/vo threads (opaque to us) |

**Total: 1 UI + 1 render + 2 event loop + N mpv internal = ~8-12 threads** for a running PIP session.

This is **normal and acceptable**. Both VLC and mpv spawn many internal threads; the overhead is in the single-digit MB of RAM and negligible CPU when idle.

### 1.5 Real-World PIP Implementations (Source Code Analysis)

Below are actual source code implementations from major players, fetched directly from their repositories.

---

#### 1.5.1 VLC Media Player — Qt/QML PIP (`PIPPlayer.qml`)

**Source**: [`videolan/vlc` — `modules/gui/qt/player/qml/PIPPlayer.qml`](https://github.com/videolan/vlc/blob/master/modules/gui/qt/player/qml/PIPPlayer.qml)

VLC's PIP uses a **single `libvlc_media_player` instance** — no second decoder. The PIP window is a QML `T.Control` that embeds a `VideoSurface` connected to the **same** video output provider (`MainCtx.videoSurfaceProvider`). This is **Approach C (Shared GPU Texture)** — they reuse the same decode pipeline.

```qml
// Key architectural points from PIPPlayer.qml:
T.Control {
    id: root
    width: Math.round(VLCStyle.dp(320, VLCStyle.scale))     // 320dp default
    height: Math.round(VLCStyle.dp(180, VLCStyle.scale))    // 180dp default
    objectName: "pip window"

    Drag.active: dragHandler.active     // Drag-to-move

    // Tap: toggle play/pause. Double-tap: return to main window
    TapHandler {
        onDoubleTapped: MainCtx.playerView = true            // Exit PIP
        onTapped: MainPlaylistController.togglePlayPause()    // Play/Pause
    }

    // Drop shadow on the video surface
    background: VideoSurface {
        videoSurfaceProvider: MainCtx.videoSurfaceProvider    // ← SAME provider as main window
    }

    // Controls: Play/Pause, Close, Fullscreen buttons
    // Auto-hide: controls only show on hover
    contentItem: Item {
        visible: hoverHandler.hovered || playButton.hovered || closeButton.hovered
        // Play/Pause icon (center), Close (top-right), Fullscreen (top-left)
    }
}
```

**VLC Architecture Summary:**
- Single decode pipeline, single `VideoSurfaceProvider`
- PIP window is a separate QML `Window` (or `T.Control` in overlay mode)
- `Compositor` factory pattern picks platform backend (DirectComposition on Windows, X11/Wayland on Linux)
- No thread-per-window — all on Qt's main event loop
- Controls auto-hide, drag-to-move, double-click to exit

**Compositor source** (`compositor.cpp`):
```cpp
// Factory pattern — picks compositor backend per platform
static compositorList[] = {
    {"dcomp",    &instanciateCompositor<CompositorDirectComposition>}, // Win10+
    {"platform", &instanciateCompositor<CompositorPlatform>},           // Win/macOS native
    {"win7",     &instanciateCompositor<CompositorWin7>},               // Win7 fallback
};
```

---

#### 1.5.2 mpv-PiP Lua Script (`WatanabeChika/mpv-PiP`)

**Source**: [`WatanabeChika/mpv-PiP` — `mpv-PiP.lua`](https://github.com/WatanabeChika/mpv-PiP/blob/main/mpv-PiP.lua)

This is a **single-instance** approach — it doesn't create a second mpv context. Instead, it manipulates mpv's own window properties to make the **same window** behave as PIP:

```lua
-- Entry: save original state
local function save_original_options()
    original_options.fullscreen   = mp.get_property_bool("fullscreen")
    original_options.border       = mp.get_property_bool("border")
    original_options.ontop        = mp.get_property_bool("ontop")
    original_options.window_scale = mp.get_property_number("window-scale")
end

-- Enter PIP: borderless + always-on-top + scaled to 30%
local function enable_pip()
    save_original_options()
    mp.set_property_bool("fullscreen", false)    -- exit fullscreen
    mp.set_property_bool("border", false)        -- remove decorations
    mp.set_property_bool("ontop", true)          -- always on top
    mp.set_property_number("window-scale", 0.3)  -- scale to 30%
    pip_enabled = true
end

-- Exit PIP: restore all original properties
local function disable_pip()
    restore_original_options()
    pip_enabled = false
end

-- Persists across file changes
mp.register_event("file-loaded", function()
    if pip_enabled then mp.add_timeout(0.1, enable_pip) end
end)

-- Auto-exit PIP on fullscreen
mp.observe_property("fullscreen", "bool", function(name, value)
    if value and pip_enabled then disable_pip() end
end)

mp.add_key_binding("Alt+p", "toggle-pip", toggle_pip)
```

**Enhanced version** (`dyphire/mpv-scripts/pip.lua`) adds:
- Configurable `autofit` (default: `25%x25%`)
- Configurable `geometry` for position (default: `100%:100%` = bottom-right)
- Separate restore geometry and autofit values
- Geometry delay timer to avoid race conditions
- `keepaspect-window` handling

**Key difference from our approach:** mpv-PiP modifies the **same window** — no second mpv context. This is the simplest possible PIP but leaves no main window behind. Our approach (separate window + main window) is more correct for a media player app where the main window UI should remain accessible.

---

#### 1.5.3 david-j-lee/picture-in-picture (WPF/C#, MVVM + DI)

**Source**: [`david-j-lee/picture-in-picture`](https://github.com/david-j-lee/picture-in-picture)

This is the closest reference to our codebase (C# + XAML framework). Architecture:

```
App.xaml.cs
├── Services
│   └── ProcessesService       // Captures window content
├── ViewModels
│   ├── CropperViewModel       // Region selection
│   ├── MainViewModel          // Main window
│   └── PipModeViewModel       // PIP window state
├── Views
│   ├── CropperWindow          // Region selector
│   ├── MainWindow             // Main app
│   └── PipModeWindow          // PIP floating window
├── Helpers
│   └── WindowHelper           // Native Win32 interop
└── Native
    └── User32.cs              // SetWindowPos, etc.
```

Key patterns:
- **Dedicated `PipModeViewModel`** for the PIP window (not inline code-behind)
- **`ProcessesService`** captures frames from target window
- **DI constructor injection** throughout (`services.AddSingleton<ProcessesService>()`)
- **Single UI thread** — both windows share the same `Application.Current.Dispatcher`

**Thread model** (from `App.xaml.cs`):
```csharp
// All ViewModels are transient, Views singletons
services.AddSingleton<MainWindow>();
services.AddSingleton<PipModeWindow>();
services.AddTransient<PipModeViewModel>();
```

---

#### 1.5.4 mpv.net (C#/WinForms, libmpv wrapper)

**Source**: [`mpvnet-player/mpv.net`](https://github.com/mpvnet-player/mpv.net)

mpv.net is a Windows media player built in C# on top of libmpv, similar to our architecture. It does **not** have built-in PIP. However, its OSC (On-Screen Controller) implements a `pip` button in the [Zren/mpv-osc-tethys](https://github.com/Zren/mpv-osc-tethys) theme that invokes the same borderless+always-on-top approach as the Lua scripts.

---

### 1.6 Cross-Reference: Our Code vs Reference Implementations

| Feature | Our Code (Cine) | VLC PIPPlayer.qml | mpv-PiP.lua | david-j-lee WPF |
|---|---|---|---|---|
| **Approach** | Separate mpv instance | Shared VideoSurfaceProvider | Same window (props only) | Frame capture |
| **Window** | Avalonia `Window` | QML `T.Control` (overlay or window) | Same mpv window | WPF `Window` |
| **Thread model** | 1 UI thread + 2 mpv event loops | 1 Qt main thread | 1 mpv process | 1 WPF UI thread |
| **Decoder count** | 2 (dual decode) | 1 (shared) | 1 | 1 |
| **Sync method** | 100ms polling | None needed (shared) | None needed (same) | Frame capture |
| **Controls** | Always visible bar | Hover-only auto-hide | None (borderless) | Hover auto-hide |
| **Drag-to-move** | Missing | Yes (`DragHandler`) | Native | Yes (native) |
| **View/ViewModel** | Code-behind only | QML declarative | Lua script | PIP has own `PipModeViewModel` |
| **Service layer** | Inline in MainWindow | `Compositor` factory | N/A | `ProcessesService` |
| **Programmatic PIP** | Window widget embedding | QML VideoSurface | Window properties | Native capture API |

**Cine strengths vs references:**
- Has full transport controls (play/pause/prev/next) — most references are control-minimal
- Has sync timer — mpv-PiP doesn't need sync, VLC shares surface
- Close animation — unique among references

**Cine gaps vs references:**
- No drag-to-move (VLC has `DragHandler`, Windows has native)
- No auto-hide controls (VLC hides on non-hover)
- No dedicated ViewModel/Service (david-j-lee pattern)
- Dual decode doubles GPU memory (VLC shares one surface)
- No separate MVVM layer for PIP state

---

### 1.7 PIP UI Gold Standard — Consolidated from All References

After analyzing **VLC QML**, **Android PiP Guidelines**, **Apple HIG**, **YouTube PiP**, **Screenbox (UWP/C#)**, **S.T.I.T.C.H (Chrome Extension)**, and **david-j-lee (WPF)**, here is the definitive gold standard for PIP UI.

#### 1.7.1 Window Layout & Dimensions

| Aspect | Gold Standard | Source |
|---|---|---|
| **Default size** | 320×180 dp (16:9) or 400×225 dp | VLC, Android, Apple |
| **Min size** | 240×160 px | Common |
| **Max size** | 1920×1080 px | Common |
| **Aspect ratio** | Maintain video aspect ratio | Apple HIG, Android |
| **Position** | Bottom-right default, user-draggable | Android, YouTube, VLC |
| **Decoration** | No system chrome, borderless | VLC, david-j-lee, Apple |
| **Shadow/depth** | Drop shadow on PIP window | VLC (`DefaultShadow`), Apple |
| **Margin from screen edge** | 16-24 px (keeps from touching edge) | Android, macOS |

#### 1.7.2 Controls Layout (Priority Order)

```
┌────────────────────────────────────────┐
│ [Pin] [File Name]          ● [Close]   │  ← Header (auto-hide, drag handle)
├────────────────────────────────────────┤
│                                        │
│                                        │
│              VIDEO SURFACE             │  ← Tap toggles controls
│                                        │
│              ▶ (big, center)           │  ← Big play/pause overlay
│                                        │
├────────────────────────────────────────┤
│        ═══◉══════════    00:42/02:10   │  ← Seek bar + time
│   [⏮]  [▶⏸]  [⏭]     [🔊────]        │  ← Transport + volume
└────────────────────────────────────────┘
```

| Element | Behavior | Source |
|---|---|---|
| **Header** | Auto-hides after 2.5s, white 12px text, 32px height, semi-transparent black bg (`#CC000000`) | VLC, Android |
| **Close button** | Top-right corner, 32×32, always visible on header hover | VLC, YouTube, Android |
| **Pin toggle** | Pin/Unpin icon toggles `Topmost` property | Apple, macOS |
| **File name** | Shows current file name next to "PIP" label | Best practice |
| **Big center Play/Pause** | 48×48, semi-transparent circular, appears on hover + fades out | VLC, YouTube, Netflix |
| **Transport bar** | Bottom row: Previous | Play/Pause | Next — always visible | YouTube, VLC, Android |
| **Seek bar** | Clickable track with fill and thumb. 3px height track, 8px thumb. Updates live. | YouTube, VLC, Apple |
| **Time label** | `MM:SS / MM:SS` in monospace font, 10px | YouTube, VLC |
| **Volume control** | Volume slider or wheel-over-PIP. PIP muted by default, controls main volume. | Android, best practice |

#### 1.7.3 Interaction Patterns (Gold Standard)

| Interaction | Behavior | Source |
|---|---|---|
| **Drag to move** | Drag anywhere on header or video surface | VLC (`DragHandler`), Android, Apple, YouTube |
| **Double-tap video** | Return to full main window / Exit PIP | VLC (`onDoubleTapped: playerView = true`), Android |
| **Single tap video** | Toggle play/pause | VLC, YouTube |
| **Click seek bar** | Seek to position. PointerPressed, Moved, Released. | YouTube, VLC, all major players |
| **Mouse wheel over controls** | Volume adjustment | Screenbox, best practice |
| **Drag to bottom** | Dismiss PIP (swipe-down gesture) | Android, YouTube |
| **Edge snap** | Stash PIP to screen edge (left/right) — partially visible | Android (`stash` gesture) |
| **Resize** | Drag corners to resize. Maintains aspect ratio. | Android (`pinch-to-zoom`), VLC |
| **Auto-hide** | Controls + header hide after 2.5s of inactivity. Show on mouse move. | VLC (`HoverHandler`), YouTube |

#### 1.7.4 Keyboard Shortcuts (Gold Standard)

| Key | Action | Source |
|---|---|---|
| `Space` | Play/Pause | YouTube, VLC, Screenbox |
| `Escape` | Exit PIP | YouTube, VLC, S.T.I.T.C.H |
| `Left arrow` | Seek backward (5s) | YouTube, VLC |
| `Right arrow` | Seek forward (5s) | YouTube, VLC |
| `Up arrow` | Volume up | YouTube, Screenbox |
| `Down arrow` | Volume down | YouTube, Screenbox |
| `M` | Mute toggle | YouTube |
| `F` | Return to main window / fullscreen | YouTube |
| `Alt+P` | Toggle PIP mode | mpv-PiP Lua script |
| `1-4` | Resize presets (small→large) | Screenbox |
| `Alt+Arrow` | Move PIP window | S.T.I.T.C.H |

#### 1.7.5 State Save/Restore (Gold Standard)

| What to save | Why | Source |
|---|---|---|
| **PIP position** | `Position.X, Position.Y` — restore where user left it | dyphire/mpv-PiP, david-j-lee |
| **PIP size** | `Width, Height` — don't reset to default every time | dyphire/mpv-PiP |
| **Always-on-top** | Whether PIP was pinned | Best practice |
| **Volume** | PIP volume level | Best practice |
| **Active state** | Was PIP on when app closed? Auto-restore on launch. | mpv-PiP `file-loaded` re-apply |

#### 1.7.6 Animation (Gold Standard)

| Animation | Type | Duration | Easing | Source |
|---|---|---|---|---|
| **Enter PIP** | Scale-down from main video position to PIP corner | 200-300ms | cubic-bezier(0.4, 0, 0.2, 1) | Android, Apple |
| **Exit PIP** | Scale-up from PIP to main video surface, then close | 200ms | cubic-bezier(0.4, 0, 0.2, 1) | Android, Apple |
| **Controls show** | Fade in | 150ms | Linear | VLC, Apple |
| **Controls hide** | Fade out | 150ms | Linear | VLC, Apple |

#### 1.7.7 Feature Priority Matrix (Gold Standard)

```
Essential (ship now)         Recommended (next)         Polish (future)
─────────────────────      ────────────────────      ────────────────────
✓ Drag-to-move             ✓ Resize with corners     ✓ Edge snap/stash
✓ Auto-hide controls       ✓ Pin/unpin button        ✓ Swipe-down to dismiss
✓ Clickable seek bar (6px) ✓ File name in header     ✓ Resize presets (1-4)
✓ Play/Pause toggle        ✓ Keyboard shortcuts      ✓ Aspect ratio lock
✓ Close with animation     ✓ State persistence
✓ Always-on-top            ✓ Big center play button
✓ Position sync            ✓ Volume slider
✓ Back to main window      ✓ Smooth enter transition
```
### 1.7.8 Comparison: Our Current PIP vs Gold Standard

| Feature | Gold Standard | Status | Notes |
|---|---|---|---|
| Drag-to-move | ✅ Yes (header via `BeginMoveDrag`) | ✅ Done | Header + VLC pattern |
| Auto-hide controls | ✅ After 2.5s inactivity | ✅ Done | Fade in/out animation |
| Clickable seek bar | ✅ 6px thick, 12px thumb | ✅ Done | Pointer interaction, jumped from 3px → 6px |
| Play/Pause icon toggle | ✅ Based on `IsPlaying` | ✅ Done | Toggles correctly |
| Back to main window | ✅ Fullscreen button in header | ✅ Done | Closes PIP & restores main view |
| Keyboard shortcuts | ✅ Space, Esc, Arrows, Up/Down, M, F, A | ✅ Done | 10 shortcuts total |
| Close animation | ✅ Scale + fade with cubic ease-out | ✅ Done | Preserved |
| Always-on-top pin | ✅ Toggle button with icon | ✅ Done | Pin/PinOff toggle |
| File name in header | ✅ Shows `Path.GetFileName()` | ✅ Done | Truncated with ellipsis |
| Volume slider | ✅ Clickable track + mute button | ✅ Done | Controls main volume |
| Mouse wheel volume | ✅ Scroll wheel on PIP | ✅ Done | +5/-5 per step |
| Big center play button | ✅ 48×48, fades in/out on hover | ✅ Done | 0.9 max opacity |
| State persistence | ✅ Position, size, pin saved to JSON | ✅ Done | `pip_state.json` |
| Smooth enter animation | ✅ Scale-down from center, 12 steps @ 60fps | ✅ Done | Cubic ease-out |
| Double-tap to exit | ✅ Double-tap → exit | ✅ Done | 350ms threshold |
| Resize | ✅ `MinWidth/MaxWidth`, resizable | ✅ Done | 240×160 to 1920×1080 |
| Aspect ratio lock | ✅ Maintain during resize | ✅ Done | Toggle with `A` key + button in header |
| Edge snap/stash | ❌ Drag to edge | ⬜ Future | Low priority |
| Resize presets | ❌ 1-4 keys | ⬜ Future | Low priority |

> **Status: Gold Standard achieved** — all high-priority features implemented. Edge snap and resize presets remain as future polish.

---

## 2. Current Implementation Analysis

### 2.1 Files Involved

| File | Role |
|---|---|
| [`src/App/UI/Shell/MainWindow.Pip.cs`](../src/App/UI/Shell/MainWindow.Pip.cs) | Toggle logic, lifecycle management |
| [`src/App/UI/Screens/Dialogs/PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml) | PIP window XAML layout |
| [`src/App/UI/Screens/Dialogs/PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs) | PIP window code-behind |
| [`src/App/Application/Services/PlayerService.cs`](../src/App/Application/Services/PlayerService.cs) | `CreateSecondaryPlayer()` factory |
| [`src/Media/Implementations/mpv/MpvPlayer.cs`](../src/Media/Implementations/mpv/MpvPlayer.cs) | mpv wrapper (both instances) |
| [`src/App/UI/Controls/Video/D3D11VideoHost.cs`](../src/App/UI/Controls/Video/D3D11VideoHost.cs) | Native HWND host control |
| [`src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs) | PIP toggle button |
| [`src/Media/Interfaces/IMediaPlayer.cs`](../src/Media/Interfaces/IMediaPlayer.cs) | Player interface contract |

### 2.2 Architecture Diagram (Current)

```
┌─────────────────────────────────────────────────────────────┐
│                        UI Thread (STA)                      │
│                                                             │
│  ┌──────────────┐          ┌──────────────────────┐         │
│  │  MainWindow   │          │     PipWindow         │         │
│  │              │          │                       │         │
│  │ D3D11VideoHost│          │   D3D11VideoHost      │         │
│  │  (main HWND)  │          │    (pip HWND)         │         │
│  │              │          │                       │         │
│  │ HeaderBar     │◄──toggle──┤  Transport Controls   │         │
│  │ ControlsBox   │          │  Seek Bar + Thumb     │         │
│  └──────┬───────┘          │  Close Animation      │         │
│         │                  └───────────┬───────────┘         │
│         │                              │                     │
└─────────┼──────────────────────────────┼─────────────────────┘
          │                              │
          ▼                              ▼
┌─────────────────────┐    ┌─────────────────────┐
│   MpvPlayer (main)   │    │  MpvPlayer (pip)    │
│   - libmpv context A │    │  - libmpv context B │
│   - renders to HWND1 │    │  - renders to HWND2 │
│   - event loop thread│    │  - event loop thread│
│   - audio ON         │    │  - audio muted      │
└─────────────────────┘    └─────────────────────┘
          │                         │
          │     Position polling     │
          │◄────────────────────────►│
          │   (100ms DispatcherTimer) │
          │                         │
```

### 2.3 Detailed Walkthrough of Current Flow

#### Entering PIP (`MainWindow.Pip.cs:23-73`)
1. User clicks PIP button → `OnTogglePip()` called
2. Validates that media is loaded (`_viewModel.FilePath`)
3. Calls `_playerService.CreateSecondaryPlayer()` → creates new `MpvPlayer()` (uninitialized)
4. Creates `PipWindow(_pipPlayer, _mainPlayer, _filePath)`
5. Pauses main player (`_playerService.Player?.Pause()`)
6. Hides main video surface (`_videoHost.IsVideoSurfaceVisible = false`)
7. Registers `NotifyPipSync` callback
8. Shows PIP window with `_pipWindow.Show(this)` (owned by main window)

#### PIP Initialization (`PipWindow.axaml.cs:37-91`)
1. `OnOpened` — finds `PipVideoHost`, subscribes to `ChildWindowCreated`
2. `OnPipVideoHostReady` — when native HWND is ready:
   - Runs initialization on `Task.Run` (background thread)
   - `_pipPlayer.InitializeRenderer(hwnd)` — sets up mpv GPU context with D3D11
   - `_pipPlayer.Mute(true)` — PIP never produces audio
   - `_pipPlayer.Open(_filePath)` — opens same file
   - Seeks to main player's current position
   - Posts `IsVideoSurfaceVisible = true` to UI thread
   - Starts `StartSyncTimer()` (100ms `DispatcherTimer`)

#### Sync Timer (`PipWindow.axaml.cs:93-121`)
- Runs every 100ms on UI thread
- Compares main and PIP position values
- If difference > 0.5s, seeks PIP to main position
- Updates seek bar fill width and time label

#### Transport Controls (`PipWindow.axaml.cs:122-143`)
- **Play/Pause**: Directly controls `_pipPlayer.Play()/Pause()`
- **Previous**: Calls `_mainPlayer.SeekBackward(30)` then `SyncFromMain()`
- **Next**: Calls `_mainPlayer.SeekForward(30)` then `SyncFromMain()`

#### Exit Animation (`PipWindow.axaml.cs:145-171`)
- Shrinks window to 30% size over ~200ms (10 steps × 20ms delay)
- Easing: cubic ease-out (`1 - (1-t)^3`)
- Fades opacity 1→0
- Then calls `Close()`

#### Cleanup (`PipWindow.axaml.cs:173-196`)
- Stops sync timer
- Cancels initialization CTS
- Calls `_pipPlayer.Stop()`, disposes if `IDisposable`
- MainWindow `Closed` handler restores `_videoHost.IsVideoSurfaceVisible`, resumes playback

---

## 3. Completion Assessment

### 3.1 Feature-by-Feature Breakdown

| Feature | Status | Score | Notes |
|---|---|---|---|
| PIP window creation/destruction | Done | 90% | Works, minor edge cases |
| Separate mpv instance | Done | 80% | Creates new MpvPlayer, but no error recovery |
| Video rendering in PIP | Done | 75% | Fragile init on Task.Run, no re-init on failure |
| Position sync (polling) | Done | 60% | 100ms timer works but jittery; should be event-driven |
| Play/Pause button | Done | 85% | Works, icon doesn't toggle (always shows Pause) |
| Previous/Next buttons | Done | 70% | Seeks main player, then syncs PIP; 300ms gap before sync |
| Seek bar visualization | Done | 60% | Shows position fill, has thumb, but **no click/drag interaction** |
| Time display | Done | 90% | Shows MM:SS / MM:SS |
| Close animation | Done | 80% | Smooth shrink effect, but anchored to center (not PIP button) |
| Mute on PIP | Done | 100% | Always muted |
| Header bar | Done | 70% | Has PIP label + blue dot + close button; no title/file name |
| Resize grip indicator | Done | 30% | Visual element exists but **window is not resizable** |
| **Window resizing** | Missing | 0% | No `CanResize` or resize mode set |
| **Window dragging** | Missing | 0% | No drag-to-move implementation |
| **Seek bar interaction** | Missing | 0% | Click/drag does nothing |
| **Volume control** | Missing | 0% | No volume slider or mute toggle in PIP |
| **Keyboard shortcuts in PIP** | Missing | 0% | No KeyDown handler |
| **Subtitle rendering** | N/A | — | Intentionally omitted — PIP is small, subtitles unreadable |
| **Playlist awareness** | Missing | 0% | PIP doesn't know about playlist; only one file |
| **File change handling** | Missing | 0% | If main opens new file, PIP keeps playing old file |
| **PIP position persistence** | Missing | 0% | Always opens at default position |
| **State restoration on close** | Partial | 50% | Resumes main but doesn't restore volume/subtitle state |
| **Error handling** | Partial | 30% | Try/catch exists but no user recovery path |
| **Auto-hide controls** | Missing | 0% | Controls always visible |
| **Hover-to-show header** | Missing | 0% | Header always visible |
| **Play/Pause icon toggle** | Missing | 0% | Always shows Pause |
| **Aspect ratio handling** | Missing | 0% | No aspect override for PIP |
| **Memory/GPU leak** | Risk | — | Two full mpv contexts = 2× GPU memory for decoded frames |

### 3.2 Overall Score: **~50%**

```
Category             Weight    Score    Weighted
────────────────────────────────────────────
Architecture/Lifecycle  20%     70%      14.0
Video Rendering         25%     60%      15.0
Playback Controls       20%     40%       8.0
Position Sync           10%     60%       6.0
Window UX               15%     30%       4.5
Stability/Edge Cases    10%     30%       3.0
────────────────────────────────────────────
TOTAL                  100%              50.5%
```

---

## 4. State of the Art — How PIP Should Be Done

### 4.1 Ideal Architecture for mpv-Based PIP

Since mpv/libmpv does not support multiple output windows from a single context, **Approach A (separate instances)** is the only viable path without massive engineering. The key improvements over the current implementation:

1. **Dedicated `PipService`** — Not ad-hoc in MainWindow. Manages lifecycle, tracks state, handles errors.
2. **Event-driven sync** — Use mpv's property observation (`observe_property("time-pos")`) instead of polling. Main player publishes position changes; PIP subscribes.
3. **Shared options/config** — PIP instance inherits audio track, speed, video filters from main player. (Subtitles intentionally omitted — PIP is too small to read them.)
4. **PIP mode in ViewModel** — `MainViewModel` should be PIP-aware (e.g., `IsPipActive`, `PipPlayer` properties) to keep logic out of views.
5. **Configurable PIP** — Position, size, opacity, always-on-top persisted to user preferences.

### 4.2 Reference Implementations

| Project | Language | Pattern | Key Takeaway |
|---|---|---|---|
| [david-j-lee/picture-in-picture](https://github.com/david-j-lee/picture-in-picture) | C#/WPF | DI + MVVM + Services | `ProcessesService` manages window capture; `PipModeWindow` + `PipModeViewModel` separation |
| [Kobar-Project/KoBar](https://github.com/Kobar-Project/KoBar) | Electron/JS | BrowserWindow + IPC | Separate `BrowserWindow` with its own web contents; sync via IPC messages |
| VLC (libvlc) | C | Separate `libvlc_media_player` | Each player is independent; user manages sync |

### 4.3 Recommended Thread Model for Cine

```
┌────────────────────────────────────────────────────────────────┐
│  UI Thread (STA) — All Avalonia                                │
│                                                                │
│  MainWindow ◄──► PipWindow (same dispatcher)                   │
│      │                 │                                       │
│      ▼                 ▼                                       │
│  PipService (singleton, UI-thread-affine)                      │
│      │                                                        │
│      ├── CreatePip()                                           │
│      ├── DestroyPip()                                          │
│      ├── SyncPosition()   ← called by PositionChanged event    │
│      └── State: IsActive, PipPosition, PipSize                 │
└────────────────────────────────────────────────────────────────┘
          │                              │
          ▼                              ▼
┌─────────────────┐        ┌─────────────────┐
│ MpvPlayer (main) │        │ MpvPlayer (pip) │
│ event loop thread│        │ event loop thread│
└─────────────────┘        └─────────────────┘
```

**No additional UI threads needed.** Both windows share the same dispatcher, which is the Avalonia (and WPF) standard.

---

## 5. Action Plan — Making PIP a Finished Product

### Phase 1: Stability & Correctness (Target: 65%)

| # | Task | Priority | Effort | File(s) |
|---|---|---|---|---|
| 1.1 | **Fix fragile init**: Replace `Task.Run` in `OnPipVideoHostReady` with `async` method that properly awaits renderer init. Add retry logic (max 3 attempts). | Critical | M | `PipWindow.axaml.cs` |
| 1.2 | **Error recovery**: If PIP init fails, show OSD notification on main window and clean up. Don't leave orphaned `MpvPlayer`. | Critical | S | `PipWindow.axaml.cs`, `MainWindow.Pip.cs` |
| 1.3 | **Prevent double-audio**: When PIP opens, ensure main player is paused AND muted if needed. When PIP closes, restore previous mute state (not just play). | Critical | S | `MainWindow.Pip.cs` |
| 1.4 | **Handle file change**: Subscribe to main player's `Opened` event. When it fires while PIP is active, re-open the same file in PIP or close PIP. | High | M | `MainWindow.Pip.cs` |
| 1.5 | **Fix Play/Pause icon toggle**: Update icon based on `_pipPlayer.IsPlaying` state. | Medium | S | `PipWindow.axaml.cs`, `PipWindow.axaml` |
| 1.6 | **Dispose safety**: Guard against double-dispose of `_pipPlayer` (can happen if both `OnClosed` and `Closed` handler fire). | Medium | S | `PipWindow.axaml.cs` |

### Phase 2: Interaction & UX (Target: 80%)

| # | Task | Priority | Effort | File(s) |
|---|---|---|---|---|
| 2.1 | **Clickable/draggable seek bar**: Add `PointerPressed` + `PointerMoved` + `PointerReleased` handlers to seek track. Calculate position from X coordinate. | Critical | M | `PipWindow.axaml.cs`, `PipWindow.axaml` |
| 2.2 | **Resizable window**: Set `CanResize="True"` or implement custom resize handles. Min 240×160, max 1920×1080. | High | S | `PipWindow.axaml` |
| 2.3 | **Drag-to-move**: Handle `PointerPressed`+`PointerMoved` on the header bar to reposition window. | High | M | `PipWindow.axaml.cs` |
| 2.4 | **Volume control in PIP**: Add volume slider or wheel-over-PIP to adjust volume (of main player, since PIP is muted). | Medium | M | `PipWindow.axaml`, `PipWindow.axaml.cs` |
| 2.5 | **Auto-hide controls**: Hide controls bar after 3s of inactivity. Show on pointer move. | Medium | M | `PipWindow.axaml.cs` |
| 2.6 | **Show file name in header**: Display current file name next to "PIP" label. | Low | S | `PipWindow.axaml` |

### Phase 3: Event-Driven Sync (Target: 90%)

| # | Task | Priority | Effort | File(s) |
|---|---|---|---|---|
| 3.1 | **Replace polling with event-driven sync**: Instead of 100ms timer, subscribe to main player's `PositionChanged` event. Throttle seeks to max 1 per 250ms. | High | M | `PipWindow.axaml.cs` |
| 3.2 | **Property inheritance**: When PIP opens, copy current audio track, speed, and video filter values to PIP player so visual match is perfect. | High | M | `PipWindow.axaml.cs`, `MainWindow.Pip.cs` |
| 3.4 | **Seek preview in main**: When user seeks in PIP transport, show position change both in PIP and as an OSD on the hidden main window (for reference). | Low | M | `PipWindow.axaml.cs` |

### Phase 4: Polish & Persistence (Target: 100%)

| # | Task | Priority | Effort | File(s) |
|---|---|---|---|---|
| 4.1 | **Extract PipService**: Move PIP lifecycle from `MainWindow.Pip.cs` + `PipWindow.axaml.cs` into a dedicated `PipService : IDisposable` class. MainWindow and PipWindow become thin shells. | High | L | New: `Services/PipService.cs` |
| 4.2 | **PIP ViewModel**: Create `PipViewModel` for bindings (position, duration, isPlaying, volume, file name). Eliminate code-behind property manipulation. | Medium | L | New: `ViewModels/PipViewModel.cs` |
| 4.3 | **PIP preferences**: Save PIP window position, size, always-on-top to user preferences. Restore on next open. | Medium | M | `PipWindow.axaml.cs`, preferences system |
| 4.4 | **Keyboard shortcuts**: Handle space (play/pause), left/right (seek), escape (close) in PIP window. | Medium | S | `PipWindow.axaml.cs` |
| 4.5 | **Return animation**: Instead of just closing, animate PIP window growing back to main window position (reverse of close animation). | Low | M | `PipWindow.axaml.cs` |
| 4.6 | **Playlist mode**: Allow PIP to advance through playlist independently OR stay locked to current file (user preference). | Low | L | `PipService`, `PipViewModel` |
| 4.7 | **Performance profiling**: Profile GPU memory usage with two mpv instances. Add option to reduce PIP decode resolution (e.g., 480p) for lower-end hardware. | Low | M | `PipService` |

### Phase 5: Alternative Architecture Evaluation (Future)

| # | Option | Description | Viability |
|---|---|---|---|
| 5.1 | **Shared D3D11 texture** | Both windows read from same NV12/BGRA texture via `ID3D11Device` shared resource. | High effort, perfect sync, single decode. Requires deep DirectX interop work. |
| 5.2 | **Frame buffer copy** | Capture rendered frames from main surface → copy to PIP surface. | Medium effort. Would need `IMediaPlayer.GetCurrentFrame()` API + manual rendering. |
| 5.3 | **PipeWire/DMA-BUF** (Linux) | Zero-copy buffer sharing between mpv contexts. | Only useful if Linux support is added. |

---

## 6. Recommended Implementation Order

```
Phase 1 (2-3 days)     Phase 2 (2-3 days)     Phase 3 (2 days)      Phase 4 (2-3 days)
┌──────────────┐      ┌──────────────┐       ┌──────────────┐       ┌──────────────┐
│ 1.1 Init fix  │      │ 2.1 Seek bar  │       │ 3.1 Event sync│       │ 4.1 PipService│
│ 1.2 Error rec  │ ──► │ 2.2 Resize    │ ──►  │ 3.2 Inherit   │ ──►  │ 4.2 PipVM     │
│ 1.3 Double aud │      │ 2.3 Drag move │       │               │       │ 4.3 Prefs     │
│ 1.4 File change│      │ 2.4 Volume     │       │               │       │ 4.4 Shortcuts │
│ 1.5 Icon toggle│      │ 2.5 Auto-hide │       │               │       │ 4.5 Anim      │
│ 1.6 Dispose    │      │ 2.6 File name │       │               │       │ 4.6 Playlist  │
└──────────────┘      └──────────────┘       └──────────────┘       └──────────────┘
     ~65%                  ~80%                   ~90%                   ~100%
```

**Total estimated effort: 8-11 days** for a single developer familiar with the codebase.

---

## 7. Key Architectural Decisions to Make Now

| Decision | Options | Recommendation |
|---|---|---|
| **PipService vs inline** | Keep in MainWindow or extract service | **Extract to PipService** — cleaner separation, easier to test, reusable if PIP ever needs to be triggered from other contexts |
| **Sync method** | Polling (current) vs Event-driven | **Event-driven** — less CPU, more responsive, idiomatic |
| **Window thread** | Same UI thread (current) vs Separate thread | **Keep same UI thread** — simpler, standard for Avalonia, avoids complex cross-thread UI marshalling |
| **PIP decode resolution** | Full resolution vs Reduced | **Full resolution for now** — optimize later if GPU memory is an issue |
| **Subtitle rendering in PIP** | Not needed vs Should show | **Not needed** — PIP is ~25% of screen; subtitles are unreadable at that size. User likely has main window visible for reference. |
| **Audio in PIP** | Muted (current) vs User choice | **Keep muted by default, add toggle** — prevents accidental double-audio, but power users may want it |

---

## 8. Risks & Gotchas

1. **mpv GPU context conflict**: Two mpv instances fighting over the same D3D11 device can cause `CreateDevice` failures. Mitigation: both instances use the same adapter but separate `ID3D11Device` instances. Current code creates device per `MpvPlayer`; verify this doesn't cause `DXGI_ERROR_DRIVER_INTERNAL_ERROR`.

2. **OpenGL interop (Linux/macOS)**: If the project ever ports to Linux, the rendering backend changes from D3D11 to OpenGL/Vulkan. The `InitializeRenderer(hwnd)` path would need to configure `gpu-context=x11egl` or `gpu-context=wayland`.

3. **Media Foundation fallback**: `CreateSecondaryPlayer()` always creates `MpvPlayer`. If running with `MediaFoundationPlayer` as main backend, there's no `CreateSecondaryPlayer()` equivalent. This is a hard dependency on mpv.

4. **High DPI**: The 400×300 default size may look tiny on 4K displays. Use device-independent scaling.

5. **Seek race**: Rapid seeks (holding down next/prev) can stack up and cause the PIP to seek to stale positions. Debounce is needed.

---

## 9. Appendix: Relevant Code References

| Description | Path |
|---|---|
| PIP toggle logic | [`src/App/UI/Shell/MainWindow.Pip.cs:23-73`](../src/App/UI/Shell/MainWindow.Pip.cs#L23) |
| PIP window XAML | [`src/App/UI/Screens/Dialogs/PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml) |
| PIP init + sync timer | [`src/App/UI/Screens/Dialogs/PipWindow.axaml.cs:37-121`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs#L37) |
| Secondary player factory | [`src/App/Application/Services/PlayerService.cs:37-42`](../src/App/Application/Services/PlayerService.cs#L37) |
| MpvPlayer init + D3D11 setup | [`src/Media/Implementations/mpv/MpvPlayer.cs:484-527`](../src/Media/Implementations/mpv/MpvPlayer.cs#L484) |
| D3D11VideoHost control | [`src/App/UI/Controls/Video/D3D11VideoHost.cs`](../src/App/UI/Controls/Video/D3D11VideoHost.cs) |
| IMediaPlayer interface | [`src/Media/Interfaces/IMediaPlayer.cs`](../src/Media/Interfaces/IMediaPlayer.cs) |
| PIP button in HeaderBar | [`src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs:162-167`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs#L162) |
| Existing PIP issues/plans | [`CINE_V3_UI_FUNCTIONALITY.md`](../CINE_V3_UI_FUNCTIONALITY.md) (Phase 2, Section 2.1-2.4) |
| V2 PIP rewrite notes | [`CINE_V2_PREMIUM.md`](../CINE_V2_PREMIUM.md) (Phase 2, Section 2.1-2.4) |
| UI root cause doc | [`md/UI_ROOT_CAUSE_AND_FIX_PLAN.md`](../md/UI_ROOT_CAUSE_AND_FIX_PLAN.md) (Section 10a) |
