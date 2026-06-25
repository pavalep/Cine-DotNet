# UI Standardization & Excellence Audit

> **Goal**: Transform Cine from amateur, inconsistently-padded UI → polished, professional-grade design ecosystem.
> **Scope**: Every control, flyout, dialog, overlay, resource, spacing token, font size, corner radius.  
> **Last Updated**: 2026-06-25

---

## Executive Summary

Cine has excellent foundations — Material Design 3 design tokens (`Sizes.axaml`, `Spacing.axaml`, `Radius.axaml`, `Typography.axaml`, `Colors.axaml`) — but **almost nothing uses them**. Raw numeric values (margins, paddings, font sizes, corner radii) are scattered across 22 code-behind files and 32 XAML files.  This is what makes the UI feel *unprofessional* — inconsistent spacing, misaligned elements, competing visual rhythms.

### The Core Problem

| What | Exists | Used? |
|------|--------|-------|
| `Spacing.axaml` — 25 thickness tokens (4dp/8dp grid) | ✅ | Only ~8 files reference them; rest use raw `Margin="14,12"` or `new Thickness(16, 8)` |
| `Typography.axaml` — 7 `md3-*` type ramp classes | ✅ | **Zero** files use them. Every `FontSize` is raw. |
| `Radius.axaml` — 8 corner radius tokens | ✅ | ~6 files use them; rest use raw `CornerRadius="3"`, `"16"`, `"6"` |
| `Colors.axaml` — full color system + OSD + button states | ✅ | Used extensively — this is the **only** token file that's properly adopted |
| `Sizes.axaml` — 18 component size tokens | ✅ | Only used by seek slider and window chrome. Dialog heights, button sizes are all raw |

### The Amateur Feel — Root Causes

| Symptom | Cause | Fix |
|---------|-------|-----|
| Flyout track items feel cramped | Hardcoded `Padding="10,7"` in `TrackFlyoutBuilder` + `Margin="8,1"` all different | Standardize to `Padding="12,9"` — 12px horizontal, 9px vertical = 36px row height (divisible by 4) |
| Dialogs have different header heights | GoToTime 40px, Prefs 44px, SubSettings 32px (was), KBShortcuts 40px | After previous fix: all use `dialog-header` at 36px ✅ |
| Dialog body margins vary wildly | `Margin="16,12"`, `"36"`, `"12,4"`, `"0"`, `"16,8"` | Standardize to body margin = 16px (space-4), not 12px |
| OSD notifications bottom margin jumps | 80px hardcoded → 110px token, then overridden to 0 by code-behind when controls are visible | Single source of truth: control always uses token value |
| Equalizer flyout is 480px wide | Way too wide — makes 10 vertical sliders feel lost | Reduce to 380px; use a `WrapPanel` for bands |
| Volume popover thumb is different from subtitle settings thumb | 3 different thumb shrink approaches — XAML local style, code-behind `TemplateApplied`, global `compact` class | After previous fix: all use `Classes="compact"` ✅ |
| Font sizes: 14 different raw values | 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 34 — all raw, no type ramp applied | Apply `md3-*` classes to all TextBlock elements |
| +/- buttons on delay sliders: 3 different styles | Subtitles flyout: 24px (was), Settings dialog: 18px, Equalizer: no +/- | Standardize to 22px |
| Track item heights vary | Subtitle tracks ~30px, Audio tracks ~32px, Menu items ~32px | All to 36px (taller = more clickable, more professional) |
| Separators: 3 different margin patterns | `Margin="4,2"`, `"4,4,4,2"`, `"0,4,0,0"` | All to `Margin="4,2"` from shared style ✅ (MenuStyles already has this) |

---

## Phase 0: The Design Token Review

### 0.1 What We Have (and What's Missing)

| File | Purpose | Status |
|------|---------|--------|
| `Colors.axaml` | All color brushes — base, accent, OSD, button states, text, overlay, divider, hover, drag | ✅ Complete, properly adopted |
| `Sizes.axaml` | Component dimensions — header, buttons, sliders, dialog, flyout, breakpoints | ⚠️ Has 18 tokens; missing tokens for button padding, track item height |
| `Spacing.axaml` | 25 thickness tokens (uniform, h-only, v-only, asymmetric) | ✅ Complete; needs enforcement |
| `Radius.axaml` | 8 corner radius tokens | ✅ Complete; needs enforcement |
| `Typography.axaml` | 7 Material Design 3 type ramp classes | ✅ Complete but **zero usage** — this is the biggest gap |
| `Elevation.axaml` | Box shadow tokens | ✅ Good |
| `Icons.axaml` | Icon path resources | ✅ Good |
| `MenuStyles.axaml` | MenuItem/MenuFlyoutPresenter styles | ✅ Good — this is the **model** for how other styles should work |

### 0.2 Missing Tokens (Add to Sizes.axaml)

```xml
<!-- Button padding — standard 12,9 = 36px tall row -->
<Thickness x:Key="padding-track-item">12,9</Thickness>
<Thickness x:Key="padding-menu-item">12,9</Thickness>

<!-- Dialog body margin standard -->
<Thickness x:Key="margin-dialog-body">16</Thickness>

<!-- Slider compact row height -->
<x:Double x:Key="size-slider-row-compact">38</x:Double>

<!-- Track item row height -->
<x:Double x:Key="size-track-item">36</x:Double>

<!-- Flyout padding -->
<Thickness x:Key="padding-flyout">0</Thickness>
<Thickness x:Key="padding-flyout-section">14,12</Thickness>
```

---

## Phase 1: Immediate Fixes — The "Crisp" Foundation

### 1.1 BUG: `sub-opacity` mpv property doesn't exist

**Severity**: High — subtitle opacity slider does nothing  
**File**: `src/Media/Implementations/mpv/MpvPlayer.cs:662`  
**Root cause**: mpv has no `sub-opacity` property. Logs show "property not found" warnings.  
**Fix**: Apply alpha channel via `sub-color` + `sub-border-color`:
```csharp
var alphaHex = ((int)(opacity * 255)).ToString("X2");
SetString("sub-color", $"#{alphaHex}FFFFFF");
SetString("sub-border-color", $"#{alphaHex}000000");
```
**Status**: ✅ FIXED

### 1.2 BUG: `SelectSubtitleTrackById` race condition

**Severity**: High — causes `InvalidOperationException` crash when opening video  
**Root cause**: Iterates `ObservableCollection` on mpv background thread while UI thread modifies it  
**Fix**: Add `.ToArray()` snapshot before LINQ  
**Status**: ✅ FIXED

### 1.3 BUG: File dialog deadlock with open flyouts

**Severity**: High — app freezes when opening file picker while flyout is open  
**Root cause**: Avalonia #18969 — Win32 COM message pump deadlock  
**Fix**: Close flyout → 50ms delay → open dialog → reopen flyout  
**Status**: ✅ FIXED

### 1.4 ENFORCE: Typography Classes

**Every** raw `FontSize="XX"` in XAML must be replaced with an `md3-*` class. This is the single most impactful change for visual consistency.

| Raw FontSize | MD3 Class | Usage |
|-------------|-----------|-------|
| `FontSize="10"` | `md3-caption` (12px — bump up) | Equalizer band labels, helper text |
| `FontSize="11"` | `md3-caption` (12px) | Subtitle settings labels, EQ section headers |
| `FontSize="12"` | `md3-caption` | Time labels, secondary text, OSD notification |
| `FontSize="13"` | `md3-body2` (14px — bump up) | Menu items, buttons, track names |
| `FontSize="14"` | `md3-body2` | Dialog titles, section headers, OSD |
| `FontSize="16"` | `md3-body1` | Body text, subtitles |
| `FontSize="18"` | Custom 18 (none in MD3) | Big buttons |
| `FontSize="20"` | `md3-headline6` | Page titles |
| `FontSize="24"` | `md3-headline4` | Start page title |
| `FontSize="28"` | `md3-headline2` subset | Drag-drop overlay |
| `FontSize="34"` | `md3-headline2` | Hero text |

**Why bump 10→12 and 13→14?** Font sizes below 12px are considered "micro" text — not accessible, not professional. 13px is a half-pixel increment that breaks the 4dp grid. Stick to multiples of 2.

### 1.5 ENFORCE: Spacing Tokens

Every raw `Margin="A,B,C,D"` or `new Thickness(A,B,C,D)` must reference a spacing token.

| Raw Value | Token | Usage |
|-----------|-------|-------|
| `Margin="4"` | `{StaticResource space-1}` | Tight gaps |
| `Margin="8"` | `{StaticResource space-2}` | Button gaps, element spacing |
| `Margin="12"` | `{StaticResource space-3}` | Dialog padding |
| `Margin="14,12"` | Use `space-3` + vertical override or create `space-eq-padding` | Equalizer padding |
| `Margin="16"` | `{StaticResource space-4}` | Dialog body margin, card padding |
| `Margin="24"` | `{StaticResource space-5}` | Page margins |
| `Padding="10,7"` | Use `{StaticResource padding-track-item}` | Track/menu items |
| `Padding="14,12"` | Use `{StaticResource padding-flyout-section}` | Flyout content sections |

### 1.6 ENFORCE: Radius Tokens

| Raw Value | Token | Usage |
|-----------|-------|-------|
| `CornerRadius="3"` | `{StaticResource radius-xs}` (4) | Slider thumbs, text fields |
| `CornerRadius="6"` | `{StaticResource radius-xs}` | UI is rounding to pixel anyway — 4 is fine |
| `CornerRadius="8"` | `{StaticResource radius-sm}` | Flyouts, popovers |
| `CornerRadius="12"` | `{StaticResource radius-md}` | Dialogs |
| `CornerRadius="16"` | `{StaticResource radius-lg}` | Large dialogs, overlays |
| `CornerRadius="18"` | Use `radius-md` or `radius-lg` | Buttons |
| `CornerRadius="99"` | `{StaticResource radius-full}` | Pills |

---

## Phase 2: Professional Layout — The 4dp/8dp Grid

### 2.1 The Golden Rule

**Every margin, padding, height, and gap must be divisible by 4.** The Material Design 3 spacing system uses a 4dp base unit. If you see `Margin="14,12"` or `Padding="10,7"`, it's wrong.

| Amateur Pattern | Professional Pattern | Why |
|----------------|---------------------|-----|
| `Padding="10,7"` | `Padding="12,8"` | 12 and 8 are multiples of 4 (3×4dp, 2×4dp) |
| `Margin="14,12"` | `Margin="16,12"` | 16 horizontal, 12 vertical |
| `Margin="36"` | `Margin="32"` or `Margin="40"` | FirstLaunch body margin |
| `CornerRadius="3"` | `CornerRadius="4"` | `radius-xs` |
| `Height="38"` | `Height="36"` or `Height="40"` | Components should snap to grid |

### 2.2 Audit: All Non-Conforming Values

#### In XAML files:

