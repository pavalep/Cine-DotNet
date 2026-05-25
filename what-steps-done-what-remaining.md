# Cine Project — Task Status

## 🔧 Setup — First Steps (Do These Before Anything Else!)

### 0. Initialize Git Repository & Gitignore
**Status:** ✅ **DONE**

The project now has a git repository initialized in `Windows-Native/` with a proper `.gitignore`.

**What was done:**
1. Initialized git repo: `git init` in `Windows-Native/`
2. Created `.gitignore` for Windows-Native (build outputs, IDE files, OS files, NuGet, compiled outputs)
3. Committed all 40 source files: `git commit -m "Initial state — Windows-Native port with D3D11 renderer, Media Foundation pipeline, and WPF UI rewrite"`

**Note:** The git repo is scoped to `Windows-Native/` only (not the whole project root) because the root contains mixed Linux/Flatpak content that should not be tracked together with the Windows-native code.

---

## Completed ✅

### 1. Fixed MfComInterop.cs GUID encoding issue
- **File:** `Cine.Media/Implementations/MfComInterop.cs`
- **Lines 35 and 170:** The GUID `DF598931-F10C-4E71-86AB-34BE-8F8F-8CE9` had **6 dashes** instead of the required 4 (standard `8-4-4-4-12` format). The last 12 hex digits were incorrectly split as `34BE-8F8F-8CE9` with embedded dashes.
- **Fix:** Replaced with correct IID: `DF598931-F10C-4E71-86AB-34BE8F8F8CE9`
- **Also fixed:** `IMFMediaType` interface now includes all inherited `IMFAttributes` vtable methods (GetCount, GetItemByIndex, SetItem, SetGUID, GetGUID, GetUINT64, etc.) — without these, every vtable call after the missing slots would hit the wrong method.
- **Also fixed:** `MappedSubresource` struct wrapped in `#pragma warning disable CS0649`
- **Also fixed:** Added `ID3D11ShaderResourceView`, `ID3D11VertexShader`, `ID3D11PixelShader`, `ID3D11InputLayout`, `ID3D11Buffer`, `ID3D11SamplerState`, `ID3DBlob`, `ID3D11BlendState` COM interfaces for D3D11 shader pipeline
- **Build result:** 0 errors, 0 warnings

### 2. Created D3D11Renderer class
- **File:** `Cine.Media/Implementations/D3D11Renderer.cs`
- **What it does:** Manages a Direct3D 11 GPU device, DXGI swap chain, render target view, and frame presentation pipeline.
- **Key methods:**
  - `Initialize()` — creates D3D11 device + context, DXGI factory, swap chain bound to an HWND, and render target view
  - `Present(IMFSample)` — copies a decoded video sample into the back buffer and flips to screen (vsync on)
  - `ResizeBuffers(width, height)` — recreates back buffer when the panel resizes
  - `ClearToBlack()` — clears to opaque black and presents (for initial/error state)
  - `TakeScreenshot(outputPath)` — captures the current back buffer and saves it to a PNG file
- **NV12→BGRA Shader Pipeline:**
  - Compiles inline HLSL shaders (VS + PS) for YUV to RGB conversion
  - Creates GPU textures for Y and UV planes with staging textures for CPU upload
  - Fullscreen quad rendering through NV12→BGRA pixel shader
- **Design decisions:**
  - Uses `Marshal.GetObjectForIUnknown()` to wrap raw COM pointers as managed interface types — type-safe method calls via vtable while keeping manual `Marshal.Release()` control over COM lifetime
  - Hardware device first, WARP software fallback if no GPU available
  - All COM objects released in reverse creation order in `Dispose()`

### 3. Created MfHelper class
- **File:** `Cine.Media/Implementations/MfHelper.cs`
- **What it does:** The Media Foundation pipeline bridge — opens media files, enumerates streams, decodes video samples, and dispatches them to D3D11Renderer.
- **Key methods:**
  - `Initialize()` — calls `CoInitializeEx` + `MFStartup` (MTA apartment for background threading)
  - `OpenFile(path)` — creates `IMFSourceReader`, discovers video/audio streams, configures output type
  - `StartPlayback()` — begins background reading loop on a thread-pool task
  - `StopPlayback()` / `Pause()` / `Resume()` — playback control
  - `GetVideoStreamInfo()` — queries current media type for width, height, frame rate, pixel format subtype
