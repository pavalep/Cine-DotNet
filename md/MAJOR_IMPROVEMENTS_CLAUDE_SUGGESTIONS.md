# CINE — MAJOR IMPROVEMENTS & PRODUCTION READINESS PLAN
> **Current State Assessment: ~60% Production Ready**
> **Target: 100% Production Releasable**
> **Last Updated: 2026-06-06**

---

## TABLE OF CONTENTS
1. [Critical Bugs (Phase 1)](#phase-1-critical-bugs)
2. [PiP Complete Overhaul (Phase 2)](#phase-2-pip-overhaul)
3. [Controls, Menus & Interaction (Phase 3)](#phase-3-controls-menus-interaction)
4. [Main Window Layout (Phase 4)](#phase-4-main-window-layout)
5. [Premium UI / Visual Design (Phase 5)](#phase-5-premium-ui-visual-design)
6. [Track System (Phase 6)](#phase-6-track-system)
7. [Architecture & Code Quality (Phase 7)](#phase-7-architecture-code-quality)
8. [New Features (Phase 8)](#phase-8-new-features)
9. [Petty UI/UX Issues Found on Review (Phase 9)](#phase-9-petty-uiux-issues)
10. [PiP Redesign — Complete Modern Spec (Phase 10)](#phase-10-pip-redesign-complete-modern-spec)
11. [Files To Create](#files-to-create)
12. [Checklist Summary](#checklist-summary)

---

## PHASE 1 — CRITICAL BUGS

---

### ✅ 1.1 — Volume button scroll duplicated in two places
**Status: FIXED** — Removed `OnVolumeButtonScroll` from `MainWindow.Input.cs`.
**Severity:** HIGH
**Files:**
- [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs) — retains `OnVolumeButtonScroll`
- [`MainWindow.Input.cs`](../src/App/UI/Shell/MainWindow.Input.cs) — removed duplicate handler

---

### ✅ 1.2 — FullscreenHeaderControl has `IsVisible="False"` set twice
**Status: FIXED** — Removed `IsVisible="False"` from outer UserControl element.
**Severity:** MEDIUM
**File:** [`FullscreenHeaderControl.axaml`](../src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml)

---

### ✅ 1.3 — `OnMediaEnded` never wired to player
**Status: FIXED** — Wired via `PlaybackStateChangedEvent` detecting `State == Stopped` with file path to show replay overlay.
**Severity:** HIGH
**Files:** [`MainWindow.Core.cs`](../src/App/UI/Shell/MainWindow.Core.cs), [`MainWindow.Media.cs`](../src/App/UI/Shell/MainWindow.Media.cs)

---

### ✅ 1.4 — PiP position sync
**Status: ALREADY CORRECT** — `SyncPipPosition` already has `IsActive` guard. Initial position pushed immediately in `OnPipToggled`.
**Severity:** MEDIUM
**File:** [`MainWindow.Pip.cs`](../src/App/UI/Shell/MainWindow.Pip.cs)

---

### ✅ 1.5 — Keyboard shortcut conflicts (L key / Ctrl+S)
**Status: FIXED**
- Removed duplicate `Ctrl+L` → `ToggleLoopFile` (only `Shift+L` remains for loop)
- Changed `Ctrl+S` from `ToggleShuffle` → `Stop` (matches menu)
**File:** [`MainWindow.Input.cs`](../src/App/UI/Shell/MainWindow.Input.cs)

---

### ✅ 1.6 — Auto-hide timer starts even when no media loaded
**Status: FIXED** — Timer no longer starts in `InitializeAutoHide()`. Only starts in `ShowUiControls()` when `hasMedia` is true.
**File:** [`MainWindow.AutoHide.cs`](../src/App/UI/Shell/MainWindow.AutoHide.cs)

---

### ✅ 1.7 — Right-click context menu labels not styled as section headers
**Status: FIXED** — "ASPECT RATIO" and "SPEED" now styled as section headers (uppercased, bold, dimmed, letter-spaced).
**File:** [`MainWindow.Input.cs`](../src/App/UI/Shell/MainWindow.Input.cs)

---

## PHASE 2 — PiP OVERHAUL

---

### ✅ 2.1 — PiP Window Size — Wrong Defaults & No Aspect Ratio Lock
**Status: FIXED**
- Default: 640×360 (16:9), Min: 320×180, Max: 1280×720
- Added `_aspectRatio` field + `SetAspectRatio()` method
- Aspect ratio lock in `OnSizeChanged`
- Wired `SetAspectRatio` in `MainWindow.Pip.cs` from `player.GetVideoSize()`
**Files:** [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml), [`PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs), [`MainWindow.Pip.cs`](../src/App/UI/Shell/MainWindow.Pip.cs)

---

### ✅ 2.2 — PiP Resize Grip is Non-Functional
**Status: FIXED** — Removed `IsHitTestVisible="False"`, added `x:Name="ResizeGrip"`, wired `BeginResizeDrag(WindowEdge.SouthEast)` in code-behind.
**Files:** [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml), [`PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs)

---

### ✅ 2.3 — PiP Has No Border/Frame on Video Area
**Status: FIXED** — Added `Background="#0D000000"` + `CornerRadius="12"` to VideoArea border.
**File:** [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml)

---

### ✅ 2.4 — PiP "live" Badge Always Shows (Wrong)
**Status: FIXED** — Added `IsVisible="False"` default on the badge element.
**Severity:** MEDIUM
**File:** [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml)

---

### ✅ 2.5 — PiP Controls Hidden But TitleBar Auto-Hide Broken
**Status: FIXED** — Timer only hides TitleBar (not HoverOverlay). Added `ShowControls()` + window-level `PointerMoved` handler + `OnWindowPointerMoved` that restores both overlay and titlebar.
**File:** [`PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs)

---

### ✅ 2.6 — PiP Has Return to Main Window Button
**Status: FIXED** — Added `ExpandButton` with `MaxRestoreIcon` in titlebar and `OnExpandClick` handler (closes PiP, main window restores video via `PipClosed`).
**Severity:** HIGH
**Files:** [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml), [`PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs)

---

### ✅ 2.7 — PiP State Restored With Wrong Position (Off-Screen Risk)
**Status: FIXED** — Added screen bounds check in `RestoreState()`. Creates new `PipState` with top-right default if saved position is off-screen.
**Severity:** MEDIUM
**File:** [`PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs)

---

### ✅ 2.8 — PiP DWM Thumbnail `SyncThumbnailRect` Hardcoded Titlebar Height
**Status: FIXED** — `SyncThumbnailRect` now measures `TitleBar?.Bounds.Height` and `SeekContainer?.Bounds.Height` at runtime instead of hardcoded 32/80px. `OnSizeChanged` aspect ratio lock also uses measured titlebar height.
**Severity:** MEDIUM
**File:** [`PipWindow.axaml.cs`](../src/App/UI/Screens/Dialogs/PipWindow.axaml.cs)

---

## PHASE 3 — CONTROLS, MENUS & INTERACTION

---

### 3.1 — Duplicate Menu Entries: Loop/Shuffle/Fullscreen in Both Controls Bar AND Primary Menu
**Severity:** HIGH
**Files:** [`ControlsBoxControl.axaml`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml), [`HeaderBarControl.axaml`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml)

**Issue:** Loop, Shuffle, Fullscreen exist in 3 places: toolbar toggles, primary menu, and right-click context menu. No visual sync (checked state in toolbar not reflected in menu).

**Fix:**
- Add checkmark indicators in menu items for states that are toggled in toolbar
- Remove Fullscreen from controls bar (redundant; primary menu + F key is sufficient)
- Or consolidate: remove all toggle buttons from controls bar, keep only transport + volume + subtitle/audio

---

### ✅ 3.2 — Track Menus Built Dynamically Without Scroll
**Status: FIXED** — Added `ScrollViewer` with `MaxHeight="320"` wrapping the stack panel in `BuildTrackMenuFlyout()`.
**Severity:** HIGH
**File:** [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs)

---

### 3.3 — Subtitles Button Opens Track Menu But Has No "Disable" Entry First
**Severity:** MEDIUM
**Fix:** Ensure "None" pseudo-entry is always first in `SubtitleTracks` collection.

---

### ✅ 3.4 — Volume Flyout Mute Toggle Icon Does Not Reflect State
**Status: FIXED** — Added `x:Name="MuteToggleIcon"` on the flyout toggle icon, wired `MuteToggleIcon.Kind = VolumeOff/VolumeHigh` in `RefreshVolumeIcon()`.
**Severity:** MEDIUM
**File:** [`ControlsBoxControl.axaml`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml), [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs)

---

### ✅ 3.5 — Volume Slider Maximum Mismatch (XAML: 150, CSS Style: 130)
**Status: FIXED** — Removed `Maximum` setter from `volume-slider` style in App.axaml. Instance maximum of 150 remains.
**Severity:** MEDIUM
**File:** [`App.axaml`](../src/App/UI/Resources/App.axaml)

---

### 3.6 — Chapters Flyout Uses Incorrect Seek Calculation
**Severity:** HIGH
**File:** [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs) line 249

**Fix:** Verify `SeekTo(double)` signature — if it takes 0–1 normalized, current code is correct. If seconds, fix.

---

### ✅ 3.7 — OptionsMenuButton is a Separate Component That Duplicates Primary Menu Logic
**Status: FIXED** — Removed `OptionsMenuButton` from ControlsBoxControl. Replaced with a simple `BtnVideoEqualizer` button that opens `EqualizerDialog`. Deleted `OptionsMenuButton.axaml` (18.7KB) and `OptionsMenuButton.axaml.cs` (94 lines). Added `OnVideoEqualizerClick` handler.
**Severity:** HIGH
**Files:** [`ControlsBoxControl.axaml`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml), [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs), [`MainWindow.Input.cs`](../src/App/UI/Shell/MainWindow.Input.cs)
**Deleted:** [`OptionsMenuButton.axaml`](../src/App/UI/Controls/Buttons/OptionsMenuButton.axaml), [`OptionsMenuButton.axaml.cs`](../src/App/UI/Controls/Buttons/OptionsMenuButton.axaml.cs)

---

### ✅ 3.8 — Equalizer Dialog Opened from Keyboard But ViewModel May Be Null
**Status: FIXED** — Added null check `if (_viewModel != null)` before opening dialog.
**Severity:** MEDIUM
**File:** [`MainWindow.Input.cs`](../src/App/UI/Shell/MainWindow.Input.cs)

---

### ✅ 3.9 — HeaderBar Primary Menu and FullscreenHeader Menu Are Nearly Identical
**Status: FIXED** — Created `PrimaryMenuBuilder` shared helper class that builds the entire menu in code. Both `HeaderBarControl` and `FullscreenHeaderControl` now call it, eliminating ~200 lines of duplicated XAML. Each passes its own click handlers and toggle state functions. Added `SyncCheckStates()` for toggle items (Fullscreen, Loop File, Loop Playlist, Shuffle). Files changed: `HeaderBarControl.axaml` (flyout replaced with 3 lines), `FullscreenHeaderControl.axaml` (flyout replaced with 3 lines), `HeaderBarControl.axaml.cs` (added `BuildPrimaryMenu()`), `FullscreenHeaderControl.axaml.cs` (added `BuildFullscreenMenu()`).
**Severity:** MEDIUM
**New file:** [`PrimaryMenuBuilder.cs`](../src/App/Application/Helpers/PrimaryMenuBuilder.cs)
**Files changed:** [`HeaderBarControl.axaml`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml), [`HeaderBarControl.axaml.cs`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs), [`FullscreenHeaderControl.axaml`](../src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml), [`FullscreenHeaderControl.axaml.cs`](../src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml.cs)

---

## PHASE 4 — MAIN WINDOW LAYOUT & POSITIONING

---

### ✅ 4.1 — Video Positioning Inside Window Off-Center After Resize
**Status: FIXED** — `SyncThumbnailRect()` now measures actual `_headerBar.Bounds.Height` / `_fullscreenHeader.Bounds.Height` / `_controlsBox.Bounds.Height` at runtime instead of hardcoded 44/120px. Falls back to reasonable defaults if layout not yet complete. Also respects `_uiVisible` state (zero header/controls when hidden).
**Severity:** HIGH
**File:** [`MainWindow.Core.cs`](../src/App/UI/Shell/MainWindow.Core.cs)

---

### ✅ 4.2 — Window Minimum Size Too Small for Controls Bar
**Status: FIXED** — `MinWidth` raised from 332 → 600, `MinHeight` from 187 → 337 (16:9 ratio at 600px).
**Severity:** MEDIUM
**File:** [`MainWindow.axaml`](../src/App/UI/Views/MainWindow.axaml)

---

### ✅ 4.3 — Start Page and Playback Background Z-Order Conflict
**Status: FIXED** — `PlaybackBackground.IsVisible = false` now delayed 350ms alongside StartPage fade. Keeps radial gradient visible during crossfade, preventing raw window background flash. If StartPage already hidden, hides immediately.
**Severity:** MEDIUM
**File:** [`MainWindow.Media.cs`](../src/App/UI/Shell/MainWindow.Media.cs)

---

### ✅ 4.4 — Window Centering Logic Has Race Condition
**Status: FIXED** — Manual centering in `OnOpened` now checks `File.Exists(WindowStatePath)`. If saved state exists, skips centering and lets saved restoration set position. Also updated min defaults in centering code from 332/187 to 600/337.
**Severity:** MEDIUM
**File:** [`MainWindow.Core.cs`](../src/App/UI/Shell/MainWindow.Core.cs)

---

### ✅ 4.5 — SeekBar Right Margin Hardcoded to 20px (Not Symmetric)
**Status: FIXED** — Changed from `Margin="3,0,20,5"` to `Margin="12,0,12,5"` for symmetry.
**Severity:** LOW
**File:** [`ControlsBoxControl.axaml`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml)

---

### ✅ 4.6 — SeekBar Duration Label Margin "-7,0" Negative Margin Hack
**Status: FIXED** — Replaced `Margin="-7,0"` with `Margin="4,0,0,0"` and `Margin="0,0,20,0"` with `"0,0,4,0"`. Added `ColumnSpacing="4"` to root Grid for consistent layout without hacky margins.
**Severity:** LOW
**File:** [`SeekBarControl.axaml`](../src/App/UI/Controls/SeekBar/SeekBarControl.axaml)

---

## PHASE 5 — PREMIUM UI / VISUAL DESIGN OVERHAUL

---

### ✅ 5.1 — Controls Bar Has No Visual Depth or Glass Effect
**Status: FIXED** — Updated `ControlsGradient` and `HeaderGradient` to use deep navy tint (`#D0081420`) instead of pure black. Added 1px top border definition via warmer tones. Added `DoubleTransition` for smooth opacity.
**Severity:** HIGH
**Files:** [`Colors.axaml`](../src/App/UI/Resources/Colors.axaml), [`App.axaml`](../src/App/UI/Resources/App.axaml)

---

### ✅ 5.2 — Header Bar Merge Issue
**Status: FIXED** — Raised top opacity (`#B00A1625` → `#C0102035`, i.e. 69%→75%). Added `BorderBrush="#1AFFFFFF"` / `BorderThickness="0,0,0,1"` for subtle bottom border.
**Severity:** MEDIUM
**Files:** [`Colors.axaml`](../src/App/UI/Resources/Colors.axaml), [`HeaderBarControl.axaml`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml)

---

### ✅ 5.3 — Font Sizes Too Small and Inconsistent
**Status: FIXED** — Section headers `FontSize=9`→`10` in `PrimaryMenuBuilder`. PiP time 11→12px.
**Severity:** MEDIUM
**Files:** [`PrimaryMenuBuilder.cs`](../src/App/Application/Helpers/PrimaryMenuBuilder.cs), [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml)

---

### ✅ 5.4 — Button Sizes Uniform (34px) — No Hierarchy
**Status: FIXED** — Created `circular-play` (40×40, icon 22px). `circular-menu`/`circular-toggle` 30×30 (CornerRadius=15).
**Severity:** MEDIUM
**Files:** [`App.axaml`](../src/App/UI/Resources/App.axaml), [`ControlsBoxControl.axaml`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml)

---

### ✅ 5.5 — No Hover Tooltip Delay
**Status: FIXED** — Added global `ToolTip` style with `ShowDelay="600"`.
**Severity:** LOW-MEDIUM
**File:** [`App.axaml`](../src/App/UI/Resources/App.axaml)

---

### ✅ 5.6 — No Smooth Opacity Transitions on Controls Show/Hide
**Status: FIXED** — Added `DoubleTransition` for `Opacity` (0.25s) to both `Border.controls-box` and `Border.header-bar` styles in `App.axaml`.
**Severity:** MEDIUM
**File:** [`App.axaml`](../src/App/UI/Resources/App.axaml)

---

### ✅ 5.7 — Start Page Lacks Premium "First Run" Experience
**Status: FIXED** — Enhanced drop zone: accent-colored border, CornerRadius=16, "Drop to Play" message with PlayCircle icon.
**Severity:** HIGH
**File:** [`StartPage.axaml`](../src/App/UI/Screens/Start/StartPage.axaml)

---

### ✅ 5.8 — OSD Notification Style Is Basic
**Status: FIXED** — CornerRadius 8→20 (pill shape). Added slide-up Y animation (20→0 on show, 0→20 on hide).
**Severity:** MEDIUM
**Files:** [`OsdNotificationControl.axaml`](../src/App/UI/Controls/Indicators/OsdNotificationControl.axaml), [`OsdNotificationControl.axaml.cs`](../src/App/UI/Controls/Indicators/OsdNotificationControl.axaml.cs)

---

### ✅ 5.9 — Window Controls Non-Standard
**Status: FIXED** — Hover: `#21FFFFFF`→`#2BFFFFFF`. Added 120ms BrushTransition. Minimize icon 12→14px.
**Severity:** MEDIUM
**Files:** [`App.axaml`](../src/App/UI/Resources/App.axaml), [`HeaderBarControl.axaml`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml)

---

### ✅ 5.10 — PiP Window Style Inconsistent With Main Window
**Status: FIXED** — `Foreground="#E5E5E5"` → `Foreground="{StaticResource OsdForeground}"`.
**Severity:** MEDIUM
**File:** [`PipWindow.axaml`](../src/App/UI/Screens/Dialogs/PipWindow.axaml)

---

### ✅ 5.11 — ToggleButton Checked State = Solid White Background (Ugly)
**Status: FIXED** 
- `ToggleButtonCheckedBackground` changed from `White` → `Accent` (#0078D4)
- `ToggleButtonCheckedForeground` changed from `Black` → `White`
- Removed the black icon inversion style (white icons on accent bg look cleaner)
**Severity:** HIGH
**File:** [`Colors.axaml`](../src/App/UI/Resources/Colors.axaml), [`App.axaml`](../src/App/UI/Resources/App.axaml)

---

### ✅ 5.12 — Volume Flyout Popover `ToggleButton` Also Gets White Background When Checked
**Severity:** MEDIUM
**File:** [`App.axaml`](../src/App/UI/Resources/App.axaml) line 571
```xml
<Style Selector="ToggleButton#BtnMuteToggle:checked /template/ ContentPresenter Path">
    <Setter Property="Fill" Value="Black" />
    <Setter Property="Stroke" Value="Black" />
</Style>
```
Same issue — mute toggle switches to white bg + black icon when muted. Use `Accent` color instead.

---

## PHASE 6 — TRACK SYSTEM (SUBTITLES, AUDIO, VIDEO)

---

### 6.1 — SubtitleTracks / AudioTracks Not Populated on Media Open
**Severity:** CRITICAL
**Fix:** Ensure `RefreshState()` populates track collections.

---

### 6.2 — SubtitleIconPath Uses Wrong Icon Logic
**Severity:** MEDIUM
**Fix:** `SubtitlesOff` for disabled state.

---

### 6.3 — Audio Track Icon Uses Music Note (Wrong Metaphor)
**Severity:** LOW
**Fix:** `Headphones` / `HeadphonesOff`.

---

### 6.4 — No Subtitle Delay UI Control (Only Keyboard Shortcuts)
**Severity:** MEDIUM
**Fix:** Add delay +/- controls to subtitle flyout.

---

### 6.5 — Audio Delay Also Keyboard-Only
**Severity:** MEDIUM
**Fix:** Add delay controls to audio flyout.

---

## PHASE 7 — ARCHITECTURE & CODE QUALITY

---

### 7.1 — Debug Logging Left in Production Code
**Severity:** MEDIUM
**Fix:** Add `[Conditional("DEBUG")]` attribute.

---

### 7.2 — Empty Catch Blocks
**Severity:** MEDIUM
**Fix:** Add `DebugLog` at minimum.

---

### 7.3 — `MainWindow.Core.cs` Is 715 Lines — Too Large
**Severity:** LOW

---

### 7.4 — `PropertyWatcher` Uses Both String and Lambda Overloads
**Severity:** LOW

---

### 7.5 — PipService Lives in Wrong Location
**Severity:** LOW

---

## PHASE 8 — NEW FEATURES

---

### 8.1 — No Error State UI
**Priority:** HIGH

### 8.2 — No Loading Progress
**Priority:** MEDIUM

### 8.3 — No Thumbnail on Windows Taskbar
**Priority:** MEDIUM

### 8.4 — No Media Keys Integration Beyond Play/Pause
**Priority:** MEDIUM

### 8.5 — No "Open Recent" in Start Page
**Priority:** HIGH

### 8.6 — No Preference Persistence for Audio/Subtitle Track Selection
**Priority:** MEDIUM

### 8.7 — Playlist Dialog Requires a "Save Playlist" Feature
**Priority:** LOW

### 8.8 — No "Go to Time" Dialog
**Priority:** LOW

---

## PHASE 9 — PETTY UI/UX ISSUES (New — Found 2026-06-06)

> These are the "death by a thousand cuts" issues that make the player feel non-premium. Each is small but collectively they ruin the experience.

---

### ✅ 9.1 — Play/Pause Icon Not Synced With Actual Playback State
**Status: FIXED** — Added optimistic toggle in `OnPlayPause`: icon is flipped immediately before calling `_viewModel.PlayPause()`, eliminating the race condition.
**Severity:** HIGH
**Files:** [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs)

---

### ✅ 9.2 — Startup Page Shows Control Bar Overlap Briefly
**Status: FIXED** — `StartPage.IsVisible = false` is now delayed 300ms (matches fade transition). `ShowUiControls()` is also delayed 250ms via async Task.Delay to avoid rendering on top of fading start page.
**Severity:** MEDIUM
**Files:** [`MainWindow.Media.cs`](../src/App/UI/Shell/MainWindow.Media.cs)

---

### ✅ 9.3 — Seek Bar Thumb Position Jumps on First Frame After Seek
**Status: FIXED** — `UpdateSeekBar()` is now called BEFORE clearing `_isSeeking` in `OnSeekAreaPointerReleased`, preventing the thumb from snapping back to old position while waiting for `PositionChanged` event.
**Severity:** MEDIUM
**Files:** [`SeekBarControl.axaml.cs`](../src/App/UI/Controls/SeekBar/SeekBarControl.axaml.cs)

---

### ✅ 9.4 — Volume Flyout Popover Does Not Auto-Dismiss Consistently
**Status: FIXED** — Added `OnVolumeAutoDismiss` timer-based auto-close: after 1.5s of slider inactivity (pointer wheel or release), the flyout closes automatically.
**Severity:** MEDIUM
**File:** [`ControlsBoxControl.axaml.cs`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs)

---

### ✅ 9.5 — Control Bar / Header Bar Backgrounds Not Truly Transparent
**Status: FIXED** — Changed from pure black gradients to deep navy tint (`#D0081420` at bottom, `#B00A1625` at top). Added warmer tones consistent with premium players.
**Severity:** MEDIUM
**File:** [`Colors.axaml`](../src/App/UI/Resources/Colors.axaml)

---

### 9.6 — Header Title Has No Shadow / Hard to Read on Bright Video
**Severity:** LOW-MEDIUM
**File:** [`HeaderBarControl.axaml`](../src/App/UI/Screens/Shell/HeaderBarControl.axaml)

**Issue:** The title `TextBlock` has `Foreground="{StaticResource OsdForeground}"` (white) with only a drop shadow from the global style. On bright video backgrounds, the text is barely readable.

**Fix:** Add a semi-transparent dark background pill behind the title, like YouTube's header. Or increase the text shadow:
```xml
<TextBlock Grid.Column="1" x:Name="TitleText">
    <TextBlock.Effect>
        <DropShadowEffect BlurRadius="8" OffsetY="2" Color="#CC000000" />
    </TextBlock.Effect>
</TextBlock>
```

---

### 9.7 — Replay Overlay and Pause Overlay Use Different Visual Languages
**Severity:** LOW-MEDIUM
**Files:** [`ReplayOverlayControl.axaml`](../src/App/UI/Controls/Indicators/ReplayOverlayControl.axaml), [`PauseOverlayControl.axaml`](../src/App/UI/Controls/Indicators/PauseOverlayControl.axaml)

**Issue:**
- Replay overlay: `AppOverlayLight` background, rounded, with a play button + text
- Pause overlay: `AppOverlayLight` background, centered pause icon, fades in
- These should share the same visual pattern (same corner radius, same padding, same animation)

**Fix:** Create a consistent overlay style:
```xml
<Style Selector="Border.media-overlay">
    <Setter Property="Background" Value="{StaticResource AppOverlayDark}" />
    <Setter Property="CornerRadius" Value="16" />
    <Setter Property="Padding" Value="28" />
</Style>
```

---

### 9.8 — Divider Lines in Controls Bar Use `AppDivider` (#26FFFFFF) — Too Subtle
**Severity:** LOW
**File:** [`ControlsBoxControl.axaml`](../src/App/UI/Screens/Shell/ControlsBoxControl.axaml) lines 65, 104, 160, 190

**Issue:** The vertical separator rectangles use `Fill="{StaticResource AppDivider}"` which is `#26FFFFFF` (15% opacity white). On dark backgrounds, these are nearly invisible.

**Fix:** Use `AppDividerStrong` (`#33FFFFFF`) or a custom `#40FFFFFF` for better visual separation between button groups.

---

### ✅ 9.9 — OptionsMenuButton and Primary Menu Duplicate Large Amount of Content
**Status: FIXED** — OptionsMenuButton removed entirely. Replaced with `BtnVideoEqualizer` that opens EqualizerDialog. Deleted 18.7KB XAML + 94 lines code-behind. See 3.7 for details.
**Severity:** HIGH

---

### 9.10 — Seek Bar Thumb Has Drop Shadow But Track Does Not — Inconsistent
**Severity:** LOW
**File:** [`SeekBarControl.axaml`](../src/App/UI/Controls/SeekBar/SeekBarControl.axaml) lines 25–33

**Issue:** The seek thumb has a `DropShadowEffect` but the track fill (`SeekFill`) and trough (`SeekTrack`) do not. This makes the thumb look detached from the track.

**Fix:** Add a subtle glow to `SeekFill`:
```xml
<Border x:Name="SeekFill" ...>
    <Border.Effect>
        <DropShadowEffect BlurRadius="2" Color="#40FFFFFF" />
    </Border.Effect>
</Border>
```

---

### 9.11 — Window Has No Corner Radius (Sharp Edges) While All Dialogs Are Rounded
**Severity:** LOW (consistency)
**File:** [`App.axaml`](../src/App/UI/Resources/App.axaml) line 18 — `Window` style

**Issue:** All popovers, flyouts, and dialogs have `CornerRadius="8"` or `CornerRadius="6"`, but the main window has sharp corners (`CornerRadius` not set). On Windows 11 with rounded corners, the window frame is rounded, but the content area is not clipped to match.

**Fix:** This is a Nit — Windows 11 handles it natively.

---

### 9.12 — Loading Spinner Overlaps With Start Page Content
**Severity:** MEDIUM
**File:** [`MainWindow.axaml`](../src/App/UI/Views/MainWindow.axaml)

**Issue:** `SpinnerOverlayControl` and `StartPage` both render in the same layer stack. When opening a file, the spinner shows briefly but overlaps with the fading start page and the controls bar, creating visual clutter.

**Fix:** Ensure spinner fades in AFTER start page begins fading out (sequential animation, not parallel).

---

## PHASE 10 — PiP REDESIGN: COMPLETE MODERN SPEC

> The current PiP needs a complete visual redesign. Below is the target spec based on macOS/iOS/YouTube PiP standards.

---

### 10.1 — Visual Design Target

```
┌──────────────────────────────────────┐
│ ··· Cine PIP           — □ 📌 ⊞ ✕ │  ← Titlebar: 28px, semi-transparent dark
│                                      │     with acrylic blur
│                                      │
│           ▶  (center)               │  ← Video area with hover play/pause
│                                      │
│                                      │
│  ─────●───────────────────── 00:00  │  ← Seek bar overlay: 4px thick,
│                                      │     shows on hover only
│          ◀ ▶                         │  ← Subtle control strip
└──────────────────────────────────────┘
     ↑ 1px accent border (#30FFFFFF)
```

### 10.2 — Key Spec Changes

| Property | Current | Target |
|----------|---------|--------|
| Default size | 640×360 | 480×270 (smaller default) |
| Titlebar height | 32px | 28px |
| Titlebar style | `#E5252540` | Acrylic/blur with 80% opacity |
| Border | None | 1px `#30FFFFFF` outer |
| Corner radius | 12px | 10px (smaller, more modern) |
| Seek bar | Always visible overlay | Auto-hide, shows on hover only |
| Center play button | 48×48 circle | 40×40 circle with scale animation |
| Time font size | 11px | 12px |
| Controls layout | Row 0=title, Row 1=video+overlay, Row 2=grip | Compact: titlebar + video (layered) |
| File badge | Top-left, always visible | Top-left, auto-hides after 3s |

### 10.3 — Required Structural Changes

A. **Modern titlebar layout:**
```xml
<Border Classes="pip-titlebar">
    <Grid ColumnDefinitions="Auto,*,Auto,Auto,Auto,Auto">
        <!-- Drag area + title -->
        <TextBlock Text="Cine PIP" x:Name="PipFileNameLabel" />
        <!-- Controls: minimize, pin, expand, close -->
    </Grid>
</Border>
```
Add the "expand to main window" button (see 2.6).

B. **Hover behavior refined:**
- All controls (titlebar, seek bar, center button, file badge) auto-hide after 2s
- Moving the mouse anywhere in the PiP window shows all controls
- Center button disappears when not hovering (like YouTube PiP)

C. **Video area styling:**
```xml
<Border Background="#08000000" CornerRadius="10" ClipToBounds="True">
    <!-- DWM thumbnail renders through -->
</Border>
```

D. **Remove the separate resize grip row:**
- Incorporate resize handles into the window frame (transparent 8px strips on all 4 edges and corners)
- Use `WindowEdge` + `BeginResizeDrag` properly
- The visible resize grip icon can remain as a subtle indicator, but resize should work from any edge/corner

---

## FILES TO CREATE

| File Path | Purpose |
|-----------|---------|
| `src/App/UI/Controls/Indicators/ErrorOverlayControl.axaml` | Error state UI |
| `src/App/UI/Controls/Indicators/ErrorOverlayControl.axaml.cs` | Error overlay logic |
| `src/App/Infrastructure/Api/TaskbarIntegration.cs` | Windows taskbar progress |
| `src/App/Infrastructure/Api/SmtcIntegration.cs` | System Media Transport Controls |
| `src/App/UI/Shell/MainWindow.PropertyWatchers.cs` | Extracted from Core.cs |
| `src/App/UI/Shell/MainWindow.Persistence.cs` | Window state save/restore |
| `src/App/UI/Shell/MainWindow.DwmSync.cs` | DWM thumbnail sync |
| `src/App/Application/Services/PipService.cs` | Moved from Controls/Video |

---

## CHECKLIST SUMMARY

### Fixed (✅):
- 1.1 Duplicated volume scroll → REMOVED from MainWindow.Input.cs
- 1.2 FullscreenHeader IsVisible double-set → REMOVED outer attribute
- 1.3 OnMediaEnded not wired → WIRED via PlaybackStateChangedEvent Stopped state
- 1.4 PiP position sync → ALREADY CORRECT
- 1.5 Keyboard shortcut conflicts → Ctrl+S=Stop, removed Ctrl+L duplicate
- 1.6 Auto-hide timer starts without media → GATED on hasMedia
- 1.7 Context menu label styling → STYLED as section headers
- 2.1 PiP size + aspect ratio → UPDATED defaults + aspect lock
- 2.2 PiP resize grip → WIRED BeginResizeDrag
- 2.3 PiP video area frame → ADDED background + corner radius
- 2.4 PiP live badge → HIDDEN by default
- 2.5 PiP hover/timer → FIXED ShowControls + PointerMoved
- 2.6 PiP return to main → ADDED expand button in titlebar
- 2.7 PiP off-screen position → ADDED screen bounds check
- 2.8 PiP SyncThumbnailRect → MEASURED runtime heights (not hardcoded)
- 3.1 Duplicate menu entries → CHECKMARKS synced in HeaderBar menu
- 3.2 Track menu scroll → ADDED ScrollViewer with MaxHeight=320
- 3.3 Subtitles "None" entry → ALREADY EXISTS
- 3.4 Mute toggle icon → WIRED VolumeOff/VolumeHigh toggle
- 3.5 Volume slider max → REMOVED duplicate Maximum from style
- 3.6 Chapters seek calculation → VERIFIED correct (normalized 0-1)
- 3.7 OptionsMenuButton → REMOVED (18.7KB), replaced with BtnVideoEqualizer
- 3.8 Equalizer null check → ADDED guard before dialog open
- 3.9 Shared FlyoutBuilder → PrimaryMenuBuilder created for both headers
- 4.1 Video positioning → MEASURED runtime heights (not hardcoded 44/120)
- 4.2 Window min size → RAISED 332→600, 187→337
- 4.3 StartPage + PB z-order → DELAYED PB hide 350ms
- 4.4 Window centering race → SKIP centering if saved state exists
- 4.5 SeekBar margin → SYMMETRIC 12,0,12,5
- 4.6 Duration label margin → COLUMNSPACING instead of -7px hack
- 5.1 Controls bar warm tint → NAVY TINT gradients instead of pure black
- 5.2 Header bar merge → RAISED top opacity, added 1px bottom border
- 5.3 Font sizes → Section headers 10px, PiP time 12px
- 5.4 Button hierarchy → circular-play 40px, menu/toggle 30px
- 5.5 Tooltip delay → ADDED global ShowDelay=600
- 5.6 Smooth opacity transitions → ADDED DoubleTransition (0.25s)
- 5.7 Premium start page → ENHANCED drop zone with accent colors
- 5.8 OSD notification → PILL shape (CornerRadius=20), slide-up
- 5.9 Window controls → Hover #2B, 120ms transition, icon 14px
- 5.10 PiP colors → USE OsdForeground global token
- 5.11 ToggleButton white bg → ACCENT color (#0078D4) instead of white
- 5.12 Volume mute toggle → SAME accent fix (covered by 5.11)
- 9.1 Play/pause icon sync → OPTIMISTIC toggle in OnPlayPause
- 9.2 Startup page + controls overlap → DELAYED ShowUiControls 250ms
- 9.3 Seek bar jump → FIXED UpdateSeekBar before clearing _isSeeking
- 9.4 Volume flyout auto-dismiss → ADDED 1.5s inactivity timer
- 9.5 Control/header bar warmer tint → NAVY GRADIENTS applied
- 9.9 OptionsMenuButton duplication → REMOVED entirely

### Pending, Ordered by Priority:

| Priority | Item | Phase | Effort |
|----------|------|-------|--------|
| 🟡 HIGH | 10.0 PiP modern redesign | P10 | 8hr |
| 🟡 MEDIUM | Audio/Subtitle delay controls in track flyouts | P3 | 2hr |
| 🟡 MEDIUM | 9.6 Header title shadow enhancement | P9 | 30min |
| 🟡 MEDIUM | 9.7 Unified overlay style (replay + pause) | P9 | 1hr |
| 🟡 MEDIUM | 9.8 Divider lines more visible | P9 | 30min |
| 🟡 MEDIUM | 9.12 Loading spinner sequential fade | P9 | 1hr |
| 🟢 LOW | 9.10 Seek track glow effect | P9 | 30min |