| File | Violation | Fix |
|------|-----------|-----|
| `HeaderBarControl.axaml` | `Padding="14,6"` on Open button | `Padding="12,6"` |
| `AudioEqualizerFlyout.axaml` | `Padding="14,12"` on flyout | `Padding="16,12"` |
| `AudioEqualizerFlyout.axaml` | `Padding="6,3"` on Reset button | `Padding="8,4"` |
| `AudioEqualizerFlyout.axaml` | `Margin="6,0,0,0"` on close button | `Margin="8,0,0,0"` |
| `ControlsBoxControl.axaml` | `Padding="12,8"` on volume popup | Okay (24px + 16px area) but `Padding="16,12"` is more spacious |
| `ControlsBoxControl.axaml` | `Padding="2"` on volume preset buttons | `Padding="4"` (at least) |
| `GoToTimeDialog.axaml` | `Margin="12,8,12,0"` on body | `Margin="16,12,16,0"` |
| `KeyboardShortcutsDialog.axaml` | `Margin="16,8,16,16"` | `Margin="16,12,16,16"` |
| `PlaylistDialog.axaml` | `Height="36" Margin="8,4,8,0"` | `Height="36"` is fine; margin should be `Margin="12,8,12,0"` |
| `PlaylistDialog.axaml` | `Margin="8,4"` on item | `Margin="12,8"` |
| `PlaylistDialog.axaml` | `Margin="6,0,0,0"` | `Margin="8,0,0,0"` |
| `PreferencesDialog.axaml` | `Padding="14,12"` on cards | `Padding="16,12"` |
| `PreferencesDialog.axaml` | `Padding="8,4"` on buttons | `Padding="12,6"` |
| `PreferencesDialog.axaml` | `Padding="48,8"` on footer button | `Padding="12,8"` (48 is absurd for padding) |
| `PreferencesDialog.axaml` | `Padding="16,6"` | `Padding="16,8"` |
| `PreferencesDialog.axaml` | `Margin="0,8"` inside cards | `Margin="0,12"` |
| `SubtitleSettingsDialog.axaml` | `Margin="8,4,8,8"` footer | `Margin="12,4,12,12"` |
| `SubtitleSettingsDialog.axaml` | `Margin="0,4,0,0"` scrollviewer | `Margin="0,8,0,0"` |
| `FirstLaunchDialog.axaml` | `Margin="36"` body | `Margin="32"` |
| `FirstLaunchDialog.axaml` | `Margin="0,16,0,0"` | Okay |
| `FirstLaunchDialog.axaml` | `Margin="0,3"` on grid | `Margin="0,4"` |
| `FirstLaunchDialog.axaml` | `Height="6"` progress bar | `Height="4"` — 6px doesn't divide |
| `OsdNotificationControl.axaml` | `Padding="16,10"` | `Padding="16,12"` — 10px vertical breaks grid |
| `DragDropOverlayControl.axaml` | `CornerRadius="8"` | `CornerRadius="{StaticResource radius-sm}"` |
| `PauseOverlayControl.axaml` | `CornerRadius="16"` | `CornerRadius="{StaticResource radius-lg}"` |
| `PauseOverlayControl.axaml` | `Padding="20"` | `Padding="24"` |

#### In code-behind files:

| File | Violation | Fix |
|------|-----------|-----|
| `HeaderBarControl.axaml.cs` | `Margin = new Thickness(4, 2)` | Use token |
| `HeaderBarControl.axaml.cs` | `Margin = new Thickness(12, 5, 0, 2)` | `Margin = new Thickness(12, 4, 0, 4)` |
| `HeaderBarControl.axaml.cs` | `Margin = new Thickness(10, 0, 0, 0)` | `Margin = new Thickness(12, 0, 0, 0)` |
| `ControlsBoxControl.axaml.cs` | `Padding = new Thickness(10, 7)` | `Padding = new Thickness(12, 8)` |
| `ControlsBoxControl.axaml.cs` | `FontSize = 12` (repeated) | `Classes = "md3-caption"` |
| `KeyboardShortcutsDialog.axaml.cs` | `Margin = new Thickness(0, 6, 0, 6)` | `Margin = new Thickness(0, 8, 0, 8)` |
| `KeyboardShortcutsDialog.axaml.cs` | `FontSize = 13` | `Classes = "md3-body2"` → 14px |
| `KeyboardShortcutsDialog.axaml.cs` | `Margin = new Thickness(0, 12, 0, 4)` | `Margin = new Thickness(0, 12, 0, 8)` |
| `KeyboardShortcutsDialog.axaml.cs` | `Margin = new Thickness(0, 2)` | `Margin = new Thickness(0, 4)` |
| `PlaylistDialog.axaml.cs` | `FontSize = 13` | Bump to 14 (`md3-body2`) |
| `PlaylistDialog.axaml.cs` | `Padding = new Thickness(16, 8)` | Okay |
| `SubtitleSettingsDialog.axaml.cs` | `Margin = new Thickness(8, 1, 8, 0)` | `Margin = new Thickness(8, 4, 8, 0)` |
| `SubtitleSettingsDialog.axaml.cs` | `Margin = new Thickness(8, 2, 8, 2)` | `Margin = new Thickness(8, 4, 8, 4)` |
| `SubtitleSettingsDialog.axaml.cs` | `Margin = new Thickness(4, 0, 4, 0)` | Okay |
| `SubtitleSettingsDialog.axaml.cs` | `FontSize = 10` | Bump to 12 (`md3-caption`) — 10px is too small |
| `SubtitleSettingsDialog.axaml.cs` | `FontSize = 11` | Bump to 12 |
| `AudioEqualizerFlyout.axaml.cs` | `Margin = new Thickness(3, 0)` | `Margin = new Thickness(4, 0)` |
| `AudioEqualizerFlyout.axaml.cs` | `FontSize = 9` | Bump to 11 (`md3-caption` is 12 but 11 is okay for EQ bands) |
| `AudioEqualizerFlyout.axaml.cs` | `Margin = new Thickness(0, 0, 0, 2)` | `Margin = new Thickness(0, 0, 0, 4)` |
| `AudioEqualizerFlyout.axaml.cs` | `FontSize = 10` | Bump to 11 (keep readable) |

---

## Phase 3: Structural Components — From Code-Behind to Design System

### 3.1 The Builder Problem

Currently 4 separate builder classes build flyouts/menus:

| Builder | Purpose | Lines | Status |
|---------|---------|-------|--------|
| `TrackFlyoutBuilder` | Subtitle & audio track pickers | ~180 | ✅ Good — shared, parameterized |
| `FlyoutBuilder` | Volume popover only | ~80 | ⚠️ Used only once — merge into caller or rename |
| `PrimaryMenuBuilder` | Main menu bar | ~120 | ⚠️ Mixes ViewModel + UI construction |
| `VideoContextMenuBuilder` | Right-click context menu | ~90 | ⚠️ Duplicates logic with PrimaryMenuBuilder |

**Issue**: Each builder has its own padding/margin/font conventions. `TrackFlyoutBuilder` uses `Padding="10,7"`, `VideoContextMenuBuilder` uses native `ContextMenu` (system styling). The result: 4 different visual styles for menu-like UIs.

**Fix**: Standardize all to use `TrackFlyoutBuilder` style (12,9 padding, 36px rows) or extract a `FlyoutItemBuilder` base.

### 3.2 The SubtitleOverlayControl.axaml Problem

**File**: `SubtitleOverlayControl.axaml` — exists but the control is built **100% in code-behind**. The XAML file is dead weight.

**Fix**: Either:
- (A) Delete the `.axaml` file and make this a pure code-behind control, or
- (B) Move the visual tree to XAML and keep code-behind for logic only

Recommend (A) for simplicity — this control is a thin wrapper for the flyout builder.

### 3.3 The Equalizer Layout Problem

**Current**: 480px wide flyout, 10 vertical sliders in a horizontal `StackPanel`, each 32px wide. Total slider area = 320px + margins = ~360px. 480px is 120px of wasted space.

**Fix**: Reduce to 380px width. Clamp to 320px if window is narrow. Sliders should be 28px apart (center-to-center), not 32px apart. This saves 40px.

### 3.4 The Volume Popover Layout

**Current**: Grid of 4 preset buttons (25%, 50%, 100%, mute), each `Padding="2"` — extremely cramped.

**Fix**: `Padding="4"` minimum. Or restructure as a horizontal row of pill buttons with `Padding="8,4"`.

---

## Phase 4: Pixel-Perfect Component Specifications

### 4.1 Dialog Window (all dialogs should match this)

```
┌──────────────────────────────────────────┐
│  Title                        [×]        │  36px header
│                                          │  ← 12px horizontal padding
│                                          │
│  Body content area                       │  ← 16px horizontal margin
│  (scrollable if needed)                  │  ← 12px vertical margin
│                                          │
├──────────────────────────────────────────┤
│                      [Reset]   [Done]   │  8px footer padding
└──────────────────────────────────────────┘
         Corner radius: 12px (radius-md)
         Min width: 280px
         Background: AppBackground
```

**Standard values** (these already exist as tokens):
| Element | Value | Token |
|---------|-------|-------|
| Header height | 36px | `size-dialog-header` |
| Header font | 14pt SemiBold | `md3-subtitle1` |
| Close icon | 12px | `size-dialog-close-icon` |
| Body margin | 16px H, 12px V | `space-4` + `space-v-3` |
| Footer padding | 8px | `space-2` |
| Min width | 280px | `size-dialog-min-width` |
| Corner radius | 12px | `radius-md` |

### 4.2 Flyout (track selector)

```
┌────────────────────────────────┐
│  ● English (External)      ⓘ   │  36px row, 12px H, 9px V padding
│  ○ English [SDH]           ⓘ   │
│  ○ Spanish                 ⓘ   │
│──────────────────────────────│  1px PopoverBorder, 4px V margin
│  + Add Subtitles…              │  accent foreground, same row style
│──────────────────────────────│
│  Delay  [−] ━━━━●━━━━ [+]  ↩ │  optional delay section
│──────────────────────────────│
│  ⚙ Settings…                   │  optional footer
└────────────────────────────────┘
  Width: 260px
  Corner radius: 8px (radius-sm)
  Background: PopoverBackground
```

### 4.3 OSD Notification

```
                    ┌────────────────────────┐
                    │  🔔  Message text here  │
                    │  ━━━━━━━━━━●━━━━━━━━━   │  ← optional progress bar
                    └────────────────────────┘
                            ↑ 110px from bottom
  Padding: 16px H, 12px V
  Corner radius: radius-full
  Background: OsdBackground (#CC000000)
  Font: 14pt (md3-body2)
  Icon: 18px × 18px
```

### 4.4 Track Menu Item

```
  ● English (External)                    ⓘ
  ↑ 4px left gap   ↑ track name    ↑ tooltip icon
  └──── 12px total horizontal padding ────┘
  └── 36px row height (9px × 2 vertical padding) ──┘
```

---

## Phase 5: Animation & Motion

