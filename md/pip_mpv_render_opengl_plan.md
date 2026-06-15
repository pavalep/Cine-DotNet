# PiP + MainWindow: MPV OpenGL Render API Integration Plan (v2 — Production)

**Goal:** Replace the DWM thumbnail + hidden window + clipping workaround with proper
mpv render API integration using **OpenGL (ANGLE on Windows)** . mpv renders into an FBO
that's composited as part of Avalonia's rendering pipeline. Controls render naturally
on top — no clipping, no z-order fight.

**Why OpenGL (not D3D11/DXGI):**
- Avalonia's `OpenGlControlBase` provides the GL context directly
- Cross-platform potential (ANGLE on Windows, native GL on Linux/macOS)
- Eliminates dependency on Avalonia internal D3D11 device access
- Proven pattern (SaverinOnRails/Mpv.Avalonia, HanumanInstitute.LibMpv.Avalonia)

---

## Current State Analysis

### What exists (already implemented ~40%):
| File | Status | Issues |
|------|--------|--------|
| `MpvRender.cs` | ✅ P/Invoke bindings for render API | Minor: function pointer wrapper inconsistency |
| `AngleInterop.cs` | ✅ ANGLE EGL P/Invoke | No DLL load verification, no fallback |
| `AngleGlContext.cs` | ✅ ANGLE context + FBO management | No resize/context-loss handling, PBuffer waste |
| `MpvConfig.cs` | ✅ `GetRenderApiOptions()` | `hwdec=no`, `msg-level=info` too verbose |
| `MpvPlayer.cs` | ✅ `InitializeRenderApi/RenderFrame` | **Missing `report_swap`**, threading issues |
| `MpvVideoView.cs` | ✅ `OpenGlControlBase` subclass | No resize sync, no context loss, race in init |
| `PipPlayerService.cs` | ⚠️ Stub only | **`InitializeRenderApi` NEVER called — non-functional** |
| `PipService.cs` | ⚠️ Creates PipPlayerService | No ANGLE context wiring for PiP |
| `PipWindow.cs` | ⚠️ WriteableBitmap frame display | CPU readback path, no frame throttling |
| `PipSyncCoordinator.cs` | ⚠️ DispatcherTimer drift correction | Causes stutter, should use event-based sync |

### Critical bugs:
1. **PiP is non-functional** — `PipPlayerService.Initialize()` never calls `MpvPlayer.InitializeRenderApi()`
2. **Missing `mpv_render_context_report_swap()`** in `RenderFrame()` — breaks A/V sync
3. **Threading violation** — render API called from UI thread with `ADVANCED_CONTROL=0`
4. **`msg-level=info`** floods log in production

---

## Phase 1 — Fix P/Invoke & MpvRender Bindings

### Files to modify: `MpvRender.cs`

**1.1 Fix `mpv_render_context_set_update_callback` signature**

Current code uses raw delegate but wrapper struct is defined. Reconcile:

```csharp
// CURRENT (inconsistent):
[DllImport(...)]
public unsafe static extern void mpv_render_context_set_update_callback(
    IntPtr renderContext, MpvRenderUpdateFnDelegate callback, void* callbackCtx);

// FIX: Use wrapper struct consistently (or remove wrapper structs entirely)
// Option A (simpler — remove wrapper structs, use raw delegates):
public static extern void mpv_render_context_set_update_callback(
    IntPtr renderContext, IntPtr callback, void* callbackCtx);
```

**1.2 Remove `[MarshalAs(UnmanagedType.LPUTF8Str)]` conditional**

Project targets `net10.0-windows`, so `LPUTF8Str` is always available. Remove `#if NET5_0_OR_GREATER` noise.

**1.3 Verify `mpv_render_context_create` P/Invoke**

Current signature uses `IntPtr parameters` which requires manual `fixed` blocks. This is correct and matches the native API where params is a `mpv_render_param[]` terminated by `{MPV_RENDER_PARAM_INVALID, NULL}`.

**1.4 Add missing P/Invoke: `mpv_render_context_set_parameter`**

May be needed for runtime parameter changes (e.g., hwdec context).

---

## Phase 2 — Fix ANGLE Context (AngleGlContext + AngleInterop)

### Files to modify: `AngleInterop.cs`, `AngleGlContext.cs`