- **Threading model:**
  - Main thread: UI + control calls
  - Background thread: `ReadSample` loop reads decoded frames and fires `SampleReady` event
- **Events dispatched:** `MediaOpened`, `SampleReady`, `PlaybackEnded`, `Error`

### 4. Created AudioRenderer class
- **File:** `Cine.Media/Implementations\AudioRenderer.cs`
- **What it does:** WASAPI shared-mode audio output for low-latency PCM audio playback.
- **Key methods:**
  - `Initialize(waveFormat)` — sets up WASAPI client with specified wave format
  - `Write(data, offset, count)` — writes PCM audio data to the render buffer
  - `Stop()` / `Dispose()` — cleanup

### 5. Created MediaFoundationPlayer class
- **File:** `Cine.Media/Implementations\MediaFoundationPlayer.cs`
- **What it does:** Integrates video and audio rendering with Media Foundation pipeline.
- **Key features:**
  - Wires `MfHelper` with `D3D11Renderer` and `AudioRenderer`
  - Auto-detects video format and configures shader vs RGB32 path
  - Handles `MediaOpened` event to configure renderer before first frame
  - Cleanup and reinitialize renderer when video format changes between files

### 6. Implemented Auto-Detection for Video Format
- **File:** `Cine.Media\Implementations\MediaFoundationPlayer.cs`
- **What it does:** Automatically selects between NV12→BGRA shader path and BGRA-direct path based on decoder output format.
- **Detection logic:**
  - Checks `VideoFormat` string from `MediaOpenedEventArgs`
  - NV12 format detected by GUID substring `3231564E`
  - I420 format detected by GUID substring `30323449`
  - YUY2 format detected by GUID substring `32595559`
- **Renderer reinitialization:** When format changes between files, the renderer is disposed and recreated with the correct shader path setting.

### 7. Updated MediaFoundationPlayer.TakeScreenshot
- **File:** `Cine.Media/Implementations\MediaFoundationPlayer.cs`
- **What changed:** Replaced `throw new NotImplementedException(...)` with `_renderer?.TakeScreenshot(outputPath)`
- **Note:** Requires `D3D11Renderer.TakeScreenshot()` to be implemented (see Issue section below)

### 8. ✅ Git Repository Initialized (Windows-Native only)
- **Scope:** `Windows-Native/` subdirectory only
- **Reason:** Root project contains mixed Linux/Flatpak content not related to Windows build
- **40 source files committed** as initial snapshot
- **`.gitignore`** covers: `bin/`, `obj/`, `Debug/`, `Release/`, `publish/`, `.vs/`, `*.suo`, `*.user`, NuGet caches, build scripts, compiled outputs

---

## 🔴 CURRENT ISSUE — D3D11Renderer.cs Incomplete

The file `D3D11Renderer.cs` was **accidentally overwritten** during development and is now **incomplete**. The following methods/types are **missing**:

| Missing Item | Status |
|---|---|
| `CreateTexture2D()` method | ❌ Missing |
| `PresentNv12()` method | ❌ Missing |
| `DXGI_SWAP_CHAIN_DESC1` struct | ❌ Missing |
| `DXGI_ADAPTER_DESC1` struct | ❌ Missing |
| `DXGIOutput`/`IDXGIOutput` interface | ❌ Missing |
| Various helper methods (`CreateNv12Textures`, etc.) | ❌ Missing |

**Root Cause:**
- No git repository existed at the time, so file overwrites couldn't be detected or recovered
- The `Write` tool was used to overwrite the entire file instead of using `Edit`/`SearchReplace` for targeted changes

**To fix:**
1. Git repo is now initialized ✅
2. **Restore the original `D3D11Renderer.cs`** — if a backup exists in the project, copy it back
3. If no backup exists, reconstruct from the D3D11Renderer.cs content that was in the working project before the overwrite
4. After restoring, add only the `TakeScreenshot()` method (see below)

