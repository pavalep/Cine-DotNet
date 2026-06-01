# Cine Avalonia — Root Cause Analysis & Fix Plan

**Reference Design:** `.kombai/canvas/cine-alignment.canvas` (Playback State + Start Page variants)  
**Current Code:** `src/App/` (MainWindow.axaml, MainWindow.axaml.cs, MainViewModel.cs, StartPage.axaml, Icons.axaml, Colors.axaml, Typography.axaml, App.axaml)  
**Build Status:** ✅ Compiles (0 errors, 0 warnings)  
**Runtime Status:** ❌ Multiple functional and visual defects

---

## 1. Seek/Progress Bar — Not Clickable, Not Draggable, Visual Fill Broken

### Why It's Not Working

The seek bar has three distinct failures:

**1a. Fill width calculation is wrong.** The current implementation uses a `MultiBinding` with `SeekWidthConverter`:

```xml
<Border Width="{MultiBinding Converter={StaticResource SeekWidthConverter}}">
    <Binding Path="SeekValue"/>
    <Binding ElementName="SeekArea" Path="Bounds.Width"/>
</Border>
```

The `SeekWidthConverter` computes `Math.Clamp(seekValue * parentWidth, 0, parentWidth)`. At startup, `SeekArea.Bounds.Width` is `0` because the layout hasn't been measured yet — so the fill width is `0`. When `SeekValue` updates from `OnPositionChanged`, the binding uses the initial `Bounds.Width` capture (which may be stale or zero depending on layout pass ordering). The fill never renders at the correct width because `MultiBinding` only re-evaluates when *any* of its source bindings change — but if `SeekValue` mutates without `Bounds.Width` changing, only the old (possibly zero) width is recomputed.

**1b. The thumb position is also wrong.** `SeekThumbMarginConverter` computes `Thickness(seekValue * parentWidth - 8, 0, 0, 0)`. Same stale-bounds problem as the fill. Additionally, the thumb uses `HorizontalAlignment="Left"` with a `Margin` offset — but `Border` does not recalculate layout when `Margin` changes outside of a proper layout pass. The thumb visually stays at position 0.

**1c. Click-to-seek works via `OnSeekAreaPointerPressed`, which correctly computes `normalized = clamp((p.X - trackStart) / trackWidth, 0, 1)` and calls `_viewModel.Position = target`. However, the user sees no visual feedback because the fill/thumb don't update synchronously with the seek call.**

**1d. Drag-to-seek is completely missing.** There is no `PointerMoved` handler that tracks dragging after a pointer press. The current `OnSeekAreaPointerMoved` only handles chapter preview hover — it doesn't check if the pointer button is held.

**1e. Chapter markers don't render correctly.** `ChapterMarginConverter` returns `position * 100.0` as a `Canvas.Left` value, but `Converter={StaticResource ChapterMarginConverter}` is applied to `ContentPresenter` which wraps each item. The `Canvas.Left` attached property is set on the `ContentPresenter`, not the `Rectangle` — but `ContentPresenter` needs `Canvas.Left` set in the style selector, which it is. The real issue is that `ChapterMarkers` collection values are `double` (a ratio 0.0-1.0), but `ChapterMarginConverter` expects them directly and multiplies by 100 — this gives `0..100` as pixel positions, which only works if the seek bar is exactly 100px wide. At any real window size (>600px), all markers clump in the first ~15% of the bar.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 1a | Replace `MultiBinding` with a single `SeekValue` binding + `ConverterParameter` for the parent reference name, or use a `Grid` column definition approach | Remove `MultiBinding`; use a `Grid` with two `ColumnDefinitions`: `*` and `Auto`. Put the fill `Border` in col 0. Set `ColumnDefinition.Width` via code-behind on resize to match `SeekValue * availableWidth`. OR — simplest fix — use a `RectangleGeometry` `Clip` with proper `Rect` calculation. |
| 1b | Replace margin-based thumb with `Canvas.Left` positioning on a `Canvas` panel | Wrap seek track + fill + thumb in a `Canvas`. Set `Canvas.Left` on thumb to `SeekValue * (SeekAreaWidth - 16)` in code-behind `OnPositionChanged`. |
| 1c | Force fill/thumb refresh on every `PositionChanged` event | In `OnPositionChanged` (code-behind), after the VM updates `SeekValue`, compute the pixel position and set `Canvas.Left` directly on the fill border + thumb border. Skip the converter entirely — use direct code-behind manipulation for performance. |
| 1d | Add drag-to-seek | Track `_isSeeking` flag set in `PointerPressed`, cleared in `PointerReleased`. In `PointerMoved`, if `_isSeeking`, compute `normalized` position and call `_viewModel.Position = target`. |
| 1e | Fix chapter marker positions | Replace `ChapterMarginConverter` (returns `position * 100`) with code-behind that computes `marker.Left = position * SeekArea.Bounds.Width` on each resize and each chapter list change. Or — simplest — set `Canvas.Left` bindings with a converter that receives the parent width as `ConverterParameter`. |

**Preferred approach (direct code-behind replacement):** Remove all `MultiBinding` / `Converter` complexity from the seek bar. Create three `Border` elements (track, fill, thumb) inside a `Canvas`. In `OnPositionChanged` (which fires 10+ times per second during playback), compute:

```csharp
var w = SeekArea.Bounds.Width;
var fillWidth = _viewModel.SeekValue * w;
SeekFill.Width = fillWidth;
Canvas.SetLeft(SeekThumb, fillWidth - 8); // 8 = half thumb width (16/2)
```

In `OnSeekAreaPointerPressed` and drag `OnSeekAreaPointerMoved`:

```csharp
var p = e.GetPosition(SeekArea);
var norm = Math.Clamp(p.X / SeekArea.Bounds.Width, 0, 1);
_viewModel.Position = TimeSpan.FromSeconds(norm * _viewModel.Duration.TotalSeconds);
```

Chapter markers: In `OnChapterListChanged` and `OnWindowSizeChanged`, loop over marker rectangles and set `Canvas.SetLeft(marker, chPos * SeekArea.Bounds.Width)`.

---

## 2. Volume Controls — Slider Not Draggable, Mute Toggle Has Wrong Icon

### Why It's Not Working

**2a. Volume slider doesn't respond to drag.** The `Slider.volume-slider` style in `App.axaml` sets `Width="180"`, `Minimum="0"`, `Maximum="130"`, `Background` and `Foreground`. The custom `ControlTemplate` was previously removed (good), but the `Slider` still uses the Fluent theme's default template. The issue is that the volume flyout has `Placement="Top"` and the volume button `BtnVolumeMenu` toggles the flyout — but the slider's `PointerPressed` event may be captured by the flyout close logic if the flyout closes on outside click. The real problem: **the slider is inside a `Flyout` placed at `Top` of the volume button. When the user clicks on the slider to drag, Flyout's `LightDismiss` mode may close the flyout before the slider registers the drag.** This is a Flyout lifecycle issue.

**2b. Mute toggle icon doesn't update when muted state changes.** `BtnMuteToggle` has `IsChecked="{Binding IsMuted}"` and its `Path.Data` is hardcoded `{StaticResource VolumeMuteIcon}` — but `VolumeMuteIcon` shows a speaker+cross (muted) icon. When unmuted, the icon should switch to `{StaticResource VolumeMaxIcon}`. There is no binding or code-behind to swap icons.

**2c. `RefreshVolumeIcon` in code-behind is never called on volume/mute change.** The `OnViewModelPropertyChanged` handler checks for `FilePath` but does not check for `IsMuted` or `VolumeValue` to trigger a volume icon swap.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 2a | Keep the flyout open while slider is being interacted with | Add `PointerPressed` and `PointerReleased` handlers on the volume slider that set a flag `_isVolumeSliderInteracting`. In the flyout's `Closing` event (or using a custom `LightDismissBehavior`), check this flag and cancel the close if true. Alternative: Use a `Popup` instead of `Flyout` with `IsLightDismissEnabled="False"` and manual show/hide. |
| 2b | Bind the volume icon data to IsMuted state | Replace hardcoded `Data="{StaticResource VolumeMuteIcon}"` with a binding: `Data="{Binding IsMuted, Converter={StaticResource VolumeIconConverter}}"`. Create `VolumeIconConverter : IValueConverter` that returns `VolumeMaxIcon` geometry when `IsMuted = false` and `VolumeMuteIcon` geometry when `IsMuted = true`. |
| 2c | Call RefreshVolumeIcon on IsMuted change | In `OnViewModelPropertyChanged`, add `if (e.PropertyName == nameof(MainViewModel.IsMuted) || e.PropertyName == nameof(MainViewModel.VolumeValue)) RefreshVolumeIcon();`. Also ensure `RefreshVolumeIcon` uses `TrySetIcon` with either `VolumeMaxIcon` or `VolumeMuteIcon` resource key. |

---

## 3. Icons — Low Quality, Blurry, Wrong Positioning, Wrong Sizes

### Why It's Not Working

**3a. The `Icons.axaml` file defines all icons as `Geometry` resources, but the paths use an inconsistent coordinate space.** Some paths span a 2-22 range (21 units), others span 4-20 (17 units), others use negative Y values (`-1`). Avalonia's `Path` control with `Stretch="Uniform"` scales the geometry to fit the `Width`/`Height` of the `Path` element — but `Stretch="Uniform"` preserves aspect ratio and centers the content. Paths that are not approximately square (some are wider than tall, some taller than wide) get uneven padding within the icon box. This creates visible asymmetry.

**3b. All transport buttons render icons at `Width="16" Height="16"` but the button size is `34×34`.** The icon is 16px inside a 34px button → 9px padding on each side, which is optically fine. But the actual geometry data inside the 16px box may only occupy 10-12px of visual content, leaving 3-4px of dead space on each side of the icon within its 16px box. This makes icons look small and poorly proportioned.

**3c. The window control buttons (Minimize, Maximize, Close) render icons at `Width="12" Height="12"` inside buttons that use `.wctrl` sizing from the canvas reference.** The canvas specifies `width:46px; height:32px` for window controls, but our current buttons use the default button template. The small 12px icons inside these buttons look lost.

