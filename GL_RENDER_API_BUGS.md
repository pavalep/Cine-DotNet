# Cine App OpenGL Render API - Bug List

Created: 2026-06-12  
Target: Fix OpenGL/ANGLE render path for video display

---

## CRITICAL BUGS (Block Video Display)

### 1. EGL Context Not Created on Correct Thread
**Location:** `MpvPlayer.cs:InitializeRendererD3D11()` (lines 960-970)  
**Issue:** ANGLE/EGL context is created on the event loop thread, but EGL contexts are thread-affine. The context must be created on the same thread that calls `mpv_render_context_render`.  
**Symptom:** `eglMakeCurrent` fails or GL operations don't work  
**Fix:** Ensure event loop runs on dedicated thread with affine GL context, or create context in the same thread as `TryRenderFrame`  

### 2. get_proc_address Callback Not Searching All DLLs
**Location:** `MpvPlayer.cs:GlGetProcAddressCallback()` (lines 73-113)  
**Issue:** The callback searches `libGLESv2.dll` and `libEGL.dll`, but doesn't check if they're actually loaded from Chrome ANGLE or system. May return wrong function pointers.  
**Evidence:** No validation that ANGLE DLLs are the expected version (Chrome v149)  
**Fix:** Add DLL version check, verify exported functions match ANGLE signature  

### 3. mpv_render_context_create Missing Extended Param Format
**Location:** `MpvPlayer.cs:InitializeRendererD3D11()` (lines 1014-1024)  
**Issue:** The `mpv_render_param` array uses extended struct format in newer mpv, but P/Invoke may use wrong marshaling. Missing `MPV_RENDER_PARAM_INVALID` terminator check in some code paths.  
**Symptom:** Returns error -18 or -19 (MPV_ERROR_UNINITIALIZED or MPV_ERROR_NOT_IMPLEMENTED)  
**Fix:** Verify struct layout matches C declaration, ensure proper padding on 64-bit

### 4. Frame Not Displayed - Missing Avalonia Image Binding
**Location:** `MainWindow.axaml` + `MainWindow.Core.cs`  
**Issue:** `VideoFrameImage` Image control exists but may not have correct margin/alignment to fill the window area. May be obscured by StartPage or PlaybackBackground.  
**Symptom:** Video renders but is black or hidden  
**Fix:** Verify ZIndex of VideoFrameImage in AXAML, ensure margins match header+controls area  

### 5. WriteableBitmap Not Cleared on Dispose
**Location:** `MainWindow.Core.cs:_frameBitmap`  
**Issue:** Old reference to `_frameBitmap` removed but never re-added for render API path. Need to recreate bitmap on each frame size change.  
**Fix:** Implement bitmap cache/recreation in `FrameRendered` handler  

### 6. AngleGlContext.ReadPixels Uses GL_BGRA_EXT But mpv Expects RGB
**Location:** `AngleGlContext.cs:ReadPixels()` (line 244)  
**Issue:** Using `GL_BGRA_EXT` format but mpv's internal FBO may be RGBA or BGR. mpv applies flip_y but pixel format depends on video codec.  
**Symptom:** Colors wrong, blue tint, or black frame  
**Fix:** Query mpv for actual pixel format via `video-format` property, or test with RGBA first  

---

## MAJOR BUGS (May Cause Intermittent Failures)

### 7. eglGetProcAddress May Return Null
**Location:** `AngleGlContext.cs:GetGlDelegate()` (line 263)  
**Issue:** EGL extensions may not export all GL functions dynamically. `glReadPixels` should always exist but may fail on some ANGLE builds.  
**Symptom:** `InvalidOperationException: Could not load GL function`  
**Fix:** Catch exception, return dummy no-op delegate for graceful failure  

### 8. No Debug Verification of ANGLE DLLs
**Location:** `App.csproj` + deployment  
**Issue:** No verification that ANGLE DLLs (libEGL.dll, libGLESv2.dll) are actually deployed with the app. They're in `resources\libmpv-2_x86-64\` but may not copy to output.  
**Fix:** Add `<CopyToOutputDirectory>` in App.csproj or runtime check at startup  

