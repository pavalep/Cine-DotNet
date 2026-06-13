# Cine Video Rendering Fix — Action Plan

## Current State
- `mpv_render_context_create` **hangs** (never returns)
- Previous builds got `RenderFrame: err=-4` from `mpv_render_context_render`
- Reference project (`LibMpv-OpenGL-main`) runs successfully

---

## Phase 1: Platform Rendering Mode (Critical — Required for GL)

### Problem
Our `App.axaml.cs` does **not** specify `Win32RenderingMode.AngleEgl`. The reference explicitly sets it.

### Reference (working):
```csharp
// reference/.../Program.cs
builder.With(new Win32PlatformOptions
{
    RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
});
```

### Our code (before fix):
```csharp
// src/App/App.axaml.cs
return AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .With(new Win32PlatformOptions
    {
        CompositionMode = new[] { Win32CompositionMode.RedirectionSurface }
        // ❌ MISSING: RenderingMode
    });
```

### Fix already applied:
```csharp
// src/App/App.axaml.cs (line ~132)
RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software },
```

### Why this matters
- Without `AngleEgl`, Avalonia falls back to WGL (Windows GDI OpenGL), which doesn't create a modern GL context
- mpv's OpenGL render API requires **OpenGL ES 3.0+** or **OpenGL 3.3+** with proper extensions
- ANGLE translates GL calls to Direct3D 11 — this is the **only reliable path** on Windows
- Reference project always uses ANGLE for the working path