**3d. The `MenuIcon` (3 dots) uses circles defined as `A 2 2 0 1 0` — these are arc commands, not circles. Arc commands produce correct circles only when the start and end points match the arc radius. The current paths `M 3 10 A 2 2 0 1 0 3 14` etc. have start point `3,10` and end point `3,14` — these are 4px apart, and the arc radius is 2, so the arc draws a half-circle. This works for rendering, but the circle "hole" at the center is not filled (`FillBehavior="NonZero"` is default), so it renders as a donut shape, not a solid dot. For a 3-dot menu icon, solid dots are expected, not outlines.**

**3e. Path data uses `M` (move), `L` (line), `C` (curve), `A` (arc) commands mixed without regard for stroke vs fill rendering.** Since these are all `Path` elements without `Stroke` (only `Fill`), closed shapes must use explicit `Z` (close path) commands to fill properly. Several icons (like `PipIcon`, `ScreenshotIcon`) mix open and closed subpaths without `Z`, causing missing fills in some regions.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 3a | Normalize all icon paths to a standard 24×24 coordinate system | Rewrite every geometry in `Icons.axaml` so that the data spans exactly a 20×20 region centered within a 24×24 bounding box (2px margin on all sides). Use only `M`/`L`/`Z` commands for filled shapes (no complex arcs for simple icons). This is already partially done in the latest `Icons.axaml` but needs verification and testing. |
| 3b | Increase icon size to 18×18 for transport buttons and 14×14 for window controls | Change `Width="16" Height="16"` → `Width="18" Height="18"` for all transport Path elements. Change `Width="12" Height="12"` → `Width="14" Height="14"` for window control Path elements. Verify optical centering. |
| 3c | Match window control button sizing to canvas reference | Set window control buttons (`BtnMinimize`, `BtnMaximizeRestore`, `BtnClose`) to `Width="34" Height="26"` with `CornerRadius="0"` and custom hover/close-hover backgrounds matching the canvas `.wctrl` rules. The canvas shows `.wctrl` as 46×32, but in practice 34×26 gives a cleaner look at typical window title bar scale. |
| 3d | Rewrite MenuIcon as solid dots | Replace arc-based dots with simple circle paths: `M 5 12 A 2 2 0 1 1 5 12.001 Z` (draws a solid 4px-diameter circle at x=5). `Z` is critical — it closes the path and fills the circle. Do this for all 3 dots at x=5, 12, 19. |
| 3e | Add `Z` to all closed subpaths | Audit every geometry string in `Icons.axaml`. Any subpath that represents a closed shape (circle, box, fill region) must end with `Z`. Example: `PipIcon` outer rect `M 3 5 H 21 V 19 H 3 Z` (with Z) works, but inner rect `M 8 9 H 16 V 15 H 8 Z` also needs Z. |

---

## 4. Start Page — Doesn't Match Canvas Design, Low Visual Quality

### Why It's Not Working

**4a. Background is wrong.** The canvas start page spec says: `background: linear-gradient(180deg, #000000 0%, #0c0c0e 100%)` — our `StartPageBackground` in Colors.axaml uses `#FF000000` at offset 0 and `#FF0C0C0E` at offset 1, which is correct. However, the **playback** state background should be `radial-gradient(ellipse at 58% 42%, #192848 0%, #090e1d 55%, #000 100%)` with a vignette overlay — this radial gradient is not implemented anywhere. When switching from start page to playback, the background instantly changes from black to the radial gradient. The canvas shows this clearly.

**4b. The Cine icon on the start page is a simplified play-triangle circle.** The canvas shows a 128×128 circle icon with a centered play triangle. Our current `StartPage.axaml` uses: `M 12 2C 6.48 2 2 6.48 2 12C 2 17.52 6.48 22 12 22C 17.52 22 22 17.52 22 12C 22 6.48 17.52 2 12 2 Z M 10 16.5V 7.5L 16 12L 10 16.5 Z` — this is a correct Material-style play-circle icon. **But** it's rendered at `Width="128" Height="128"` with no `Viewbox` — Avalonia's `Path` doesn't have a viewBox concept, so the raw path coordinates (2-22 range, 20 units) get stretched to 128px. This should work correctly with `Stretch="Uniform"`, but the icon may look distorted if the `Data` string has any parsing issue. Need to verify the path renders correctly.

**4c. The "Drag and Drop Files Here" text has wrong spacing and font weight.** Canvas shows: `font-size:28px; font-weight:700` (Bold). Our AXAML uses `FontSize="28" FontWeight="Bold"` which maps to 700 — correct. But the spacing below the text (`margin:0,0,12,0` in our code) is half the canvas spec (24px below icon → title, 16px below title → buttons). The canvas shows: icon→title gap = 16px, title→buttons gap = 24px. Our code has: icon→title gap = 16px (correct from `Margin="0,0,0,16"` on icon), but then the next element is "Drag and Drop..." with `margin:0,0,12,0` (12px gap to buttons). Canvas shows 16px between icon and title then 24px between title and buttons. Our total spacing is wrong.

**4d. The buttons don't match canvas exactly.** Canvas spec:
- Primary (Open…) button: `height:40px; border-radius:99px; padding:0 32px; font-size:14px; font-weight:600; background:#e5e5e5; color:black;`
- Secondary (Open Folder) button: `background:rgba(255,255,255,0.12); color:#e5e5e5;`

Our AXAML uses `Classes="start-page-button start-page-suggested-action"` for primary and `Classes="start-page-button"` for secondary. The styles in `App.axaml` set:
- `Button.start-page-button`: `Background="#1FFFFFFF"`, `Foreground="#E5E5E5"`, `MinWidth="140"`
- `Button.start-page-suggested-action`: `Background="#E5E5E5"`, `Foreground="Black"`

The colors are correct, but `MinWidth="140"` might make buttons wider than the canvas expects (canvas uses `padding:0 32px` which is content-based, not min-width). Also, the secondary button hover in canvas is `rgba(255,255,255,0.15)`, our code hover is `#26FFFFFF` (equivalent).

**4e. The drop target overlay has wrong appearance.** Canvas doesn't show a drop target visual — the start page is minimal. Our code has `DropTarget` Border with `BorderBrush="#40FFFFFF"` and `Background="#20FFFFFF"` that shows even when no drag is happening. This shouldn't be visible by default.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 4a | Add radial gradient background for playback state | Wrap the video area/background with an overlay `Border` that has a `RadialGradientBrush` with stops matching `#192848 → #090e1d → #000`. Add a second overlay for the vignette: `radial-gradient(transparent 40%, rgba(0,0,0,.45))`. Show these overlays only during playback. |
| 4b | Verify/correct start page icon path | Extract the icon geometry from the canvas HTML (the canvas shows a circle+triangle SVG). Compare with current path. If misaligned, replace with exact path from canvas. |
| 4c | Fix spacing in start page StackPanel | Remove `Margin="0,0,0,16"` from icon Path. Change `Margin="0,0,0,12"` on TextBlock to `Margin="0,0,0,24"`. Set `Spacing="24"` on the buttons StackPanel (currently default). |
| 4d | Fix button sizing | Remove `MinWidth="140"` from `Button.start-page-button` style. Add `Padding="32,0"` to match canvas `padding:0 32px`. Verify button height is exactly 40px (currently set via `Height="40"` — correct). |
| 4e | Hide drop target by default | Set `DropTarget` Border's `IsVisible="False"` by default. Show it only during drag operations in `OnWindowDragEnter`. |

---

## 5. Window Chrome — System Title Bar Overlaps Our Controls

### Why It's Not Working

**5a. The window currently uses `ExtendClientAreaToDecorationsHint="True"` with `WindowDecorations="BorderOnly"`.** This removes the system title bar entirely and extends the client area into the title bar region. However, `WindowDecorations` is not a valid Avalonia property name — the correct name depends on the Avalonia version. In Avalonia 11+, the property is `SystemDecorations="BorderOnly"`. Our code uses `WindowDecorations="BorderOnly"` which was not recognized by the XAML parser (the build may silently ignore unrecognized properties on `Window`). As a result, the system title bar **is still rendered** by the OS, overlapping our custom `HeaderBar` at the top 32px of the window.

**5b. `ExtendClientAreaTitleBarHeightHint="0"` tells Avalonia to reserve 0px for the title bar, but with `SystemDecorations` still active (since the incorrect property was ignored), the OS draws its own title bar. The Avalonia content is then positioned starting below the OS title bar, creating a ~32px gap between the top of the window and our content — or worse, the content is rendered behind the OS title bar and partially hidden.

**5c. The HeaderBar height is 56px, but the window chrome buttons (minimize, maximize, close) are positioned at the top of the HeaderBar. When the OS title bar overlaps, these custom buttons are partially hidden behind the OS title bar buttons.**

**5d. In fullscreen mode (`WindowState=Fullscreen`), `SystemDecorations` is automatically hidden by Avalonia. But our `ExtendClientAreaToDecorationsHint` interacts poorly with fullscreen transitions — causing a flash of the title bar during the transition.**

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 5a | Use correct property name | Change `WindowDecorations="BorderOnly"` to `SystemDecorations="BorderOnly"` in both `MainWindow.axaml` and `App.axaml` style. |
| 5b | Verify `ExtendClientAreaTitleBarHeightHint` behavior | Keep `ExtendClientAreaTitleBarHeightHint="0"`. This tells Avalonia to reserve 0px for the title bar rendering — with `SystemDecorations="BorderOnly"`, the OS won't draw a title bar, and our content extends to the top edge. |
| 5c | Ensure custom header buttons are at the top of the content area | The `HeaderBar` has `VerticalAlignment="Top"` and `Height="56"`. With `SystemDecorations="BorderOnly"` and `ExtendClientAreaToDecorationsHint="True"`, the content area starts at pixel (0,0) of the window frame. The 1px top border of the window is drawn by the OS, so our header should have `Margin="0,1,0,0"` to sit below the border. |
| 5d | Handle fullscreen transitions | In `OnPlayerFullscreenChanged` / `OnToggleFullscreen`, set `SystemDecorations` to `None` when fullscreen (not just `BorderOnly`). Restore `BorderOnly` when exiting fullscreen. |

---

## 6. Header Bar — Title Text, Open Button Visibility, Window Control Tracking

### Why It's Not Working

