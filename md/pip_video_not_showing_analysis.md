# PiP Video Not Showing — Analysis Document

## Problem

Video renders correctly in MainWindow but **does not appear** in the PiP (Picture-in-Picture) window. The PiP overlay controls (top bar, seek bar, play/pause button) render and function correctly, but the video area beneath them is blank/dark.

## Architecture

The app uses a **two-window PiP architecture**:

```
┌─────────────────────────────┐   ← Topmost, Transparent, Z-order ABOVE PipWindow
│ PipOverlayWindow            │
│  - Top bar (48px)           │
│  - Center play/pause        │
│  - Bottom bar (seek+mute)   │
│  - Auto-hide after 3s       │
└─────────────────────────────┘

┌─────────────────────────────┐   ← Opaque, Background="#FF0D0D0D"
│ PipWindow (video only)      │
│  - DWM thumbnail fills full │
│    window rect              │
│  - LoadingOverlay           │
│  - 8 resize edge handles    │
└─────────────────────────────┘
```

**Position sync:** PipWindow runs a 100ms DispatcherTimer that calls `_overlay.SyncGeometry(Position, Width, Height)` to keep the overlay aligned.

**Auto-hide:** When overlay is hidden (`_overlayVisible=false`), PipWindow's `OnPipWindowPointerMoved` re-shows it. When overlay is visible, its own hover timer (3s) hides it.

## Current State of All Files

### [PipWindow.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml)

```xml
<Window ... Background="#FF0D0D0D" PointerMoved="OnPipWindowPointerMoved">
    <Window.Styles>
        <Style Selector="Border.pip-container">
            <Setter Property="CornerRadius" Value="12" />
            <Setter Property="BoxShadow" Value="0 6 32 0 #AA000000, 0 0 0 1 #25FFFFFF" />
            <Setter Property="ClipToBounds" Value="True" />
        </Style>
        <Style Selector="Border.pip-resize-edge">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="IsHitTestVisible" Value="True" />
        </Style>
    </Window.Styles>
    <Grid>
        <Border Classes="pip-container" ZIndex="0">
            <Grid>
                <Border x:Name="VideoArea" Background="Transparent"
                        ClipToBounds="True" CornerRadius="12" />
                <Border x:Name="LoadingOverlay" Background="#99000000"
                        IsVisible="False" IsHitTestVisible="False" CornerRadius="12" />
            </Grid>
        </Border>
        <!-- 8 resize handles at ZIndex="30" -->
    </Grid>
</Window>
```

Key observation: `VideoArea.Background="Transparent"` — no opaque surface for DWM to composite into. Window itself has `Background="#FF0D0D0D"` but DWM compositing in Av＠Valonia may not work the same as raw Win32.

### [PipWindow.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml.cs)

- Creates `PipOverlayWindow` on demand via `ShowOverlay()`
- Overlay creation deferred 100ms after `OnOpened`
- `SyncThumbnailRect()` calls `_dwmManager.UpdateTarget(id, 255, true, 0, 0, w, h)` — full window
- `EnableDwmMirror(manager)` — registers DWM thumbnail target
- `OnPipWindowPointerMoved` — triggers `ShowOverlay()` when `_overlayVisible` is false
- `DestroyOverlay()` — called on close

### [PipOverlayWindow.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipOverlayWindow.axaml)

```xml
<Window ... Topmost="True" TransparencyLevelHint="Transparent"
         Background="Transparent" IsHitTestVisible="True">
    <Grid Background="Transparent">
        <!-- Top bar Z10, Center play Z10, Bottom bar Z10, Badge Z20 -->
    </Grid>
</Window>
```

### [PipOverlayWindow.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipOverlayWindow.axaml.cs)

- Full control logic: play/pause, seek, mute, pin, expand, close
- `ShowControls()` sets `Opacity=1, IsHitTestVisible=true`
- `HideControls()` sets `Opacity=0, IsHitTestVisible=false`
- `SyncGeometry(position, w, h)` aligns to PipWindow
- Events forwarded to PipWindow via C# events

### [PipService.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs)

- Creates PipWindow, calls `Show()`, wires events
- Calls `EnableDwmMirror(_dwmManager)` after Show()
- Calls `ShowAllControls()` / `StartHoverTimer()`
- Logs confirm PiP starts successfully with valid HWND

### [MainWindow.Pip.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Pip.cs)

- On entering PiP: sets `_videoHost.IsVideoSurfaceVisible = false` (hides main window thumbnail)
- On exiting PiP: sets `_videoHost.IsVideoSurfaceVisible = true`
- Passes filename, aspect ratio, play state to PipWindow

### [DwmThumbnailManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Video/DwmThumbnailManager.cs)

- Uses `DwmRegisterThumbnail(dest, src, out id)` native API
- UpdateTarget applies `DWM_TNP_VISIBLE | DWM_TNP_OPACITY` flags + optional `DWM_TNP_RECTDESTINATION`
- DWM log confirms: thumbnail registered (id=2, thumbId=0x2), UpdateTarget called with visible=True, rect=(0,0,w,h)
- `hr=0x0` (S_OK) on all DWM calls

