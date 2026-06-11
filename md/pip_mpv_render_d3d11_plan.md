# PiP + MainWindow: MPV D3D11 Render API Integration Plan

**Goal:** Replace the hidden window + DWM thumbnail + clipping workaround with proper
mpv render API integration. mpv renders into a D3D11 texture that's composited as part
of Avalonia's normal rendering pipeline. Controls render naturally on top — no clipping,
no z-order fight, no DWM thumbnail.

**Reference:** [imgui demo with `render_dxgi.h`](https://gist.github.com/dragonflylee/5b73c0df5db85dba03488d592f1af23a)
- Uses `mpv_dxgi_init_params { d3dDevice, d3dSwapChain }`
- `MPV_RENDER_API_TYPE_DXGI` with `mpv_render_context_create()`
- Renders into the app's own swap chain

---

## Phase 1 — Add render_dxgi.h P/Invoke bindings

- New file: `src/Media/Implementations/mpv/MpvRender.cs`
- Bindings for:
  - `mpv_render_context_create`
  - `mpv_render_context_free`
  - `mpv_render_context_render`
  - `mpv_render_context_set_update_callback`
  - `mpv_render_context_update`
  - `mpv_render_param` struct
  - `mpv_dxgi_init_params` struct
  - `MPV_RENDER_API_TYPE_DXGI` constant
  - `MPV_RENDER_PARAM_API_TYPE`, `MPV_RENDER_PARAM_DXGI_INIT_PARAMS` constants
  - `MPV_RENDER_UPDATE_FRAME` flag
- Same pattern as existing `MpvInterop.cs`

**Depends on:** Nothing (new file)

---

## Phase 2 — Create D3D11 device/swap chain shared with Avalonia

- New file: `src/App/UI/Controls/Video/D3D11VideoRenderer.cs`
- Gets Avalonia's underlying `ID3D11Device` via:
  - `TopLevel.TryGetPlatformHandle()` → HWND
  - Access Avalonia's Skia `GRContext` → get `ID3D11Device` via `GetD3DDevice` or `QueryInterface`
- Creates a swap chain for the target HWND using the shared device
- Or: creates a shared `ID3D11Texture2D` that mpv renders into, and Avalonia draws

**Two sub-approaches:**
- **A: Shared device + per-window swap chain** — mpv renders into app's swap chain
- **B: Shared texture** — mpv renders into texture, app composites as overlay

**Approach A** is the imgui demo pattern and is simpler.

**Depends on:** Phase 1

---

## Phase 3 — Add render API path to MpvPlayer

- Modify `MpvPlayer.cs`:
  - Add `InitializeRendererD3D11(ID3D11Device, IDXGISwapChain)` — the render-API init
  - Existing `InitializeRenderer(IntPtr hwnd)` stays as `wid` fallback
  - Store `mpv_render_context` reference
  - On each mpv event wakeup, call `mpv_render_context_render()`
  - Thread-safe: mpv render API must be called from render thread, not core thread

**The render loop:**
1. mpv fires `mpv_on_update` callback → signals "new frame available"
2. App calls `mpv_render_context_update()` → gets flags
3. If `MPV_RENDER_UPDATE_FRAME` → call `mpv_render_context_render()`
4. Present the swap chain

**Depends on:** Phase 1, Phase 2

---

## Phase 4 — Wire PiP with render API (trial target)

- Modify `PipPlayerService.cs`:
  - Remove hidden window + DWM thumbnail setup
  - Instead: use `D3D11VideoRenderer` to get a swap chain for PiP HWND
  - Call `MpvPlayer.InitializeRendererD3D11(device, swapChain)`
  - No DWM thumbnail, no clip rect, no `UpdateThumbnailRect`

**PiP controls:** Now render naturally on top of the video surface.

**Depends on:** Phase 3

---

## Phase 5 — Migrate MainWindow to render API (production)

- Same pattern as Phase 4 but for MainWindow
- Remove `D3D11VideoHost` (hidden window)
- Remove `DwmThumbnailManager` entirely
- MainWindow controls render on top naturally

**Depends on:** Phase 4 validation

---

## Phase 6 — Cleanup

- Remove:
  - `D3D11VideoHost.cs`
  - `DwmThumbnailManager.cs`
  - `PipOverlayWindow.axaml`/`.cs` (already deleted)
  - Hidden window code in `PipPlayerService`
  - `SyncThumbnailRect`, `UpdateDwmClipRect`, `OnClipRectNeeded` in PipWindow
  - All `MpvConfig` low-quality PiP profile (quality is now per-render, not per-instance)

**Depends on:** Phase 5

---

## Risks & notes

- **Avalonia's D3D11 device access** — internal API, may change. Need to check `Avalonia.DirectX` namespace or `SkiaSharp.SKColorSpace` interop
- **Threading** — mpv render API requires `mpv_render_context_render()` on the render thread, but mpv commands on the core thread. Need a lock or queue.
- **Swap chain resize** — PiP resize must resize the swap chain too
- **Fallback** — `wid` path stays as fallback for systems without D3D11 support

## Implementation order

```
Phase 1 (MpvRender.cs P/Invoke)
  └─> Phase 2 (D3D11VideoRenderer)
       └─> Phase 3 (MpvPlayer render-API path)
            └─> Phase 4 (Wire PiP — trial)
                 └─> Phase 5 (Wire MainWindow — production)
                      └─> Phase 6 (Cleanup)
```
