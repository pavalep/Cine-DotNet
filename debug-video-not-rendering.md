# Debug: video-not-rendering

Status: [OPEN]

## Symptoms
- App builds and runs
- MainWindow shows but video area is white (empty)
- PipWindow also shows white (no video)
- After DXGI render API fix: black screen instead of white (progress — swap chain works)

## Root Cause Trace

### Layer 1: D3D11 COM interop broken
**Status: CONFIRMED → FIXED**

- `D3D11VideoRenderer init failed: Value cannot be null (pUnk)` at `Marshal.GetObjectForIUnknown`
- `QueryInterface(IDXGIDevice)` returned S_OK but `dxgiDevice=0x0`
- **Root cause**: Custom `IUnknown` COM interface with `out IntPtr` returns null pointer despite S_OK
- **Fix**: Replaced device+QueryInterface chain with direct `CreateDXGIFactory1` P/Invoke, bypassing COM interop entirely
- Verified: `cine_d3d11.log` now shows swap chain created successfully

### Layer 2: Render context creation fails with -19
**Status: FIXED**

Even after creating a valid swap chain, `mpv_render_context_create` returned **-19** (`MPV_ERROR_INIT_FAILED`).

**Root Cause**: Hypothesis 5 was correct. `libmpv-2.dll` does not support the custom/experimental DXGI render API (`MPV_RENDER_API_TYPE_DXGI`). The standard mpv render API only supports OpenGL and Software.

**Fix**: Bypassed the DXGI render API entirely. Since `D3D11VideoHost.cs` creates a native Win32 child HWND anyway, we simply reverted `MainWindow.Core.cs` and `PipPlayerService.cs` to use the standard `InitializeRenderer(_childVideoHwnd)`. This passes the `wid` property to mpv, allowing it to natively render to the child window and manage its own D3D11 swap chain. The z-order and clipping behavior remains identical because it is still using the same Win32 child window.

## Hypotheses to test next (priority order):

1. **H1 (most likely)**: `D3D11CreateDeviceAndSwapChain` is either not exported from `d3d11.dll` on Win11, or the P/Invoke signature is wrong, or it creates a device that mpv doesn't accept.
   - **Fix**: Use `D3D11CreateDevice` (known-good P/Invoke) + `CreateDXGIFactory1` + `IDXGIFactory.CreateSwapChain` separately instead of the combined API.

2. **H2**: The device is a WARP (software) device, which mpv's DXGI render API rejects.
   - **Fix**: Check the feature level; ensure hardware device; add `D3D11_CREATE_DEVICE_VIDEO_SUPPORT`.

3. **H3**: The swap chain size (1x1) at init time causes mpv to fail.
   - **Fix**: Resize the swap chain to the actual HWND size before calling `mpv_render_context_create`.

### Layer 3: ANGLE OpenGL Rendering (Current)
**Status: FIXED**

Video is playing but rendering empty/black screens when using `mpv_render_context` with OpenGL and ANGLE (`libEGL.dll`).

**Root Cause**: `AngleGlContext` was creating a 1x1 pbuffer surface by default. When `MpvPlayer.TryRenderFrame` called `mpv_render_context_render` with FBO 0, it rendered into this 1x1 surface. OpenGL clipped the rendering and `glReadPixels(w, h)` to 1x1, meaning the returned frame buffer was empty/black (only 1 pixel was written).
**Fix**: Added `ResizeSurfaceIfNeeded` to `AngleGlContext` to dynamically recreate the EGL pbuffer surface whenever the video frame dimensions `w` and `h` exceed the current surface size. Called this in `TryRenderFrame` before `MakeCurrent()`.

## Build: clean (0 errors, 0 warnings)

## Remaining after video fix:
1. Aspect ratio not maintained
2. Overlay layering of controls/topbar on video
3. PiP window (secondary render context)