**2.1 Verify ANGLE DLL loading**

```csharp
// Add to AngleInterop:
public static bool IsAvailable
{
    get
    {
        try
        {
            // Probe by loading and getting display
            var display = eglGetDisplay(IntPtr.Zero);
            return display != EGL_NO_DISPLAY;
        }
        catch { return false; }
    }
}
```

**2.2 Remove PBuffer surface creation (waste)**

The 1x1 PBuffer surface is never used for rendering. Remove it entirely.
Only create EGL context, not surface, since we render to an FBO.

**2.3 Add context- loss recovery**

When `eglMakeCurrent` fails, reinitialize the context.

**2.4 Add GL error checking**

After each GL call in `CreateFboInternal`, call `glGetError()` and log failures.

**2.5 Remove RGBA→BGRA swap in `ReadPixels`**

The current code swaps R and B after reading GL_RGBA pixels. This is correct because
Avalonia expects BGRA, but document why this is needed.

**2.6 Add `EnsureFboSize` thread safety**

Make `EnsureFboSize` safe to call from the render thread by adding a lock.

---

## Phase 3 — Fix MpvPlayer Render API Path

### Files to modify: `MpvPlayer.cs`

**3.1 Add `mpv_render_context_report_swap()` call**

```csharp
public unsafe void RenderFrame(int fbo, int width, int height)
{
    if (_renderContext == IntPtr.Zero || !_renderApiReady) return;
    if ((MpvRenderNative.mpv_render_context_update(_renderContext) & 
         MpvRenderNative.MPV_RENDER_UPDATE_FRAME) == 0)
        return;

    using var mh = new MarshalHelper();
    var fboStruct = new MpvRenderNative.MpvOpenglFbo { Fbo = fbo, W = width, H = height, InternalFormat = 0 };

    var parameters = new MpvRenderNative.MpvRenderParam[]
    {
        new() { Type = MpvRenderNative.MPV_RENDER_PARAM_OPENGL_FBO, Data = &fboStruct },
        new() { Type = MpvRenderNative.MPV_RENDER_PARAM_FLIP_Y, Data = (void*)mh.AllocHGlobalValue(1) },
        new() { Type = MpvRenderNative.MPV_RENDER_PARAM_INVALID, Data = null }
    };

    fixed (MpvRenderNative.MpvRenderParam* p = parameters)
    {
        var err = MpvRenderNative.mpv_render_context_render(_renderContext, (IntPtr)p);
        if (err != 0)
            DebugLog($"RenderFrame: err={err} fbo={fbo} size={width}x{height}");
    }

    // CRITICAL FIX: report swap to mpv for proper A/V sync
    MpvRenderNative.mpv_render_context_report_swap(_renderContext);
}
```

**3.2 Fix `InitializeRenderApi` threading**

The current code sets `MPV_RENDER_PARAM_ADVANCED_CONTROL = 0` (false). This causes
mpv to use timeouts when the render API is called from a different thread than the
core thread, and logs warnings. Since our `RenderFrame()` is called from Avalonia's
render thread (via `OnOpenGlRender`), we MUST either:

- **Option A (Recommended):** Set `ADVANCED_CONTROL = 1` (true) — but this means
  we MUST guarantee we never call libmpv API from the render thread. Remove the
  `mpv_get_property` / `mpv_set_property` calls from the event loop while a render
  call is in progress.
  
- **Option B:** Use a dedicated render thread instead of Avalonia's render thread.
  This is more complex but provides better isolation.

**Recommendation: Option A** — simpler, and the current code already avoids calling
libmpv API from the render thread.

**3.3 Fix `MarshalHelper` lifetime**

The current code uses `using var mh = new MarshalHelper()` inside `RenderFrame`.
This is safe because `mpv_render_context_render` is synchronous — the memory is
valid during the call. Keep this but add a comment documenting the assumption.

**3.4 Fix `_getProcCb` / `_updateCb` delegate lifetime**

These are stored as fields (preventing GC) — correct. But verify they are explicitly
nulled in `DeinitializeRenderApi()` and `Dispose()`.

**3.5 Fix `OnOpenGlRender` bounds check**

