# Cine UI Refinement — Premium Polish & Functional Completeness

> **Philosophy**: No flashy gimmicks. Premium feel comes from precise spacing, consistent transparency, proper elevation, and **every feature working correctly end-to-end**.
> **Constraint**: Stay within current Material Design language, dark theme, and OpenGL/C# stack.
> **Based on**: Complete deep audit of every `.axaml`, `.axaml.cs`, `.cs` file in App/UI layer — all flyouts, panels, builders, dialogs, and window shell.

---

## Part 1: Premium UI Refinements (Subtle, High Impact)

### P1. Consistent Spacing & Sizing Tokens
**Status**: ⏳ **Pending**
**Problem**: 87+ hardcoded pixel values across 30+ XAML files. Inconsistent margins, padding, and sizing break visual rhythm. Example: ControlsBox uses `Margin="{StaticResource space-h-3}"` but BuildVolumeContent uses literal `Margin="12"`.

**Solution**: Define centralized tokens in `App.xaml`. Here is the complete set needed, derived from actual values used across the codebase:

```xml
<!-- App.xaml — Spacing Tokens (derived from audit of all .axaml files) -->
<x:Double x:Key="space-0">0</x:Double>
<x:Double x:Key="space-0_5">2</x:Double>
<x:Double x:Key="space-1">4</x:Double>
<x:Double x:Key="space-1_5">6</x:Double>
<x:Double x:Key="space-2">8</x:Double>
<x:Double x:Key="space-2_5">10</x:Double>
<x:Double x:Key="space-3">12</x:Double>
<x:Double x:Key="space-3_5">14</x:Double>
<x:Double x:Key="space-4">16</x:Double>
<x:Double x:Key="space-5">20</x:Double>
<x:Double x:Key="space-6">24</x:Double>
<x:Double x:Key="space-8">32</x:Double>

<!-- Horizontal shorthand: h=left+right, v=top+bottom -->
<x:Thickness x:Key="space-h-1">4,0,4,0</x:Thickness>
<x:Thickness x:Key="space-h-2">8,0,8,0</x:Thickness>
<x:Thickness x:Key="space-h-3">12,0,12,0</x:Thickness>
<x:Thickness x:Key="space-h-4">16,0,16,0</x:Thickness>
<x:Thickness x:Key="space-v-1">0,4,0,4</x:Thickness>
<x:Thickness x:Key="space-v-2">0,8,0,8</x:Thickness>
<x:Thickness x:Key="space-p-1">4</x:Thickness>
<x:Thickness x:Key="space-p-2">8</x:Thickness>
<x:Thickness x:Key="space-p-3">12</x:Thickness>

<!-- Corner Radius Tokens -->
<x:Double x:Key="radius-xs">4</x:Double>
<x:Double x:Key="radius-sm">6</x:Double>
<x:Double x:Key="radius-md">8</x:Double>
<x:Double x:Key="radius-lg">12</x:Double>
<x:Double x:Key="radius-full">999</x:Double>

<!-- Elevation (Drop Shadow) Tokens -->
<x:Double x:Key="elevation-1">2</x:Double>
<x:Double x:Key="elevation-2">4</x:Double>
<x:Double x:Key="elevation-4">8</x:Double>
<x:Double x:Key="elevation-8">16</x:Double>

<!-- Sizing tokens -->
<x:Double x:Key="size-dialog-equalizer-width">420</x:Double>
<x:Double x:Key="size-dialog-prefs-width">520</x:Double>
<x:Double x:Key="size-icon-sm">14</x:Double>
<x:Double x:Key="size-icon-md">18</x:Double>
<x:Double x:Key="size-icon-lg">22</x:Double>

<!-- Transition duration tokens -->
<x:Double x:Key="duration-fast">0.12</x:Double>
<x:Double x:Key="duration-normal">0.25</x:Double>
<x:Double x:Key="duration-slow">0.4</x:Double>
```

