# PiP (Picture-in-Picture) Issue Analysis

## Overview

The PiP feature creates a small floating window that mirrors the video via DWM (Desktop Window Manager) thumbnail API. The video renders in a **hidden offscreen popup HWND** (at -32000, -32000), and DWM thumbnails project it onto **two targets simultaneously**:

```
mpv renders → Hidden popup HWND (offscreen -32000,-32000)
                   ↓ DwmRegisterThumbnail (same source, multiple targets)
                ┌────────────────────────────────────────────┐
                │                                            │
     MainWindow (main thumbnail id=1)    PipWindow (main thumbnail id=2)
     clipped to video area                fills whole PIP window
```

**Current status:** PiP enter/exit flow is implemented and should work in the normal path.  
When PiP fails, it is now expected to be an initialization timing issue (`SourceHwnd`/window handle not ready) or a DWM registration failure, not a permanent `EnterPip()` logic failure.

---

## Entry Point: `MainWindow.OnPipToggled`

**File:** `src/App/UI/Shell/MainWindow.Pip.cs` (lines 1-108)

1. `_headerBar.PipToggled` event fires (from header button or menu item)
2. `OnPipToggled()` checks `_pipService.IsActive`
3. If not active, calls `_pipService.EnterPip()`
4. If `EnterPip()` returns `null`, shows OSD: "PiP failed — check cine_pip.log"

---

## `PipService.EnterPip()` Flow

**File:** `src/App/Application/Services/PipService.cs` (lines 36-99)

```
EnterPip()
├── if (_disposed) → return null
├── if (_isActive) → return existing window
├── _pipWindow = new PipWindow()
├── _pipWindow.Show()
├── _pipWindow.EnableDwmMirror(_dwmManager) 
│   └── This calls DwmThumbnailManager.RegisterTarget(pipWindowHwnd)
├── _pipWindow.ShowAllControls()
├── _pipWindow.StartHoverTimer()
└── return _pipWindow
```

**Note:** `_dwmManager.SourceHwnd` was already set during `MainWindow.OnOpened()` → `TryRegisterDwmThumbnail()` → `VideoHost.RegisterMainWindow()` → `manager.SetSource(_hiddenHwnd)`.

---

## `DwmThumbnailManager.RegisterTarget()` Flow

**File:** `src/App/UI/Controls/Video/DwmThumbnailManager.cs` (lines 52-86)

```
RegisterTarget(IntPtr destHwnd)
├── if (_disposed || _sourceHwnd == IntPtr.Zero || destHwnd == IntPtr.Zero) → return 0
├── var hr = DwmRegisterThumbnail(destHwnd, _sourceHwnd, out thumbId)
├── if (hr < 0 || thumbId == IntPtr.Zero) → return 0
├── DWM_THUMBNAIL_PROPERTIES { fVisible=true, opacity=255 }
├── DwmUpdateThumbnailProperties(thumbId, ref props)
└── return id
```

### Known `RegisterTarget` failure modes:
1. `_sourceHwnd == IntPtr.Zero` → source not set (not initialized or SetSource not called)
2. `destHwnd == IntPtr.Zero` → PipWindow HWND not available
3. `DwmRegisterThumbnail` returns error HRESULT (negative)
4. `thumbId == IntPtr.Zero` → DWM registration failed

---

## `PipWindow.EnableDwmMirror()` Flow

**File:** `src/App/UI/Screens/Dialogs/PipWindow.axaml.cs` (lines 53-86)

```
EnableDwmMirror(DwmThumbnailManager manager)
├── if (_thumbnailId > 0) → return (already registered)
├── if (manager.SourceHwnd == IntPtr.Zero) → return (no source)
├── var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero
├── if (handle == IntPtr.Zero)
│   └── Subscribe to this.Opened → OnOpenedRetryMirror (deferred registration)
├── else
│   └── DoEnableMirror(handle)
└──
```

**The race condition fix:** When `TryGetPlatformHandle()` returns `IntPtr.Zero` (native window not yet created), it now defers to `Opened` event. But `OnOpenedRetryMirror` also calls `DoEnableMirror` with the same checks.

---

## `PipWindow.DoEnableMirror()` Flow

**File:** `src/App/UI/Screens/Dialogs/PipWindow.axaml.cs` (lines 79-86)

```
DoEnableMirror(IntPtr handle)
├── if (_dwmManager == null || _thumbnailId > 0) → return
├── _thumbnailId = _dwmManager.RegisterTarget(handle)
├── if (_thumbnailId > 0)
│   └── SyncThumbnailRect()
└──
```

**Note:** There is ALSO a registration attempt in `PipWindow.OnOpened()` (lines 212-224):

```
OnOpened()
├── RestoreState()
├── SetupHoverTimer()
├── ApplyAspectRatioConstraint()
├── if (_dwmManager != null && _thumbnailId == 0)
│   ├── handle = TryGetPlatformHandle()?.Handle
│   ├── _thumbnailId = _dwmManager.RegisterTarget(handle)
│   └── SyncThumbnailRect()
└──
```

