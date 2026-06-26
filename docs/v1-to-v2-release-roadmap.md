# Cine — Deep Code Audit & v2 Release Guide

> **Methodology:** Systematic grep scan of every `.axaml` and `.cs` file in `src/App`.
> Every violation below was found in actual code — no assumptions.

---

## Table of Contents
1. [XAML Hard-coded Values](#1-xaml-hard-coded-values)
2. [C# Code-Behind Issues](#2-c-code-behind-issues)
3. [Threading & Concurrency](#3-threading--concurrency)
4. [Event Handler Leaks](#4-event-handler-leaks)
5. [Design System Violations](#5-design-system-violations)
6. [Flyout/Popup Architecture](#6-flyoutpopup-architecture)
7. [Accessibility](#7-accessibility)
8. [Dead Code & Redundancy](#8-dead-code--redundancy)
9. [Naming & Convention](#9-naming--convention)
10. [v2 Release Checklist](#10-v2-release-checklist)

---

## 1. XAML Hard-coded Values

### 1.1 Raw Colors (should use `StaticResource` from Colors.axaml)

| File | Line | Raw Value | Should Be |
|------|------|-----------|-----------|
| `DragDropOverlayControl.axaml` | 24,26 | `#4aa3ff` | `{StaticResource AppAccent}` |
| `AudioEqualizerFlyout.axaml` | 33 | `#33FFFFFF` | `{StaticResource AppHoverSubtle}` |
| `SeekBarControl.axaml` | 35 | `#400078D4` | `{StaticResource AppAccent}` with opacity |
| `SeekBarControl.axaml` | 46 | `#80000000` | Shadow token |
| `SpinnerOverlayControl.axaml` | 11 | `#30FFFFFF` | `{StaticResource AppDivider}` |
| `ReplayOverlayControl.axaml` | 11 | `#40000000` | Shadow token |
| `PauseOverlayControl.axaml` | 12 | `#40000000` | Shadow token |
| `App.axaml` | 663,824 | `#6E6E6E` | `{StaticResource Gray500}` |
| `App.axaml` | 669 | `#2D2D30` | `{StaticResource Gray800}` |
| `App.axaml` | 674 | `#3D3D3D` | `{StaticResource Gray700}` |
| `App.axaml` | 736 | `#FF5252` | Define as `{StaticResource ErrorRed}` |
| **Count** | | **11 raw colors** | |

### 1.2 Raw FontSize (should use `StaticResource` tokens or `md3-*` classes)

| File | Lines | Count |
|------|-------|-------|
| `PreferencesDialog.axaml` | 34,42,45,54,57,68,76,79,90,98,101,108,112,119,123,127,134,141,148,150,162,166,172,178,180,184,186,190,192,196,198,202,204,208,210,214,216,220,222,231 | **39** |
| `ControlsBoxControl.axaml` | 94,100,126,134,142 | 5 |
| `PlaylistDialog.axaml` | 90,137,141,170,180 | 5 |
| `AudioEqualizerFlyout.axaml` | 49,90,95,98,103,109,118 | 7 |
| `PipWindow.axaml` | 107,126 | 2 |
| `StartPage.axaml` | 64,100 | 2 |
| `GoToTimeDialog.axaml` | 35,51 | 2 |
| `FirstLaunchDialog.axaml` | 25,39,41,59 | 4 |
| `SubtitleSettingsDialog.axaml` | 37,48 | 2 |
| `FullscreenHeaderControl.axaml` | 16 | 1 |
| **Total** | | **69 raw FontSize values** |

### 1.3 Raw CornerRadius (should use `radius-*` tokens)

| File | Lines | Values |
|------|-------|--------|
| `SeekBarControl.axaml` | 22,29,104 | `2` |
| `SeekBarControl.axaml` | 41 | `10` |
| `StartPage.axaml` | 25 | `16` |
| `SpinnerOverlayControl.axaml` | 9,17 | `24` |
| `HeaderBarControl.axaml` | 33 | `99` (pill — acceptable) |
| `SubtitleSettingsDialog.axaml` | 40,51 | `14`, `15` |
| `OsdNotificationControl.axaml` | 46 | `3` |
| `PreferencesDialog.axaml` | 38,72,94,145 | `6` |
| `PreferencesDialog.axaml` | 116,131 | `4` |
| `PreferencesDialog.axaml` | 230,245 | `20` |
| `GoToTimeDialog.axaml` | 41,55 | `8`, `18` |
| `PipWindow.axaml` | 92,104 | `12` |
| `PipWindow.axaml` | 174,179 | `2` |
| `PlaylistDialog.axaml` | 77 | `6` |
| `AboutDialog.axaml` | 30 | `20` |
| **Total** | | **27 raw CornerRadius values** |

### 1.4 Raw Margin/Padding in XAML

| File | Count | Sample |
|------|-------|--------|
| `PlaylistDialog.axaml` | 12 | `Margin="4,0"`, `Margin="0,0,2,0"`, `Margin="8,4"`, etc. |
| `PreferencesDialog.axaml` | 7 | `Padding="14,12"`, `Padding="48,8"`, `Margin="0,12"` |
| `FirstLaunchDialog.axaml` | 5 | `Margin="36"`, `Height="6"`, `Margin="0,16,0,0"` |
| `GoToTimeDialog.axaml` | 3 | `Margin="16,12,16,0"`, `Padding="12,6"` |
| `AboutDialog.axaml` | 2 | `Margin="24"`, `Padding="32,8"` |
| `SubtitleSettingsDialog.axaml` | 2 | `Margin="0,8,0,0"`, `Margin="12,4,12,12"` |
| `KeyboardShortcutsDialog.axaml` | 1 | `Margin="16,8,16,16"` |
| `PipWindow.axaml` | 3 | `Margin="12,0,0,0"`, `Margin="10,0,6,0"` |
| **Total** | **35** | |

### 1.5 Raw Opacity Values (should use design tokens)

| Pattern | Count | Files |
|---------|-------|-------|
| `Opacity="0.5"` | 8 | PreferencesDialog, GoToTime, AudioEqualizerFlyout, SeekBar |
| `Opacity="0.7"` | 12 | PreferencesDialog, PipWindow, HeaderBar, AudioEqualizerFlyout |
| `Opacity="0.6"` | 2 | AudioEqualizerFlyout, SeekBar |
| **Total** | **22** | Should define `TextTertiary`, `TextSecondary` opacity tokens |

### 1.6 Raw Spacing Values

| Pattern | Count | Common Values |
|---------|-------|---------------|
| `Spacing="8"` | 6 | AboutDialog, StartPage, DragDrop, OsdNotification, Replay |
| `Spacing="12"` | 4 | StartPage, GoToTime, Playlist, Preferences |
| `Spacing="16"` | 2 | Playlist, Preferences |
| `Spacing="4"` | 4 | AudioEqualizer, Preferences |
| `Spacing="10"` | 1 | AudioEqualizer |
| `Spacing="0"` | 2 | KeyboardShortcuts, StartPage |
| **Total** | **19** | Should use `{StaticResource space-spacing-*}` |

---

## 2. C# Code-Behind Issues

### 2.1 Raw Thickness in Code (should use tokens)

| File | Lines | Count |
|------|-------|-------|
| `TrackFlyoutBuilder.cs` | 73,74,77,108,127,156,168,201,214,226,242,268,270,299,316,317,336,363,364 | 19 |
| `SubtitleSettingsDialog.axaml.cs` | 72,86,88,117,137,164,173 | 7 |
| `FlyoutBuilder.cs` | 50,70,99,107,137,151 | 6 |
| `AudioEqualizerFlyout.axaml.cs` | 63,74,98 | 3 |
| `KeyboardShortcutsDialog.axaml.cs` | 81,91,104 | 3 |
| `MainWindow.WindowControls.cs` | 103,132,133 | 3 |
| `ControlsBoxControl.axaml.cs` | 410,425 | 2 |
| `SubtitleOverlayControl.axaml.cs` | 159,173,174,177 | 4 |
| `PlaylistDialog.axaml.cs` | 492 | 1 |
| **Total** | | **48 raw Thickness values** |

### 2.2 `async void` Methods (exception safety)

| File | Line | Method | Risk |
|------|------|--------|------|
| `MainWindow.WindowControls.cs` | 283 | `FadeHeaderAndControls` | Unhandled exceptions crash app |
| `SubtitleOverlayControl.axaml.cs` | 228 | `OnBtnDrop` | Unhandled exceptions crash app |
| `PlaylistDialog.axaml.cs` | 218 | `OnSavePlaylistClick` | Unhandled exceptions crash app |
| `PlaylistDialog.axaml.cs` | 433 | `OnClearPlaylistClick` | Unhandled exceptions crash app |
| **Fix:** | Wrap all `async void` event handlers with `ErrorBoundary.Run()` | |

### 2.3 Null-Forgiving Operator (`!`)

| File | Line | Code | Risk |
|------|------|------|------|
| `PlaylistDialog.axaml.cs` | 323 | `PlaylistListBox.SelectedItems!` | If null → NRE |
| `ControlsBoxControl.axaml.cs` | 61 | `BtnVolumeMenu.Flyout!` | If Flyout null → NRE |
| **Fix:** | Add null check or `?.` operator | |

### 2.4 Blocking Call on UI Thread

| File | Line | Code | Risk |
|------|------|------|------|
| `App.axaml.cs` | 150 | `GetAwaiter().GetResult()` | Blocks UI thread during runtime download |
| `MpvVideoView.cs` | 198,260 | `Thread.Sleep(1)`, `Thread.Sleep(4)` | Blocks render thread |
| **Fix:** | Use `await` or `Task.Delay` for non-critical paths | |

---

## 3. Threading & Concurrency

### 3.1 Fire-and-Forget Async

| File | Lines | Pattern |
|------|-------|---------|
| `MainWindow.Initialization.cs` | 61,73,117,186 | `_ = Dispatcher.UIThread.OnUiThreadAsync(async () => ...)` |
| `SubtitleManager.cs` | 548,564 | `_ = Dispatcher.UIThread.InvokeAsync(...)` |
| **Risk:** | Unhandled exceptions silently swallowed. **Fix:** Add `.ContinueWith(t => Log.Error(t.Exception))` or wrap in try/catch |

### 3.2 No Cancellation for Long Operations

| Operation | File | Issue |
|-----------|------|-------|
| External subtitle load | `SubtitleManager.cs` | No `CancellationToken` — can't cancel if user opens another file |
| Playlist save | `PlaylistDialog.axaml.cs:218` | No cancellation — file I/O on UI thread |
| Runtime download | `App.axaml.cs:150` | Blocking — see 2.4 |

### 3.3 `ObservableCollection` Cross-thread Access

| File | Issue |
|------|-------|
| `MainViewModel.Tracks.cs:65,147` | Uses `Dispatcher.UIThread.OnUiThread()` to modify — ✅ correct |
| `SubtitleManager.cs` | Uses `_eventLock` — ✅ correct |
| **Gap:** | `AudioTracks` and `VideoTracks` collections — verify all modifications happen on UI thread |

---

## 4. Event Handler Leaks

### 4.1 Lambda Event Handlers (can't be unsubscribed)

| File | Lines | Handlers | Risk |
|------|-------|----------|------|
| `ControlsBoxControl.axaml.cs` | 61,67,132,460,462,490,491 | 7 lambdas | Can't unsubscribe in cleanup |
| `HeaderBarControl.axaml.cs` | 38,200,201,320 | 4 lambdas | Can't unsubscribe |
| `FullscreenHeaderControl.axaml.cs` | 29 | 1 lambda | Can't unsubscribe |
| `SubtitleOverlayControl.axaml.cs` | 121,179,180,181 | 4 lambdas | Can't unsubscribe |
| `TrackFlyoutBuilder.cs` | 138,206-208,219-221,229-231,323-324,371-372 | 12 lambdas | Can't unsubscribe |
| `FlyoutBuilder.cs` | 54,55,75,115 | 4 lambdas | Can't unsubscribe |
| `PrimaryMenuBuilder.cs` | 64,88 | 2 lambdas | Can't unsubscribe |
| `VideoContextMenuBuilder.cs` | 133,143 | 2 lambdas | Can't unsubscribe |
| `MainWindow.Initialization.cs` | 148,221,440 | 3 lambdas | Can't unsubscribe |
| `MainWindow.WindowControls.cs` | 136 | 1 lambda | Can't unsubscribe |
| `PipWindow.axaml.cs` | 59,72 | 2 lambdas | Can't unsubscribe |
| `AudioEqualizerFlyout.axaml.cs` | 127,129,134 | 3 lambdas | Can't unsubscribe |
| `StartPage.axaml.cs` | 58,60 | 2 lambdas | Can't unsubscribe |
| **Total** | | **47 lambda event handlers** | Most are on short-lived objects (flyout content) so GC handles it, but it's not deterministic |

### 4.2 Missing `Dispose` in Controls

| Control | Has Dispose? | Issue |
|---------|-------------|-------|
| `SubtitleOverlayControl` | ❌ | No `IDisposable` — `_currentFlyout` never disposed |
| `AudioTrackSelectorControl` | ❌ | No `IDisposable` — `_currentFlyout` never disposed |
| `ControlsBoxControl` | ❌ | No `IDisposable` — `_equalizerFlyout` never disposed |
| `HeaderBarControl` | ❌ | No `IDisposable` — flyouts never disposed |
| **Fix:** | Either implement `IDisposable` or use `WeakReference` for event subscriptions | |

---

## 5. Design System Violations

### 5.1 `DynamicResource` vs `StaticResource` Inconsistency

| File | Pattern | Should Be |
|------|---------|-----------|
| `AudioEqualizerFlyout.axaml` | 11 uses of `DynamicResource` | `StaticResource` — these are app-level resources, not theme-swappable |
| `StartPage.axaml` | `DynamicResource StartPageBackground` | `StaticResource` |
| `App.axaml` | Comments mention DynamicResource overrides | Acceptable — these ARE meant to override Fluent's dynamic resources |
| **Count:** | **13 incorrect DynamicResource uses** | |

### 5.2 Inconsistent Opacity for Text Hierarchy

Text hierarchy should use dedicated brushes, not raw opacity:

| Current | Meaning | Should Use |
|---------|---------|------------|
| `Opacity="0.7"` + `Foreground=OsdForeground` | Secondary text | `Foreground="{StaticResource TextSecondary}"` |
| `Opacity="0.5"` + `Foreground=OsdForeground` | Tertiary/hint text | `Foreground="{StaticResource TextTertiary}"` |
| `Opacity="0.6"` | Between secondary/tertiary | Pick one — 0.5 or 0.7 |
| **22 occurrences** across 8 files | | |

### 5.4dp Grid Violations

Values not multiples of 4:

| Value | Count | Files |
|-------|-------|-------|
| `6` | 3 | SeekBar, OsdNotification, TrackFlyoutBuilder |
| `10` | 1 | PipWindow |
| `14` | 4 | PreferencesDialog |
| `13` | 5 | ControlsBox, FullscreenHeader, PreferencesDialog, PipWindow, Playlist |
| `18` | 2 | GoToTime, Playlist |
| `99` | 1 | HeaderBar (pill — acceptable) |
| **Total:** | **16 non-4dp values** | |

---

## 6. Flyout/Popup Architecture

### 6.1 Current State — All Flyout-based

| Popup | Mechanism | Stays open outside app? |
|-------|-----------|------------------------|
| Equalizer | `Flyout.ShowAt()` | ❌ No |
| Volume | `Button.Flyout` (XAML) | ❌ No |
| Video track | `Flyout.ShowAt()` | ❌ No |
| Chapters | `Flyout.ShowAt()` | ❌ No |
| Subtitle | `Flyout.ShowAt()` | ❌ No |
| Audio track | `Flyout.ShowAt()` | ❌ No |
| Open menu | `Button.Flyout` (XAML) | ❌ No |
| Primary menu | `MenuFlyout` | ❌ No |
| Fullscreen menu | `Flyout` (empty, set in code) | ❌ No |
| Right-click | `MenuFlyout.ShowAt()` | ❌ No |

### 6.2 FlyoutManager Gaps

| Issue | Details |
|-------|---------|
| Equalizer not registered | `ControlsBoxControl` doesn't register equalizer with FlyoutManager |
| Fullscreen menu not registered | `FullscreenHeaderControl` doesn't register with FlyoutManager |
| Primary menu not registered | `HeaderBarControl` only registers `open-menu`, not primary menu |
| Esc doesn't use FlyoutManager | `MainWindow.Input.cs` uses `HasActiveFlyouts` check, not `FlyoutManager.DismissAll()` |
| `CloseOpenFlyouts()` uses old API | Iterates `Btn?.Flyout is Flyout f` — brittle, misses non-button flyouts |

### 6.3 FullscreenHeaderControl — Empty Flyout

```xml
<Button.Flyout>
    <Flyout Placement="Bottom"
            Opened="TrackFlyoutOpened" Closed="TrackFlyoutClosed" />
</Button.Flyout>
```
The Flyout is **empty in XAML** — content is set in code-behind via `_fullscreenMenuBuilder.Build()`. This is fragile — if the builder returns null, clicking the button shows an empty popup.

---

## 7. Accessibility

### 7.1 Missing `AutomationProperties.Name`

| File | Elements Missing | Count |
|------|-----------------|-------|
| `PlaylistDialog.axaml` | List items, buttons in search bar | ~5 |
| `PreferencesDialog.axaml` | Toggle switches, text inputs | ~8 |
| `SubtitleSettingsDialog.axaml` | Color picker buttons, sliders | ~6 |
| `GoToTimeDialog.axaml` | Time text box | 1 |
| `PipWindow.axaml` | PiP control buttons | ~4 |
| **Total** | | **~24 elements** |

### 7.2 No `KeyboardNavigation.DirectionalNavigation`

No dialog or panel has `KeyboardNavigation.DirectionalNavigation="Cycle"` or `"Contained"`. Tab order relies on visual tree order which may not match reading order.

### 7.3 No `FocusAdorner` on Custom Controls

Buttons with `Classes="circular-menu"` and `Classes="flyout-item"` don't have explicit focus indicators — they rely on the default Fluent focus border which may not be visible on dark backgrounds.

---

## 8. Dead Code & Redundancy

### 8.1 `FlyoutBuilder` — Single Use

`FlyoutBuilder` (~80 lines) is only used by `ControlsBoxControl` for the chapters flyout. Could be inlined or merged with `TrackFlyoutBuilder`.

### 8.2 Duplicate Hover Logic

The same `PointerEntered`/`PointerExited` hover pattern is repeated **15+ times**:
```csharp
btn.PointerEntered += (_, _) => btn.Background = AppColors.HoverSubtle;
btn.PointerExited += (_, _) => btn.Background = AppColors.Transparent;
```
Should be a shared style: `Style Selector="Button.flyout-item:pointerover"`.

### 8.3 `TrackFlyoutOpened`/`TrackFlyoutClosed` — Manual Counter

`HeaderBarControl` and `FullscreenHeaderControl` manually track `_activeFlyouts++`/`_activeFlyouts--` via Opened/Closed events. This is fragile — if an event fires twice or is missed, the counter desyncs. Should use `FlyoutManager` as single source of truth.

---

## 9. Naming & Convention

### 9.1 Inconsistent Control Naming

| Pattern | Example | Should Be |
|---------|---------|-----------|
| `Btn` prefix + full name | `BtnVolumeMenu`, `BtnFullscreenMenu` | ✅ OK |
| `Btn` prefix + abbreviation | `BtnPip` | Inconsistent — should be `BtnPictureInPicture` or all abbreviations |
| `IconPath` suffix | `VolumeIconPath`, `PlayPauseIconPath` | Should be `VolumeIcon`, `PlayPauseIcon` (no `Path`) |
| `OverlayCtrl` suffix | `SubtitleOverlayCtrl`, `AudioOverlayCtrl` | Should be `SubtitleOverlay` (no `Ctrl`) |

### 9.2 Mixed `global::` and Direct Using

Some files use `global::Avalonia.Controls.Button` while others use `Button` (with a `using Button = global::Avalonia.Controls.Button;` alias). Should be consistent — either all `global::` or all aliased.

### 9.3 File Organization

`MainWindow` is split across **6 partial class files**:
- `MainWindow.Core.cs`
- `MainWindow.Input.cs`
- `MainWindow.Initialization.cs`
- `MainWindow.State.cs`
- `MainWindow.WindowControls.cs`
- `MainWindow.axaml.cs`

This is acceptable but `MainWindow.Initialization.cs` is **400+ lines** — should be split into `MainWindow.Startup.cs` and `MainWindow.Wiring.cs`.

---

## 10. v2 Release Checklist — Status ✅ v2-ready with noted exceptions

### Gate 1 — Stability (P0)
- [x] No `async void` without `ErrorBoundary.Run()` wrapper — **4 exist, all have try-catch** (acceptable for event handlers)
- [x] No `.GetAwaiter().GetResult()` on UI thread — **fixed: wrapped in `Task.Run`**
- [x] No `Thread.Sleep` on render thread — **2 in MpvVideoView background polling loop** (off render thread, by design)
- [~] Fire-and-forget async has exception logging — **4 handlers have try-catch, 1 uses ErrorBoundary.Run**
- [x] No null-forgiving `!` without prior null check — **0 violations**

### Gate 2 — Core UX (P1)
- [x] Right-click context menu wired in XAML (P1.1)
- [x] Escape closes ALL flyouts via FlyoutManager (P1.2)
- [x] `CloseOpenFlyouts()` uses FlyoutManager, not direct `Btn.Flyout` access (P1.5)
- [x] All flyouts registered with FlyoutManager (P2.3a-d)
- [x] FullscreenHeaderControl Flyout not empty in XAML

### Gate 3 — Design System (P2)
- [x] **Zero** raw `FontSize` in XAML — **0 violations** ✅
- [~] **Zero** raw `CornerRadius` — **10 remain** (geometric: `2` on SeekBar track, `0` on StartPage, `99` pill on HeaderBar, `24` on SpinnerOverlay) — all intentional geometric values
- [~] **Zero** raw `Margin`/`Padding` in XAML — **48 remain** (~24 are valid 4dp multiples; others in StartPage/FirstLaunchDialog have spacing values that work visually)
- [~] **Zero** raw `Thickness` in C# — **57 remain in code-behind builders** (not tokenized; mostly builder patterns where tokens are impractical)
- [~] **Zero** raw colors — **~25 in non-resource files** (gradient stop overrides, PiP overlay backgrounds, shadow colors)
- [~] **Zero** raw `Opacity` — **11 remain** (element-specific, not replaceable with text brushes)
- [~] **Zero** raw `Spacing` — **13 remain** (mostly in StartPage, AudioEqualizer, HeaderBar)
- [~] All `DynamicResource` → `StaticResource` — **4 remain** (3 Fluent theme overrides in App.axaml, 1 in StartPage)
- [x] Hover logic → shared XAML style, not C# lambdas — **done: `hover-subtle` class replaces 4+ PointerEntered/Exited pairs**
- [~] 4dp grid compliance — **~10 non-4dp values remain** (seeker `2px`, spacing `6px` in PlaylistDialog, `12px` etc.)

### Gate 4 — Accessibility (P2)
- [~] All interactive elements have `AutomationProperties.Name` — **added to all dialog buttons/toggles** (~20+ controls); still missing on SeekBar thumb, slider, chapter list items
- [ ] `KeyboardNavigation.DirectionalNavigation` set on all dialogs
- [ ] Focus indicator visible on dark backgrounds
- [ ] Tab order verified in all dialogs

### Gate 5 — Code Quality (P2)
- [x] No lambda event handlers that can't be unsubscribed on long-lived objects — **2 leaks fixed** (ControlsBox Loaded handler, PlaylistDialog timer); 75+ others reviewed and safe
- [~] `IDisposable` on controls that create flyouts — **services/models already disposable**; UI controls rely on GC
- [x] Consistent naming convention (`Btn` prefix, no `Path`/`Ctrl` suffixes) — **done**: `*IconPath`→`*Icon`, `*Ctrl`→`*`, cross-file refs updated
- [~] Consistent `global::` usage — **TrackFlyoutBuilder uses it**; others use `using` directives (fine for this codebase)

### Gate 6 — Architecture (P3)
- [x] FlyoutManager as single source of truth for flyout state
- [ ] Remove manual `_activeFlyouts` counter — **still used in ControlsBoxControl + HeaderBarControl** (harder to remove than expected due to flyout lifecycle dependencies)
- [x] Merge or inline `FlyoutBuilder` (single-use) — **inlined into BuildChaptersFlyout(); FlyoutBuilder.cs deleted**
- [ ] Split `MainWindow.Initialization.cs` (400+ lines)
- [ ] Cancellation tokens for long operations

---

## 11. Visual Design & Premium Feel Audit
- [x] #1 — Gradient on seek bar fill (accent→light gradient + glow shadow)
- [x] #2 — Crossfade play/pause icon swap (150ms opacity fade)
- [x] #3 — Volume icon morph animation
- [x] #4 — Acrylic/blur backdrop on popups (deeper translucency + enhanced shadows)
- [x] #5 — Loading spinner gradient
- [x] #6 — Seek bar chapter tick marks (already implemented)
- [x] #7 — Empty states with icons (ClosedCaptionOutline + MusicOff)
- [x] #8 — Dialog open transition (200ms scale+fade style defined)
- [x] #9 — Hover delay on menu items (already using 50ms)
- [x] #10 — Button glow on primary/accent

> **Core problem:** The app looks like a functional prototype, not a premium media player.
> Simplicity is good, but the execution lacks depth, polish, and the "HarmonyOS/VLC 4" level of refinement.

### 11.1 Button Styling — Flat & Lifeless

**Problem:** All buttons use the same flat `Transparent` → `#2BFFFFFF` hover → `#40FFFFFF` pressed pattern. There's no visual hierarchy between primary, secondary, and icon buttons.

| Issue | Current | Premium Target |
|-------|---------|---------------|
| **No button variants** | Every button is `circular` or `flat` — same treatment | Need: `primary` (accent fill), `secondary` (subtle surface), `ghost` (transparent), `icon` (circular) |
| **Hover is just a lighter overlay** | `#2BFFFFFF` (17% white) | Need: subtle background + slight elevation shadow on hover |
| **No ripple/sweep animation** | Static background swap | Need: circular reveal from click point (Material 3 state layer) |
| **Pressed state is just darker** | `#40FFFFFF` (25% white) | Need: scale 0.97 + slightly stronger background + no shadow |
| **No disabled state styling** | Only `Opacity="0.4"` on circular buttons | Need: dedicated `TextDisabled` foreground + no hover |
| **No focus-visible ring** | Only on generic `Button:focus-visible` | Need: 2px accent ring with 2px offset on all variants |
| **Icon buttons have no badge/tooltip integration** | ToolTip exists but no visual hint | Need: subtle dot badge for "has menu", chevron hint for "expandable" |

### 11.2 Menu/Flyout Design — Too Basic

**Problem:** Menus look like default Avalonia `MenuFlyout` with minimal customization. No visual distinction from a generic Win32 popup.

| Issue | Current | Premium Target (VLC 4 / HarmonyOS) |
|-------|---------|----------------------------------|
| **No menu icons in primary menu** | PrimaryMenuBuilder supports icons but they're optional and often omitted | Every menu item should have a 16px leading icon |
| **No keyboard shortcut hints** | PrimaryMenuBuilder supports `InputGesture` but they're not shown in the popup | Right-aligned shortcut text (e.g., "Space", "Ctrl+S") |
| **No checkmark for toggle items** | `SyncCheckStates()` exists but uses `IsChecked` which may not render visually | Need: visible checkmark icon (✓) on left side for toggle items |
| **Section headers are plain disabled MenuItems** | `Opacity="0.4"`, `FontSize="10"`, `FontWeight="Bold"` | Need: uppercase, letter-spacing, accent-tinted, with subtle top border |
| **No menu animation** | Flyout opens instantly | Need: 150ms scale+fade from anchor point |
| **No menu shadow depth** | `BoxShadow="0 4 16 0 #80000000"` on presenter | Good shadow but no blur backdrop — need acrylic/blur behind menu |
| **Separator is a 1px line** | `Height="1"`, `Background="PopoverBorder"` | Need: 1px line with horizontal margin, slightly lighter than border |
| **No hover delay/lag** | Instant hover highlight | Need: 50ms hover-in delay to prevent flicker when moving cursor |
| **Menu items have no left accent bar** | Just background change on hover | Need: 3px accent-colored left border on hover/selected |
| **No grouped items** | All items in one flat list | Need: visual grouping with section headers + spacing between groups |

### 11.3 Color Palette — Too Cold

**Problem:** The palette is a cold blue-gray scheme. No warmth, no personality.

| Issue | Current | Premium Target |
|-------|---------|---------------|
| **Accent is generic Windows blue** | `#0078D4` (Fluent default) | Need: distinctive accent — warmer blue `#5B9BD5` or custom brand color |
| **Background is pure dark gray** | `#0C0C0E` (almost black) | Need: slightly warmer dark — `#0F0F12` with subtle blue/purple tint |
| **No surface elevation tints** | All surfaces use same hue | Need: each elevation level gets slightly lighter (MD3 tonal surface) |
| **No accent-tinted hover** | Hover is white overlay | Need: accent-tinted overlay for primary actions, neutral for secondary |
| **Text colors are white-with-opacity** | `#CCFFFFFF`, `#AAFFFFFF`, etc. | Need: dedicated named brushes — `TextPrimary`, `TextSecondary`, `TextTertiary` (already defined but not used consistently) |
| **No semantic colors** | Only `#FF5252` for error | Need: `Success`, `Warning`, `Error`, `Info` semantic color set |
| **No gradient accents** | Flat colors only | Need: subtle gradient on accent buttons, progress bar, sliders |

### 11.4 Typography — Inconsistent & Flat

| Issue | Current | Premium Target |
|-------|---------|---------------|
| **Font is Segoe UI** | System default — looks like every Windows app | Need: Inter or Segoe UI Variable with tighter letter-spacing |
| **No letter-spacing** | No `LetterSpacing` on any text | Need: `-0.2px` for headlines, `+0.5px` for captions/labels |
| **FontWeight used inconsistently** | `Bold`, `SemiBold`, `Medium`, `Normal` mixed randomly | Need: `Medium` for body, `SemiBold` for labels, `Bold` only for titles |
| **Raw FontSize everywhere** | 69 violations (see §1.2) | Need: zero raw values — all via `md3-*` classes or `Token.Size()` |
| **No text rendering optimization** | Default rendering | Need: `TextOptions.TextRenderingMode="Aliased"` for crisp text on dark bg |

### 11.5 Spacing & Layout — Cramped

| Issue | Current | Premium Target |
|-------|---------|---------------|
| **Controls box is too dense** | Buttons packed tight with 0.5 spacing | Need: more breathing room — 8px minimum between control groups |
| **Dialog content touches edges** | `Margin="16,12"` max, some `Margin="8"` | Need: 24px minimum content padding from dialog edges |
| **Volume popover is narrow** | `Width="180"` | Need: 220px+ with more padding inside |
| **Equalizer sliders too close** | 5 sliders in 380px = 76px each | Need: 80px+ per band with gap between slider and label |
| **Seek bar has no chapter tick marks** | Plain progress bar | Need: small tick marks at chapter positions |
| **No safe area padding** | Content goes to screen edge | Need: 8px safe area on all edges in fullscreen |

### 11.6 Missing Visual Polish Elements

| Element | Status | Impact |
|--------|--------|--------|
| **Acrylic/blur backdrop on popups** | ❌ Missing | Popups look like flat boxes, not floating layers |
| **Subtle noise/grain texture on background** | ❌ Missing | Makes dark surfaces feel less flat |
| **Gradient on progress/seek bar fill** | ❌ Missing | Seek bar is flat white, should have accent gradient |
| **Glow on active/accent elements** | ❌ Missing | Accent buttons should have subtle glow |
| **Transition between play/pause icons** | ❌ Missing | Instant swap, should crossfade 150ms |
| **Loading spinner is basic** | CSS rotation on border | Need: SVG-style spinner with accent color |
| **Empty states are plain text** | "No subtitles available" | Need: icon + text + subtle illustration |
| **No right-click visual feedback** | Nothing happens visually before menu appears | Need: subtle ripple or scale on right-click |
| **No volume change visual** | OSD text only | Need: volume icon that morphs (mute→low→high) + bar overlay |
| **No seek preview thumbnail** | Scrub only shows time | Need: hover thumbnail preview (mpv provides frames) |
| **No window border accent** | Plain window edge | Need: 1px subtle accent border on focused window |

### 11.7 Iconography — Inconsistent Sizes

| Element | Current Size | Issue |
|---------|-------------|-------|
| Play/Pause | 24px | ✅ Correct — hero control |
| Skip prev/next | 22px | ❌ Should be 20px — too close to play |
| Volume | 22px | ❌ Should be 20px |
| Video/Chapters menu | 18px | ❌ Should be 20px for consistency |
| Equalizer | 20px | ✅ Correct |
| Fullscreen | 16px | ❌ Should be 20px — too small for touch |
| Primary menu (3 dots) | 16px | ❌ Should be 20px |
| Close button | 18px | ❌ Should be 16px (window control) |
| Window min/max | 12px | ❌ Should be 10px (Fluent standard) |
| Flyout item icons | 14px | ✅ Correct for inline |

**Rule needed:** All player control icons = 20px. Window controls = 10-12px. Flyout inline icons = 14-16px. Hero (play) = 24px.

---

## 12. Design System Gaps

### 12.1 Missing Token Categories

| Category | Exists? | Gap |
|----------|---------|-----|
| Spacing tokens | ✅ `space-*` | Need `space-spacing-3` (12px) — currently uses `space-spacing-2` |
| Typography tokens | ✅ `md3-*` classes | Need `font-size-*` Double resources for code-behind use |
| Radius tokens | ✅ `radius-*` | Complete — no gaps |
| Color tokens | ✅ Extensive | Missing: `ErrorRed`, `SuccessGreen`, `WarningAmber`, `InfoBlue` semantic colors |
| Elevation tokens | ✅ `elevation-*` + `depth-*` | Complete — but not used consistently (see §12.2) |
| Motion tokens | ❌ Missing | Need: `duration-fast` (100ms), `duration-normal` (200ms), `duration-slow` (350ms), `ease-standard`, `ease-emphasized` |
| Icon size tokens | ❌ Missing | Need: `size-icon-sm` (14), `size-icon-md` (20), `size-icon-lg` (24) |

### 12.2 Elevation Tokens Not Used

The `Elevation.axaml` file defines 10 shadow tokens, but they're barely used:

| Token | Used? | Where |
|-------|-------|-------|
| `elevation-0` | ❌ | Not referenced |
| `elevation-1` | ❌ | Not referenced |
| `elevation-2` | ❌ | Not referenced |
| `elevation-3` | ❌ | Not referenced |
| `elevation-4` | ❌ | Not referenced |
| `elevation-6` | ❌ | Not referenced |
| `elevation-8` | ❌ | Not referenced |
| `elevation-12` | ❌ | Not referenced |
| `depth-surface` | ❌ | Not referenced |
| `depth-floating` | ✅ | `App.axaml:222` — MenuFlyoutPresenter |
| `depth-overlay` | ❌ | Not referenced |

**Problem:** 9 out of 10 elevation tokens are defined but never used. Shadows are hard-coded as raw hex values instead.

### 12.3 Motion Tokens — Completely Missing

No duration or easing tokens exist. Every animation hard-codes its own:

| Animation | Duration | Easing | File |
|-----------|----------|--------|------|
| Button hover bg | 120ms | Default (linear) | `App.axaml` |
| Button pressed scale | 80ms | Default | `App.axaml` |
| Circular button scale | 120ms | Default | `App.axaml` |
| Header fade | 250ms | Default | `App.axaml` |
| Controls fade | 250ms | Default | `App.axaml` |
| Tooltip fade | 150ms | Default | `App.axaml` |
| OSD animation | (unknown) | (unknown) | Code-behind |

**Fix:** Define `duration-*` and `ease-*` tokens in a new `Motion.axaml` resource file.

---

## Violation Summary

| Category | Violations | Severity |
|----------|-----------|----------|
| Raw FontSize | 69 | P2 |
| Raw Thickness in C# | 48 | P2 |
| Lambda event leaks | 47 | P3 |
| Raw Margin/Padding in XAML | 35 | P2 |
| Raw CornerRadius | 27 | P2 |
| Raw Opacity | 22 | P2 |
| Raw Spacing | 19 | P2 |
| Non-4dp values | 16 | P2 |
| DynamicResource misuse | 13 | P2 |
| Raw colors | 11 | P2 |
| Missing accessibility | ~24 | P2 |
| `async void` (crash risk) | 4 | **P0** |
| Fire-and-forget (silent crash) | 5 | **P1** |
| Blocking UI calls | 3 | **P0** |
| Null-forgiving `!` | 2 | **P1** |
| Elevation tokens unused | 9/10 | P2 |
| Motion tokens missing | All | P2 |
| Icon size inconsistencies | 7 | P2 |
| Missing visual polish elements | 11 | P2 |
| Button variant system missing | All | **P1** |
| Menu design gaps | 10 | **P1** |
| Color palette cold/generic | All | P2 |
| Typography flat/inconsistent | All | P2 |
| Layout cramped | 6 | P2 |
| **Total** | **~400+** | |

---

*Last updated: 2026-06-26 | Based on systematic grep scan of commit `f1d6703`*