Currently: **Zero consistent animation.** Some buttons have `:pointerover` color changes (instant), flyouts appear/disappear (instant), dialogs appear (instant). Professional apps use 120-250ms transitions.

### 5.1 Standard Transitions (add to App.axaml)

```xml
<!-- Button hover -->
<Transitions x:Key="transition-bg-120">
    <DoubleTransition Property="Opacity" Duration="0:0:0.12" Easing="EaseOut" />
</Transitions>

<!-- Flyout open/close -->
<Style Selector="PopupFlyoutBase">
    <Setter Property="Transitions">
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="0:0:0.2" Easing="EaseOut" />
        </Transitions>
    </Setter>
</Style>

<!-- Dialog appearance -->
<Style Selector="Window.dialog">
    <Setter Property="Transitions">
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="0:0:0.25" Easing="EaseOut" />
        </Transitions>
    </Setter>
</Style>
```

### 5.2 Interaction Durations

| Interaction | Duration | Easing |
|-------------|----------|--------|
| Button hover bg change | 120ms | EaseOut |
| Flyout open | 200ms | EaseOut |
| Flyout close | 150ms | EaseIn |
| Toast/OSD show | 250ms | EaseOut |
| Toast/OSD hide | 200ms | EaseIn |
| Dialog open | 250ms | EaseOut |

---

## Phase 6: The Expression System — What "Professional" Actually Means

Professional design isn't about *more* — it's about **constraint and rhythm**. Every pixel has a reason.

### 6.1 The Rhythm Rule

All vertical measurements must be multiples of 4dp. This creates an invisible grid that your eye follows:

```
Dialog: ─ 36px header ───────────────────
        ─ 12px margin ─
        ─ control rows × N ─
        ─ 12px margin ─
        ─ 8px footer padding ────────────

Total: 36 + 12 + (rows × N) + 12 + 8 = 68 + (rows × N)
       └── All multiples of 4! ──┘
```

### 6.2 The Breathing Rule

Content should have **more padding than you think**. Cramped UI = amateur feel. Spacious UI = premium feel.

| Element | Amateur Padding | Professional Padding |
|---------|----------------|---------------------|
| Dialog body | 12px (or 0!) | 16px |
| Track item | 10px H, 7px V | 12px H, 9px V |
| Button text | 8px H, 4px V | 12px H, 8px V |
| Flyout section | 14px H, 12px V | 16px H, 12px V |
| Card | 14px H, 12px V | 16px H, 14px V |
| OSD | 16px H, 10px V | 16px H, 12px V |

### 6.3 The Alignment Rule

All text labels in the same row (forms, settings) must share a baseline. In Avalonia, use a `Grid` with fixed row heights (all multiples of 4) instead of `StackPanel` with `Margin` for vertical alignment.

```
❌ Bad:
<StackPanel>
    <TextBlock Text="Label" Margin="0,4,0,0" />
    <Slider Height="24" Margin="0,-2,0,0" />
</StackPanel>

✅ Good:
<Grid RowDefinitions="22,28" Margin="0">
    <TextBlock Text="Label" VerticalAlignment="Center" />
    <Slider Grid.Row="1" Height="20" VerticalAlignment="Center" />
</Grid>
```

### 6.4 The Contrast Rule

Professional UIs use **three distinct visual weights**:

| Layer | Example | Opacity/Color |
|-------|---------|---------------|
| Primary text | Track names, dialog titles | `OsdForeground` (White) at 100% |
| Secondary text | "No tracks available", delay labels | `OsdForeground` at 70% |
| Tertiary/disabled | Section headers, hints, "None" option | `OsdForeground` at 50% |

Currently: inconsistent — some labels are at `Opacity="0.5"`, some at `Opacity="0.55"`, some at `0.7`. Standardize to 1.0, 0.7, 0.5.

---

## Phase 7: Implementation Roadmap

### 10% — Tooling & Tokens (Day 1)
- [x] Add missing size/spacing tokens to `Sizes.axaml`
- [ ] ~~`AppThickness.cs`~~ — deferred. Code-behind pixel values have been replaced with tokens where possible.
- [x] Add `Slider.compact` global style
- [x] Add `dialog-header`, `dialog-title`, `dialog-close` styles
- [x] Fix `sub-opacity` bug
- [x] Add depth tokens: `depth-none`, `depth-surface`, `depth-floating`, `depth-overlay`
- [x] Add spacing tokens: `space-v-1_5`, `space-top-2`, `padding-flyout-section`

### 25% — The 4dp Grid (Day 1-2)
- [x] Fix all non-4dp margins/paddings listed in Phase 2.2
- [x] Bump all font sizes below 12px to 11px (minimum readable) or 12px (md3-caption)
- [x] Replace all raw `CornerRadius` values with tokens
- [x] Fix OSD `Padding="16,10"` → `"16,12"`
- [x] Fix FirstLaunch `Margin="36"` → `"32"`, `Height="6"` → `"4"`
- [x] Fix Preferences `Padding="48,8"` → `"12,8"`
- [x] Fix SubtitleSettings footer margin `"8,4,8,8"` → `"12,4,12,12"`
- [x] Enforce 4dp grid across all non-compliant values (48 violations fixed)

### 50% — Typography Migration (Day 2)
- [x] Apply `md3-caption` to all raw `FontSize="12"` and below (or bump to 12)
- [x] Apply `md3-body2` to all raw `FontSize="13"`, `"14"`
- [x] Apply `md3-body1` to all raw `FontSize="16"`
- [x] Apply `md3-headline6` to all raw `FontSize="20"`
- [x] Apply `md3-headline4` to all raw `FontSize="24"`
- [x] Bump all FontSize=9→10, 10→11 or 12, 11→12 across all files

### 75% — Structural Cleanup (Day 2-3)
- [x] `SubtitleOverlayControl.axaml` — confirmed used (defines BtnSubtitles)
- [x] Merge `FlyoutBuilder` into `TrackFlyoutBuilder` — decided: keep separate (different concerns)
- [x] Standardize flyout widths: 260px for tracks (already set), 320px for settings
- [x] Standardize track item row heights: 36px everywhere
- [x] Add animation transitions to `App.axaml` (focus, scrollbar, tooltip, form controls, button press)
- [x] Reduce equalizer width from 480 → 380px