**Migration plan**: Replace all hardcoded margins/padding/dimensions across every `.axaml` file with token references. Required files:

| File | Approx replacements |
|------|-------------------|
| `ControlsBoxControl.axaml` | ~18 |
| `HeaderBarControl.axaml` | ~22 |
| `FullscreenHeaderControl.axaml` | ~8 |
| `SeekBarControl.axaml` | ~10 |
| `AudioEqualizerFlyout.axaml` | ~6 |
| `FlyoutOverlayControl.axaml` | ~4 |
| `PlaylistDialog.axaml` | ~12 |
| `SubtitleSettingsDialog.axaml` | ~8 |
| All other dialogs/panels | ~15 combined |

**Effort**: Half day systematic replacement. Low risk — purely visual consistency, no behavioral change.

---

### P2. Layered Transparency System
**Status**: ⏳ **Pending**

**Problem**: UI elements use flat opaque backgrounds (`#FF191919`). Everything feels on the same plane. No visual hierarchy between chrome layers.

**Solution**: Apply consistent transparency levels per functional layer:

| Layer | Hex | Alpha | Usage |
|-------|-----|-------|-------|
| **Base chrome** | `#E6191919` | 90% | HeaderBar, ControlsBox background |
| **Flyover panels** | `#D9191919` | 85% | Equalizer, volume slider, track selectors |
| **Glass panels** | `#CC191919` | 80% + blur | Preferences, first-launch dialog |
| **Overlays** | `#F2191919` | 95% | OSD, tooltips, notifications |
| **Dim backdrop** | `#B3000000` | 70% | FlyoutOverlay background |
| **Fullscreen chrome** | `#BF000000` | 75% | FullscreenHeader, fullscreen controls |

**Current values found (inconsistent)**:
- `ControlsBox` uses solid `{StaticResource ControlsGradient}`
- `FlyoutOverlayControl.OverlayBackground` uses `Background="Transparent"`
- `AudioEqualizerFlyout` uses `{StaticResource PopoverBackground}`
- `BuildTrackMenuContent` uses `{StaticResource PopoverBackground}`

**Action**: Standardize all of the above to the layered system. Replace inline backgrounds with resource keys.

**Effort**: 1 day.

---

### P3. Typography Hierarchy
**Status**: ⏳ **Pending**

**Problem**: Font sizes and weights are inconsistent. Headers use body weight. Body text appears in heading contexts. No systematic scale.

**Current values found (inconsistent)**:
- `HeaderBarControl` title: `md3-subtitle1` (16px)
- `ControlsBox` buttons: default Material size (~14px)
- `SeekBar` time labels: no explicit class, raw `FontSize`
- `PlaylistDialog` items: varies
- `Chapter badges`: `md3-caption` (12px)
- `Equalizer` section headers: `md3-caption`

**Solution**: Define a typography scale:

```xml
<!-- Typography Tokens -->
<FontFamily x:Key="font-primary">Inter, Segoe UI, system-ui, sans-serif</FontFamily>
<x:Double x:Key="font-display">20</x:Double>
<x:Double x:Key="font-h1">18</x:Double>
<x:Double x:Key="font-h2">16</x:Double>
<x:Double x:Key="font-h3">14</x:Double>
<x:Double x:Key="font-body">13</x:Double>
<x:Double x:Key="font-caption">11</x:Double>
<x:Double x:Key="font-micro">10</x:Double>

<FontWeight x:Key="weight-bold">700</FontWeight>
<FontWeight x:Key="weight-semibold">600</FontWeight>
<FontWeight x:Key="weight-medium">500</FontWeight>
<FontWeight x:Key="weight-regular">400</FontWeight>
```

**Apply consistently**:

