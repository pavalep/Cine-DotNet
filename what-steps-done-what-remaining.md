# Cine Project — Task Status

## 🔧 Setup — First Steps (Done!)

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

## 🔴 CURRENT ISSUE — Application Crash on Startup (NullReferenceException)

**Status:** ✅ **FIXED**

**Problem:** The application crashed with `NullReferenceException` on line 93 of `MainApp.cs` because `playerPanel.Resize` event was being subscribed before `playerPanel` was created.

**Root Cause:** In the `MainForm` constructor, `playerPanel.Resize += OnPlayerPanelResize;` was called before `InitializeUI()` which creates `playerPanel`.

**Fix:** Moved `playerPanel.Resize += OnPlayerPanelResize;` to after `InitializeUI();` call.

**Files changed:**
- `Cine.WinUI\MainApp.cs` — Reordered event subscription after UI initialization

**Additional fixes applied:**
- D3D11Renderer.cs — Added debug diagnostics that were later removed
- MfComInterop.cs — PreserveSig attributes already fixed in prior commits

---

## 🔴 CURRENT ISSUE — Basic UI Only (Not Feature-Complete)

**Status:** 🔄 **IN PROGRESS**

**Problem:** The current WinForms UI is a basic implementation with only essential controls. It does not match the feature set of the Python UI (window.py).

**What's currently implemented:**
- Basic WinForms layout with video panel, playlist sidebar, transport controls
- MediaFoundationPlayer integration with native D3D11 rendering
- Open file dialog and basic playlist

**What's missing (compared to Python UI):**
- Menu bar with File, Playback, View, Help menus
- Proper seek bar with time display
- Volume slider with mute toggle
- Speed control
- Subtitle track selection
- Audio track selection
- Fullscreen toggle with proper UI
- Drag and drop support
- Auto-hide UI in fullscreen mode
- Keyboard shortcuts (50+ bindings from Python)
- Chapter navigation
- Video filters (contrast, brightness, gamma, saturation)
- Proper status bar with playback state

**To implement:**
1. Design UI layout matching Python reference (window.py:1186-1345)
2. Implement all missing controls and their event handlers
3. Add keyboard shortcuts
4. Implement auto-hide UI for fullscreen mode

---

## Remaining ❌

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | Fix NullReferenceException in MainForm | ✅ Done | Moved playerPanel.Resize after InitializeUI() |
| 2 | Build with 0 errors | ✅ Done | Build succeeds with 0 errors, 0 warnings |
| 3 | Application runs without crash | ✅ Done | Exits cleanly with code 0 |
| 4 | Implement full UI (match Python) | 🔄 In Progress | Basic UI exists, needs full feature set |
| 5 | Implement NextChapter/PreviousChapter | Not started | Stubs exist in `MediaFoundationPlayer` |
| 6 | Implement video filters | Not started | Stubs exist, need GPU shader pipeline |
| 7 | Fullscreen auto-hide UI | Not started | Toggle works, UI doesn't auto-hide |
| 8 | Drag & drop support | Not started | Not implemented yet |
| 9 | Testing with various video files | Not started | Verify color accuracy, format detection |
| 10 | Screenshot UI integration | Not started | `TakeScreenshot` method ready, need UI button |

---

## Phase Progress

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1: Video Rendering | ✅ Complete | D3D11Renderer with GPU-accelerated frame presentation |
| Phase 2: Audio + Seeking | ✅ Complete | WASAPI audio output, duration tracking, seeking support |
| Phase 3: YUV→RGB Conversion | ✅ Complete | NV12→BGRA shader pipeline, auto-detection of format |
| Phase 4: UI Implementation | 🔄 In Progress | Basic WinForms UI, needs full feature set |
| Phase 5: Feature Completion | ⏳ Pending | Screenshot, chapters, filters, keyboard shortcuts |
| Phase 6: Testing & Polish | ⏳ Pending | Cross-format testing, performance optimization |