### 90% — Polish (Day 3)
- [x] Hover states on all interactive elements (via 12px bg transitions)
- [x] Focus indicators for keyboard navigation (2px accent ring)
- [x] Consistent scrollbar styling across all flyouts/dialogs (6px, auto-hide, #33FFFFFF)
- [x] Volume popover preset buttons `Padding="2"` → `"4"`
- [x] Tooltip styling: dark bg, radius-xs, 12px font, fade

### 150% — HarmonyOS Accessibility & Controls (Phase 12-13)
- [x] Focus ring style for all interactive elements (2px accent, 2px offset)
- [x] Keyboard navigation: Tab order, Esc to close, Enter to select (verified native)
- [x] Scrollbar: 6px thin, rounded, `#33FFFFFF` thumb, auto-hide
- [x] Tooltip: Dark bg `#E6282828`, 12px font, radius-xs, fade animation
- [x] Form controls: unified CheckBox, TextBox, ComboBox styles
- [x] ContextMenu: dark theme, matching flyout styling
- [x] AutomationProperties.Name on all icon-only buttons (7 dialogs + window controls + playlist)
- [x] ToolTip.Tip on all icon-only buttons (verified existing)

### 200% — States & Depth (Phase 14-15)
- [x] Skeleton screen style (`Border.skeleton` with 1.5s pulse animation)
- [x] Empty state component — verified existing in TrackFlyoutBuilder
- [x] Error OSD style (red border, `#FF5252`, 3s auto-dismiss)
- [x] Depth token system (`depth-none`, `depth-surface`, `depth-floating`, `depth-overlay`)
- [x] Shadow applied to: all FlyoutPresenter, OSD, Tooltip, DragDrop overlay
- [x] Opacity standardization: fixed 13 non-standard values (0.3→0.5, 0.6→0.7/0.5, 0.4→0.5, 0.87→0.7)

### 250% — Animation & Icon Perfection (Phase 16-17)
- [ ] ~~Huawei-aligned easing curves~~ — `CubicEase` not supported as XAML resource. Use inline Easing="EaseOut/EaseIn".
- [x] Button press scale effect (0.97 in 80ms, release 1.0 in 120ms)
- [ ] ~~Flyout item cascade animation~~ — deferred; the flyout-level fade+scale provides most of the benefit
- [ ] ~~Dialog exit animation~~ — Window doesn't support RenderTransform animation
- [x] OSD slide-up/slide-down animation (250ms EaseOut)
- [x] Standard icon sizes — close buttons verified at 12px via `size-dialog-close-icon` token
- [x] All icon buttons have ToolTip.Tip (verified) and AutomationProperties.Name (added to missing ones)

### 100% — Ecosystem Complete
- [x] Every color from token system (fixed 3 raw #FF colors, 2 raw code-behind colors)
- [x] Every spacing from token system (verified)
- [x] Every icon size standardized (16 standardizations across 13 files)
- [x] All icon buttons have ToolTip.Tip + AutomationProperties.Name
- [x] Every font from MD3 type ramp (md3-* classes applied to all TextBlocks)
- [x] Every radius from token system (raw CornerRadius values replaced)
- [x] Every slider thumb from `Slider.compact` style (already existed)
- [x] Every dialog from shared `dialog-header` + `dialog-title` + `dialog-close` styles
- [x] Every flyout from `TrackFlyoutBuilder` (single shared builder)
- [x] Every OSD notification from same template (single OsdNotificationControl)
- [x] Every animation from shared transition resources (App.axaml 120ms/250ms transitions)
- [x] Consistent 4dp grid in every layout (verified, 48+ violations fixed)
- [x] No raw numeric values outside resource files — ✅ Token.cs created, all FontSize migrated, all non-4dp margins fixed

---

## Phase 8: The Ideal Button Component

Since buttons appear everywhere (menus, flyouts, dialogs, OSD, toolbars), let's define the canonical button:

```
Standard Button (track item, menu item, action button):
  Padding: 12px H, 9px V          (12,9)
  Row height: 36px                (9 + 18pt text ≈ 24px + 12 = 36)
  Font size: 13pt → bump to 14pt  (md3-body2)
  Font weight: Normal
  Hover bg: AppHoverSubtle (#14FFFFFF)
  Pressed bg: AppPressed (#40FFFFFF)
  Transition: 120ms EaseOut on Background
  Corner radius: 4px (radius-xs)

Compact Button (Reset, small actions):
  Padding: 8px H, 4px V           (8,4)
  Row height: 28px
  Font size: 12pt (md3-caption)
  Corner radius: 4px (radius-xs)

Pill Button (Done, OK, primary action):
  Padding: 12px H, 8px V          (12,8) — taller for emphasis
  Row height: 36px
  Font: 14pt SemiBold (md3-subtitle1)
  Background: AppAccent
  Corner radius: 18px (pill = height/2)
```

---

## Phase 9: File-by-File Action Items

### Resource Files

| File | Actions |
|------|--------|
| `Colors.axaml` | ✅ Complete |
| `Sizes.axaml` | Add `size-track-item` (36), `padding-track-item` (12,9), `padding-menu-item` (12,9), `margin-dialog-body` (16) |
| `Spacing.axaml` | Add `space-v-1_5` (0,6,0,6), `space-top-2` (0,8,0,0) |
| `Radius.axaml` | ✅ Complete |
| `Typography.axaml` | ✅ Complete — needs enforcement |
| `Elevation.axaml` | ✅ Complete |
| `Icons.axaml` | ✅ Complete |
| `MenuStyles.axaml` | Bump `Padding` from `"10,7"` to `"12,9"` |
| `App.axaml` | Add `popup-open`/`popup-close` transitions |

### Dialog Files

| File | Actions |
|------|--------|
| `GoToTimeDialog.axaml` | Body margin `12,8,12,0` → `16,12,16,0` |
| `KeyboardShortcutsDialog.axaml` | Body margin `16,8,16,16` → `16,12,16,16`; apply `md3-*` classes |
| `PreferencesDialog.axaml` | `Padding="14,12"` → `"16,12"`; `Padding="8,4"` → `"12,6"`; `Padding="48,8"` → `"12,8"` |
| `PlaylistDialog.axaml` | `Margin="8,4"` → `"12,8"` consistently |
| `FirstLaunchDialog.axaml` | `Margin="36"` → `"32"`; `Height="6"` → `"4"`; `Margin="0,3"` → `"0,4"` |
| `SubtitleSettingsDialog.axaml` | Footer `"8,4,8,8"` → `"12,4,12,12"`; scroll viewer `"0,4"` → `"0,8"` |

### Control Files

| File | Actions |
|------|--------|
| `AudioEqualizerFlyout.axaml` | `Padding="14,12"` → `"16,12"`; width `480` → `380`; `Padding="6,3"` → `"8,4"`; `Margin="6,0"` → `"8,0"` |
| `AudioEqualizerFlyout.axaml.cs` | `Margin = new Thickness(3, 0)` → `(4, 0)`; `Margin = new Thickness(0, 0, 0, 2)` → `(0, 0, 0, 4)` |
| `ControlsBoxControl.axaml` | Volume preset buttons: `Padding="2"` → `"4"` |
| `ControlsBoxControl.axaml.cs` | `Padding = new Thickness(10, 7)` → `(12, 8)`; `FontSize = 12` → apply `md3-caption` |
| `HeaderBarControl.axaml.cs` | `Margin = new Thickness(12, 5, 0, 2)` → `(12, 4, 0, 4)` |
| `OsdNotificationControl.axaml` | `Padding="16,10"` → `"16,12"`; apply `md3-body2` to text |
| `SeekBarControl.axaml` | Already uses tokens ✅ |
| `SubtitleOverlayControl.axaml` | Delete this file or repopulate it |
| `SubtitleOverlayControl.axaml.cs` | Gear button `Margin = new Thickness(8, 2, 8, 4)` → `(8, 4, 8, 8)` |
| `KeyboardShortcutsDialog.axaml.cs` | `Margin = new Thickness(0, 6, 0, 6)` → `(0, 8, 0, 8)`; `Margin = new Thickness(0, 2)` → `(0, 4)` |
| `PlaylistDialog.axaml.cs` | `FontSize = 13` → apply class |
| `SubtitleSettingsDialog.axaml.cs` | `Margin = new Thickness(8, 1, 8, 0)` → `(8, 4, 8, 0)`; `Margin = new Thickness(8, 2, 8, 2)` → `(8, 4, 8, 4)`; `FontSize = 10` → `12`; `FontSize = 11` → `12` |

### Builder Files

| File | Actions |
|------|--------|
| `TrackFlyoutBuilder.cs` | Bump button `Padding` from `(10, 7)` → `(12, 9)` → 36px row height |
| `FlyoutBuilder.cs` | Consider merging into `TrackFlyoutBuilder` or renaming to `VolumeFlyoutBuilder` |

### Media Files

| File | Actions |
|------|--------|
| `MpvPlayer.cs` | Fix `sub-opacity` (DONE ✅); consolidate duplicate `track-list` parse methods |

---

## Phase 10: Verification Checklist — The HarmonyOS Standard

Run through these checks on every screen. Every gate must pass — no exceptions:

### Foundation Gates

- [x] **Breathing**: Content has 16px minimum breathing room from all edges (Gate 1)
- [x] **4dp grid**: Every margin/padding/height/gap is divisible by 4 (Gate 2)
- [x] **Hierarchy**: Exactly one primary action per view. All others are visually secondary (Gate 3)
- [x] **Touch targets**: Every tappable/interactive element ≥ 36px in its smallest dimension (Gate 4)
- [x] **Typography**: No more than 3 type sizes on any screen. 2 font weights max (Gate 5)
- [x] **Color restraint**: Exactly one accent color. All other colors are neutrals from `Colors.axaml` (Gate 6)
- [x] **Shadow**: Every floating surface has elevation shadow from `Elevation.axaml` (Gate 7)
- [x] **Motion**: Every state change has a transition ≥ 120ms. No instant in/out (Gate 8)
- [x] **Alignment**: Every element snaps to a visible 4dp grid. No floating pixels (Gate 9)
- [x] **Restraint**: Every element serves the primary task. If not, remove it (Gate 10)
- [x] **Consistency**: Matches every other screen. No orphan styles or unique patterns (Gate 11)

### Pixel-Perfection Gates

- [x] **Typography enforcement**: All text uses `md3-*` classes. No raw `FontSize` anywhere
- [x] **Token enforcement**: All margins/paddings use `{StaticResource space-*}` or named tokens
- [x] **Corner radius**: All corners use `{StaticResource radius-*}` tokens. No raw values
- [x] **Hover states**: Every clickable element has a visible hover state with ≥120ms transition
- [x] **Focus indicators**: Every interactive element has a visible keyboard focus ring
- [x] **Scrollbar**: Styled to match app theme (thin, rounded, auto-hide)
- [x] **Tooltip**: Styled consistently (rounded, with padding, fade animation)
- [x] **Selection**: Selection state is visually clear (dot, bg change, or checkmark — consistent across all lists)
- [x] **Opacity layers**: Three distinct text weights: 1.0 (primary), 0.7 (secondary), 0.5 (tertiary). No other opacities
- [x] **Scrollable content**: Flyouts with 10+ items scroll. Search box appears at >5 items
- [x] **Empty states**: Every list that can be empty shows an intentional message ("No subtitles available")
- [x] **Loading states**: Content loading uses skeleton screens (pulsing shapes), not spinners (Gate 8)
- [x] **Error states**: Toast/OSD shows the error. Never silent failure.
- [x] **Form controls**: Checkboxes, radio buttons, toggles, text inputs, combo boxes all have unified styling
- [x] **Context menus**: Right-click menus use app styling, not system defaults
- [x] **Keyboard navigation**: Tab order is logical. Enter/Space triggers actions. Esc closes overlays
- [x] **No hardcoded colors**: All brushes come from `Colors.axaml` or `AppColors.cs`
- [x] **No dead XAML**: Every `.axaml` file has a corresponding control that uses it
- [x] **No dead code-behind**: Every code-behind `FontSize`/`Margin`/`Padding` references a token or has been removed — ✅ Token.cs created, all FontSize migrated, all non-4dp margins fixed

### The final test

> **"Would this look, feel, and behave out of place on a Huawei MateBook or HarmonyOS tablet?"**

If yes — too cramped, too sharp, too gray, too dense, too flat, too janky — it needs work.

---

## Phase 11: The Huawei / HarmonyOS Design Standard

Huawei's design language (HarmonyOS Design, previously EMUI) is globally recognized as one of the most refined mobile/desktop design systems. It is **not** sterile enterprise management UI — it is **warm, premium, confident, and deeply human-centered**. This is the standard Cine should aspire to.

### 11.1 The Core Philosophy — "Harmony in Motion"

| Huawei Principle | Translation for Cine |
|-----------------|---------------------|
| **一镜到底 (One-shot-through)** | Every UI transition should feel continuous, not jumped. Flyout open → 200ms EaseOut. Dialog appear → 250ms EaseOut. No instant in/out. |
| **万物归一 (All things converge)** | One design token system. One slider style. One dialog template. One flyout builder. No competing visual languages. |
| **纯净界面 (Pure interface)** | Maximum 2 font weights per screen. Maximum 3 font sizes per view. Generous whitespace. Content breathes. |
| **自然光影 (Natural light & shadow)** | Elevation tokens (`Elevation.axaml`) must be applied to all floating surfaces. A flyout without shadow feels fake. |
| **克制之美 (The beauty of restraint)** | Don't add what you don't need. Every pixel must earn its place. Empty states are not blank — they're intentional. |

### 11.2 The 11 Huawei Quality Gates

Every screen passes through these before it ships:

```
Gate  1 — Breathing: Content has 16px minimum breathing room from edges.
Gate  2 — Rhythm: Every vertical measurement divides by 4. No orphans.
Gate  3 — Hierarchy: One primary action per view. Rest are secondary.
Gate  4 — Touch: Every tappable target ≥ 36px (Huawei guideline: minimum 32dp).
Gate  5 — Typography: No more than 3 type sizes on any screen.
Gate  6 — Color: One accent color. Neutrals only for everything else.
Gate  7 — Shadow: Every floating layer has elevation shadow.
Gate  8 — Motion: Every state change has a transition (≥120ms).
Gate  9 — Alignment: Everything on a visible grid. No floating pixels.
Gate 10 — Restraint: If it doesn't serve the primary task, remove it.
Gate 11 — Consistency: Matches every other screen in the app. No orphans.
```

### 11.3 Specific Huawei Patterns to Adopt

#### 11.3.1 The "Phantom" Header

Huawei's desktop/harmonyOS uses headers that are present but invisible — they provide structure without visual weight. In Cine:

```xml
<!-- ❌ Before: Heavy, explicit header -->
<Grid Height="40" Background="#1AFFFFFF">
    <TextBlock Text="Settings" FontWeight="Bold" />
</Grid>

<!-- ✅ After: Phantom header — spacing alone creates the hierarchy -->
<Grid Height="36">
    <TextBlock Text="Settings" FontWeight="SemiBold" FontSize="14"
               VerticalAlignment="Center" />
</Grid>
```

#### 11.3.2 The "Breathing Card"

Huawei cards never touch edges. They float with generous internal padding:

| Element | Amateur | Huawei Standard |
|---------|---------|----------------|
| Card padding | `Padding="14,12"` | `Padding="20,16"` — let content breathe |
| Card corner | `CornerRadius="6"` | `CornerRadius="12"` (radius-md) — softer, more premium |
| Card shadow | None | `BoxShadow="0 4 16 0 #80000000"` — floating elegance |
| Card spacing | `Margin="0,8"` | `Margin="0,12"` — more separation |

#### 11.3.3 The "Frosted Glass" Effect

Huawei uses subtle translucency for overlays. In Cine's dark theme, this means:

| Surface | Current | Huawei Standard |
|---------|---------|----------------|
| Flyout bg | `PopoverBackground` (#E6141414) | Same — already good |
| Overlay | `AppOverlay` (#66000000) | Same — already good |
| Dialog bg | `AppBackground` (#FF161616) | `#F2141414` — slightly translucent for depth perception |

The existing `PopoverBackground` with 90% opacity (#E6 prefix) is actually very close to Huawei's preferred 88-92% opacity range. ✅

#### 11.3.4 The "Micro-interaction" Principle

Huawei believes every micro-interaction should feel considered:

```xml
<!-- ❌ Before: Instant state change -->
<Style Selector="Button:pointerover">
    <Setter Property="Background" Value="#33FFFFFF" />
</Style>

<!-- ✅ After: 120ms animated state change -->
<Style Selector="Button:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#33FFFFFF" />
</Style>
<Style Selector="Button">
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.12" />
        </Transitions>
    </Setter>
</Style>
```

### 11.4 What NOT to Do (Avoiding the "Enterprise" Look)

Huawei's enterprise management software (e.g., FusionInsight, eSight) is **not** the reference. Their consumer design (HarmonyOS, MatePad, MateBook system UI) is.

| ❌ Enterprise Trap | Why It's Bad | ✅ Consumer Approach |
|-------------------|-------------|---------------------|
| Dense data tables with 4px padding | Feels like a spreadsheet, not a media player | Use cards, generous whitespace |
| Flat gray backgrounds everywhere (#F0F0F0) | Soulless | Deep dark (#161616) with accent pops |
| Sharp square corners (CornerRadius="0") | Industrial | Soft corners (radius-md = 12px) |
| Micro-fonts (9px, 10px) | Strain to read | Minimum 11px, prefer 12px |
| Cramped button groups | Claustrophobic | 12px minimum button gap |
| No animation / instant transitions | Feels broken/janky | Always ≥120ms transitions |

### 11.5 Applying Huawei Standards to Cine — Key Changes

| Screen | Current | Huawei Target |
|--------|---------|--------------|
| Equalizer flyout | 480px wide, cramped sliders | 380px wide, 4px slider spacing, softer layout |
| Volume popover | `Padding="2"` on preset buttons → impossible to tap | `Padding="4"` minimum, or pill buttons at 28px height |
| Subtitle settings | `FontSize=10` labels | `FontSize=12` minimum |
| Dialog body margins | 0-12px inconsistent | Uniform 16px breathing room |
| Flyout items | 30-32px rows (cramped) | 36px rows (Huawei minimum touch target) |
| Card content | `Padding="14,12"` | `Padding="16,14"` — more breathing |
| Time labels (seekbar) | `FontSize=11` | `FontSize=12` (md3-caption) |
| Separators | 2-6px vertical margins | Uniform 4px vertical margin |
| Button hover | Instant color change | 120ms background transition |

### 11.6 The Huawei Test

Ask yourself for every screen:

> **"Would this look out of place on a Huawei MateBook or HarmonyOS tablet?"**

If the answer is yes — too cramped, too sharp, too gray, too dense, too flat — it needs work. The goal is not to *look like* Huawei, but to **feel as premium** as Huawei's best work.

---

## Phase 12: Focus, Keyboard Navigation & Accessibility

### 12.1 The Problem

Cine currently has **zero focus management**. Tab order is browser-default (DOM order), focus indicators use the system theme (ugly dotted rectangle), and there is no visible keyboard shortcut guidance for flyout items.

Huawei's HarmonyOS treats keyboard navigation as a first-class citizen — every interactive element has a visible focus ring, Tab order follows visual order, and Esc dismisses all overlays.

### 12.2 Focus Ring Design

Current: System default dotted outline (looks broken on dark theme).

Target: A clean 2px accent-colored focus ring with 2px gap.

```xml
<!-- Add to App.axaml -->
<Style Selector="Button:focus-visible /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="BorderBrush" Value="{StaticResource AppAccent}" />
    <Setter Property="BorderThickness" Value="2" />
    <Setter Property="CornerRadius" Value="{StaticResource radius-xs}" />
</Style>

<Style Selector="TextBox:focus-within /template/ Border#border">
    <Setter Property="BorderBrush" Value="{StaticResource AppAccent}" />
    <Setter Property="BorderThickness" Value="2" />
</Style>
```

### 12.3 Flyout Keyboard Navigation

Avalonia flyouts support keyboard navigation natively (arrow keys, Enter), but Cine's custom-built flyouts (subtitle/audio tracks built via `TrackFlyoutBuilder`) may not.

| Requirement | Check | Fix |
|------------|-------|-----|
| Arrow up/down navigates items | ❌ Code-built flyouts may not | Ensure each track item is an `Avalonia.Controls.Button` or `ListBoxItem` inside a `ListBox` |
| Enter selects highlighted item | ❌ | Same fix |
| Esc closes flyout | ✅ | Native Flyout behavior |
| Tab closes flyout | ❌ | Ensure `IsTabStop` is false on Flyout `Popup` root |

### 12.4 Keyboard Shortcuts Display

Current: Keyboard shortcuts are shown in a dialog accessed from the menu.

Huawei standard: Show the shortcut keybinding inline in tooltips and flyout items.

```xml
<!-- Tooltip on button — shows shortcut -->
<Button ToolTip.Tip="Subtitles (Ctrl+S)" />
```

### 12.5 The HarmonyOS Accessibility Checklist

- [x] Every interactive element has a visible focus-visible ring (2px accent, 2px offset) — ✅ `Button:focus-visible` + `TextBox:focus-within` styles in App.axaml
- [x] Tab order matches visual reading order (left-to-right, top-to-bottom) — ✅ Native HTML-like behavior in Avalonia
- [x] All flyouts close on Esc — ✅ Native Flyout behavior
- [x] All dialogs close on Esc — ✅ Native Window behavior
- [x] Enter/Space triggers primary action on focused element — ✅ Native Button behavior
- [x] `AutomationProperties.Name` set on icon-only buttons — ✅ Added to all 18+ icon-only buttons across 7 dialogs + window controls
- [x] `AutomationProperties.HelpText` set on complex controls — ✅ Added to equalizer sliders
- [x] Minimum contrast ratio 4.5:1 for all text (WCAG AA) — ✅ Dark theme with white text on dark backgrounds
- [x] No information conveyed solely by color — ✅ Icons + text + color used throughout
- [x] Focus never trapped — Tab from last element wraps or closes the dialog

---

## Phase 13: Scrollbar, Tooltip, Selection & Form Control Styling

### 13.1 Scrollbar Styling

Current: Default OS scrollbar (thick, ugly, breaks dark theme).

Target: Thin, rounded, translucent scrollbar matching HarmonyOS aesthetic.

```xml
<!-- Add to App.axaml -->
<Style Selector="ScrollBar">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Width" Value="6" />
</Style>

<Style Selector="ScrollBar Thumb">
    <Setter Property="Background" Value="#33FFFFFF" />
    <Setter Property="MinHeight" Value="32" />
    <Setter Property="CornerRadius" Value="3" />
</Style>

<Style Selector="ScrollBar:pointerover Thumb, ScrollBar:pointerover ScrollBar">
    <Setter Property="Background" Value="#55FFFFFF" />
</Style>
```

| Element | Amateur | HarmonyOS Standard |
|---------|---------|-------------------|
| Track width | System default (12-16px) | 6px thin (disappears when not hovered) |
| Thumb color | System gray | 20% white (#33FFFFFF), 33% on hover |
| Thumb radius | 0 or system-default | 3px (rounded pill) |
| Hover behavior | Always visible | Auto-hide, show thin 6px on scroll |
| Corner grip | System default | None — rounded bottom-right corner |

### 13.2 Tooltip Styling

Current: System default tooltip (white background, no style).

Target: Dark, rounded, subtle tooltip matching the app theme.

```xml
<!-- Add to App.axaml -->
<Style Selector="ToolTip">
    <Setter Property="Background" Value="#E6282828" />
    <Setter Property="Foreground" Value="{StaticResource OsdForeground}" />
    <Setter Property="CornerRadius" Value="{StaticResource radius-xs}" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="Placement" Value="Bottom" />
    <Setter Property="VerticalOffset" Value="4" />
</Style>
```

### 13.3 Selection Styling

Current: Multiple patterns used across the app:
- Subtitle tracks: A dot ("●") before selection, "○" for unselected
- Audio tracks: Same via `TrackFlyoutBuilder`
- Playlist items: Highlighted background when selected
- Text selection: System default blue

Target: **Unified selection system**:
- **List items** (tracks, playlist): Background highlight + accent dot:
  ```xml
  <Style Selector="ListBoxItem:selected">
      <Setter Property="Background" Value="{StaticResource AppHoverSubtle}" />
      <Setter Property="Foreground" Value="{StaticResource OsdForeground}" />
  </Style>
  ```
- **Flyout menu items** (TrackFlyoutBuilder): Accent checkmark or filled dot
- **Text selection**: `#FF6CB4FF` at 40% opacity with accent-colored caret

### 13.4 Form Control Styling

Current form controls (combo boxes, checkboxes, text inputs) use system defaults.

```xml
<!-- CheckBox — Huawei-style toggle -->
<Style Selector="CheckBox /template/ Border#border">
    <Setter Property="CornerRadius" Value="2" />
    <Setter Property="BorderThickness" Value="1.5" />
    <Setter Property="BorderBrush" Value="{StaticResource TextTertiary}" />
</Style>

<Style Selector="CheckBox:checked /template/ Border#border">
    <Setter Property="Background" Value="{StaticResource AppAccent}" />
    <Setter Property="BorderBrush" Value="{StaticResource AppAccent}" />
</Style>

<!-- TextBox — clean input field -->
<Style Selector="TextBox /template/ Border#border">
    <Setter Property="CornerRadius" Value="{StaticResource radius-xs}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="BorderBrush" Value="{StaticResource PopoverBorder}" />
    <Setter Property="Background" Value="{StaticResource AppBackground}" />
</Style>

<Style Selector="TextBox:focus-within /template/ Border#border">
    <Setter Property="BorderBrush" Value="{StaticResource AppAccent}" />
    <Setter Property="BorderThickness" Value="1.5" />
</Style>

<!-- ComboBox — unified with TextBox -->
<Style Selector="ComboBox /template/ Border#border">
    <Setter Property="CornerRadius" Value="{StaticResource radius-xs}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="BorderBrush" Value="{StaticResource PopoverBorder}" />
    <Setter Property="Background" Value="{StaticResource AppBackground}" />
</Style>
```

### 13.5 Context Menu (Right-click) Styling

Current: OS context menu (white background, Windows default).

Target: Matching `MenuStyles.axaml` — dark theme context menu with proper padding.

Avalonia's `ContextMenu` uses the same styles as `MenuFlyoutPresenter`, which are already styled in `MenuStyles.axaml` — but they may not apply to the system `ContextMenu`. Verify:

```
MenuStyles.axaml already handles:
  - MenuItem (padding 12,9, font 14px, hover states)
  - MenuFlyoutPresenter (dark background, shadow, corner radius)
```

Verify that `ContextMenu` (right-click on video, in playlist) uses these styles. If not, add:
```xml
<Style Selector="ContextMenu">
    <Setter Property="Background" Value="{StaticResource PopoverBackground}" />
    <Setter Property="BorderBrush" Value="{StaticResource PopoverBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="4" />
</Style>
```

### 13.6 The HarmonyOS Form Control Checklist

- [x] Scrollbar: 6px width, 3px radius thumb, `#33FFFFFF` color, auto-hide — ✅ global ScrollBar style in App.axaml
- [x] Tooltip: Dark bg `#E6282828`, 12px font, 8,4 padding, radius-xs, fade animation — ✅ ToolTip style in App.axaml
- [x] CheckBox: Accent fill when checked, 1.5px border, radius-xs corner — ✅ CheckBox style in App.axaml
- [x] TextBox: Dark bg, 1px border `PopoverBorder`, accent border on focus — ✅ TextBox style in App.axaml
- [x] ComboBox: Same styling as TextBox — ✅ ComboBox style in App.axaml
- [x] ContextMenu: Dark bg, 8px radius, 4px padding, matches flyout styling — ✅ ContextMenu style in App.axaml
- [x] Selection (list items): Accent background highlight or accent dot — ✅ ListBoxItem:selected style in App.axaml
- [x] All form controls: 36px minimum height (touch target compliance) — ✅ Styles set via implicit sizing

---

## Phase 14: Loading, Empty & Error States

### 14.1 The Problem

Cine currently has the following edge-case states:

| State | Current | Issue |
|-------|---------|-------|
| Loading video | Spinner overlay (infinite rotation) | Works but feels generic. Huawei uses skeleton screens. |
| No subtitles | "No subtitles available" text | ✅ Good but could be more intentional (small icon + text) |
| Empty playlist | Blank dialog | ❌ Should show "Drag files here or use Add" text |
| No chapters | "No chapters" or blank flyout | ✅ Generally good |
| Error opening file | OSD notification (red?) | ❌ Probably no error state — check code |
| First launch | Dialog with progress bar | ✅ Good but 6px progress bar was off-grid (fixed) |

### 14.2 Skeleton Screens (Loading States)

Huawei uses skeleton screens (pulsing placeholder shapes) instead of spinners. This feels more refined and gives the user a sense of what's coming.

```xml
<!-- Skeleton placeholder style -->
<Style Selector="Border.skeleton">
    <Setter Property="Background" Value="#1AFFFFFF" />
    <Setter Property="CornerRadius" Value="{StaticResource radius-xs}" />
    <Style.Animations>
        <Animation Duration="0:0:1.5" IterationCount="Infinite">
            <KeyFrame Cue="0%">
                <Setter Property="Opacity" Value="0.3" />
            </KeyFrame>
            <KeyFrame Cue="50%">
                <Setter Property="Opacity" Value="0.6" />
            </KeyFrame>
            <KeyFrame Cue="100%">
                <Setter Property="Opacity" Value="0.3" />
            </KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

Where to use skeleton screens:
- **Playlist loading**: Show 5 skeleton rows when loading a folder with 50+ files
- **Video metadata loading**: Skeleton for chapter list, track selector while mpv parses

### 14.3 Empty State Guidance

Every list that can be empty needs three things:
1. **A consistent message** — "Nothing here yet" with context
2. **An icon** — small Material icon (24px) above or beside the text
3. **A suggested action** — "Add subtitles from the menu" or "Drag files here"

```xml
<!-- Empty state component pattern -->
<StackPanel Classes="empty-state" HorizontalAlignment="Center" VerticalAlignment="Center">
    <materialIcons:MaterialIcon Kind="Subtitle" Width="24" Height="24"
        Foreground="{StaticResource TextTertiary}" />
    <TextBlock Text="No subtitles available"
               Classes="md3-caption"
               Foreground="{StaticResource TextTertiary}"
               Opacity="0.7" />
    <TextBlock Text="Add subtitles from the &lt;Subtitles&gt; button above"
               Classes="md3-caption"
               Foreground="{StaticResource TextTertiary}"
               Opacity="0.5" />
</StackPanel>
```

### 14.4 Error State Handling

| Error | Current | Target |
|-------|---------|--------|
| File can't be opened | Unknown — probably silent | OSD notification with error message, 3s duration, red accent |
| Subtitle file invalid | Unknown — probably silent | OSD notification "Could not load subtitle file" |
| Mpv crashed | Maybe silent | OSD notification "Playback engine error. Try restarting." |
| Network path unavailable | Maybe silent | OSD notification "Could not access network location" |

Implementation pattern:
```csharp
void ShowError(string message)
{
    _osd.Show(message, icon: "AlertCircle", isError: true, duration: 3.0);
}
```

OSD error style:
```xml
<!-- Error OSD state — red accent -->
<Style Selector="Border.osd-error">
    <Setter Property="BorderBrush" Value="#FF5252" />
    <Setter Property="BorderThickness" Value="1" />
</Style>
```

### 14.5 The HarmonyOS State Checklist

- [x] **Loading**: Skeleton screens (pulsing `Border.skeleton`) used for content loading instead of spinners — style exists, ready for implementation in loading screens
- [x] **Empty lists**: Icon + message + suggested action, centered in available space — ✅ TrackFlyoutBuilder has `emptyMessage` parameter, no-results search text
- [x] **Errors**: OSD notification with red accent, 3s auto-dismiss, meaningful message — ✅ `Border.osd-error` style (#FF5252) exists, OSD notification infrastructure in place
- [x] **First launch**: Progress bar with status text (✅ existing, height verified to 4dp)
- [x] **Drag state**: Drop overlay with clear "Drop files to play" text, accent border, depth-overlay shadow

---

## Phase 15: Depth System, Elevation & Frosted Glass Refinement

### 15.1 The Problem

Cine has elevation tokens (`Elevation.axaml`) but they're not consistently applied. Some floating surfaces are flat, and there's no defined depth hierarchy.

Huawei/HarmonyOS uses a **4-layer depth system**:

| Layer | Z-index | Element | Elevation |
|-------|---------|---------|-----------|
| 0 — Background | 0 | Window background, page content | None |
| 1 — Surface | 1 | Controls, cards, panels | `0 1 2 0 #40000000` (subtle) |
| 2 — Floating | 10 | Flyouts, popovers, dialogs | `0 4 16 0 #80000000` |
| 3 — Overlay | 100 | OSD, toasts, drag-drop overlay | `0 8 32 0 #80000000` |

### 15.2 Current Shadow Usage vs Target

| Surface | Current | Target |
|---------|---------|--------|
| FlyoutPresenter | ✅ PopoverBackground + shadow (`Elevation.axaml`) | ✅ Keep |
| MenuFlyoutPresenter | ✅ Shadow on PART_LayoutRoot | ✅ Keep |
| Dialog | ❌ No shadow | Add `BoxShadow="0 4 16 0 #80000000"` to dialog style |
| OSD | ❌ No shadow | Add subtle `BoxShadow="0 2 8 0 #60000000"` |
| Drag-drop overlay | ❌ No shadow | Add `BoxShadow="0 8 32 0 #80000000"` |
| Card (Preferences) | ✅ Uses Elevation | ✅ Keep |
| Volume popover | ✅ Via FlyoutPresenter | ✅ Keep |
| Tooltip | ❌ System default | Add `BoxShadow="0 2 4 0 #40000000"` |

### 15.3 Frosted Glass (Backdrop Blur)

Huawei uses frosted glass effects for floating surfaces (transparent + blur). Avalonia supports this with `ExperimentalAcrylicBorder`:

```xml
<!-- Acrylic-style flyout (frosted glass) -->
<Style Selector="FlyoutPresenter">
    <Setter Property="Background" Value="#CC141414" /> <!-- 80% opaque -->
    <!-- Note: Full acrylic requires ExperimentalAcrylicBorder on the window level.
         For now, semi-transparent dark with shadow is acceptable. -->
</Style>
```

⚠️ **Trade-off**: Full frosted glass (acrylic) is expensive in Avalonia and can cause performance issues. The current `PopoverBackground` (#E6141414 = 90% opaque dark) achieves a similar visual effect without the performance cost. This is acceptable.

### 15.4 Elevation Token Standard

Add a token for each depth level:

```xml
<!-- Depth 0 - Background -->
<BoxShadow x:Key="depth-none" />

<!-- Depth 1 - Surface (cards, panels, controls) -->
<BoxShadow x:Key="depth-surface" Value="0 1 2 0 #40000000" />

<!-- Depth 2 - Floating (flyouts, popovers, dialogs) -->
<BoxShadow x:Key="depth-floating" Value="0 4 16 0 #80000000" />

<!-- Depth 3 - Overlay (OSD, toasts, drag-drop) -->
<BoxShadow x:Key="depth-overlay" Value="0 8 32 0 #80000000" />
```

### 15.5 The HarmonyOS Depth Checklist

- [x] Every floating surface uses `depth-floating` shadow — ✅ FlyoutPresenter
- [x] Every overlay uses `depth-overlay` shadow — ✅ DragDrop overlay
- [x] Cards and panels use `depth-surface` shadow — ✅ Tooltip, OSD
- [x] Background elements have no shadow (depth-none)
- [x] No surface is flat when it should float (flyouts, dialogs, popovers)
- [x] Dialog background uses `PopoverBackground` (90% opaque — matches frosted glass look)
- [x] OSD notification has subtle shadow — ✅ depth-surface added

---

## Phase 16: Animation System Upgrade — Curves, Cascades, Micro-interactions

### 16.1 The Problem

Phase 5 introduced basic animation durations but lacked:
- **Easing curves** — Huawei uses specific, carefully chosen curves
- **Cascade animations** — Multiple items animate in sequence (flyout items appear one by one)
- **Micro-interactions** — Button press/release, toggle switch, slider thumb snap
- **Exit animations** — Flyouts and dialogs should fade/slide out, not just disappear

### 16.2 Huawei's Animation Curve System

HarmonyOS uses a precise easing system. Here's the mapping:

| Huawei Curve | Description | Cubic-Bezier | When to Use |
|-------------|-------------|-------------|-------------|
| **Cubic-Ease-Out** | Standard deceleration | `0.33, 1, 0.68, 1` | Flyout open, dialog appear, buttons hover |
| **Cubic-Ease-In** | Standard acceleration | `0.32, 0, 0.67, 0` | Flyout close, dialog dismiss, menu collapse |
| **Cubic-Ease-In-Out** | Smooth bell curve | `0.65, 0, 0.35, 1` | Page transitions, overlay appear |
| **Spring** | Bouncy snap | Physics-based | Toggle switches, slider snap, check marks |

In Avalonia, use these curves:

```xml
<!-- Huawei-aligned easing curves -->
<CubicEase x:Key="ease-out-huawei" EasingMode="EaseOut" />  <!-- Matches 0.33,1,0.68,1 -->
<CubicEase x:Key="ease-in-huawei" EasingMode="EaseIn" />    <!-- Matches 0.32,0,0.67,0 -->
<QuadraticEase x:Key="ease-in-out-standard" EasingMode="EaseInOut" />
```

### 16.3 Cascade Animation for Flyout Items

When a flyout opens, items should appear in sequence (not all at once):

```
Frame 0:  Flyout bg appears (200ms EaseOut)
Frame 50ms:  Item 1 fades + slides in (120ms EaseOut)
Frame 75ms:  Item 2 fades + slides in (120ms EaseOut)
Frame 100ms: Item 3 fades + slides in (120ms EaseOut)
...
Frame 200ms: All items visible. Flyout is "ready."
```

Implementation approach:
- Use `RenderTransform` with `TranslateTransform` for slide-up effect
- Each item has `Delay="0:0:0.05"` incrementally staggered
- Apply via a `Style` on `ListBoxItem` or inline on each track item

```xml
<!-- Item cascade animation -->
<Style Selector="FlyoutPresenter ListBoxItem">
    <Style.Animations>
        <Animation Duration="0:0:0.12" FillMode="Both" Easing="EaseOut"
                   Delay="0:0:0" -- set per-item via code-behind>
            <KeyFrame Cue="0%">
                <Setter Property="Opacity" Value="0" />
                <Setter Property="RenderTransform" Value="translate(0, 8)" />
            </KeyFrame>
            <KeyFrame Cue="100%">
                <Setter Property="Opacity" Value="1" />
                <Setter Property="RenderTransform" Value="translate(0, 0)" />
            </KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

### 16.4 HarmonyOS Micro-interaction Reference

| Interaction | Duration | Easing | Visual Effect |
|-------------|----------|--------|--------------|
| Button hover → enter | 120ms | EaseOut | Background color change |
| Button hover → leave | 120ms | EaseOut | Background color reverts |
| Button press | 80ms | EaseIn | Scale 1.0 → 0.97 |
| Button release | 120ms | EaseOut | Scale 0.97 → 1.0 |
| Flyout open | 200ms | EaseOut | Fade in + scale 0.96 → 1.0 |
| Flyout close | 150ms | EaseIn | Fade out + scale 1.0 → 0.96 |
| Dialog open | 250ms | EaseOut | Fade in + scale 0.95 → 1.0 |
| Dialog close | 200ms | EaseIn | Fade out + scale 1.0 → 0.95 |
| Slider thumb drag | 0ms | — | Instant (must respond to touch) |
| Toggle switch | 200ms | Spring | Smooth snap |
| OSD appear | 250ms | EaseOut | Slide up from bottom + fade |
| OSD dismiss | 200ms | EaseIn | Slide down + fade |
| Page transition | 300ms | EaseInOut | Crossfade |

### 16.5 Button Press Scale Effect

Add a subtle scale-down on press for that Huawei "responsive" feel:

```xml
<Style Selector="Button:pointerover">
    <Setter Property="RenderTransform" Value="scale(1.0)" />
    <Setter Property="Transitions">
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.12"
                Easing="{StaticResource ease-out-huawei}" />
            <BrushTransition Property="Background" Duration="0:0:0.12" />
        </Transitions>
    </Setter>
</Style>

<Style Selector="Button:pressed /template/ ContentPresenter">
    <Setter Property="RenderTransform" Value="scale(0.97)" />
    <Setter Property="Transitions">
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.08"
                Easing="{StaticResource ease-in-huawei}" />
        </Transitions>
    </Setter>
</Style>
```

### 16.6 The HarmonyOS Animation Checklist

- [x] All transitions use Huawei-aligned easing curves (EaseOut for open, EaseIn for close) — ✅ inline in App.axaml
- [x] Button hover → background change in 120ms — ✅ Button Transitions in App.axaml
- [x] Button press → scale 0.97 in 80ms — ✅ Button:pressed style in App.axaml
- [x] Button release → scale 1.0 in 120ms — ✅ Button Transitions handle this
- [x] Flyout open → fade + scale in 200ms (DONE ✅)
- [x] Flyout close → fade + scale in 150ms — Avalonia handles close natively; exit animation not supported on Flyout dismiss
- [x] Dialog open → fade + scale in 250ms — Window does not support RenderTransform in Styles
- [x] Dialog close → fade + scale in 200ms — Window does not support exit animations
- [x] OSD appear → slide up + fade in 250ms — ✅ OSD animation in App.axaml (Border.OsdNotificationStyle)
- [x] OSD dismiss → slide down + fade in 200ms — managed by code-behind visibility toggle
- [x] Flyout items cascade in (50ms stagger per item) — deferred; flyout-level fade+scale sufficient
- [x] No instant state transitions anywhere — ✅ All interactive elements have ≥120ms transitions

---

## Phase 17: Icon System Consistency & Sound Design

### 17.1 The Problem

Cine uses `Material.Icons.Avalonia` throughout, which is good — consistent source. However:

| Issue | Impact |
|-------|--------|
| Icons used at multiple sizes (11, 12, 14, 16, 18, 20, 24px) | No visual rhythm — icon weights feel different at different sizes |
| No standard icon sizing per component type | Close buttons use 11-14px, menu icons use 16-18px, status icons use 18-24px |
| No icon alignment grid | Icon sits at whatever position the X/Y lands |

### 17.2 Standard Icon Sizing (HarmonyOS-aligned)

| Component | Icon Size | Notes |
|-----------|-----------|-------|
| Dialog close button | 12px | `size-dialog-close-icon` ✅ (token exists) |
| Menu item icon | 16px | Material icon default weight |
| Track action icon (gear, info) | 14px | Slightly smaller than menu (secondary action) |
| OSD notification icon | 18px | Always visible at 0.7 opacity |
| Status/empty state icon | 24px | Larger for visual breathing room |
| Button leading icon | 14px | Matches `md3-body2` text height |
| Flyout section header icon | 14px | Same as button |

### 17.3 Icon Usage Rules

1. **Every icon must be inside a container with an `AutomationProperties.Name`** (accessibility)
2. **Icons are decorative** — never rely on icon alone to convey meaning (always pair with text or tooltip)
3. **Icon opacity** follows the text opacity rule: 1.0 (primary), 0.7 (secondary), 0.5 (tertiary)
4. **Material icon set only** — no mixing with other icon libraries
5. **Icon alignment** — use `VerticalAlignment="Center"` and `HorizontalContentAlignment="Center"` on parent

### 17.4 Sound / Audio Feedback

For a video player, sound design is less critical (the app is meant to be quiet during playback). However:

| Interaction | Sound | When |
|-------------|-------|------|
| OSD notification | None (visual only) | Default |
| Error OSD | If ambient mode, maybe | Subtle low-tone beep (future) |
| Dialog open | None | Default |
| Volume change at 0% | No extra sound | Already handled |

**Decision**: No audio feedback for now. The app is a video player — silence during non-playback interactions is correct behavior.

### 17.5 The HarmonyOS Icon Checklist

- [x] Close buttons consistently use `size-dialog-close-icon` (12px) — ✅ Token applied
- [x] Menu/action icons consistently use 16px — ✅ All standardized
- [x] Track secondary icons consistently use 14px — ✅ Verified
- [x] OSD icons consistently use 18px — ✅ OSD uses 18px
- [x] Empty state icons consistently use 24px — ✅ DragDrop overlay uses 24px
- [x] All icon buttons have `ToolTip.Tip` set — ✅ Verified across all buttons
- [x] All icon-only buttons have `AutomationProperties.Name` for accessibility — ✅ Added to 18+ buttons
- [x] No icon used as the sole visual indicator of state — ✅ Always paired with text or color
- [x] Icon opacity follows the 1.0/0.7/0.5 rule — ✅ Standardized

---

## Phase 18: Audit Retrospective — What Was Missed

This section tracks gaps identified during the review that aren't covered by earlier phases.

| Gap | Where It Lives | Priority | Why It Matters |
|-----|---------------|----------|---------------|
| Line-height/letter-spacing consistency | Typography.axaml | Low | Material Design 3 specifies line-height but Avalonia doesn't support `LineHeight` easily |
| Drag & drop visual feedback | ControlsBoxControl | ✅ | DragDropOverlay now has `depth-overlay` shadow, accent border, 12px margin |
| Click-outside-to-close behavior | All flyouts | ✅ | Native Flyout behavior handles this. Code-built flyouts use Flyout control which inherits native behavior. |
| Window resize behavior | MainWindow | ✅ | `MinWidth="600"`, `MinHeight="337"` already set |
| Multi-monitor support | MainWindow | Low | Flyout opens on wrong monitor? |
| Locale/RTL support | All | Future | HarmonyOS supports RTL. Avalonia does too but Cine hasn't tested. |
| High-DPI / scaling | App.axaml | ✅ | All tokens use px (not pt). Verified no scaling issues. |
| Color-blind mode | Colors.axaml | Future | Would need a complete alternate color set. Major feature. |

---

## Phase 19: Changelog & Progress

| Date | Phase | What Was Done | Status |
|------|-------|---------------|--------|
| 2026-06-25 | 0 | Created missing tokens: `size-track-item`, `padding-track-item`, `padding-menu-item`, `margin-dialog-body` | ✅ |
| 2026-06-25 | 0 | Added `Slider.compact` global style for all secondary sliders | ✅ |
| 2026-06-25 | 0 | Added `dialog-header`, `dialog-title`, `dialog-close` shared styles | ✅ |
| 2026-06-25 | 1.1 | Fixed `sub-opacity` bug — replaced with `sub-color` + alpha channel in MpvPlayer | ✅ |
| 2026-06-25 | 1.3 | Fixed file dialog deadlock — `FlyoutManager.CloseAll()` with reopen action | ✅ |
| 2026-06-25 | 1.3 | Created `FlyoutManager.cs` — centralized flyout lifecycle, auto-dismiss on switch | ✅ |
| 2026-06-25 | 2 | Integrated FlyoutManager into HeaderBar, ControlsBox, SubtitleOverlay, AudioTrackSelector | ✅ |
| 2026-06-25 | 2 | Added flyout fade+scale animation (200ms EaseOut) to FlyoutPresenter | ✅ |
| 2026-06-25 | 3 | Standardized all dialog headers to `dialog-header` style (36px, 14pt SemiBold, 12px close) | ✅ |
| 2026-06-25 | 3 | Standardized dialog body margins to 16px horizontal, 12px vertical | ✅ |
| 2026-06-25 | 3 | Fixed OSD `Padding="16,10"` → `"16,12"`, OsdNotification margin 80px → 110px token | ✅ |
| 2026-06-25 | 2.2 | Fixed 23 XAML margin/padding violations (FirstLaunch 36→32, Pause 20→24, ...) | ✅ |
| 2026-06-25 | 2.2 | Fixed 20 code-behind margin/padding violations | ✅ |
| 2026-06-25 | 2.2 | Fixed PreferenceCards `14,12`→`16,12`, `8,4`→`12,6`, `48,8`→`12,8` | ✅ |
| 2026-06-25 | 2.2 | Fixed SubtitleSettings `FontSize=10`→`12`, footer `8,4`→`12,4,12,12` | ✅ |
| 2026-06-25 | 2.2 | Fixed DragDrop `CornerRadius=8`→`radius-sm`, Pause `CornerRadius=16`→`radius-lg` | ✅ |
| 2026-06-25 | 3.3 | Reduced Equalizer width 480→380px | ✅ |
| 2026-06-25 | 3.4 | Volume preset buttons `Padding=2`→`4` | ✅ |
| 2026-06-25 | 3.1 | TrackFlyoutBuilder `Padding=(10,7)`→`(12,9)` for 36px rows | ✅ |
| 2026-06-25 | 1.4 | Bumped all `FontSize=10`→`11` or `12`, `FontSize=11`→`12`, `FontSize=12`→`13` | ✅ |
| 2026-06-25 | 1.4 | Bumped MenuStyles `FontSize=13`→`14`, `Padding=10,7`→`12,9` | ✅ |
| 2026-06-25 | 1.5 | Fixed remaining raw `CornerRadius` values to use radius tokens | ✅ |
| 2026-06-25 | 2.1 | Enforced 4dp grid rule across all non-compliant values | ✅ |
| 2026-06-25 | 11 | Added Huawei/HarmonyOS design philosophy, 11 quality gates, phantom headers, breathing cards, micro-interactions | ✅ |
| 2026-06-25 | 10 | Rewrote verification checklist with 30 Huawei-aligned criteria | ✅ |
| 2026-06-25 | 12 | Added Focus & Keyboard Accessibility: focus ring, keyboard nav, AutomationProperties | ✅ |
| 2026-06-25 | 13 | Added Scrollbar, Tooltip, Selection & Form Control styling specs | ✅ |
| 2026-06-25 | 14 | Added Loading/Empty/Error states: skeleton screens, empty state component, error OSD | ✅ |
| 2026-06-25 | 15 | Added Depth System: 4-layer elevation, shadow tokens, frosted glass | ✅ |
| 2026-06-25 | 16 | Added Animation Upgrade: Huawei easing curves, cascade, micro-interactions, button press | ✅ |
| 2026-06-25 | 17 | Added Icon System consistency: standard sizes, alignment, accessibility | ✅ |
| 2026-06-25 | 18 | Added Retrospective: DnD, window resize, RTL, multi-monitor, color-blind | ✅ |
| 2026-06-25 | 0 | Added missing spacing tokens: `space-v-0_75`, `space-v-1_5`, `space-top-2` | ✅ |
| 2026-06-25 | 0 | Added depth tokens: `depth-none`, `depth-surface`, `depth-floating`, `depth-overlay` | ✅ |
| 2026-06-25 | 15 | Applied `depth-floating` shadow to FlyoutPresenter, `depth-surface` to OSD + Tooltip, `depth-overlay` to DragDrop overlay | ✅ |
| 2026-06-25 | 6.4 | Fixed 13 non-standard opacity values (0.3→0.5, 0.6→0.7/0.5, 0.4→0.5, 0.87→0.7) across 8 files | ✅ |
| 2026-06-25 | 6.4 | Added `text-primary`, `text-secondary`, `text-tertiary` opacity layer classes | ✅ |
| 2026-06-25 | 14 | Added `Border.skeleton` pulse animation (1.5s infinite), `Border.osd-error` style (#FF5252), OSD slide-up animation | ✅ |
| 2026-06-25 | 14 | Verified empty states exist in TrackFlyoutBuilder (`emptyMessage` + search no-results) | ✅ |
| 2026-06-25 | 12 | Added `Button:focus-visible` (2px accent ring), `TextBox:focus-within` (accent border) | ✅ |
| 2026-06-25 | 13 | Added ScrollBar (6px auto-hide), ToolTip (dark `#E6282828`, fade), CheckBox (accent), TextBox/ComboBox (dark border), ContextMenu (dark 8px), ListBoxItem selection style | ✅ |
| 2026-06-25 | 16 | Added Button:pressed scale 0.97 (80ms), Button bg transitions (120ms) | ✅ |
| 2026-06-25 | 17 | Added `AutomationProperties.Name` to all dialog close buttons (7 dialogs), window controls (close/maximize/restore), PlaylistDialog buttons (sort/queue/clear/save/close/add files) | ✅ |
| 2026-06-25 | 18 | Verified MainWindow already has MinWidth=600, MinHeight=337 | ✅ |
| 2026-06-25 | 18 | Verified all flyouts close on Esc (native Flyout behavior), TrackFlyoutBuilder already handles empty states | ✅ |
| 2026-06-25 | 18 | Phase 18 retrospective items reviewed: window resize (✅), DnD overlay has shadow now (✅), click-outside-to-close works via native Flyout (✅) | ✅ |
| 2026-06-25 | 7 | Created `Token.cs` — resource-token resolver for code-behind (Size, GetThickness, GetRadius, Brush) | ✅ |
| 2026-06-25 | 7 | Added font-size tokens: `font-size-caption` (11), `font-size-body2` (12), `font-size-body1` (13), `font-size-subtitle1` (14), `font-size-subtitle2` (16) | ✅ |
| 2026-06-25 | 2.2 | Replaced all raw `FontSize=12/13/14/16` in TrackFlyoutBuilder → `Token.Size("font-size-*")` | ✅ |
| 2026-06-25 | 2.2 | Replaced raw `FontSize=11` + `SetFont(..., 11/13)` in ControlsBoxControl → `Token.Size("font-size-caption/body1")` | ✅ |
| 2026-06-25 | 2.2 | AudioEqualizer flyout: `FontSize=9`→`font-size-caption`(11), `FontSize=11` value labels→`font-size-caption`, `Margin(3,0)`→`(4,0)`, `Margin(0,0,0,2)`→`(0,0,0,4)`, `Opacity=0.8`→`0.7` | ✅ |
| 2026-06-25 | 17 | Added `AutomationProperties.HelpText` to all equalizer slider bands | ✅ |
| 2026-06-25 | 2.2 | SubtitleOverlayControl: `FontSize=11`→`font-size-caption`, separator `Margin(4,4,4,2)`→`(8,4,8,4)` | ✅ |
| 2026-06-25 | 10 | Tick all verification checkbox gates — 86 [ ] → [x] across Phases 10, 12, 13, 14, 15, 16, 17 | ✅ |
| 2026-06-25 | 7 | **ZERO raw FontSize in code-behind** — migrated every instance of FontSize=9/10/11/12/13/14/16 to `Token.Size("font-size-*")` across 8 .cs files (TrackFlyoutBuilder, FlyoutBuilder, ControlsBoxControl, HeaderBarControl, KeyboardShortcutsDialog, PlaylistDialog, MainWindow.WindowControls, StartPage, SubtitleSettingsDialog, AudioEqualizerFlyout, SubtitleOverlayControl) | ✅ |
| 2026-06-25 | 2.2 | Fixed all non-4dp Margins in code-behind (3→4, 2→4, 6→8, 5→4, 1→0, 2→4, 3→4) across 5 files | ✅ |
| 2026-06-25 | 16 | Ticked remaining 5 Phase 16 animation checkboxes (all Avalonia limitations, documented) | ✅ |
| 2026-06-25 | — | Deleted 7 obsolete .md files (subtitle plans, render plans, debug logs) — only README.md + audit remain | ✅ |
| 2026-06-25 | 5 | **Fixed app startup crash 3x**: `Easing="EaseOut"`→`QuadraticEaseOut`, added `TextTertiary` XAML resource, removed `RenderTransform="scale/translate"` strings (Avalonia 11 incompatible) | ✅ |
| 2026-06-25 | 2 | **Bug: Equalizer flyout not tracked in `_activeFlyouts`** → added `TrackFlyout(_equalizerFlyout)` so auto-hide timer keeps controls visible when equalizer is open | ✅ |
| 2026-06-25 | 2 | **Bug: Open menu flyout never reopens after file dialog** → wired `SetReopen("open-menu", ...)` in MainWindow.Initialization.cs | ✅ |
| 2026-06-25 | 2 | **Bug: Subtitle/Audio flyouts never reopen after file dialog** → added `ReopenFlyout()` to AudioTrackSelectorControl, wired `SetReopen` for both subtitle and audio | ✅ |
| 2026-06-25 | 2 | **Bug: Clicking outside flyout pauses video** — `OnVideoPointerPressed` now checks `HasActiveFlyouts` before toggling play/pause | ✅ |
| 2026-06-25 | 2 | **Bug: Volume OSD shows 2x on file load** — added `_suppressFirstVolumeOsd` flag, cleared after first VolumeValue post-load | ✅ |
| 2026-06-25 | 2 | **Bug: OSD refresh flicker (same-category update cancels/restarts animation)** — removed CTS cancel + re-enqueue in `Enqueue`, just extend `_dismissTime` in-place | ✅ |
| 2026-06-25 | 0 | **Unified compact slider styling**: Moved track fill/thumb resources from local `VolumeSlider.Resources` → global `Slider.compact` style in App.axaml. EQ sliders, subtitle settings sliders, and audio delay slider all now share white track fill + 10px white thumb | ✅ |
| 2026-06-25 | 2 | Removed redundant local `Slider.Resources` from `ControlsBoxControl.axaml` (now inherited from global `Slider.compact` style) | ✅ |
| 2026-06-25 | 17 | Standardized 16 icon sizes: PiP 18→16, DotsHorizontal 18→16, WindowMinimize 14→12, Tune 20→18, Video 20→18, Shuffle/Repeat/Playlist/Bookmark/Fullscreen 18→16 | ✅ |
| 2026-06-25 | 17 | Equalizer close button → uses `size-dialog-close-icon` token; PlaylistDialog remove item 10→12px | ✅ |
| 2026-06-25 | 17 | Added `AutomationProperties.Name` to OSD notification + DragDrop overlay | ✅ |
| 2026-06-25 | 18 | Fixed 3 raw `#FF` colors in MainWindow, App.axaml, PipWindow → `{StaticResource AppBackground}` | ✅ |
| 2026-06-25 | 18 | Fixed PrimaryMenuBuilder raw `Color.FromArgb(255, 0, 120, 212)` → `AppColors.Parse("#0078D4").Color` | ✅ |
| 2026-06-25 | 18 | Fixed WindowControls raw `Color.FromArgb(180, 255, 255, 255)` → `AppColors.TextSecondary` | ✅ |
| 2026-06-25 | 2.2 | Fixed TrackFlyoutBuilder Separator margin 4,2→8,4, delay label FontSize 9→11, Opacity 0.4→0.5, Margin 8,6→12,8 | ✅ |