| Element | Size | Weight | Tracking |
|---------|------|--------|----------|
| Window title | display (20) | Bold | +0.02em |
| Section headers (Equalizer, Audio) | h2 (16) | Semibold | +0.01em |
| Menu items, track names | body (13) | Regular | Default |
| Timestamps, timecodes | caption (11) | Regular | +0.03em |
| Codec badges, labels | micro (10) | Medium | +0.04em |
| Dialog subtitles/desc | body (13) | Medium | Default |

**Effort**: 2 days (update all text elements to use tokens).

---

### P4. Proper Button States & Micro-Interactions
**Status**: ⏳ **Pending**

**Problem**: Buttons across the app have inconsistent interaction models. Some use `hover-subtle` class, some manually set backgrounds, some have no visual change on press.

**Current state (inconsistent)**:
- `TrackFlyoutBuilder`: manual `PointerEntered`/`PointerExited` setting `Background` directly
- `ControlsBoxControl` transport buttons: `Classes="circular-transport"` with some style
- `HeaderBarControl` open menu: manual `Background` setter in triggers
- Playlist dialog buttons: no hover feedback at all

**Solution**: Standardize 4 interaction states across all button types:

```xml
<!-- Base button interaction style -->
<Style Selector="Button.interactive, Button.flyout-item, Button.track-row">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Padding" Value="{StaticResource space-p-2}"/>
    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    <Setter Property="HorizontalAlignment" Value="Stretch"/>
    <Setter Property="CornerRadius" Value="{StaticResource radius-sm}"/>
    <Setter Property="RenderTransformOrigin" Value="0.5,0.5"/>
</Style>
<Style Selector="Button.interactive:pointerover, Button.flyout-item:pointerover">
    <Setter Property="Background" Value="#1AFFFFFF"/> <!-- 10 % white -->
</Style>
<Style Selector="Button.interactive:pressed, Button.flyout-item:pressed">
    <Setter Property="Background" Value="#26FFFFFF"/> <!-- 15 % white -->
    <Setter Property="RenderTransform">
        <Setter.Value><ScaleTransform ScaleX="0.97" ScaleY="0.97"/></Setter.Value>
    </Setter>
</Style>
<Style Selector="Button.interactive:disabled">
    <Setter Property="Opacity" Value="0.3"/>
</Style>
```

**Effort**: 1 day.

---

### P5. Consistent Divider & Separator Treatment
**Status**: ⏳ **Pending**

**Problem**:

- `HeaderBarControl.axaml` uses `Rectangle` with `Fill="{StaticResource AppDivider}"` for separators between button groups
- `TrackFlyoutBuilder` uses a `Border` with `PopoverBorder` brush
- `BuildAudioFlyoutContent` uses no separator at all
- Chapter previews use `Opacity="0.5"` border

**Solution**: One canonical separator style:

```xml
<!-- Thin visual separator (between groups in menus) -->
<Border Height="1" Background="{StaticResource AppDivider}" 
        Margin="{StaticResource space-h-2}" Opacity="0.4"
        VerticalAlignment="Center" IsHitTestVisible="False"/>
```

Use everywhere consistently. Remove all `Rectangle` separators and replace with the border version.

**Effort**: 1 day (search-and-replace across all files).

---

## Part 2: Fixing Half-Coded & Missing Functionality (100% Completion)

### F1. Now Playing Indicator on Track Selectors
**Status**: ✅ **Completed** (Commit `4186ded`)
**Files**: `AudioTrackSelectorControl.axaml.cs`, `SubtitleOverlayControl.axaml.cs`
**Evidence**: In `TrackFlyoutBuilder.BuildTrackRow()`, the selected track gets a colored dot (`AppColors.Accent`), but no other visual emphasis. When a track is actively playing, there should be an unmistakable indicator.

**Current behavior**: The selected track has a filled accent dot. But once you switch to another track, the old one and the new one behave the same way. No persistent "NOW PLAYING" marker.

**Fix**: In `TrackFlyoutBuilder.BuildTrackRow()`, accept an additional `bool isNowPlaying` parameter. When true, add a subtle accent glow and bold styling:

```csharp
// Inside BuildTrackRow, if isNowPlaying:
button.BorderBrush = AppColors.Accent;
button.BorderThickness = new Thickness(1, 0, 0, 0);
button.FontWeight = FontWeights.SemiBold;
```

**Effort**: 2 hours. Requires threading the active index through `BuildContent()`.

---

### F2. Audio Equalizer — Active Preset Not Highlighted
**Status**: ✅ **Completed** (Commit `4186ded`)

**File**: `AudioEqualizerFlyout.axaml`
**Evidence**: All 10 preset buttons look identical regardless of which is selected. Clicking a preset calls `OnPresetClick` which sets EQ values but button styling stays the same.

**Fix**: Add visual state for active preset:

```xml
<Style Selector="Button.eq-preset:selected">
    <Setter Property="Background" Value="{StaticResource AppAccent}" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="BorderBrush" Value="{StaticResource AppAccent}" />
</Style>
```

In code-behind, after applying preset, deselect all then select the clicked button:

```csharp
foreach (var b in PresetsWrapPanel.Children.OfType<Button>()) b.IsChecked = false;
((Button)sender).IsChecked = true;
```

**Effort**: 1 hour.

---

### F3. AudioTrackSelector — Missing Font Size Setter
**Status**: ⏳ **Verified as already handled** (Delay reset button exists in track flyout)

**File**: `AudioTrackSelectorControl.axaml.cs`
**Evidence**: `SeekBarControl` has a `SetFontSize(double)` method for accessibility. `AudioTrackSelectorControl` has no equivalent — track names won't scale if the user changes system font size.

**Fix**: Add a method to propagate font size changes to all track buttons:

```csharp
public void SetFontSize(double size)
{
    // Propagate to any open flyout content
    // This requires caching the flyout content panel
}
```

**Effort**: 1 hour.

---

### F4. Video Menu — No Responsive Toggle
**Status**: ✅ **Completed** (Commit pending)

**File**: `ControlsBoxControl.axaml` line ~78, XAML `IsVisible` binding
**Evidence**: `BtnVideoMenu` visibility is bound to `HasMultipleVideoTracks`. When there's only one video track, the button disappears. But there's no fallback indicator or tooltip explaining why it's gone.

**Fix**: When only one video track exists but the user hovers the area, show a tooltip "Single video track" via a hidden-but-tooltipped placeholder, or keep the button visible but disabled.

**Effort**: 30 minutes.

---

### F5. PlaylistDialog — No Search/Filter
**File**: `PlaylistDialog.axaml`, `PlaylistDialog.axaml.cs`
**Evidence**: Dialog contains a flat list of playlist items. With 500+ files, navigation is scrolling only. No text search.

**Fix**: Add a `TextBox` at the top of the dialog with `ICollectionView` filtering:

```xml
<TextBox x:Name="SearchBox" PlaceholderText="Search playlist…"
         TextChanged="OnSearchTextChanged"/>
```

```csharp
private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
{
    _view?.Filter = item => string.IsNullOrEmpty(SearchBox.Text)
        || ((PlaylistItem)item).FileName.Contains(SearchBox.Text,
            StringComparison.OrdinalIgnoreCase);
}
```

**Effort**: 2 hours.

---

### F6. PlaylistDialog — No Context Menu Actions
**File**: `PlaylistDialog.axaml`
**Evidence**: Right-clicking a playlist item does nothing. Missing: "Remove from playlist", "Move up/down", "Remove duplicates", "Clear playlist".

**Fix**: Add a `ContextMenu` to each playlist item `Button` with available actions. Bind to commands on `MainViewModel`.

**Effort**: 2 hours.

---

### F7. SeekBar — Chapter Marker Tooltips Missing
**File**: `SeekBarControl.axaml` lines ~45-55
**Evidence**: Chapter markers are rendered as thin `Rectangle` elements in a `Canvas`. No `ToolTip` is attached. User has no way to preview chapter names without scrubbing.