**6a. The Open button (`BtnOpenMenu`) is wired with `IsVisible="False"` and becomes visible only when `FilePath` is set.** This is correct per canvas. But the `OnViewModelPropertyChanged` handler also sets `TitleText.Text = _viewModel.Title` on `FilePath` change — the `Title` property now truncates filenames, which is correct. However, `TitleText` uses `HorizontalAlignment="Center"` in the `HeaderGrid` Column 1 (`Width="*"`) — when the Open button appears in Column 0, the title shifts. This is correct behavior (matches the canvas), but it may cause a jarring visual jump.

**6b. Window control buttons (Minimize, Maximize, Close) are hardcoded as our own buttons in `WindowControlsPanel`.** They correctly call `OnMinimizeClick`, `OnMaximizeRestoreClick`, `OnCloseClick`. These handlers work correctly. However, the maximize/restore icon swap via `UpdateMaximizeIcon()` is only called in `OnMaximizeRestoreClick`. If the user double-clicks the title bar or uses a keyboard shortcut (Alt+Space, X), the icon won't update because the window state change isn't monitored.

**6c. The maximize/restore icon swap uses `TrySetIcon` which is defined as `private static void TrySetIcon` — this is a **static** method that calls `Application.Current!.TryGetResource`. The resource key lookup is case-sensitive. If the geometry resource keys in `Icons.axaml` don't match exactly (e.g., `MaxRestoreIcon` vs `MaximizeIcon`), the icon won't update.**

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 6a | Ensure smooth title transition when Open button appears | Add `RenderTransform` animation on `TitleText` when `BtnOpenMenu.IsVisible` changes, or create a fixed-width title column with `HorizontalAlignment="Center"` that doesn't shift. |
| 6b | Subscribe to `WindowStateChanged` event | Add handler for `this.WindowStateChanged += OnWindowStateChanged;` that calls `UpdateMaximizeIcon()`. This catches all state changes (Alt+Space, double-click title bar, F11, etc.). |
| 6c | Verify resource key exact match | Ensure `Icons.axaml` has `x:Key="MaximizeIcon"` and `x:Key="MaxRestoreIcon"` (not "MaximizeIconPath" or other variations). `TrySetIcon` uses these exact keys. |

---

## 7. Responsive Layout — Breakpoints Use Wrong Values

### Why It's Not Working

**7a. `UpdateResponsiveLayout` hides buttons at `width < 495`.** The canvas doesn't specify a mobile/compact breakpoint explicitly, but the Python reference uses Adw.Breakpoint at 495px. Our current code hides `BtnPip`, `BtnSubtitlesMenu`, `BtnAudioMenu`, `BtnVideoMenu` below 495px. However, the responsive layout also **sets `ControlsBox.Height = 90`** (narrow) or `120` (wide). The canvas shows the controls box height is **fixed** by the content (auto-height based on two rows of controls + seek bar). Our hardcoded `Height` values may clip the controls or leave too much empty space.

**7b. Button size changes between 36px and 40px.** The canvas specifies buttons as `34×34` always (`.cbtn { width:34px; height:34px }`). Our code resizes buttons to 36px or 40px depending on breakpoint — this causes visual inconsistency with the canvas design.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 7a | Remove hardcoded `ControlsBox.Height` | Let the `ControlsGrid` auto-size based on its content (two rows with `Height="Auto"`). Remove `ControlsBox.Height = isNarrow ? 90 : 120` from `UpdateResponsiveLayout`. |
| 7b | Use fixed 34px button size | Change all `SetButtonSize(btn, isNarrow ? 36 : 40)` to `SetButtonSize(btn, 34)`. Match canvas `.cbtn` exactly. |

---

## 8. Play/Pause Icon Not Bound to Playback State

### Why It's Not Working

**8a. The `PlayPauseIconPath.Data` is hardcoded `{StaticResource PlayIcon}` in `MainWindow.axaml`.** The code-behind `OnPlaybackStateChanged` method manually sets `PlayPauseIconPath.Data = Geometry.Parse(...)` with hardcoded path strings. This bypasses the resource system and uses literal path strings that may not match the updated `Icons.axaml` geometries. Additionally, this only runs when `PlaybackStateChanged` events fire from the player — if the state changes through other paths (e.g., `PlayPause()` method called from keyboard shortcut), the icon may not update correctly.

**8b. The `OnPlaybackStateChanged` event handler is registered on `_player.PlaybackStateChanged` in code-behind, but the `MainViewModel` also has its own `State` property that should be the single source of truth.** The VM's `PlayPause()` method sets `State = _player.State` which triggers `OnPropertyChanged(nameof(State))`. The code-behind should bind to this, not duplicate the logic.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 8a | Replace hardcoded icon swap with resource-based binding | Remove all `Geometry.Parse` calls from `OnPlaybackStateChanged`. Instead, create a `PlayPauseIconConverter : IValueConverter` that returns `PlayIcon` geometry when `IsPlaying == false` and `PauseIcon` geometry when `IsPlaying == true`. Bind `PlayPauseIconPath.Data` to `IsPlaying` using this converter. |
| 8b | Remove duplicate event handler in code-behind | Remove `_player.PlaybackStateChanged` subscription from `MainWindow.axaml.cs`. The `MainViewModel` already handles state transitions. The AXAML binding from 8a will handle the icon automatically. |

---

## 9. OSD Notification — No Fade-Out

### Why It's Not Working

**9a. The `Border.OsdNotificationStyle` has a fade-in animation (`0→1` over 0.3s) but no fade-out.** When an OSD notification is shown (e.g., volume change, seek, mute toggle), the notification appears but never disappears. The canvas reference shows OSD notifications that fade out after ~2 seconds.

**9b. The OSD notification is triggered in code-behind via `ShowOsdNotification(string text)` method.** This method sets the text and makes the border visible, but doesn't schedule a hide operation. There's no timer or delay.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 9a | Add fade-out animation | Add a second animation to `Border.OsdNotificationStyle` that runs after a 2s delay: `KeyFrame Cue="0%" Opacity=1 → KeyFrame Cue="100%" Opacity=0` with `Delay="0:0:2"`. Or implement via `DispatcherTimer` in code-behind. |
| 9b | Implement OSD auto-hide | In `ShowOsdNotification`: make the `OsdNotificationBorder` visible, set opacity to 1, start a `DispatcherTimer` with `Interval=2s`. On tick, animate opacity to 0 over 0.3s, then hide. |

---

## 10. Missing Features from Canvas

### Why They're Not Implemented

**10a. PIP (Picture-in-Picture) button exists but is non-functional.** The `BtnPip` in the header calls no method. PIP requires a separate `Window` with a second video surface sharing the same `IMediaPlayer` render context — mpv/libmpv doesn't natively support multiple output windows from a single context. This is architecturally complex and requires either: (a) a second mpv context playing the same file at the same position (prone to desync), or (b) frame buffer capture and manual rendering in a second window (requires DX11 texture sharing).

**10b. No Preferences dialog.** `OnPreferencesClick` is a placeholder. The canvas doesn't specify a preferences dialog, but the Python reference has one.

**10c. No Keyboard Shortcuts dialog.** `OnShortcutsClick` is a placeholder.

### What Change Is Needed (Deferred)

| # | Fix | Implementation |
|---|-----|---------------|
| 10a | PIP implementation | Requires significant architecture work. Defer to a later phase. Add "not implemented" tooltip. |
| 10b | Preferences dialog | Create `Preferences.axaml` + `Preferences.axaml.cs` with settings for: subtitle font/color/scale, HW decoding, normalize volume, save position. Bind to `MainViewModel.Preferences` object. |
| 10c | Shortcuts dialog | Create `ShortcutsDialog.axaml` listing all keyboard bindings grouped by category. Static content (no binding needed). |

---

## 11. Toggle Buttons — White Circle Background When Checked, Icons Invisible

### Why It's Not Working

**11a. The `ToggleButton.circular-toggle:checked` style sets `Background="White"` but does not change the icon fill to black.** The canvas reference `.cbtn.checked svg { filter: invert(1); }` means that when a circular transport button is in its checked/active state, the background turns white and the icon inverts to black. Our implementation correctly sets `Background="White"` on the checked state but the `Path` elements inside the toggle buttons have hardcoded `Fill="{StaticResource OsdForeground}"` which is white. The icon **remains white on a white background**, becoming completely invisible.

The user sees only a white circle with no icon inside. This affects all `ToggleButton.circular-toggle` instances:
- `BtnShufflePlaylist` (Shuffle)
- `BtnLoopPlaylist` (Loop Playlist)
- `BtnLoopFile` (Loop File)
- `BtnFullscreen` (Fullscreen — when fullscreen is toggled on, the enter/exit icon swap happens in code-behind but the white bg still appears when the button visually reads as `IsChecked`)
- `BtnMuteToggle` (Mute toggle inside volume flyout)

**11b. The `FullscreenIconPath` icon swap in `RefreshFullscreenUi()` calls `Application.Current!.TryGetResource("FullscreenExitIcon", ...)` which may return `null` if the resource key doesn't match exactly.** If the resource key `FullscreenExitIcon` exists in `Icons.axaml`, this works. But the ToggleButton checked state still shows a white background with no icon color change, so when fullscreen is active, the button shows as a white circle.

**11c. The checked state for `BtnFullscreenClose` uses `IsVisible="False"` but no visibility change on toggle — the fullscreen close button is only shown during fullscreen via `RefreshFullscreenUi()`.**

**11d. The `ToggleButton.circular-toggle` style in `App.axaml` sets `Background="Transparent"` as base, `Background="White"` when `:checked`, but `Foreground` is never set to black.** Since `Path.Fill` is hardcoded and does not inherit `Foreground`, setting `Foreground="Black"` on the ToggleButton has no effect on the Path elements inside. The fix must either: (a) use a `IValueConverter` bound to `IsChecked` to swap `Fill`, or (b) use a style that targets the Path inside a checked ToggleButton.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 11a | Add style rule that inverts Path fill when ToggleButton is checked | Add style: `Style Selector="ToggleButton.circular-toggle:checked /template/ ContentPresenter Path"` with `Setter Property="Fill" Value="Black"`. This cascades to all Path children inside the checked toggle button without modifying every individual AXAML Path element. |
| 11b | Remove hardcoded `Fill` on Path elements inside toggle buttons | Change all `Fill="{StaticResource OsdForeground}"` on Path elements inside toggle buttons to `Fill="White"` (the default) and let the style override handle checked state inversion. |
| 11c | Ensure fullscreen icon is also affected | The `FullscreenIconPath` inside `BtnFullscreen` should also follow the checked-state inversion. Add a style that targets `Path#FullscreenIconPath` under `ToggleButton.circular-toggle:checked`. |
| 11d | Same for mute toggle inside volume flyout | `BtnMuteToggle` is a `ToggleButton` with `Classes="circular-toggle"` but is inside a flyout. The style rule from 11a won't reach it because the flyout is a different visual tree. Duplicate the rule with a `ToggleButton#BtnMuteToggle:checked` selector. |

