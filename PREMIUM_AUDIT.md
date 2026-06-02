# Cine Premium UX Audit & Execution Plan

## Phase A: Icon Quality & Alignment

### A1 — Circular button icon centering
**Issue**: Many `Path` icons inside `circular-menu` buttons (34×34) are not visually centered when the hover/active circle background appears. The `Stretch="Uniform"` combined with non-square viewBoxes causes the icon to float off-center.

**Fix**: Wrap all Paths in `Viewbox` with explicit `Stretch="Uniform"`, or ensure all icon `Geometry` viewBoxes are square (16×16 or 24×24). If using Material.Icons pack, they handle this natively.

### A2 — Replace hand-crafted SVGs with Material Icons
**Issue**: Current `.axaml` geometry paths are hand-crafted approximations. They lack the precision of Material Design icons (e.g. Play icon is `M8 5 V 19 L 19 12 Z` vs Material's `M8 5 V 19 L 19 12 Z` which is sharp but not perfectly padded).

**Fix**: Install `Material.Icons.Avalonia` v3.0.2 NuGet and replace all `Path` elements with `<materialIcons:MaterialIcon Kind="Play" />` for consistent quality and automatic centering.

### A3 — Icon toggle states not updating visually
**Issue**: Play/Pause, Mute/Unmute, Volume icons don't reliably swap when state changes because code-behind sets `Path.Data` via `TrySetIcon()` but the binding often overrides or the geometry reference is stale.

**Fix**: Use `materialIcons:MaterialIcon` and toggle `Kind` property in code-behind directly, or use DataTriggers in XAML.

### A4 — Aspect ratio combo shows blank on open
**Issue**: `ComboBoxItem Tag="-1" Content="Original" IsSelected="True"` sets the first item as selected in XAML, but when the flyout opens, the combo displays empty/no selection because the items aren't bound to a ViewModel `SelectedItem`.

**Fix**: Set `SelectedIndex="0"` explicitly in `SyncAspectRatioSelection()` and ensure combo has a proper `SelectedValue`.

### A5 — Volume slider drag thumb not updating icon live
**Issue**: Dragging volume slider doesn't show icon change (speaker→arcs→mute) until finger lifts.

**Fix**: Add `PointerPressed`/`PointerMoved` handlers on the slider to update the icon in real-time.

## Phase B: Interactions

### B1 — Double-tap on video to play/pause
**Issue**: No gesture handler on the video surface for double-tap. GTK/python reference has `gesture_click` with `button=1` and `n_presses=2`.

**Fix**: Add `PointerPressed` handler on `VideoHost` measuring time between taps. If <300ms between two presses, toggle play/pause.

### B2 — All popups/flyouts closable with Escape
**Issue**: OptionsMenu, Playlist, Shortcuts dialogs don't close on Escape key press.

**Fix**: Ensure flyouts have `OverlayDismiss="True"` and dialogs handle `KeyDown(Escape)` → `Close()`.

### B3 — Seek bar still not working
**Issue**: The `_seekUpdateTimer` reads `_viewModel.Duration` which calls `_player.Duration` → `GetDouble("duration")`. Duration might be misreported if property not observed.

**Fix**: Also observe `duration` property via `mpv_observe_property`, and use cached duration in main window to avoid native call on UI thread.

## Phase C: Animations & Polish

### C1 — Button hover transition
**Issue**: `Circular-menu` buttons have `transition:background 0.12s` but the visual transition is instant in Avalonia.

**Fix**: Add `Transitions` to button style with `TimeSpan.FromMilliseconds(120)` for `Background` property.

### C2 — Slider thumb animation on seek
**Issue**: Seek bar thumb jumps instantly when dragging. Should have a smooth follow animation.

**Fix**: Add `Transition` on `Margin` property with 100ms easing.

### C3 — Volume popover fade
**Issue**: Volume popover appears/disappears instantly without fade.

**Fix**: Add `OpacityTransition(0.15s)` to popup/popover style.

### C4 — Flyout open/close animation
**Issue**: Options menu flyout opens with no animation.

**Fix**: Add `Animations` to flyout content with scale+opacity keyframes.

## Execution Order

1. Install `Material.Icons.Avalonia` package
2. Fix icon centering (A1) — Viewbox wrap
3. Fix AspectRatio combo default (A4)
4. Add double-tap play/pause (B1)
5. Fix Escape closability (B2)
6. Fix icon toggle states (A3)
7. Fix seek bar duration (B3)
8. Add animations (C1-C4)