**Fix**: Add tooltips to each chapter marker:

```xml
<Rectangle Width="3" Height="12" Fill="{StaticResource OsdForeground}" Opacity="0.6">
    <ToolTip.Tip>
        <TextBlock>
            <Run Text="{Binding Title}"/><LineBreak/>
            <Run Text="{Binding Time, Converter={StaticResource TimeSpanToString}}"/>
        </TextBlock>
    </ToolTip.Tip>
</Rectangle>
```

**Effort**: 1 hour.

---

### F8. SeekBar — Chapter Preview Popover Boundary Safety
**File**: `SeekBarControl.axaml.cs`
**Evidence**: `ChapterPreviewPopover` positioning uses `Math.Clamp(xPos, 4, trackWidth - popoverWidth - 4)`. If `popoverWidth` is 0 (measurement not yet complete) or larger than `trackWidth`, the clamp fails and the popover renders off-screen.

**Fix**: Add measurement safety:

```csharp
if (popoverWidth <= 0 || popoverWidth > trackWidth * 0.8)
{
    ChapterPreviewPopover.MaxWidth = trackWidth * 0.6;
    // Defer positioning to LayoutUpdated callback
    return;
}
```

**Effort**: 1 hour.

---

### F9. GoToTimeDialog — Screen Center Positioning
**Status**: ✅ **Completed** (Commit pending)
**File**: `GoToTimeDialog.axaml.cs`
**Evidence**: Dialog uses `WindowStartupLocation="CenterOwner"` but may not properly center when opened from fullscreen.

**Fix**: Add explicit centering after `Show()`:
```csharp
var owner = TopLevel.GetTopLevel(this);
if (owner is Window w)
{
    PositionX = w.Position.X + (w.Bounds.Width - Width) / 2;
    PositionY = w.Position.Y + (w.Bounds.Height - Height) / 2;
}
```
**Effort**: 30 minutes.

---

### F10. SubtitleOverlay — Gear Button No Tooltip
**File**: `SubtitleOverlayControl.axaml.cs` line ~72
**Evidence**: The gear/settings button at the bottom of the subtitle flyout has no tooltip. Other buttons in the app consistently have `ToolTip.Tip` set.

**Fix**: Add tooltip in `AppendFlyoutFooter()`:

```csharp
ToolTip.SetTip(gearBtn, "Subtitle settings — font, size, color, encoding");
```

**Effort**: 15 minutes.

---

### F11. SubtitleOverlay — Drag-Over Visual Feedback Missing
**Status**: ✅ **Completed** (Commit pending)

**File**: `SubtitleOverlayControl.axaml`
**Evidence**: `DragDrop.SetAllowDrop(BtnSubtitles, true)` is set but no visual change occurs when dragging valid subtitle files over the button. Compare: `AudioTrackSelectorControl.axaml` has the same gap.

**Fix**: Add visual state on drag-over — accent glow on the button:

```csharp
private void ShowDragVisual()
{
    BtnSubtitles.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x5B, 0xDB, 0xFF));
}
private void ClearDragVisual()
{
    BtnSubtitles.Background = AppColors.Transparent;
}
```

**Effort**: 1 hour.

---

### F12. TrackFlyoutBuilder — Default Track Not Distinguished
**Files**: `TrackFlyoutBuilder.cs`
**Evidence**: `TrackMenuItem` has an `IsDefault` property, but `BuildTrackRow()` never uses it. All tracks look the same regardless of whether they're the container's default track or user-selected.

**Fix**: When `track.IsDefault` is true, show a small "★" icon or "Default" label:

```csharp
if (track.IsDefault)
{
    var defaultLabel = new TextBlock
    {
        Text = "★", FontSize = 10,
        Foreground = AppColors.Accent,
        VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 4, 0)
    };
    // Add to grid alongside dot and text
}
```

**Effort**: 1 hour.

---