```csharp
protected override unsafe void OnOpenGlRender(GlInterface gl, int fbo)
{
    if (_player == null || _isIdle) return;
    if (Bounds.Width <= 0 || Bounds.Height <= 0) return;  // FIX: guard against 0-size

    var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
    var w = Math.Max(1, (int)(Bounds.Width * scaling));
    var h = Math.Max(1, (int)(Bounds.Height * scaling));

    _player.RenderFrame(fbo, w, h);
}
```

**3.6 Add `EnableHardwareDecoding` property**

Change `hwdec=no` to `hwdec=auto-safe` when ANGLE can support it on Windows.

```csharp
public bool EnableHardwareDecoding { get; set; }

// In GetRenderApiOptions, conditionally set hwdec:
if (EnableHardwareDecoding)
    options["hwdec"] = "auto-safe";
```

**3.7 Fix `msg-level` for production**

Change from `all=info` to `all=warn` to reduce log noise.

**3.8 Fix `_isIdle` frame-request logic**

The `_isIdle` flag prevents rendering when paused. But seeking, subtitle changes,
and filter changes should also trigger re-render. Change to event-based:

```csharp
// Instead of _isIdle, use a flag that's set whenever a new frame might be needed:
private volatile bool _needsRender;

// In InitializeRenderApi's update callback:
_onFrameReady = () => {
    _needsRender = true;
    Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
};

// In OnOpenGlRender:
protected override unsafe void OnOpenGlRender(GlInterface gl, int fbo)
{
    if (_player == null || !_needsRender) return;
    _needsRender = false;
    // ...render...
}
```

---

## Phase 4 — Fix PiP Pipeline (Make It Functional)

### Files to modify: `PipPlayerService.cs`, `PipService.cs`, `PipWindow.cs`, `AngleGlContext.cs`

**4.1 Rewrite `PipPlayerService.cs`**

Current code NEVER initializes the render API. Complete rewrite needed:

```csharp
public class PipPlayerService : IDisposable
{
    private MpvPlayer? _player;
    private AngleGlContext? _angleContext;
    private Thread? _renderThread;
    private CancellationTokenSource? _renderCts;
    private bool _disposed;

    public IMediaPlayer? Player => _player;
    public event Action<byte[], int, int>? FrameRendered;
    public event EventHandler<string>? Error;

    public bool Initialize()
    {
        // 1. Create ANGLE context
        _angleContext = new AngleGlContext(1920, 1080);
        
        // 2. Create MpvPlayer and initialize render API
        _player = new MpvPlayer();
        _player.Error += OnError;
        _player.Mute(true);

        // 3. Initialize render API with ANGLE get_proc_address
        _player.InitializeRenderApi(
            name => {
                var ptr = AngleInterop.eglGetProcAddress(name);
                return ptr;
            },
            () => {
                // Signal new frame available
                _frameReady = true;
            });

        // 4. Start dedicated render thread
        _renderCts = new CancellationTokenSource();
        _renderThread = new Thread(() => RenderLoop(_renderCts.Token))
        {
            Name = "PiP-Render",
            IsBackground = true
        };
        _renderThread.Start();

        return true;
    }

    private volatile bool _frameReady;

    private void RenderLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_frameReady && _angleContext != null && _player != null)
            {
                _frameReady = false;
                
                _angleContext.MakeCurrent();
                _angleContext.BindFbo();
                
                _player.RenderFrame(
                    _angleContext.FboHandle,
                    _angleContext.Width,
                    _angleContext.Height);
                
                // Read back pixels for PiP display
                var pixels = _angleContext.ReadPixels(
                    _angleContext.Width,
                    _angleContext.Height);
                
                _angleContext.ReleaseCurrent();
                
                // Deliver to UI thread
                var w = _angleContext.Width;
                var h = _angleContext.Height;
                Dispatcher.UIThread.Post(() => {
                    FrameRendered?.Invoke(pixels, w, h);
                }, DispatcherPriority.Render);
            }
            else
            {
                Thread.Sleep(10); // prevent busy-wait
            }
        }
    }

    // ... cleanup, open, seek, stop, dispose ...
}
```

**4.2 Fix `PipWindow.UpdateFrame` — Add frame throttling**