### Reference
- [mpv documentation: OpenGL render API](https://mpv.io/manual/stable/#options-vo)
- [ANGLE project](https://github.com/google/angle) — Google's GL→Direct3D translator

---

## Phase 2: Delegate Types — Pointer ABI Match (Critical)

### Problem
Our delegate type `MpvGetProcAddressDelegate` accepts and returns `IntPtr`, but the reference uses `void*` for both arguments and return values. This creates an ABI mismatch since the calling convention (Cdecl) and pointer size matter at the native boundary.

### Reference (working):
```csharp
// reference/.../Delegates.cs
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void* MpvOpenglInitParamsGetProcAddress(
    void* ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
```

### Our code (current):
```csharp
// src/Media/Implementations/mpv/MpvRender.cs
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate IntPtr MpvGetProcAddressDelegate(IntPtr ctx, IntPtr name);
// ❌ IntPtr vs void* may differ in marshaling
// ❌ Uses IntPtr for name string instead of string with MarshalAs
```

### Fix needed:
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void* MpvGetProcAddressDelegate(
    void* ctx, 
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
```

### Similarly for the update callback:
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void MpvRenderUpdateFnDelegate(void* ctx);
```

### Reference
- [.NET UnmanagedFunctionPointer docs](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute)
- [Cdecl calling convention — x64 ABI](https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention)

---

## Phase 3: Init Params Struct — `GetProcAddressCtx` Type

### Problem
Our `MpvOpenglInitParams` uses `IntPtr` for `GetProcAddressCtx`. The reference uses `void*`. In the C struct, this is a `void*` field.

### Reference:
```csharp
// reference/.../Structs.cs
public unsafe struct MpvOpenglInitParams
{
    public MpvOpenglInitParamsGetProcAddressFunc GetProcAddress;
    public void* GetProcAddressCtx;
}
```

### Our code:
```csharp
public struct MpvOpenglInitParams
{
    public IntPtr GetProcAddress;
    public IntPtr GetProcAddressCtx;  // ❌ should be void*
}
```

### Fix:
```csharp
public unsafe struct MpvOpenglInitParams
{
    public IntPtr GetProcAddress;
    public void* GetProcAddressCtx;
}
```

---

## Phase 4: Init Sequence — Match Reference Exactly

### Problem
Our init passes `advanced_control=0` but also comes with different param ordering and memory management.

### Reference init (what works):
```csharp
// reference/.../MpvContextBase_Rendering.cs
using var marshalHelper = new MarshalHelper();

var parameters = new List<MpvRenderParam>
{
    new() { Type = MpvRenderParamType.ApiType,
            Data = (void*)marshalHelper.StringToHGlobalAnsi("opengl") },
    new() { Type = MpvRenderParamType.OpenGlInitParams,
            Data = (void*)marshalHelper.AllocHGlobal(new MpvOpenglInitParams {...}) },
    // Optional X11/Wayland display params here
    new() { Type = MpvRenderParamType.AdvancedControl,
            Data = (void*)marshalHelper.AllocHGlobalValue(0) },
    new() { Type = MpvRenderParamType.Invalid, Data = null }
};
```

### Key observations:
1. Reference uses `MarshalHelper` for ALL allocations — auto-freed on Dispose
2. `AdvancedControl = 0` (not 1)
3. `GetProcAddressCtx = null` (0)
4. No `FlipY` in init params
5. Delegate is `.AllocHGlobal()`'d via struct copy (structure-to-ptr)

### Our current code is close but needs:
1. `MpvGetProcAddressDelegate` signature changed (Phase 2)
2. `MpvOpenglInitParams.GetProcAddressCtx` as `void*` (Phase 3)
3. Verify `AdvancedControl = 0` is passed correctly

---

## Phase 5: Render Path — Match Reference Exactly

### Problem
Our `RenderFrame` passes `&fboStr` directly into `MpvRenderParam.Data`. The reference uses `GCHandle.Alloc` for the FBO struct and `marshalHelper.AllocHGlobalValue` for `FlipY`.

### Reference (working):
```csharp
// reference/.../MpvContextBase_Rendering.cs
public void OpenGlRender(int width, int height, int fb = 0, int flipY = 0)
{
    using var marshalHelper = new MarshalHelper();

    var fbo = new MpvOpenglFbo() { W = width, H = height, Fbo = fb };
    var handle = GCHandle.Alloc(fbo, GCHandleType.Pinned);
    
    var parameters = new MpvRenderParam[]
    {
        new() { Type = MpvRenderParamType.OpenGlFbo, Data = &fbo },
        new() { Type = MpvRenderParamType.FlipY,
                Data = (void*)marshalHelper.AllocHGlobalValue(flipY) },
        new() { Type = MpvRenderParamType.Invalid }
    };

    fixed (MpvRenderParam* parametersPtr = parameters)
    {
        RenderContextRender(parametersPtr);
    }
    handle.Free();
}
```

### Key differences:
| Aspect | Reference | Our Code |
|--------|-----------|----------|
| FBO storage | `GCHandle.Alloc` (pinned) | Direct `&fboStr` local |
| FlipY storage | `marshalHelper.AllocHGlobalValue(flipY)` | `&flipY` local |
| flipY value | Parameter (0 by default) | Hardcoded `1` |
| report_swap | NOT called | Called after render |
| sizeof(MpvOpenglFbo) | 16 bytes (4 ints) | Same |

### Why GCHandle + AllocHGlobal matters:
- `&local` in a `fixed` block only pins the array, not the structs themselves
- When `fixed` block exits, the local structs can be moved by GC
- If mpv reads them asynchronously (possible with ANGLE's multi-threaded GL), this causes `-4` (invalid parameter)
- `GCHandle.Alloc` + `AllocHGlobalValue` keeps data pinned/handled until explicitly freed

### Fix for RenderFrame:
```csharp
public unsafe void RenderFrame(int fbo, int width, int height)
{
    if (_renderContext == IntPtr.Zero || !_renderApiReady) return;
    if ((MpvRenderNative.mpv_render_context_update(_renderContext) & MPV_RENDER_UPDATE_FRAME) == 0)
        return;

    using var mh = new MarshalHelper();
    var fboStruct = new MpvRenderNative.MpvOpenglFbo { Fbo = fbo, W = width, H = height, InternalFormat = 0 };
    var handle = GCHandle.Alloc(fboStruct, GCHandleType.Pinned);

    var parameters = new MpvRenderNative.MpvRenderParam[]
    {
        new() { Type = MPV_RENDER_PARAM_OPENGL_FBO, Data = &fboStruct },
        new() { Type = MPV_RENDER_PARAM_FLIP_Y,
                Data = (void*)mh.AllocHGlobalValue(1) },
        new() { Type = MPV_RENDER_PARAM_INVALID, Data = null }
    };

    fixed (MpvRenderNative.MpvRenderParam* p = parameters)
    {
        var err = MpvRenderNative.mpv_render_context_render(_renderContext, (IntPtr)p);
        if (err == 0)
            MpvRenderNative.mpv_render_context_report_swap(_renderContext);
        else
            DebugLog($"RenderFrame: err={err}");
    }
    handle.Free();
}
```

---

## Phase 6: P/Invoke Signatures — Match Reference ABI

### Problem
Our DllImport uses `IntPtr parameters` but reference uses `MpvRenderParam* parameters` (typed pointer).

### Reference:
```csharp
[DllImport("mpv-2", CallingConvention = CallingConvention.Cdecl)]
public static extern int mpv_render_context_create(
    out IntPtr renderContext, IntPtr mpvHandle, MpvRenderParam* parameters);
```

### Our code (already correct):
```csharp
[DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
public static extern int mpv_render_context_create(
    out IntPtr renderContext, IntPtr mpvHandle, IntPtr parameters);
```

### Analysis
Both work on 64-bit. However, `IntPtr` vs `MpvRenderParam*` doesn't matter for P/Invoke since both are pointer-sized. The reference uses the typed version for clarity. **No change needed** but consider matching for consistency.

---

## Phase 7: Video View Control — Match Reference OpenGlView.cs

### Problem
Our `MpvVideoView.cs` may differ from the reference in rendering loop logic.

### Reference:
```csharp
// reference/.../OpenGlView.cs
public class OpenGlView : OpenGlControlBase, IVideoView
{
    MpvContextBase? _context;
    bool _initialized;
    volatile bool _isIdle;

    // ...
    
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_context == null || _isIdle) return;
        
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var w = Math.Max(1, (int)(Bounds.Width * scaling));
        var h = Math.Max(1, (int)(Bounds.Height * scaling));
        
        _context.OpenGlRender(w, h, fb);
    }
}
```

### Our code (after latest edits):
```csharp
protected override unsafe void OnOpenGlRender(GlInterface gl, int fbo)
{
    if (_player == null || _isIdle || !_player.IsRenderApiReady)
    {
        gl.ClearColor(0, 0, 0, 1);
        gl.Clear(0x4000);
        return;
    }
    // ...
}
```

### Issue
Our code adds `!IsRenderApiReady` check and clears to black. Reference simply returns early. The `gl.Clear()` call in a `return` branch should be fine but adds unnecessary GL state changes. **Low priority fix.**

---

## Phase 8: Render Context Lifecycle — `report_swap` Timing

### Problem
The reference does NOT call `mpv_render_context_report_swap`. Our code does.

### Analysis from mpv docs:
- `mpv_render_context_report_swap()` tells mpv that the frame has been presented
- It should only be called AFTER the actual swap (presentation)
- Since we render into Avalonia's FBO and Avalonia handles the swap, calling `report_swap` prematurely may cause mpv to advance to the next frame before the current one was actually displayed
- This can cause: timing drift, dropped frames, or ERR=-4 if mpv advances state incorrectly

### Fix:
```csharp
// Remove report_swap call — let Avalonia handle the presentation timing
var err = MpvRenderNative.mpv_render_context_render(_renderContext, (IntPtr)p);
if (err != 0)
    DebugLog($"RenderFrame: err={err}");
// NO report_swap
```

Or alternatively, call report_swap only in `OnOpenGlRender` after a successful render to match actual swap timing (advanced — YAGNI for now).

---

## Phase 9: `hwdec` Option — Avoid `nvcuda.dll` Hang

### Problem
`"hwdec" = "auto-copy"` causes mpv to try loading `nvcuda.dll` which fails with "Cannot load nvcuda.dll" message, potentially hanging initialization.

### Fix:
```csharp
// MpvConfig.cs
["hwdec"] = "no",  // Software decoding — avoids GPU decode driver issues
```

### Reference
- [mpv manual: --hwdec](https://mpv.io/manual/stable/#options-hwdec)
- Software decoding is the safest default; users can enable GPU decode explicitly

---

## Phase 10: mpv Library Version

### Check
Our app uses `mpv-2.dll` (the "mpv-2" ABI). Verify:
1. The DLL is 64-bit (matches our `net10.0-windows` TFM)
2. Built with `--enable-libmpv-render` (required for render API)
3. Compatible with ANGLE's GLES 3.0

### Reference DLL
The reference project bundles `libmpv-2.dll` in `MpvDll/win-x64/`. Consider using the same DLL or verifying our DLL's render API build flags.

---

## Execution Order (Priority)

| Phase | Priority | Dependency | Status |
|-------|----------|------------|--------|
| Phase 1 | **P0** | None | Applied (needs verify) |
| Phase 2 | **P0** | None | Needs rewrite |
| Phase 3 | **P0** | Phase 2 | Needs rewrite |
| Phase 4 | **P0** | Phase 2,3 | Needs rewrite |
| Phase 5 | **P0** | Phase 2,3,4 | Needs rewrite |
| Phase 6 | P2 | None | OK |
| Phase 7 | P1 | None | Needs minor fix |
| Phase 8 | **P0** | None | Needs fix |
| Phase 9 | P1 | None | Applied |
| Phase 10 | P2 | None | Verify |

---

## Summary — Root Causes of ERR=-4 and Hang

`err=-4` from `mpv_render_context_render` means **MPV_ERROR_INVALID_PARAMETER**.

### Primary causes:
1. **Delegate ABI mismatch** (Phase 2) — mpv calls our `get_proc_address` callback but gets wrong calling convention → invalid function pointer behavior
2. **Memory not properly pinned** (Phase 5) — `&local` goes stale → mpv reads garbage data, interprets as invalid param
3. **FlipY AllocHGlobal missing** (Phase 5) — pointer to stack local dies when fixed block exits

### Hang cause:
Tied to Phase 2 — mpv calls `get_proc_address` with wrong convention → stack corruption → infinite wait in GL init

---

## Online References

- [mpv libmpv C API](https://github.com/mpv-player/mpv/blob/master/libmpv/client.h)
- [mpv render API](https://github.com/mpv-player/mpv/blob/master/libmpv/render.h)
- [mpv render.h — render_gl.h](https://github.com/mpv-player/mpv/blob/master/libmpv/render_gl.h)
- [Avalonia OpenGlControlBase docs](https://docs.avaloniaui.net/docs/guides/graphics-and-animation/opengl)
- [ANGLE on Windows](https://github.com/google/angle/blob/main/doc/DevSetup.md)
- [.NET P/Invoke best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [C# UnmanagedCallersOnly vs UnmanagedFunctionPointer](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/function-pointers)
- [StackOverflow: mpv_render_context_render error -4](https://stackoverflow.com/questions/tagged/libmpv)
- [HanumanInstitute/LibMpv-OpenGL](https://github.com/HanumanInstitute/LibMpv-OpenGL) — Reference project