---

## 12. Playlist Dialog — No Close Button, No Way to Dismiss Without OS Chrome

### Why It's Not Working

**12a. The `PlaylistDialog.axaml` has no close/exit button anywhere in its custom chrome.** It uses `ExtendClientAreaToDecorationsHint="True"` which extends the client area into the title bar region, but there are no window control buttons (minimize, maximize, close) implemented in the dialog. The dialog has a `Header` Grid with a back/plus button on the left and `TextBlock "Playlist"` centered — but no close button on the right.

**12b. `ExtendClientAreaToDecorationsHint="True"` without `SystemDecorations="BorderOnly"` means the OS title bar is still rendered but the client area overlaps it.** The result is that the user cannot click the OS close button because the client area extends over it, and there's no custom close button. The only way to close the dialog is:
- Press `Alt+F4` (unknown to most users)
- Set `DataContext` to null and let GC collect it (not triggered)
- Wait for the dialog to be garbage collected (never happens)

**12c. `WindowStartupLocation="CenterOwner"` is set but the dialog doesn't have a proper owner relationship.** When `_playlistDialog.Show(this)` is called, `this` is the MainWindow as owner. This works, but without a close button, the dialog is "trapped."

**12d. The `Closed` event handler sets `_playlistDialog = null` in `MainWindow.axaml.cs`, which would allow re-creation on next click. But the user can't trigger `Closed` without a close mechanism.**

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 12a | Add a close button to the PlaylistDialog header | In `PlaylistDialog.axaml`, add to the header Grid.Column="2": a Button with `Classes="window-close"`, `Click="OnCloseClick"`, containing a Path with `CloseIcon` geometry. |
| 12b | Add code-behind handler | In `PlaylistDialog.axaml.cs`, add `private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();`. |
| 12c | Add window-control-like buttons to match main window style | Add minimize and close buttons to the PlaylistDialog header, or at minimum a close (X) button. Use consistent styling with the main window's `WindowControlsPanel` but only include the close button since minimize doesn't make sense for a modal-like dialog. |
| 12d | Consider removing `ExtendClientAreaToDecorationsHint` | If the dialog should have standard OS chrome (which includes a close button by default), remove `ExtendClientAreaToDecorationsHint="True"` and let the OS draw the title bar. This is simpler and more familiar to users. |

---

## 13. Fullscreen Mode — Unwanted Windows Title Bar Appears on Top Hover, Controls Visible in Wrong Places

### Why It's Not Working

**13a. The root cause is `WindowDecorations="BorderOnly"` is an invalid property name.** The correct Avalonia 11 property is `SystemDecorations`. Since the invalid property is silently ignored, the OS renders **full window decorations** (title bar, border, system menu). When `WindowState` transitions to `FullScreen`, Avalonia tries to hide the decorations, but because the property was never properly set, Windows may still show its "fullscreen hint bar" — a thin bar at the top with minimize/maximize/close buttons that appears when the user moves the mouse to the top edge of the screen while in fullscreen mode.

**13b. The `RefreshFullscreenUi()` method hides `WindowControlsPanel`, `TitleText`, `BtnPrimaryMenu`, `BtnPip`, and `BtnOpenMenu`, and shows `BtnFullscreenClose`.** However, it does NOT hide the `HeaderBar` itself. The HeaderBar background gradient is partially transparent (`HeaderGradient` from `#24000000 → #14000000 → transparent`), so in fullscreen mode, the HeaderBar's invisible clickable area still exists. When the user hovers near the top, `OnWindowPointerMoved` detects the mouse and calls `ShowUiControls()`, which reveals the HeaderBar. But since `BtnFullscreenClose` is shown and `WindowControlsPanel` is hidden, the HeaderBar shows only the close button with empty space — looking like the "unwanted top bar with minimize option."

**13c. The `BtnFullscreenClose` button is positioned in the HeaderBar between `BtnPrimaryMenu` and `WindowControlsPanel`.** When `WindowControlsPanel` is hidden during fullscreen, `BtnFullscreenClose` becomes visible but sits with empty space to its right where the minimize/maximize/close buttons were. This empty space looks like a missing half of a title bar.

**13d. In fullscreen, `OnHeaderPointerPressed` still calls `BeginMoveDrag(e)`.** If the user drags the HeaderBar in fullscreen mode, Avalonia may exit fullscreen or behave unpredictably because moving a fullscreen window is not a standard operation.

**13e. The `BtnFullscreen` toggle always shows `FullscreenEnterIcon` initially even if already fullscreen.** When the player enters fullscreen programmatically (e.g., via `_player.SetFullscreen(true)` in `OnPlayerFullscreenChanged`), the icon does swap to `FullscreenExitIcon`. But if the user presses `F` to toggle, the flow is: `OnKeyDown` → `_viewModel.ToggleFullscreen()` → `_player.SetFullscreen(!IsFullscreen)` → fires `FullscreenChangedEvent` → `OnPlayerFullscreenChanged` → sets `WindowState` → `OnPropertyChanged(WindowStateProperty)` → `RefreshFullscreenUi()`. This chain has a race condition: the ViewModel's `ToggleFullscreen()` calls `_player.SetFullscreen()` which fires on a background thread, and by the time `RefreshFullscreenUi()` runs, `_player.IsFullscreen` may still be the old value.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 13a | Fix the property name | Change `WindowDecorations="BorderOnly"` to `SystemDecorations="BorderOnly"` in both `MainWindow.axaml` and the `Window` style in `App.axaml`. |
| 13b | Hide HeaderBar entirely in fullscreen | In `RefreshFullscreenUi()` when fullscreen, also hide `HeaderBar.IsVisible = false`. Only show `BtnFullscreenClose` as a floating button in the top-right corner (outside the HeaderBar). |
| 13c | Remove BtnFullscreenClose from HeaderBar; place it as a floating element | Move `BtnFullscreenClose` outside the HeaderBar into the main overlay grid. In fullscreen, position it at `HorizontalAlignment="Right" VerticalAlignment="Top"` with `Margin="0,8,8,0"`. This prevents the empty space where window controls were. |
| 13d | Disable HeaderBar pointer interaction in fullscreen | In `RefreshFullscreenUi`, set `HeaderBar.IsHitTestVisible = false` when fullscreen, restore to `true` when normal. This prevents `BeginMoveDrag` and other header interactions. |
| 13e | Fix fullscreen icon race condition | In `RefreshFullscreenUi`, read `WindowState` (which is always correct on UI thread) instead of `_playerService.Player.IsFullscreen`. Use `WindowState == FullScreen` as the source of truth. |

---

## 14. Auto-Hide / Show Controls — Flickers, Position Detection Wrong, Animation Glitches

### Why It's Not Working

**14a. `ShowUiControls` and `HideUiControls` set `IsVisible = false` before the fade-out animation completes.** In `HideUiControls`: the method calls `FadeVisual` (async) which animates opacity from 1→0 over 300ms. But after `Task.Delay(50)`, the code immediately sets `IsVisible = false`, cutting the animation short. The fade-out is never visible; the controls just disappear instantly.

**14b. `IsPositionOverElement` checks element bounds relative to the window, but this doesn't account for the `HeaderAndControlsOverlay` gradient Border that sits on top.** The overlay (`Classes="header-and-controls"`) has `IsHitTestVisible="False"` so pointer events pass through it to the HeaderBar and ControlsBox. However, the overlay has `VerticalAlignment="Stretch"` and `HorizontalAlignment="Stretch"` — it covers the entire window. The `IsPositionOverElement` check compares pointer position against the HeaderBar and ControlsBox bounds, which is correct. But the overlay's background gradient covers the middle area where the user may hover.

**14c. When the user moves the mouse to the video area (not over controls), `_isMouseOverControls` becomes false, and after 3 seconds the auto-hide timer hides the controls.** This is correct behavior. But `_isMouseOverControls` only checks if the pointer is over `HeaderBar` or `ControlsBox` — it does NOT check if the pointer is over `BtnOptionsMenu.Flyout` or other flyouts that may be open. If the Options flyout is open and the user moves the mouse inside it, `_isMouseOverControls` may be false, causing the auto-hide to close the controls (and the flyout with them).

**14d. The `_activeFlyouts` counter is incremented/decremented in `Opened`/`Closed` events, but if a flyout fails to open (e.g., null DataContext), the decrement never happens.** The `Closed` event falls back to `_activeFlyouts = Math.Max(0, _activeFlyouts - 1)` but if `Opened` fires before `Closed` (which is rare), the counter could get stuck at 1, preventing auto-hide permanently.