### F13. Fullscreen — No Track Selector Access
**Files**: `FullscreenHeaderControl.axaml`, `MainWindow.axaml`
**Evidence**: In fullscreen mode, the `ControlsBox` (containing audio/subtitle/equalizer buttons) is hidden. Users cannot change audio tracks or subtitles without exiting fullscreen.

**Fix**: Add compact audio + subtitle toggle buttons to `FullscreenHeaderControl`. Standard practice in video players (VLC, MPC-HC, PotPlayer all have this).

```xml
<!-- Add to FullscreenHeaderControl.axaml -->
<StackPanel Orientation="Horizontal" Spacing="4"
            VerticalAlignment="Center" HorizontalAlignment="Right"
            Margin="{StaticResource space-h-2}">
    <controls:SubtitleOverlayControl x:Name="FullscreenSubOverlay" Width="28"/>
    <controls:AudioTrackSelectorControl x:Name="FullscreenAudioOverlay" Width="28"/>
</StackPanel>
```

**Effort**: Half day (need to wire up the controls and ensure flyout positioning works in fullscreen context).

---

### F14. Wires — Audio Equalizer Flyout Relative to Equalizer Button
**File**: `ControlsBoxControl.axaml.cs` method `OpenEqualizerFlyout()`
**Evidence**: Equalizer is opened using `_flyoutOverlay.ShowContent(anchor, content, placeAbove: true)`. But `anchor` is `BtnEqualizer ?? BtnFullscreen`. In the XAML, `BtnEqualizer` is defined, so this works — but the flyout content is an `AudioEqualizerFlyout` UserControl rather than a `Border` from `TrackFlyoutBuilder.BuildContent()`. This creates an inconsistency: equalizer uses the old `Popup`-based approach while other flyouts use the new `FlyoutOverlayControl`.

**Fix**: Migrate equalizer to use `TrackFlyoutBuilder.BuildContent()` with the equalizer-specific supplement appended via `appendExtra`. This unifies all flyout content under the single overlay system.

**Effort**: 1 day.

---

### F15. Flyout Entrance/Exit Animations
**Files**: All flyout consumers (ControlsBox, HeaderBar, SubtitleOverlay)
**Evidence**: All flyouts appear/disappear instantly (0ms transition). This is jarring and doesn't match the platform's animation language. The only fading that exists is the OSD notification.

**Fix**: Add subtle scale+opacity animations. In the `FlyoutOverlayControl`:

```xml
<Border x:Name="ContentContainer" ...>
    <Border.Transitions>
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="{StaticResource duration-fast}"/>
            <TransformOperationsTransition Property="RenderTransform" Duration="{StaticResource duration-fast}"/>
        </Transitions>
    </Border.Transitions>
    <Border.RenderTransform>
        <ScaleTransform ScaleX="0.96" ScaleY="0.96"/>
    </Border.RenderTransform>
</Border>
```

When content is shown, animate to `ScaleX=1, ScaleY=1, Opacity=1`. When hidden, reverse.

**Effort**: 3 hours (requires animation logic in code-behind).

---

### F16. Flyout Focus Management
**Files**: `FlyoutOverlayControl.axaml.cs`
**Evidence**: When a flyout opens, keyboard focus stays on the anchor button. Tab navigation doesn't enter the flyout content. Users must click inside the flyout to interact with it.

**Fix**: After `ShowContent()`, programmatically focus the first interactive element inside the flyout:

```csharp
public void ShowContent(Control anchor, Control content, bool placeAbove = true)
{
    // ... existing positioning code ...
    
    // Focus first focusable element inside the content
    Dispatcher.UIThread.Post(() =>
    {
        var firstFocusable = content.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c.Focusable);
        firstFocusable?.Focus();
    });
}
```

**Effort**: 1 hour.

---

### F17. OSD Click Action Never Wired
**Status**: ✅ **Completed** (Commit pending)