### 9. PipPlayerService Mutes Audio
**Location:** `PipPlayerService.cs:Initialize()` (line 48)  
**Issue:** Calls `player.Mute(true)` but audio should be handled by primary player. This is correct, but mute state may not be synced.  
**Symptom:** PiP has no audio (expected, but user may think it's broken)  
**Fix:** This is intentional - just add comment clarifying audio comes from main window  

### 10. mpv_render_context_render Not Made Context-Current First
**Location:** `MpvPlayer.cs:TryRenderFrame()` (line 1144)  
**Issue:** Calls `mpv_render_context_render` but may not ensure ANGLE context is current on that thread immediately before the call.  
**Symptom:** Random -18 errors on some frames  
**Fix:** Call `_angleContext.MakeCurrent()` at the very start of TryRenderFrame, check return value  

---

## MINOR BUGS (Cleanup / Edge Cases)

### 11. Missing libmpv-2.dll Version Check
**Location:** App startup  
**Issue:** No check that libmpv-2.dll is the correct version (0.39.0 with render API)  
**Symptom:** Works on dev machine, fails on others with older DLL  
**Fix:** Add DLL version check at startup, show error if incompatible  

### 12. No Disposal of Native Handles in TryRenderFrame
**Location:** `MpvPlayer.cs:TryRenderFrame()` (lines 1127-1130)  
**Issue:** `fboPtr` and `flipPtr` are allocated every frame but not freed in finally block  
**Fix:** Add finally block to Marshal.FreeHGlobal both pointers  

### 13. AngleGlContext.Surface Ignored
**Location:** `AngleGlContext.cs` field `_eglSurface`  
**Issue:** Surface created but `eglSwapBuffers` may not be meaningful with NO_SURFACE.  
**Fix:** Always create 1x1 pbuffer surface (even if EGL_NO_SURFACE is accepted)  

### 14. PipWindow Not Finding PipVideoFrame
**Location:** `PipWindow.axaml.cs:UpdateFrame()`  
**Issue:** If `PipVideoFrame` Image control doesn't resolve from AXAML, UpdateFrame silently fails  
**Fix:** Throw InvalidOperationException in constructor if PipVideoFrame is null  

### 15. Missing Debug Logging
**Location:** All critical functions  
**Issue:** Insufficient telemetry to diagnose where failure occurs  
**Fix:** Add DebugLog calls at:
- AngleGlContext constructor (each EGL call)
- GlGetProcAddressCallback (return values)
- mpv_render_context_create (exact error codes)
- TryRenderFrame (pixel dimensions read back)
- FrameRendered event (whether fired)
- PipWindow.UpdateFrame (dimensions received)

---

## VERIFICATION CHECKLIST

- [x] libmpv-2.dll v0.39.0 in resources (shinchiro build with render API)
- [x] libEGL.dll + libGLESv2.dll deployed from Chrome v149
- [x] AngleInterop.cs eglGetProcAddress P/Invoke
- [x] MpvRender.cs mpv_render_context_* P/Invoke
- [ ] Build succeeds without errors
- [ ] App launches without critical exceptions
- [ ] libmpv-2.dll loads without error 126 (module not found)
- [ ] eglInitialize succeeds (v1.4 or 1.5)
- [ ] eglBindAPI(EGL_OPENGL_ES_API) succeeds
- [ ] eglChooseConfig returns >0 configs
- [ ] eglCreateContext succeeds (ES 3.0 or ES 2.0)
- [ ] eglMakeCurrent succeeds
- [ ] mpv_render_context_create returns 0 (success)
- [ ] mpv_render_context_render returns 0 (success)
- [ ] glReadPixels returns pixel data (non-empty byte array)
- [ ] FrameRendered event fires (non-null bitmap size)
- [ ] VideoFrameImage displays bitmap (visible in window)
- [ ] PiP Window displays frame (secondary player)

---

## RECOMMENDED DEBUG STRATEGY

### Phased Approach

**Phase 1: Verify ANGLE Context Creation**
- Add try-catch in InitializeRendererD3D11 with detailed error message
- Output each EGL call result to Debug.WriteLine
- Check cine_debug.log for "ANGLE: context created" message

**Phase 2: Verify mpv_render_context_create**
- Add explicit check for renderErr return value
- If -18: log "MPV_ERROR_UNINITIALIZED - vo=null conflict"
- If -4  : log "MPV_ERROR_INVALID_PARAMETER - API type mismatch"
- If -19: log "MPV_ERROR_NOT_IMPLEMENTED - bad DLL"

**Phase 3: Verify Frame Rendering**
- Add logging in TryRenderFrame at start/end
- Check w×h from dwidth/dheight properties
- Test with hardcoded 640x480 if properties are 0

**Phase 4: Verify Frame Display**
- Add OnPlayerFrameRendered logging in MainWindow.Core.cs
- Check WriteableBitmap dimensions match frame
- Verify VideoFrameImage.Visibility = true

**Phase 5: Verify PiP**
- Log entry in PipPlayerService.Initialize()
- Verify secondary player opens file
- Check PipWindow.UpdateFrame receives non-empty pixels

---

## FILE REFERENCES

- `src/Media/Implementations/mpv/AngleInterop.cs` - EGL P/Invoke
- `src/Media/Implementations/mpv/AngleGlContext.cs` - ANGLE context management
- `src/Media/Implementations/mpv/MpvRender.cs` - mpv render API P/Invoke
- `src/Media/Implementations/mpv/MpvPlayer.cs` - Render API initialization + frame rendering
- `src/Media/Implementations/mpv/MpvConfig.cs` - Render API options (vo=null)
- `src/App/UI/Views/MainWindow.axaml` + `MainWindow.Core.cs` - VideoFrameImage display
- `src/App/Application/Services/PipPlayerService.cs` - Secondary player
- `src/App/Application/Services/PipService.cs` - PiP coordination
- `src/App/UI/Screens/Dialogs/PipWindow.axaml` + `.cs` - PiP display

---

## RESOURCES DEPLOYMENT PATH

```
resources/libmpv-2_x86-64/
├── libmpv-2.dll       (117MB, v0.39.0)
├── libEGL.dll         (Chrome ANGLE)
└── libGLESv2.dll      (Chrome ANGLE)

Output/Debug/net10.0-windows/
├── App.exe
├── libmpv-2.dll       (must be here)
├── libEGL.dll         (must be here)
└── libGLESv2.dll      (must be here)
```

If DLLs missing from output, add in App.csproj:
```xml
<Content Include="..\..\resources\libmpv-2_x86-64\libEGL.dll">
  <Link>libEGL.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="..\..\resources\libmpv-2_x86-64\libGLESv2.dll">
  <Link>libGLESv2.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```