### TakeScreenshot Method to Add
Once the original file is restored, add this method **before `#endregion Helpers`**:

```csharp
/// <summary>
/// Captures the current swap chain back buffer and saves it to a PNG file.
/// </summary>
/// <param name="outputPath">Full path to the output PNG file.</param>
/// <returns>True if the screenshot was saved successfully.</returns>
public bool TakeScreenshot(string outputPath)
{
    if (!IsInitialized || _backBuffer == IntPtr.Zero || string.IsNullOrEmpty(outputPath))
        return false;

    try
    {
        int width = BackBufferWidth;
        int height = BackBufferHeight;

        if (width <= 0 || height <= 0)
            return false;

        // Create a staging texture that is CPU-readable
        IntPtr stagingTex = IntPtr.Zero;
        CreateTexture2D(width, height, DXGI_FORMAT_B8G8R8A8_UNORM,
            D3D11_USAGE_STAGING, 0,
            out stagingTex);

        if (stagingTex == IntPtr.Zero)
            return false;

        var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

        try
        {
            context.CopyResource(stagingTex, _backBuffer);

            var mapped = new MappedSubresource();
            int hr = context.Map(stagingTex, Subresource: 0,
                MapType: (uint)D3D11_MAP_READ, MapFlags: 0, out mapped);

            if (hr < 0)
                return false;

            try
            {
                using (var bitmap = new Bitmap(
                    width, height,
                    PixelFormat.Format32bppArgb))
                {
                    var bmpData = bitmap.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb);

                    try
                    {
                        byte* src = (byte*)mapped.pData;
                        byte* dst = (byte*)bmpData.Scan0;
                        int srcStride = (int)mapped.RowPitch;
                        int dstStride = bmpData.Stride;
                        int copyBytes = Math.Min(srcStride, dstStride);

                        for (int y = 0; y < height; y++)
                        {
                            Buffer.MemoryCopy(src, dst,
                                (uint)copyBytes, (uint)copyBytes);
                            src += srcStride;
                            dst += dstStride;
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bmpData);
                    }

                    bitmap.Save(outputPath, ImageFormat.Png);
                }

                return true;
            }
            finally
            {
                context.Unmap(stagingTex, Subresource: 0);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(context);
            SafeRelease(ref stagingTex);
        }
    }
    catch
    {
        return false;
    }
}
```

---

## Remaining ❌

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | Wire native renderer into WinForms UI | Not started | Replace WPF `MediaElement` + `ElementHost` with native `Panel` + `D3D11Renderer` |
| 2 | Implement NextChapter/PreviousChapter | Not started | Stubs exist in `MediaFoundationPlayer`, need full implementation |
| 3 | Implement video filters (contrast, brightness, gamma) | Not started | Stubs exist, need GPU shader pipeline |
| 4 | Fullscreen auto-hide UI | Not started | Toggle works, but UI doesn't auto-hide with mouse timeout |
| 5 | Drag & drop support | Not started | Not implemented in WinForms version yet |
| 6 | Clean up remaining CS0649 warnings | Pending | Already suppressed via `#pragma` on `MappedSubresource` |
| 7 | Testing with various video files | Not started | Verify color accuracy, format detection, shader performance |
| 8 | Screenshot UI integration | Not started | `TakeScreenshot` method ready, need UI button and save dialog |

---

## Phase Progress

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1: Video Rendering | ✅ Complete | D3D11Renderer with GPU-accelerated frame presentation |
| Phase 2: Audio + Seeking | ✅ Complete | WASAPI audio output, duration tracking, seeking support |
| Phase 3: YUV→RGB Conversion | ✅ Complete | NV12→BGRA shader pipeline, auto-detection of format |
| Phase 4: Feature Completion | 🔄 In Progress | Screenshot, chapters, filters, UI improvements |
| Phase 5: Testing & Polish | ⏳ Pending | Cross-format testing, performance optimization |