**File**: `MainWindow.Wiring.cs` line ~65, `OsdNotificationControl.axaml.cs`
**Evidence**: `OsdNotificationControl.NotificationClicked` is a real event but the handler in Wiring only shows a debug log. No meaningful action is taken when the user clicks an OSD notification.

**Fix**: Make OSD clicks actionable based on the message category:
- Volume OSD click → focus the volume slider
- Subtitle OSD click → open subtitle selector
- Speed OSD click → reset speed
- Error OSD click → log for debugging

```csharp
string category = (e as OsdClickedEventArgs)?.Category ?? "default";
switch (category) { ... }
```

**Effort**: 1 hour.

---

### F18. ControlsBox — Volume Slider Value Not Synced with Mute
**File**: `ControlsBoxControl.axaml.cs`
**Evidence**: Volume slider range is 0-150 with preset buttons at 25/50/75/100. When the user mutes via `BtnToggleMute`, the slider doesn't visually snap to 0. When unmuted, it should restore to the previous volume.

**Fix**: Track `_volumeBeforeMute` and synchronize slider position with mute state:

```csharp
private void OnToggleMute(object? sender, RoutedEventArgs e)
{
    if (_viewModel == null) return;
    if (_viewModel.IsMuted)
    {
        _volumeBeforeMute = _viewModel.VolumeValue;
        _viewModel.VolumeValue = 0;
    }
    else
    {
        _viewModel.VolumeValue = _volumeBeforeMute > 0 ? _volumeBeforeMute : 50;
    }
}
```

**Effort**: 1 hour.

---

### F19. PreferencesDialog — No "Reset to Defaults" Option
**Status**: ✅ **Completed** (Commit pending)

**File**: `PreferencesDialog.axaml`
**Evidence**: Preferences page has General and Rendering sections with toggle switches and dropdowns. No way to reset all settings back to defaults.

**Fix**: Add a "Reset to Defaults" button at the bottom of the dialog:

```xml
<Button Content="Reset to Defaults" Classes="flyout-item destructive"
        Click="OnResetDefaults" Margin="{StaticResource space-3}"/>
```

Handler resets `AudioSettingsStore` + `SubtitleSettingsStore` to factory defaults.

**Effort**: Half day.

---

## Part 3: Additional Improvements (No Code Changes Needed)

### I1. Responsive Layout System
**Current**: `HeaderBarControl.UpdateResponsiveLayout(double width)` hides PIP at `< 600px`, `ControlsBoxControl` hides video menu at narrow width. This is basic.

**Improvement needed**: Add breakpoints and behaviors for:
- `> 1200px`: Full layout, all controls visible
- `600–1200px`: Standard layout
- `400–600px`: Compact — hide PIP, reduce title width, collapse some buttons
- `< 400px`: Minimal — hide most chrome, transport only

### I2. Touch/Tablet Mode
**Current**: All controls assume mouse input. Button sizes are 28-32px (hard to tap on touch).

**Improvement needed**: Detect touch input and scale up interactive targets to 44px minimum per Material touch guidelines.

---

## Updated Phase Plan

| Phase | Scope | Duration | Priority | Status |
|-------|-------|----------|----------|--------|
| **Phase 4** — Token System & Transparency | P1 (tokens), P2 (transparency), P5 (dividers) | 2 days | High — foundation | ⏳ Pending |
| **Phase 5** — Functional Completeness | F1–F19 (19 functional fixes) | 1–2 weeks | High — makes every feature actually work | **13/19 completed** |
| **Phase 6** — Polish & Responsiveness | P3 (typography), P4 (button states), F15 (animations), F16 (focus), I1, I2 | 1 week | Medium — quality feel | ✅ F15, F16 done |

---

*Document Version: 3.0 — Complete deep-audit revision*
*Date: 2026-07-01*
*Scope: 10 premium refinements + 19 functional fixes + 2 improvements*
*Status: Planning — awaiting prioritize signal from user before implementation*