### [MainWindow.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Views/MainWindow.axaml) (Reference)

```xml
<Window ... Background="#FF0C0C0E" TransparencyLevelHint="None" ...>
```

MainWindow has `TransparencyLevelHint="None"` — no transparency. DWM composites into a standard opaque Win32 window surface. All controls float in the same Grid via VerticalAlignment.

## DWM Log From Last Run

```
[12:26:37.702] DwmRegisterThumbnail dest=0x1707B0 src=0x140298 hr=0x0 thumbId=0x2
[12:26:37.703] DwmUpdateThumbnailProperties(init) hr=0x0
[12:26:37.703] RegisterTarget OK: id=2 thumbId=0x2
[12:26:37.705] UpdateTarget id=2 visible=True opacity=255 rect=(0,0,780,438) hr=0x0
[12:26:37.765] UpdateTarget id=2 visible=True opacity=255 rect=(0,0,780,438) hr=0x0
```

All DWM operations returned `hr=0x0` (success). The thumbnail IS registered and updated. The video source HWND is correct.

## PiP Log From Last Run

```
[12:26:37.586] EnterPip start
[12:26:37.587] Creating PipWindow...
[12:26:37.701] Show() returned
[12:26:37.701] SourceHwnd=0x140298
[12:26:37.756] EnterPip success
```

No exceptions. PipWindow created, shown, DWM mirror enabled.

## Hypotheses

### Hypothesis A — `Background="Transparent"` on VideoArea prevents DWM compositing

DWM thumbnails in Avalonia's Skia rendering model may require an opaque backing surface inside the window. The PipWindow itself has `Background="#FF0D0D0D"` (opaque), but the `VideoArea` Border (the actual content area) has `Background="Transparent"`. DWM might need the destination HWND's client area to have an opaque region to composite into. The `BoxShadow` and `ClipToBounds` on `pip-container` may also interact with DWM compositing in unexpected ways.

**Evidence:** MainWindow works with `TransparencyLevelHint="None"` and an opaque background. Previous attempts with `TransparencyLevelHint="Transparent"` failed consistently.

**Test:** Make `VideoArea.Background="#FF0D0D0D"` (opaque).

### Hypothesis B — `ClipToBounds="True"` on pip-container clips DWM rendering

DWM composites at the HWND level, outside Avalonia's clip tree. The `ClipToBounds="True"` combined with `CornerRadius="12"` might cause the Skia render target to allocate a clipped surface that doesn't match the DWM destination rect, causing the thumbnail to render in a region Avalonia considers "clipped out."

**Evidence:** DWM rect is (0,0,780,438) — full window. But pip-container clips to rounded corners.

**Test:** Temporarily remove `ClipToBounds` and `CornerRadius` from pip-container.

### Hypothesis C — Overlay window steals focus/rendering priority

The `PipOverlayWindow` with `Topmost="True"` creates on top of PipWindow. Even though it's transparent, it might interfere with DWM's thumbnail painting to the underlying window. The overlay's input handling (`IsHitTestVisible="True"`) could block DWM compositing to the window beneath.

**Evidence:** This is a two-window approach. DWM thumbnails are per-HWND.

**Test:** Temporarily disable overlay creation and check if video shows in bare PipWindow.

### Hypothesis D — `_videoHost.IsVideoSurfaceVisible = false` stops all DWM compositing

When entering PiP, MainWindow sets `_videoHost.IsVideoSurfaceVisible = false`, which calls `UpdateTarget` with `visible=false` on the **main window's** thumbnail. But the PipWindow creates a **separate** thumbnail (id=2), which is set to `visible=true`. These are independent. However, if `IsVideoSurfaceVisible` on the VideoHost also toggles something in mpv's rendering pipeline (not just DWM), the source might stop producing frames entirely.

**Evidence:** Unclear — need to check if `IsVideoSurfaceVisible=false` affects mpv's render loop or only DWM visibility.

**Test:** Check if audio still plays during PiP (proves mpv is running). Check if DWM source HWND is still valid.

## Key Test Actions

1. **Make VideoArea opaque:** Change `VideoArea.Background="Transparent"` → `"#FF0D0D0D"`
2. **Remove rounded corners:** Comment out `ClipToBounds` and `CornerRadius="12"` on pip-container
3. **Disable overlay:** Comment out `ShowOverlay()` in `OnOpened` to test bare PiP window
4. **Verify source:** Check if mpv hidden window is still producing frames (audio plays)
5. **Check source thumbnail:** Verify `_dwmManager.SourceHwnd` is not IntPtr.Zero when PiP is active
6. **Remove resize handles:** They sit at ZIndex=30 on top of the video container — verify they don't occlude the DWM area
7. **Try with TransparencyLevelHint="None":** Like MainWindow — no transparency at all