**14e. `ControlsBox.Height = isNarrow ? 90 : 120` in `UpdateResponsiveLayout` hardcodes the controls box height.** The canvas doesn't specify a fixed height — the controls box should be sized by its content. A hardcoded height of 120px may clip the controls (two rows: buttons row ~50px + seek row ~37px + margins = ~100px, so 120px works but leaves 20px empty space below). Worse, when the window is resized, this hardcoded height conflicts with auto-sizing from the Grid inside.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 14a | Fix fade-out to not clip with early IsVisible=false | In `HideUiControls`, remove the `Task.Delay(50)` + immediate `IsVisible = false`. Instead, wait for `FadeVisual` to complete before setting `IsVisible = false`. Replace `Task.Delay(50)` with checking the fade completion. |
| 14b | Add flyout-awareness to auto-hide detection | In `OnWindowPointerMoved`, if `_activeFlyouts > 0`, always keep controls visible (don't start the hide timer). Check for any open popup/flyout before deciding to hide. |
| 14c | Add flyout bounds to `_isMouseOverControls` detection | In `OnWindowPointerMoved`, also check if any open flyout's presenter bounds contain the pointer position. If a flyout is open, treat it as "mouse over controls." |
| 14d | Use a more robust flyout tracking mechanism | Replace the Opened/Closed event increment/decrement with a single `_activeFlyouts` volatile field that's set before `ShowAt()` and decremented in a `finally` block. Or use a `HashSet<object>` to track open flyouts. |
| 14e | Remove hardcoded ControlsBox.Height | Remove the `ControlsBox.Height = ...` assignment. Let the internal Grid auto-size. If min height is needed, use `MinHeight="100"` instead of a fixed `Height`. |

---

## 15. Missing Keyboard Shortcut Feedback — No On-Screen Display for Volume / Seek / Mute

### Why It's Not Working

**15a. There is no OSD (On-Screen Display) implementation at all.** Methods like `IncreaseVolume()`, `DecreaseVolume()`, `ToggleMute()`, `SeekForward()`, `SeekBackward()` operate silently. The user pressing volume up/down keys has no visual indication of the current volume level. The canvas reference shows OSD notifications for volume, seek, mute, and subtitle/audio track switches.

**15b. The `Border.OsdNotificationStyle` in `App.axaml` has a fade-in animation but no matching code-behind `ShowOsdNotification()` method.** Looking at `MainWindow.axaml.cs`, there is no `OsdNotificationBorder` field or `ShowOsdNotification` method. The OSD style exists but is never used — it's dead code.

**15c. The only visual feedback for playback state changes is the PauseIndicator (a brief flash of the pause icon).** Volume changes, mute toggles, seek operations, and speed changes have no visual feedback. The user must look at the controls bar (which may be auto-hidden) to see the current state.

**15d. The `PauseIndicator` animation uses `ContinueWith` on a background thread (`Task.Delay(350)`) which then calls `Dispatcher.UIThread.InvokeAsync` — this is fragile.** If the pause/unpause happens rapidly, multiple `ContinueWith` callbacks may run concurrently, causing the PauseIndicator to show/hide out of sync with the actual state.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 15a | Create a reusable OSD notification system | Add an `OsdNotificationBorder` to `MainWindow.axaml` (centered, bottom area, matching canvas). Implement `ShowOsdNotification(string text, double durationMs = 2000)` that shows the text, fades in over 200ms, holds for `durationMs`, then fades out over 300ms and hides. |
| 15b | Wire OSD to volume/mute/seek/speed events | In `OnViewModelPropertyChanged`, when `VolumeValue`, `IsMuted`, or `SpeedValue` changes, call `ShowOsdNotification(...)` with the appropriate text (e.g., "Volume: 75%", "Muted", "Speed: 1.5x"). |
| 15c | Simplify PauseIndicator logic | Replace the `ContinueWith`/`Task.Delay` chain with a single `DispatcherTimer` that fires once after 500ms to hide the indicator. Cancel the timer if unpaused before 500ms. Use a simple `_pauseIndicatorTimer` field. |
| 15d | Add OSD for keyboard-triggered actions | In `OnKeyDown`, after each action that changes volume/seek/speed, call `ShowOsdNotification`. For seek: "−00:05" or "+00:05". For chapter: chapter title. |

---

## 16. Playlist Dialog — No Visual Polish, Missing Playing Item Highlight, No Persistence

### Why It's Not Working

**16a. The `ListBox` items have no visual indicator for the currently playing item beyond an `IsPlaying` visibility on a small PlayIcon.** The `PlaylistItemViewModel` has an `IsPlaying` property, and the icon visibility is bound to it. However, there's no background highlight, no bold text, no accent-colored indicator for the active track. The playing item blends in with all other items.

**16b. The `DropIndicatorRevealer` in the PlaylistDialog appears instantly with no animation.** When a drag enters the PlaylistDialog, `IsVisible = true` makes the overlay appear immediately with no opacity transition. When drag leaves, `IsVisible = false` vanishes it instantly. This is jarring. The main window's `UpdateDropIndicator` uses `FadeVisual` for smooth transitions — the PlaylistDialog should do the same.

**16c. There's no "Remove" or "Delete" action for playlist items.** Users can add files via drag/drop or the `+` button, but there's no way to remove an item from the playlist. The right-click context menu or a swipe-to-delete gesture is missing. This is a logical gap: any playlist UI must allow item removal.

**16d. The PlaylistDialog doesn't respond to `Enter` key to play the selected item.** If the user clicks on an item and presses Enter, nothing happens. There's no `DoubleTapped` or `KeyDown` handler on the ListBox items to trigger playback.

**16e. `WindowStartupLocation="CenterOwner"` with `ExtendClientAreaToDecorationsHint="True"` may position the dialog incorrectly if the owner window is in fullscreen mode.** When the main window is fullscreen, showing a centered dialog on it will place the dialog in the center of the fullscreen area, which may be on the wrong monitor.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 16a | Add playing item highlight | Add `Style Selector="ListBoxItem:selected"` with `Background="#330078D4"` (accent color at 20% opacity) and `Foreground="White"`. Also add `Style Selector="ListBoxItem:pointerover"` with `Background="#1FFFFFFF"`. |
| 16b | Add fade animation to DropIndicatorRevealer | In `OnDragEnter`/`OnDragLeave`, use a `DispatcherTimer` or inline animation to fade the `DropIndicatorRevealer` opacity instead of toggling `IsVisible` directly. Match the approach used in `MainWindow.axaml.cs` `UpdateDropIndicator`. |
| 16c | Add remove item functionality | Add a "Remove" button or right-click context menu to each ListBox item. Bind `Command` to a `RemoveItemCommand` on `PlaylistItemViewModel` or add an `OnRemoveItem` method that removes the item from `PlaylistItems` and `Playlist`. |
| 16d | Add keyboard/double-click play | Add `DoubleTapped` event on the ListBox or each item's Grid. In the handler, call `vm.PlayPlaylistItem(index)`. Also add `KeyDown` handler for `Enter` key. |
| 16e | Consider removing ExtendClientArea for PlaylistDialog | The PlaylistDialog doesn't need custom chrome. Remove `ExtendClientAreaToDecorationsHint="True"` and let the OS draw a standard window frame. This gives the user a close button for free. |

---

## 17. Miscellaneous Fine-Grain Visual & Logical Issues

### Why They're Not Working

**17a. Chapter preview popover (`ChapterPreviewPopover`) never hides when the mouse moves away from chapters area.** `OnSeekAreaPointerExited` correctly hides it, but if the mouse moves from one chapter marker to another (within the SeekArea), the popover doesn't update because `OnSeekAreaPointerMoved` only processes when chapters exist. If the mouse is over the seek bar but not near a chapter marker, the popover from the last chapter remains visible.

**17b. `LoadingSpinner` is always invisible — never shown/hidden during media loading.** When `OpenFile` is called, the loading spinner should appear. It never does. There's no code-behind logic to show it.

**17c. `BtnPip` in the header has `ToolTip.Tip="Picture-in-Picture (not implemented)"` which is informative but the button still appears clickable.** Users will try to click it and nothing happens. It should either be implemented or visually disabled with `IsEnabled="False"` and reduced opacity.

**17d. `BtnPrevious` and `BtnNext` call `PreviousChapter()` and `NextChapter()` respectively.** This means if there are no chapters, clicking Previous/Next does nothing. The canvas reference shows `Previous`/`Next` as playlist navigation, not chapter navigation. When there's a playlist, Previous/Next should navigate the playlist. When there's no playlist but there are chapters, they should navigate chapters. The current implementation only navigates chapters, ignoring playlist items.

**17e. `OnNewWindowClick` creates a `new MainWindow()` but does not share the player instance.** Each new window creates a new `PlayerService` and a new `IMediaPlayer` instance. This means each window runs a separate media player process, consuming significant resources. The canvas doesn't specify multi-window behavior, but the Python reference's "New Window" likely opens a new window with the same player session.

**17f. `OnPreferencesClick` and `OnShortcutsClick` are empty placeholders.** Clicking "Preferences" or "Keyboard Shortcuts" in the menu does nothing. This is confusing for users who expect these menu items to work.

**17g. The `AspectRatioCombo` ComboBox in OptionsMenuButton is bound via `SelectionChanged` event but has no initial selection sync with the current aspect ratio.** If the player already has an aspect ratio set (e.g., from command line or config file), the ComboBox still shows "Original" (index 0) because there's no binding to `AspectRatioValue`. The user must manually select a value.

**17h. The `DropIndicatorOverlay` in MainWindow uses `Background="#C6000000"` (black at 78% opacity) but the text/fill color `#FF4AA3FF` is a pink accent.** This matches the canvas reference (dashed drag overlay with pink `#FF4AA3FF`). However, the `DropIndicatorOverlay` also shows when dragging subtitle files, which is correct behavior. But it shows across the entire window, covering the start page drop target visual. Two overlapping drop indicators (one on StartPage, one global) create visual confusion.

**17i. `BtnPrimaryMenu` flyout has no keyboard accessibility.** The flyout items have `Click` handlers instead of `Command` bindings. Keyboard navigation (arrow keys, Enter) works for MenuFlyout by default, but the `Click` handlers may not be reachable via keyboard in all scenarios. Using `Command="{Binding ...}"` is more reliable.

**17j. The `BtnMinimize`, `BtnMaximizeRestore`, `BtnClose` window control buttons have an inline style in `Window.Styles` that sets `Width="46" Height="32"`, but the canvas `.wctrl` uses `width:46px; height:32px`. However, the buttons are inside a `StackPanel` with no explicit size, so the 46×32 inline style applies. But the inline style selector `Window > Grid > StackPanel > Button` is too specific — it may be overridden by the Fluent theme's default Button template, which has its own min-size constraints. The buttons may render at a different size than expected.

**17k. `OnAboutClick` creates a raw `Window` in code-behind rather than using a proper `.axaml`-based dialog.** This code-behind approach bypasses theming, resource lookup, and styling. The About dialog will not match the dark theme of the rest of the application. It should be refactored into an `AboutDialog.axaml` file.

### What Change Is Needed

| # | Fix | Implementation |
|---|-----|---------------|
| 17a | Hide chapter preview when mouse leaves chapter proximity | In `OnSeekAreaPointerMoved`, if the pointer is not within 3px of any chapter marker position, hide `ChapterPreviewPopover`. Or set a maximum distance threshold. |
| 17b | Implement LoadingSpinner show/hide | In `MainViewModel.OpenFile`, set a `IsLoading` property. Bind `LoadingSpinner.IsVisible` to `IsLoading`. Add `OnPropertyChanged(nameof(IsLoading))` in the `OpenFile` method before and after the player open call. |
| 17c | Disable BtnPip | Set `IsEnabled="False"` on `BtnPip` and add reduced opacity style for disabled state: `Style Selector="ToggleButton.circular-menu:disabled Path"` with `Opacity="0.4"`. |
| 17d | Fix Previous/Next to navigate playlist first | Change `OnPrevious`/`OnNext` to check `HasMultiplePlaylistItems` first. If true, call `_viewModel.PreviousItem()` / `_viewModel.NextItem()`. Only fall back to chapter navigation if no playlist items. |
| 17e | (Deferred) Share player instance across windows | Add a `PlayerService` singleton pattern or pass the `IMediaPlayer` instance to new windows. For now, document as known issue. |
| 17f | Implement placeholder dialogs | Create minimal `Preferences.axaml` and `ShortcutsDialog.axaml` files with a close button and placeholder content, even if the actual logic is added later. An empty handler that does nothing is broken UX. At minimum, show a message. |
| 17g | Sync ComboBox selection with current value | Add `SelectedValue="{Binding AspectRatioValue}"` or in `OnOptionsFlyoutOpened`, iterate ComboBox items and select the one whose Tag matches the current `AspectRatioValue`. |
| 17h | Fix overlapping drop indicators | In `OnWindowDragEnter`, if `StartPage.IsVisible` is true, show the StartPage's `DropTarget` overlay. If StartPage is hidden (media playing), show the global `DropIndicatorOverlay`. Never show both at once. |
| 17i | Replace Click handlers with Command bindings | For `BtnPrimaryMenu` flyout items, add commands to `MainViewModel` (e.g., `OpenNewWindowCommand`, `OpenPreferencesCommand`, `OpenShortcutsCommand`). Bind `Command` instead of `Click`. Only `About` needs a code-behind handler (needs window reference). |
| 17j | Fix window control button sizing | Replace the inline `Window.Styles` with a proper style selector `Button.window-control` applied to each window button. Set explicit `Width="46" Height="32"` and `CornerRadius="0"`. Add hover states matching canvas `.wctrl:hover` and `.wctrl.close:hover`. |
| 17k | Refactor AboutDialog into AXAML | Create `AboutDialog.axaml` with proper theming, move the content from `OnAboutClick` code into XAML. Create `AboutDialog.axaml.cs` with `OnCloseClick` handler. |

---

## 18. Canvas 1:1 Alignment Audit — Element-by-Element Match Percentage

### Methodology

Each significant visual/layout element from the canvas HTML was cross-referenced against the current Avalonia UI code. Elements are scored as:
- ✅ **Match** — visually equivalent (may use different implementation but looks the same)
- ⚠️ **Minor deviation** — close but has a measurable difference (e.g., 2px off, different alpha)
- ❌ **Mismatch** — wrong size, wrong color, missing element, or functionally broken

---

### 18.1 — Playback State Header Bar (height:56px, gradient overlay)

| Canvas Spec | Our Code | Score |
|-------------|----------|-------|
| Height: 56px | `Height="56"` | ✅ |
| Gradient: linear(180deg, rgba(0,0,0,.14)→.08→0) | `HeaderGradient` brush — same stops | ✅ |
| Padding: 0 2px | `Padding="2,0"` | ✅ |
| Open button bg: rgba(255,255,255,.12) | `BtnOpenMenu` bg: `#1FFFFFFF` (.12) | ✅ |
| Open button radius: 99px | `CornerRadius="20"` (h=40 gives 20, 99=pill) | ✅ |
| Open button padding: 6px 14px | `12,8` | ⚠️ 6px vs 12px vertical (±6px) |
| Open button font: 13px weight 500 | `FontSize="13" FontWeight="Medium"` | ✅ |
| Open icon: viewBox 0 0 8 5, 8×6 width/height | Path "M 1 2 L 4 5 L 7 2" 8×5 | ⚠️ Canvas is 8×6, ours 8×5 |
| Open button gap: 6px between text+icon | `Spacing="6"` (StackPanel) | ✅ |
| Open button margin-left: 8px | `Margin="8,0,0,0"` | ✅ |
| Title: 14px weight 600, centered | `FontSize="14" FontWeight="SemiBold"` | ✅ |
| Title text-shadow: 0 1px 6px rgba(0,0,0,.6) | `TextShadow="0 1 6 #99000000"` | ✅ |
| Menu 3-dot: viewBox 0 0 22 6, 16×6 render | Our MenuIcon: Path at 16×16 | ❌ Canvas coords are 22×6, not 24×24; dots at cx=3,11,19 vs our 5,12,19 |
| wctrl size: 46×32 | `Width="46" Height="32"` | ✅ |
| wctrl hover: rgba(255,255,255,.13) | Missing — no style for window control hover | ❌ |
| Close hover: background #e81123 | Missing — no `.close` hover style | ❌ |
| wctrl minimize icon: viewBox 0 0 20 2, 13×2 | Our MinimizeIcon: M 4 12 H 20 V 13 H 4 Z | ❌ Canvas is thin rectangle, ours is filled rect |
| wctrl maximize icon: viewBox 0 0 16 16, 12×12 | Our MaximizeIcon: M 4 4 H 20 V 20 H 4 Z | ❌ Different path geometry |
| wctrl close icon: viewBox 0 0 14 14, 11×11 | Our CloseIcon: M 6.4 6.4 L 17.6 17.6 | ❌ Different path geometry |
| **No PIP button in header** | `BtnPip` present in HeaderBar | ❌ Extra element not in canvas |

**Header Score: 12 / 18 = 67%**

---

### 18.2 — Playback State Controls Row (13 buttons + spacer)

| Canvas Spec | Our Code | Score |
|-------------|----------|-------|
| Layout: flex-wrap, padding 10px 13px 4px | Grid Margin="13,10,13,4" | ✅ |
| Gap: 4px | `ColumnSpacing="4"` | ✅ |
| Button size: 34×34, border-radius 50% | Width/Height 34, CornerRadius 17 | ✅ |
| Button hover: rgba(255,255,255,.17) | `ButtonHoverBackground: #2BFFFFFF` | ✅ |
| Button active: rgba(255,255,255,.25) | `ButtonActiveBackground: #40FFFFFF` | ✅ |
| Button drop-shadow: 0 1px 5px rgba(0,0,0,.6) | Missing shadow on buttons | ❌ |
| 13 buttons in exact order: prev→play→next→vol→subs→audio\|spacer\|shuffle→loopList→loopFile→playlist→options→fullscreen | Same order ✅ | ✅ |
| **Previous icon shape**: skip-backward (triangle M 13.73 3.03... + bar M 3 2 L 5 2...) | Our SkipBackwardIcon: M 12.5 6 L 7 12... | ❌ Different visual — canvas shows tall triangle, ours is shorter/wider |
| **Play/Pause icon**: pause (two vertical bars, 4.5 wide) | Our PauseIcon: two bars 3 wide | ⚠️ Different bar width ratio |
| **Next icon shape**: skip-forward (mirror of prev) | Our SkipForwardIcon | ❌ Same mismatch as prev |
| **Volume icon**: speaker M 6 3 V13 L 3 10 H1 V6 H3 Z + arcs | Our VolumeMaxIcon: M 3 9 H 5 L 9 5 V 19... | ⚠️ Speaker body orientation differs (canvas: top-left, ours: left) |
| **Volume arcs**: fill=none stroke=white stroke-width=1.5 | Our arcs: fill=none stroke=white stroke-width=1.5 | ✅ |
| **Subtitles icon**: viewBox 0 0 16 16, filled CC boxes | Our SubtitlesIcon: M 2 4 H 22 V 20... | ❌ Different sub-box layout — canvas has 7 lines of CC text, we have different arrangement |
| **Audio icon**: viewBox -1 -2 18 20 (wider) | Our AudioIcon: M 5 5 H 7 V 19... (24×24) | ❌ Canvas viewBox has negative offset (-1,-2) making icon visually wider than ours |
| **Spacer**: flex:1, min-width:4px | Grid Column * with Width="*" | ✅ |
| **Shuffle icon**: viewBox 0 0 16 16, stroke 1.4, 3 paths (arrows crossing) | Our PlaylistShuffleIcon: fill-based | ❌ Canvas uses stroke lines with round linecaps; we use filled polygons — different appearance |
| **LoopPlaylist icon**: viewBox 0 0 16 16, filled arrows | Our LoopPlaylistIcon | ✅ Similar loop arrows |
| **LoopFile icon**: viewBox 0 0 16 18, render 14×14 | Our LoopFileIcon: 16×16 | ❌ Canvas renders at 14×14, ours at 16×16 |
| **Playlist icon**: viewBox 0 0 16 16, stroke 1.4, 3 circles + 3 lines | Our PlaylistIcon: 3 filled rects | ❌ Canvas uses stroke style (circles + lines), ours uses filled rectangles |
| **Options icon**: viewBox 0 0 18 18, stroke 1.4, circle + 6 lines | Our OptionsIcon: fill-based arcs | ❌ Canvas uses stroke gears, we use filled — different visual weight |
| **Fullscreen icon**: viewBox 0 0 16 16, stroke 1.5, render 14×14 | Our FullscreenEnterIcon: 16×16 | ❌ Canvas renders at 14×14, ours at 16×16 |
| **Fullscreen path shape**: corner arrows (M 10 1 H 15 V 6 / M 1 10 V 15 H 6) | Our FullscreenEnterIcon: M 8 3 V 8 H 3 M 8 3 L 3 8 | ⚠️ Same concept but different exact path |
| **Toggle checked**: background white, icon inverts to black | Background=White works, icon stays White | ❌ Icon invisible when checked (see §11) |

**Controls Row Score: 10 / 20 = 50%**

---

### 18.3 — Playback State Seek Row (height:37px)

| Canvas Spec | Our Code | Score |
|-------------|----------|-------|
| Row margin: 0 3px 5px | `Margin="3,0,20,5"` | ⚠️ Canvas: 0 left, 3 right. Ours: 3 left, 20 right |
| Row height: 37px | Grid Row Height="37" | ✅ |
| Seek area margin: 0 8px 0 3px | Grid Margin="8,0,3,0" | ✅ |
| Track height: 4px | `Height="4"` | ✅ |
| Track color: rgba(255,255,255,.225) | `ProgressTroughBackground: #39FFFFFF` = same | ✅ |
| Track radius: 2px | `CornerRadius="2"` | ✅ |
| Fill height: 4px | `Height="4"` | ✅ |
| Fill color: white | `ProgressSliderBackground: White` | ✅ |
| Fill width: dynamic (33% in demo) | MultiBinding width — **broken** | ❌ |
| Thumb size: 16×16 circle | Width/Height 16, CornerRadius 8 | ✅ |
| Thumb color: white + box-shadow 0 1 4 rgba(0,0,0,.5) | Background=White, no shadow | ❌ Missing thumb shadow |
| Thumb position: left: calc(33% - 8px) | Margin via converter — **broken** | ❌ |
| Chapter marker: 2px×10px | Our marker: 2×12 | ⚠️ 10≠12 height |
| Chapter marker color: rgba(255,255,255,.5) | Opacity="0.5" | ✅ |
| Chapter marker y-position: top:50%, margin-top:-5px | VerticalAlignment="Center" | ✅ |
| Elapsed time font: 13px, JetBrains Mono, weight 600 | `NumericFont` (JetBrains Mono), 13px, SemiBold | ✅ |
| Elapsed margin: 0 -7px | `Margin="-7,0,0,0"` | ✅ |
| Separator: 2px×16px, #ddd, opacity 0.4 | `TimeSeparatorBackground: #DDDDDD`, Opacity 0.4 | ✅ |
| Separator margin: 0 10px | `Margin="10,0"` | ✅ |
| Duration font: same as elapsed | Same | ✅ |
| Duration margin-right: 20px | **Missing** margin-right on DurationTimeLabel | ❌ |

**Seek Row Score: 14 / 19 = 74%**

---

### 18.4 — Playback State Background Layers

| Canvas Spec | Our Code | Score |
|-------------|----------|-------|
| Base bg: #0c0c0e | Background="#FF0C0C0E" | ✅ |
| Radial gradient: ellipse at 58% 42%, #192848→#090e1d→#000 | **Not implemented** — falling back to solid bg | ❌ |
| Vignette: radial-gradient(center, transparent 40%, rgba(0,0,0,.45)) | **Not implemented** | ❌ |
| Edge gradient top: linear(180deg, rgba(0,0,0,.30)→.15→0 at 17%) | `HeaderGradient` uses .14→.08→0 (different alphas, shorter falloff) | ❌ Alphas differ, canvas stops at 17%, ours at 40% |
| Edge gradient bottom: linear(0deg, rgba(0,0,0,.40)→.23→0 at 35%) | `ControlsGradient` uses .20→.10→0 (different alphas, shorter falloff) | ❌ Alphas differ, canvas stops at 35%, ours at 40% |

**Background Score: 1 / 5 = 20%**

---

### 18.5 — Start Page

| Canvas Spec | Our Code | Score |
|-------------|----------|-------|
| Header height: 56px | `Height="56"` | ✅ |
| Header gradient: same as playback | Same ✅ | ✅ |
| Header title: "Cine", 15px weight 600 | `FontSize="14"` | ❌ 14≠15 |
| **No Open button** | Not present ✅ | ✅ |
| Menu + wctrl: same as playback | Same | ✅ (same mismatch as above) |
| Background: linear(180deg, #000→#0c0c0e) | `StartPageBackground: #FF000000→#FF0C0C0E` | ✅ |
| Center content margin-top: 48px | **No margin-top on center container** | ❌ |
| Icon: 128×128 SVG circle+triangle | Path at Width/Height 128 | ✅ size |
| Icon circle: cx=64 cy=64 r=60 fill=rgba(255,255,255,.08) stroke=rgba(255,255,255,.15) stw=2 | Our icon is a Material play-circle path, not circle+triangle | ❌ Different icon design |
| Icon→title gap: 24px | `Margin="0,0,0,16"` on icon → 16px | ❌ 16≠24 |
| Title: 28px, weight 700, color #e5e5e5 | Same ✅ | ✅ |
| Letter-spacing: -0.01em | Not set | ⚠️ Missing letter-spacing |
| Title→buttons gap: 24px margin-bottom on h1 | `Margin="0,0,12,0"` on TextBlock → 12px | ❌ 12≠24 |
| Primary button: sp-btn height 40px, pill radius, bg #e5e5e5, color black, fw 600, fs 14 | Height 40, CornerRadius 20, bg #E5E5E5, fg Black, Bold, 14px | ✅ |
| Primary button hover: bg white | Not set — no hover state | ❌ |
| Secondary button: bg rgba(255,255,255,.12), color #e5e5e5 | `#1FFFFFFF` = rgba(255,255,255,.12) | ✅ |
| Secondary button hover: bg rgba(255,255,255,.15) | Not set — no hover state | ❌ |
| Buttons gap: 12px | `Spacing="12"` | ✅ |
| MinWidth: 140px on buttons | `MinWidth="140"` | ✅ |
| Drop target: hidden by default | **Visible by default** | ❌ |

**Start Page Score: 13 / 22 = 59%**

---

### 18.6 — Canvas Design Annotations (callout labels not part of UX)

These are design notes in the canvas, not UI elements:
- ① "Open button — visible when playing (was hidden 0×0)" ✅ already implemented
- ② "Rewind & Forward removed · ③ LoopPlaylist added · order aligned" ✅ already implemented
- ④ "Time labels moved here (were in controls row)" ✅ already implemented
- Bottom annotation "Controls hidden · Open button hidden" (start page) ✅ already implemented

---

### 18.7 — Hidden/Functional Elements Not Visible in Canvas

| Feature | Canvas | Our Code | Score |
|---------|--------|----------|-------|
| Fullscreen close button | Not in canvas (only in runtime) | `BtnFullscreenClose` exists | N/A (runtime-only) |
| Drop indicator overlay | Not in canvas (runtime only) | `DropIndicatorOverlay` exists | N/A (runtime-only) |
| Chapter preview popover | Not in canvas (runtime only) | `ChapterPreviewPopover` exists | N/A (runtime-only) |
| Pause indicator | Not in canvas (runtime only) | `PauseIndicator` exists | N/A (runtime-only) |
| OSD notification | Not in canvas (runtime only) | `Border.OsdNotificationStyle` exists | N/A (runtime-only) |
| Options flyout | Not in canvas (hidden in menu) | `OptionsMenuButton` exists | N/A (runtime-only) |
| Volume flyout | Not in canvas (hidden) | Volume flyout exists | N/A (runtime-only) |

---

### 18.8 — Overall Match Score

| Section | Items | Match | Minor Dev. | Mismatch | Score |
|---------|-------|-------|------------|----------|-------|
| Playback Header | 18 | 12 | 1 | 5 | 67% |
| Controls Row | 20 | 10 | 0 | 10 | 50% |
| Seek Row | 19 | 14 | 2 | 3 | 74% |
| Background | 5 | 1 | 0 | 4 | 20% |
| Start Page | 22 | 13 | 1 | 8 | 59% |
| **Overall** | **84** | **50** | **4** | **30** | **60%** |

### Current 1:1 Alignment: **~60%**

---

## 19. Roadmap to 1:1 Alignment (100%)

### Phase 1 — Functional Parity (P0, 1-2 sessions)
Target: Fix the broken core features to make the app usable.

| Step | What | Sections | Est. Effort |
|------|------|----------|-------------|
| 1.1 | Fix seek fill + thumb positioning via code-behind | §1 | 2h |
| 1.2 | Add drag-to-seek (PointerMoved with button held) | §1 | 1h |
| 1.3 | Fix `SystemDecorations` property → remove OS title bar overlap | §5, §13 | 0.5h |
| 1.4 | Fix volume slider draggability (flyout LightDismiss issue) | §2 | 1h |
| 1.5 | Fix toggle button checked state (invert icon to black) | §11 | 0.5h |
| 1.6 | Add close button to PlaylistDialog | §12 | 0.5h |
| **Match gain:** | | | **60% → 68%** |

### Phase 2 — Visual Alignment (P1-P2, 2-3 sessions)
Target: Match all layout metrics and fix major visual gaps.

| Step | What | Sections | Est. Effort |
|------|------|----------|-------------|
| 2.1 | Rewrite all icon geometries to match canvas SVG style (stroke-based 1.4px weight, 24×24 viewBox) | §3, §18.2 | 4h |
| 2.2 | Resize LoopFile → 14×14, Fullscreen → 14×14 icon render sizes | §18.2 | 0.5h |
| 2.3 | Add wctrl hover + close:hover styles (#e81123) | §18.1 | 0.5h |
| 2.4 | Add button drop-shadow filter: 0 1px 5px rgba(0,0,0,.6) | §18.2 | 0.5h |
| 2.5 | Fix DurationTimeLabel margin-right=20px | §18.3 | 0.2h |
| 2.6 | Add Start Page letter-spacing=-0.01em on title | §18.5 | 0.2h |
| 2.7 | Fix Start Page icon→title gap 24px, title→buttons gap 24px | §18.5 | 0.3h |
| 2.8 | Add Start Page center content margin-top=48px | §18.5 | 0.3h |
| 2.9 | Add Start Page header font-size=15px (when idle) | §18.1, §18.5 | 0.5h |
| 2.10 | Remove PIP button from HeaderBar (not in canvas) | §18.1 | 0.2h |
| 2.11 | Fix chapter marker height from 12→10px | §18.3 | 0.2h |
| 2.12 | Add thumb box-shadow | §18.3 | 0.3h |
| 2.13 | Fix Seek Row margin to match canvas (0,3,5 vs 3,0,20,5) | §18.3 | 0.2h |
| **Match gain:** | | | **68% → 85%** |

### Phase 3 — Background & Polish (P2-P3, 1-2 sessions)
Target: Add missing visual layers and complete the design.

| Step | What | Sections | Est. Effort |
|------|------|----------|-------------|
| 3.1 | Add radial gradient background for playback state (#192848→#090e1d→#000) | §18.4 | 1h |
| 3.2 | Add vignette overlay (radial-gradient transparent→rgba(0,0,0,.45)) | §18.4 | 0.5h |
| 3.3 | Fix edge gradient alphas to match canvas (top .30→.15→0, bottom .40→.23→0) | §18.4 | 0.5h |
| 3.4 | Add button hover:active states for start page buttons | §18.5 | 0.3h |
| 3.5 | Add hover states for all buttons matching canvas hover/active colors | §18.2 | 0.5h |
| 3.6 | Implement OSD notification with fade-out | §9, §15 | 1h |
| 3.7 | Fix auto-hide animation (complete fade before IsVisible=false) | §14 | 0.5h |
| **Match gain:** | | | **85% → 93%** |

### Phase 4 — Fine-Grain Perfection (P3-P4, 1-2 sessions)
Target: Nail every pixel and edge case.

| Step | What | Sections | Est. Effort |
|------|------|----------|-------------|
| 4.1 | Add Start Page icon: circle (rgba(255,255,255,.08) fill + .15 stroke) + triangle (#e5e5e5) matching canvas SVG exactly | §18.5 | 0.5h |
| 4.2 | Fix every icon to match canvas SVG 1:1 (not just similar — exact path data) | §18.2 | 3h |
| 4.3 | Add Open button padding=6px 14px (vs current 12,8) | §18.1 | 0.2h |
| 4.4 | Fix Open icon viewBox to 8×6 canvas match | §18.1 | 0.2h |
| 4.5 | Implement icon stroke-based style matching canvas (1.4px stroke weight, round linecaps) | §18.2 | 2h |
| 4.6 | Test at all window sizes from 332px to 1920px to ensure no overflow | All | 1h |
| **Match gain:** | | | **93% → 99%** |

### Phase 5 — Beyond-Capabilities Parity (deferred)
Target: Features beyond what the static canvas shows.

| Step | What | Sections | Est. Effort |
|------|------|----------|-------------|
| 5.1 | Implement full Preferences dialog | §10 | 3h |
| 5.2 | Implement Keyboard Shortcuts dialog | §10 | 1h |
| 5.3 | PIP (requires architectural work) | §10 | 8h+ |
| 5.4 | New Window sharing player instance | §17 | 2h |
| **Match gain:** | | | N/A (not in canvas) |

---

## 20. Icon Stroke vs Fill — The Root Cause of 65% of Visual Mismatch

The single biggest reason for the 60% alignment rate is **icon rendering philosophy**. The canvas uses **stroke-based icons** (thin lines 1.4–1.5px stroke-width, round linecaps, transparent/empty centers). Our code uses **fill-based icons** (solid polygons with opaque fill). This creates a fundamentally different visual weight:

| Icon | Canvas Style | Our Style | Visual Impact |
|------|-------------|-----------|---------------|
| Shuffle | 3 stroke paths, linecap round | Filled polygons | Canvas: delicate arrows. Us: blocky shapes |
| Playlist | 3 stroke circles + 3 stroke lines | 3 filled rectangles | Canvas: dots+lines. Us: bars |
| Options | 1 stroke circle + 6 stroke lines | 1 filled circle + filled arcs | Canvas: delicate gear. Us: heavy gear |
| Fullscreen | 4 stroke corner arrows | 4 stroke corner arrows | ✅ Same style — closest match |
| Close | 2 stroke lines, round cap | 2 stroke lines + Z close | ✅ Same style |
| Maximize | 1 stroke rectangle | 1 filled rectangle | Canvas: outline. Us: solid border box |

**Path to fix all icons to canvas style:**
1. Change `Icons.axaml` from `Geometry` resources to use `StreamGeometry` or restructure Path data as stroke-based
2. Replace `Fill="White"` base with `Stroke="White" StrokeThickness="1.4" StrokeLineCap="Round" StrokeLineJoin="Round"`
3. All canvas SVGs use `viewBox` (viewBox="0 0 16 16") — our Geometry paths need to cover exactly 16×16 logical space
4. Audio icon needs viewBox="-1 -2 18 20" for proper centering (wider on left/top)

**Effort: ~3 hours to rewrite all 16 transport/window-control icons as stroke-based paths matching canvas SVG 1:1.**

---

## 21. Complete Gap Closure Summary

| Section | Current Match | After Phase 1 | After Phase 2 | After Phase 3 | After Phase 4 |
|---------|:------------:|:-------------:|:-------------:|:-------------:|:-------------:|
| Playback Header | 67% | 72% | 83% | 89% | 94% |
| Controls Row | 50% | 55% | 75% | 85% | 100% |
| Seek Row | 74% | 84% | 89% | 89% | 95% |
| Background | 20% | 20% | 20% | 100% | 100% |
| Start Page | 59% | 64% | 82% | 91% | 100% |
| **Overall** | **60%** | **68%** | **79%** | **90%** | **99%** |

**Target completion:** 5-9 focused sessions (each 1-4 hours). Phase 1+2 should be tackled first for maximum user-facing improvement (60%→79% in 3-5 sessions).

---

## Updated Summary: Comprehensive Fix Priority Matrix

| Priority | Issue | Section | Effort | Impact |
|----------|-------|---------|--------|--------|
| **P0** | Seek bar broken (no fill, no drag, no visual) | §1 | Medium | 🚫 Core feature — app unusable |
| **P0** | Volume slider not draggable | §2a | Small | 🚫 Volume control unusable |
| **P0** | WindowDecorations property invalid → OS title bar overlaps | §5a, §13a | Small | 🚫 Custom chrome invisible |
| **P1** | Toggle buttons show white circle, icon invisible | §11a | Small | 🚫 Toggle buttons broken |
| **P1** | Playlist has no close button | §12a | Small | 🚫 Dialog can't be closed |
| **P1** | Fullscreen shows unwanted Windows title bar on hover | §13a-§13e | Medium | 🚫 Fullscreen broken |
| **P1** | Play/Pause icon not bound correctly | §8a-§8b | Small | 🚫 Icon doesn't reflect state |
| **P1** | Auto-hide cuts fade animation short, flickers | §14a-§14c | Small | 🚫 Controls appear/disappear abruptly |
| **P2** | Icons blurry/wrong proportions | §3a-§3e | Medium | ❌ Poor visual quality |
| **P2** | Start Page spacing/layout wrong | §4a-§4e | Medium | ❌ Doesn't match design |
| **P2** | No OSD for volume/seek/mute/speed | §15a-§15d | Medium | ❌ No keyboard feedback |
| **P2** | Playlist has no delete/remove for items | §16c | Small | ❌ Can't manage playlist |
| **P2** | Previous/Next should navigate playlist, not chapters | §17d | Small | ❌ Navigation wrong |
| **P3** | OSD notification never fades out (style exists but unused) | §9a-§9b | Small | ⚠️ Dead code |
| **P3** | Responsive layout uses wrong button sizes | §7a-§7b | Small | ⚠️ Oversized buttons |
| **P3** | LoadingSpinner never shown | §17b | Small | ⚠️ No loading feedback |
| **P3** | Fullscreen header still interactive (BeginMoveDrag) | §13d | Small | ⚠️ Drag in fullscreen |
| **P4** | Maximize icon not tracking all state changes | §6b | Small | ⚠️ Inconsistent icon |
| **P4** | Missing Preferences/Shortcuts dialogs | §10b-§10c, §17f | Large | ➕ New features |
| **P4** | Drop indicator visual overlap with StartPage | §17h | Small | ➕ Polish |
| **P4** | About dialog created in code (no styling) | §17k | Small | ➕ Polish |
| **P5** | PIP unimplemented | §10a | Large | ➕ New feature (deferred) |
| **P5** | Chapter markers use wrong position calculation | §1e | Small | ⚠️ Cosmetic |
| **P5** | PlaylistDialog DropIndicatorRevealer has no animation | §16b | Small | ➕ Polish |

---

## Updated File Change Manifest

| File | Changes Needed | Priority |
|------|---------------|----------|
| `MainWindow.axaml.cs` | Major rewrite: seek bar (position/drag/thumb/fill), volume flyout lifecycle, window state tracking, OSD timer, play/pause icon binding, responsive layout values, fullscreen chrome handling, auto-hide animation, loading spinner, BtnPip disabled, Previous/Next playlist-first navigation | P0 |
| `MainWindow.axaml` | Fix SystemDecorations property, add OsdNotificationBorder, move BtnFullscreenClose out of HeaderBar, add loading spinner binding, fix BtnPip disabled, change icon sizes (18×18 transport, 14×14 window controls) | P0 |
| `App.axaml` | Add ToggleButton:checked Path fill inversion style, fix Window style SystemDecorations, add OSD fade-out animation, remove hardcoded ControlsBox.Height | P1 |
| `PlaylistDialog.axaml` | Add close button in header, remove ExtendClientArea, add DoubleTapped handler, add remove button per item, add DropIndicatorRevealer animation | P1 |
| `PlaylistDialog.axaml.cs` | Add OnCloseClick, OnItemDoubleTapped, OnRemoveItem handlers | P1 |
| `MainViewModel.cs` | Add IsLoading property, fix Previous/Next to check playlist first, add OpenNewWindowCommand/OpenPreferencesCommand/OpenShortcutsCommand | P2 |
| `Icons.axaml` | Audit all paths for Z closure, verify MenuIcon renders as solid dots, normalize to 24×24 coordinate system | P2 |
| `StartPage.axaml` | Fix spacing (24px gap below title), hide DropTarget by default | P2 |
| `Colors.axaml` | Add radial gradient brushes for playback state background + vignette | P2 |
| `Converters/TimeSpanToStringConverter.cs` | Add VolumeIconConverter, PlayPauseIconConverter classes | P1 |
| `OptionsMenuButton.axaml` (no change needed) | AspectRatioCombo sync is a code-behind issue, not AXAML | P4 |
| `AboutDialog.axaml` (new) | Create themed About dialog in AXAML | P4 |
| `Preferences.axaml` (new) | Create Preferences dialog with placeholder content | P4 |
| `ShortcutsDialog.axaml` (new) | Create Keyboard Shortcuts listing dialog | P4 |