```csharp
private DateTime _lastFrameTime = DateTime.MinValue;
private const double MinFrameIntervalMs = 33; // ~30fps for PiP

public void UpdateFrame(byte[] pixels, int width, int height)
{
    // Throttle to ~30fps for PiP (not a primary display)
    var now = DateTime.UtcNow;
    if ((now - _lastFrameTime).TotalMilliseconds < MinFrameIntervalMs)
        return;
    _lastFrameTime = now;

    // ... existing bitmap update code ...
}
```

**4.3 Fix `PipSyncCoordinator` — Use event-based sync instead of polling**

Replace `DispatcherTimer` with position-change event subscription:

```csharp
// Subscribe to primary player's PositionChanged
_primary.PositionChanged += OnPrimaryPositionChanged;

private void OnPrimaryPositionChanged(object? sender, PositionChangedEventArgs e)
{
    if (_disposed || _isSyncing) return;
    
    var secondaryPos = _secondary.Position.TotalSeconds;
    if (secondaryPos < 0) return;
    
    var drift = Math.Abs(e.Position.TotalSeconds - secondaryPos);
    if (drift > DriftThresholdSeconds)
    {
        _isSyncing = true;
        _secondary.Seek(e.Position);
        _isSyncing = false;
    }
}
```

**4.4 Wire PiP ANGLE context in `PipService.EnterPip()`**

```csharp
// After creating PipWindow and PipPlayerService:
if (!_pipPlayerService.Initialize())
{
    Log.Warning("PipPlayer init failed");
    CleanupPip();
    return null;
}

// Wire frame rendering
_pipPlayerService.FrameRendered += (pixels, w, h) =>
{
    Dispatcher.UIThread.Post(() => _pipWindow?.UpdateFrame(pixels, w, h),
        DispatcherPriority.Render);
};
```

---

## Phase 5 — Fix MpvVideoView (Main Window)

### Files to modify: `MpvVideoView.cs`

**5.1 Add resize synchronization**

```csharp
protected override void OnOpenGlRender(GlInterface gl, int fbo)
{
    if (_player == null || _needsRender == false) return;
    if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

    var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
    var w = Math.Max(1, (int)(Bounds.Width * scaling));
    var h = Math.Max(1, (int)(Bounds.Height * scaling));

    _player.RenderFrame(fbo, w, h);
    _needsRender = false;
}
```

**5.2 Add context-loss detection**

```csharp
private bool _contextValid = true;

protected override void OnOpenGlRender(GlInterface gl, int fbo)
{
    if (!_contextValid)
    {
        // Attempt recovery
        _contextValid = TryRecoverContext(gl);
        if (!_contextValid) return;
    }
    // ... render ...
}
```

**5.3 Ensure `OnOpenGlDeinit` cleans up properly**

```csharp
protected override void OnOpenGlDeinit(GlInterface gl)
{
    _player?.DeinitializeRenderApi();
    _initialized = false;
    _contextValid = false;
}
```

**5.4 Add `DetachPlayer` method**

For clean cleanup when the player is disposed:

```csharp
public void DetachPlayer()
{
    _player = null;
    _initialized = false;
}
```

---

## Phase 6 — Fix MpvConfig

### Files to modify: `MpvConfig.cs`

**6.1 Fix `GetRenderApiOptions`**

```csharp
public static Dictionary<string, string> GetRenderApiOptions(bool enableHwDec = false)
{
    return new Dictionary<string, string>
    {
        ["terminal"] = "no",
        ["msg-level"] = "all=warn",          // FIX: was "info" — too verbose
        ["keep-open"] = "yes",
        ["keep-open-pause"] = "no",
        ["osc"] = "no",
        ["vo"] = "libmpv",                   // REQUIRED for render API
        ["hwdec"] = enableHwDec ? "auto-safe" : "no",
        ["volume-max"] = "150"
    };
}
```

**6.2 Keep `GetFullOptions` for `wid` fallback path**

The `wid` path (native HWND embedding) should remain as a fallback for systems
without ANGLE/OpenGL support.

---

## Phase 7 — Error Handling & User Feedback

### Files to modify: `MpvVideoView.cs`, `MainWindow.Core.cs`, `PipService.cs`

**7.1 Add ANGLE availability check at startup**

```csharp
// In MainWindow.Core.cs InitVideoRenderer():
if (!AngleInterop.IsAvailable)
{
    DebugLog("ANGLE/EGL not available — falling back to wid path");
    // Use InitializeRenderer(hwnd) instead
    return;
}
```

