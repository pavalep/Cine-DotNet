# Debug Session: video-transparent

Status: [OPEN]

Symptom:
- When a video is loaded (and especially when going fullscreen), the video area becomes transparent (desktop shows through).

Expected:
- Video surface stays opaque (black background while loading, then frames).

Repro Steps:
1) Launch app
2) Open any video
3) Toggle fullscreen (optional but makes it worse)
4) Observe video area becomes transparent

Hypotheses (falsifiable):
- H1: The native child HWND (D3D11VideoHost) becomes visible before it has painted/received WM_PAINT, so it visually exposes the desktop.
- H2: The child HWND is created/recreated during fullscreen and momentarily loses its parent/size (bounds go 0x0), causing a transparent region until the next paint.
- H3: D3D11 swapchain Present/ResizeBuffers isn’t happening after fullscreen/resize, so the surface stays uninitialized and appears transparent.
- H4: Visibility toggles (IsVideoSurfaceVisible / IsVisible) are out of sequence around Open/Fullscreen, showing the native surface too early.
- H5: Window composition/transparency settings are re-enabled at runtime during fullscreen (TransparencyLevelHint/ExtendClientArea), making the whole top-level transparent.

Evidence Plan:
- Start debug server and capture runtime events for:
  - VideoHost: bounds, parent HWND, child HWND creation, show/hide state, WM_PAINT counts
  - MainWindow: OnOpened, OnMediaOpened, OnPlaybackStateChanged, fullscreen toggle, SizeChanged, effective background/transparency flags
  - Renderer: initialization, resize calls, present calls (high level only)

Verification:
- Compare pre-fix vs post-fix logs:
  - Child HWND should paint black immediately on visibility.
  - Fullscreen should trigger resize + paint, and video area should remain opaque.