**Potential conflict:** `OnOpenedRetryMirror` is subscribed to `this.Opened +=` but when `OnOpened()` fires, it also tries to register. If both fire, `DoEnableMirror` will see `_dwmManager == null` because... wait, `_dwmManager` was set in `EnableDwmMirror` already. Actually `OnOpenedRetryMirror` subscribes to `this.Opened` which fires after `OnOpened()` returns. But `OnOpened()` ALSO tries to register. This could cause double-registration attempts.

---

## Initialization Sequence (MainWindow)

**File:** `src/App/UI/Shell/MainWindow.Core.cs`

### Constructor → `OnWindowInitialized()` (line ~340-362):
```
_dwmManager = new DwmThumbnailManager();  ← shared DWM manager created
_pipService = new PipService(_dwmManager);  ← receives shared DWM manager
_headerBar.PipToggled += OnPipToggled;
```

### `OnOpened()` (line ~364-412):
```
_videoHost?.EnsureHiddenWindowCreated();  ← hidden popup HWND created
TryRegisterDwmThumbnail();  ← calls _videoHost.RegisterMainWindow(...)
                             → manager.SetSource(_hiddenHwnd) [sets _sourceHwnd]
                             → manager.RegisterTarget(mainWindowHwnd) [main thumb]
```

**Important:** The shared `_dwmManager` has `_sourceHwnd` set ONLY after `TryRegisterDwmThumbnail()` completes. If PiP is triggered before this point, `_dwmManager.SourceHwnd` is `IntPtr.Zero`.

---

## Current Failure Checkpoints (Most Likely)

### 1) Source HWND not ready when PiP is triggered
If `MainWindow.OnOpened()` has not finished `TryRegisterDwmThumbnail()` yet, `_dwmManager.SourceHwnd` is still `IntPtr.Zero` and `EnableDwmMirror()` exits early.

### 2) PiP destination HWND not ready at first attempt
`EnableDwmMirror()` now defers to `Opened` and also retries registration on a timer (short bounded retry window).  
If failure persists, it is typically due to invalid source handle or DWM API failure, not a one-frame timing race.

### 3) DWM registration failure (`DwmRegisterThumbnail`)
If source and destination handles are valid but DWM returns a failing HRESULT, PiP cannot mirror video.  
This is visible in `cine_dwm.log`.

### 4) Duplicate registration attempts
Both `OnOpenedRetryMirror` and `OnOpened()` can attempt registration.  
Current guard (`_thumbnailId > 0`) makes this mostly safe; duplicate calls should be no-op after the first success.

---

## Log Files to Check

| Log | Path | Content |
|-----|------|---------|
| PiP trace | `%LOCALAPPDATA%\Cine\cine_pip.log` | `PipService.EnterPip()` trace (newly added) |
| DWM trace | `%LOCALAPPDATA%\Cine\cine_dwm.log` | All DWM API calls and results |
| VideoHost | `%LOCALAPPDATA%\Cine\cine_videohost.log` | Hidden window creation |
| Error log | `%LOCALAPPDATA%\Cine\cine_errors.log` | App-level errors |
| Crash dumps | `%LOCALAPPDATA%\Cine\crash\crash_*.txt` | Fatal exception dumps |

---

## Key Source Files

| File | Lines | Description |
|------|-------|-------------|
| `src/App/UI/Shell/MainWindow.Pip.cs` | 1-108 | PiP toggle handler + sync methods |
| `src/App/UI/Shell/MainWindow.Core.cs` | 340-412 | DWM manager init + thumbnail registration |
| `src/App/Application/Services/PipService.cs` | 1-128 | PiP window lifecycle manager |
| `src/App/UI/Screens/Dialogs/PipWindow.axaml.cs` | 1-513 | PiP window UI + DWM mirroring |
| `src/App/UI/Controls/Video/DwmThumbnailManager.cs` | 1-180 | DWM thumbnail wrapper |
| `src/App/UI/Controls/Video/D3D11VideoHost.cs` | 1-200 | Hidden video window + main thumbnail registration |

---

## Test Protocol

To validate PiP:
1. Launch the app
2. Open a video file (any format)
3. Wait for video to play in main window
4. Press `Ctrl+P` or click the PiP button in the header
5. Verify PiP window appears and shows mirrored video
6. If it fails, check `cine_pip.log` + `cine_dwm.log`

### Expected in logs (success):
```
EnterPip start
Creating PipWindow...
Show() returned
SourceHwnd=0xXXXXXX
EnableDwmMirror: handle=0xYYYYYY
DoEnableMirror: Registering target, dest=0xYYYYYY source=0xXXXXXX
DwmRegisterThumbnail dest=0xYYYYYY src=0xXXXXXX hr=0x0 thumbId=0xZZZZZZ
RegisterTarget OK: id=2 thumbId=0xZZZZZZ
DoEnableMirror: registered id=2
EnterPip success
```

### Possible failure patterns in logs:
```
Pattern A: "SourceHwnd=0x0" → DWM source not set
Pattern B: "handle=0x0" → PipWindow HWND not ready
Pattern C: "hr=0xXXXXXXXX" (negative) → DwmRegisterThumbnail failed
Pattern D: Exception logged → Some other error
```