**7.2 Add render API error event**

```csharp
// In MpvPlayer:
public event EventHandler<string>? RenderError;

// In RenderFrame, when err != 0:
RenderError?.Invoke(this, $"Render error: {err}");
```

**7.3 Show user-friendly error on ANGLE failure**

```csharp
// In MainWindow, on ANGLE init failure:
ShowOsdNotification(
    MaterialIconKind.AlertCircle,
    "Video rendering failed — try restarting Cine",
    5000);
```

---

## Phase 8 — Performance Optimizations

**8.1 Reduce PiP readback resolution**

Instead of 1920x1080, use the actual PiP window size:

```csharp
// In PipPlayerService, when window resizes:
_angleContext.EnsureFboSize(pipWidth, pipHeight);
```

**8.2 Add optional frame skipping for PiP**

If frames arrive at 60fps but PiP displays at 30fps, skip every other frame:

```csharp
private int _frameSkipCounter;

private void RenderLoop(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        if (_frameReady)
        {
            _frameReady = false;
            _frameSkipCounter++;
            if (_frameSkipCounter % 2 != 0)
                continue; // skip — render next frame instead
            
            // ... render and read back ...
        }
    }
}
```

**8.3 Optimize WriteableBitmap update**

Use `ILockFramebuffer` directly instead of row-by-row Marshal.Copy:

```csharp
using (var fb = _pipFrameBitmap.Lock())
{
    unsafe
    {
        fixed (byte* src = pixels)
        {
            Buffer.MemoryCopy(src, (void*)fb.Address, 
                (uint)(width * height * 4),
                (uint)(width * height * 4));
        }
    }
}
```

---

## Phase 9 — Threading Model Documentation

### The correct threading model for mpv render API:

```
┌──────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│  Core Thread     │     │  Render Thread    │     │  UI Thread           │
│  (mpv event loop)│     │  (Avalonia GL)    │     │  (Dispatcher)        │
│                  │     │                   │     │                     │
│ mpv_command      │     │ mpv_render_       │     │ Dispatch timers     │
│ mpv_get_property │     │ context_render()   │     │ Event handlers      │
│ mpv_set_property │◄──► │ mpv_render_       │     │ WriteableBitmap     │
│ mpv_wait_event   │     │ context_update()   │     │ update              │
│                  │     │ report_swap()      │     │                     │
│                  │     │                   │     │                     │
└──────────────────┘     └──────────────────┘     └─────────────────────┘
       │                        │                          │
       │ mpv_set_wakeup_callback│ mpv_render_context_       │
       │ fires when events      │ set_update_callback      │
       │ are ready              │ fires when new frame     │
       ▼                        ▼                          ▼
```

**Rules:**
1. `mpv_render_context_create()` must be called on the render thread
2. `mpv_render_context_render()` must be called on the render thread with GL context current
3. `mpv_command()`, `mpv_get_property()`, `mpv_set_property()` from core thread only
4. `mpv_render_context_set_update_callback()` callback fires from mpv internal thread — use `Dispatcher.UIThread.Post` to marshal to UI thread
5. `mpv_render_context_free()` must be called on the render thread

---

## Phase 10 — Platform Abstraction

### Files to create/modify:

**10.1 Create `IPlatformGLContext.cs` interface**

```csharp
public interface IPlatformGLContext : IDisposable
{
    bool IsValid { get; }
    void MakeCurrent();
    void ReleaseCurrent();
    IntPtr GetProcAddress(string name);
}
```

**10.2 Implement `AngleGLContext : IPlatformGLContext` (Windows)**

Move ANGLE-specific code into this implementation.

**10.3 Add platform detection**

```csharp
public static class GLContextFactory
{
    public static IPlatformGLContext Create()
    {
        if (OperatingSystem.IsWindows())
            return new AngleGLContext();
        else if (OperatingSystem.IsLinux())
            return new NativeEGLContext(); // Future
        else if (OperatingSystem.IsMacOS())
            return new CocoaGLContext(); // Future
        else
            throw new PlatformNotSupportedException();
    }
}
```

---

## Phase 11 — Cleanup & Removal

### Files to remove:

| File | Reason |
|------|--------|
| `D3D11VideoHost.cs` | D3D11-specific, not needed for OpenGL |
| `DwmThumbnailManager.cs` | DWM thumbnail workaround, replaced by render API |
| `PipOverlayWindow.axaml`/`.cs` | Already deleted per original plan |

### Code to remove:

- All `wid`-specific code in `PipPlayerService` (hidden window + DWM thumbnail)
- `SyncThumbnailRect`, `UpdateDwmClipRect`, `OnClipRectNeeded` in PipWindow
- `MpvConfig.GetLowQualityOptions()` — quality is now per-render, not per-instance

---

## Phase 12 — Testing & Verification

**12.1 Unit tests:**
- Verify `mpv_render_context_create` returns non-zero context
- Verify `MpvRenderNative` P/Invoke signatures match mpv header
- Verify `AngleGLContext` creates valid GL context

**12.2 Integration tests:**
- Main window video plays with render API
- PiP window opens and shows synchronized video
- PiP controls render on top of video
- Window resize triggers proper FBO resize
- Context loss recovery works

**12.3 Performance benchmarks:**
- GPU memory usage (main window + PiP)
- Frame render latency (main window)
- PiP frame rate (should be stable at 30fps)
- CPU usage comparison (render API vs wid path)

---

## Implementation Order

```
Phase 1 (Fix MpvRender.cs P/Invoke)
  └─> Phase 2 (Fix AngleGLContext)
       └─> Phase 3 (Fix MpvPlayer render API)
            └─> Phase 4 (Fix PiP — make it WORK)
                 ├─> Phase 5 (Fix MpvVideoView main window)
                 ├─> Phase 6 (Fix MpvConfig)
                 ├─> Phase 7 (Error handling)
                 ├─> Phase 8 (Performance)
                 ├─> Phase 9 (Documentation)
                 └─> Phase 10 (Platform abstraction)
                      └─> Phase 11 (Cleanup obsolete code)
                           └─> Phase 12 (Testing)
```

---

## Appendix: Critical Issues Summary

| # | Issue | Severity | File | Fix |
|---|-------|----------|------|-----|
| C1 | PiP doesn't render at all | **CRITICAL** | `PipPlayerService.cs` | Call `InitializeRenderApi` + create ANGLE context |
| C2 | Missing `report_swap` | **CRITICAL** | `MpvPlayer.cs` | Add `mpv_render_context_report_swap()` after render |
| C3 | Threading violation with ADVANCED_CONTROL=0 | **HIGH** | `MpvPlayer.cs` | Either set ADVANCED_CONTROL=1 or use dedicated render thread |
| C4 | `msg-level=info` in production | **MEDIUM** | `MpvConfig.cs` | Change to `warn` |
| C5 | No resize guard in `OnOpenGlRender` | **MEDIUM** | `MpvVideoView.cs` | Check Bounds.Width/Height > 0 |
| C6 | `hwdec=no` disables GPU decoding | **MEDIUM** | `MpvConfig.cs` | Make configurable |
| C7 | No ANGLE availability check | **MEDIUM** | `MainWindow.Core.cs` | Check before init |
| C8 | WriteableBitmap row-by-row copy | **LOW** | `PipWindow.cs` | Use `Buffer.MemoryCopy` |
| C9 | No PiP frame rate throttle | **LOW** | `PipWindow.cs` | Add ~30fps throttle |
| C10 | DispatcherTimer drift polling | **LOW** | `PipSyncCoordinator.cs` | Use event-based sync |
| C11 | PBuffer surface waste | **LOW** | `AngleGlContext.cs` | Remove PBuffer creation |
| C12 | ANGLE only (no platform abstraction) | **LOW** | All files | Add IPlatformGLContext interface |

---

## Reference implementations

- [SaverinOnRails/Mpv.Avalonia](https://github.com/SaverinOnRails/Mpv.Avalonia) — OpenGL mpv in Avalonia
- [HanumanInstitute.LibMpv.Avalonia](https://feed.nuget.org/packages/HanumanInstitute.LibMpv.Avalonia/0.10.0-rc.1) — NuGet package, mature implementation
- [mpv render.h](https://github.com/mpv-player/mpv/blob/master/include/mpv/render.h) — Official API header
- [mpv render_gl.h](https://github.com/mpv-player/mpv/blob/master/include/mpv/render_gl.h) — OpenGL-specific API header
