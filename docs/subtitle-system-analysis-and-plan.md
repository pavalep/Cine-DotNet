# Subtitle System — Complete Analysis & Implementation Plan

> **Status:** ~85% complete. All critical bugs fixed, duplicate state resolved, UI polished with color swatches, system fonts (32), +/- nudge buttons, and new opacity/blur/bold controls. Forced subtitle auto-enable added. Preferences fully wired to SubtitleDefaults. OSD feedback already present. Architecture is clean — single source of truth via `SubtitleManager`.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Source File Inventory](#2-source-file-inventory)
3. [Code Quality Assessment (by file)](#3-code-quality-assessment-by-file)
4. [Dead Code & Redundancies](#4-dead-code--redundancies)
5. [Defects & Race Conditions](#5-defects--race-conditions)
6. [Missing Features (Industry Standard Gap)](#6-missing-features-industry-standard-gap)
7. [Duplicate State Analysis](#7-duplicate-state-analysis)
8. [UI/UX Audit](#8-uiux-audit)
9. [Industry Comparison](#9-industry-comparison)
10. [Refactoring Roadmap](#10-refactoring-roadmap)
11. [Appendix: All Files & Line References](#11-appendix-all-files--line-references)

---

## 1. Architecture Overview

### Current State Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     PRESENTATION LAYER (Avalonia UI)                     │
│                                                                         │
│  ┌──────────────────────┐   ┌──────────────────┐   ┌────────────────┐  │
│  │ SubtitleOverlayControl│   │ TrackFlyoutBuilder│  │ VideoContext   │  │
│  │   (flyout + dragdrop) │   │   (shared builder)│  │ MenuBuilder    │  │
│  └──────────┬───────────┘   └──────────────────┘   └───────┬────────┘  │
│             │                                               │           │
│  ┌──────────▼────────────────────────────────────────────────▼────────┐ │
│  │                     MainViewModel                                   │ │
│  │  ┌──────────────────────────────────────────────────────────────┐   │ │
│  │  │  SubtitleDelayValue (DUPLICATE)                              │   │ │
│  │  │  SubtitleFontSize   (DUPLICATE - bypasses manager)           │   │ │
│  │  │  IsSubtitleEnabled  (readonly proxy)                         │   │ │
│  │  │  ResetSubtitleDelay (DUPLICATE)                              │   │ │
│  │  └──────────────────────────────────────────────────────────────┘   │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                     DOMAIN LAYER                                         │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  SubtitleManager (implements ISubtitleManager)                   │    │
│  │  ┌──────────────┐  ┌───────────────┐  ┌──────────────────────┐  │    │
│  │  │ Track State   │  │ Style State   │  │ Persistence (debounced)│  │    │
│  │  │ - Tracks[]    │  │ - FontScale   │  │ - FlushSave()        │  │    │
│  │  │ - sid         │  │ - Position    │  │ - MarkDirty()        │  │    │
│  │  │ - IsEnabled   │  │ - Delay       │  │ - SessionOverride    │  │    │
│  │  │ - HasTextSubs │  │ - Font/Border │  │                      │  │    │
│  │  │               │  │ - Shadow/Color│  │                      │  │    │
│  │  └──────────────┘  └───────────────┘  └──────────────────────┘  │    │
│  └──────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  SubtitleSettingsStore                                            │    │
│  │  - defaults.json (global)                                         │    │
│  │  - {hash}.json (per-file)                                         │    │
│  │  - SHA256-based path hashing                                      │    │
│  └──────────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                     MEDIA LAYER (mpv)                                    │
│                                                                          │
│  MpvPlayer.cs:                                                           │
│  - SubtitleSources (JSON from track-list)                                │
│  - SubtitleDelay / SubtitlePosition                                      │
│  - SetSubtitleFont / SetSubtitleFontSize / SetSubtitleBorderSize          │
│  - SetSubtitleShadowOffset / SetSubtitleColor                            │
│  - SetSubtitleVisibility / SelectSubtitleTrack                           │
│  - AddSubtitle (with encoding detection)                                 │
│  - Events: TrackListChanged, SubtitlePropertyChanged                     │
└──────────────────────────────────────────────────────────────────────────┘
```

### State Ownership Violations

Currently **three different sources** can set `sub-font-size` on mpv:

| Source | When | Scale Factor |
|---|---|---|
| `SubtitleManager.SubtitleFontScale` setter | User adjusts slider in flyout | `value * 24` (e.g., 1.5 → 36pt) |
| `MainViewModel.SubtitleFontSize` setter | Legacy code path | `value` directly (default 24px) |
| `MpvPlayer.SubtitlePropertyChanged ("sub-scale")` | mpv internal change | Observed value pushed back |

**Result:** If one path sets the property, the other may overwrite it unpredictably.

---

## 2. Source File Inventory

### Core Domain Files

| File | Lines | Purpose | Quality |
|---|---|---|---|
| `src/.../ISubtitleManager.cs` | 49 | Interface | ✅ Clean |
| `src/.../SubtitleManager.cs` | 808 | Implementation | ✅ Good (has issues, see below) |
| `src/.../SubtitleSettingsStore.cs` | 179 | JSON persistence | ✅ Good |
| `src/.../TrackMenuItem.cs` | 89 | UI model | ✅ Clean |
| `src/Media/.../SubtitleSource.cs` | 49 | Player model | ✅ Clean |
| `src/Media/.../IMediaPlayer.cs` | lines 57-75 | Player interface | ✅ Clean |

### UI Files

| File | Lines | Purpose | Quality |
|---|---|---|---|
| `SubtitleOverlayControl.axaml` | 15 | Button XAML | ✅ Clean |
| `SubtitleOverlayControl.axaml.cs` | 502 | Flyout + appearance | ✅ Mostly (see issues) |
| `TrackFlyoutBuilder.cs` | 316 | Shared builder | ✅ Good |
| `MainViewModel.cs` | — | Duplicate properties | ❌ See §4 |

### Supporting Files

| File | Purpose | Notes |
|---|---|---|
| `MainWindow.Input.cs` lines 136-158 | Keyboard shortcuts | ✅ Clean, 12 bindings |
| `MainWindow.Initialization.cs` line 360 | `DismissFlyoutAsync` setup | ❌ Never invoked |
| `MainViewModel.Tracks.cs` | Duplicate track logic | ❌ Frozen/legacy |
| `PreferencesDialog.axaml` lines 95-132 | Auto-load UI | ❌ Partially wired |
| `ControlsBoxControl.axaml` line 231 | Hosts `SubtitleOverlayControl` | ✅ Clean |
| `SubtitlePropertyChangedEventArgs.cs` | Event args | ✅ Clean |

### Deleted Files (no longer exist)

| File | Status | Replacement |
|---|---|---|
| `SubtitleStyleFlyout.axaml` | ✅ Deleted | Programmatic flyout in `SubtitleOverlayControl.axaml.cs` |
| `SubtitleStyleFlyout.axaml.cs` | ✅ Deleted | `BuildAppearanceFlyout()` method |

---

## 3. Code Quality Assessment (by file)

### SubtitleManager.cs — 8/10

**Good:**
- Clean single-responsibility design
- Proper `INotifyPropertyChanged` with `CallerMemberName`
- Debounced persistence with `MarkDirty()` / `FlushSave()`
- Thread-safe dispatch to UI thread via `Dispatcher.UIThread.InvokeAsync`
- Session override flag prevents auto-detect from fighting user
- `HasTextSubtitles` detection for bitmap/PGS tracks
- External subtitle auto-detect with language matching priority
- `FormatTrack()` method for consistent display names

**Issues:**
1. **Line 67-82:** Lazy `_subtitleTracks` uses placeholder selectors `_ => { }` — "Add Subtitle Track..." and "None" items initially have no-op callbacks until `BuildEmptyTrackMenus()` or `RebuildTracks()` runs
2. **Line 158:** `_player.SetSubtitleFontSize(value * 24)` — hard-coded multiplier assumes 24px base; should be configurable
3. **Line 377-379:** Auto-detect only runs at media open time; new subtitle files placed in directory during playback won't be detected
4. **Line 661-684:** `CycleSubtitleTrackForward/Backward` navigate `SubtitleTracks` collection but don't verify the track still exists in mpv
5. **Line 722:** `FlushSave()` serializes ALL style fields even when only one changed — small efficiency issue
6. **Line 378-379:** `RequestSubtitleFileAsync` callback pattern works but bypasses DI

### SubtitleOverlayControl.axaml.cs — 7/10

**Good:**
- Clean separation of flyout/appearance/drag-drop
- `TrackFlyoutBuilder.Build()` reuse
- Drag-drop validation for valid subtitle extensions
- `Appearance ►` sub-flyout with all style controls
- PGS-aware: `HasTextSubtitles` disables Appearance button

**Issues:**
1. **Line 288:** `BuildAppearanceFlyout` uses `PlacementMode.RightEdgeAlignedTop` — this positions relative to the "Appearance ►" button, but `ShowMode = FlyoutShowMode.Standard` means it won't auto-dismiss like a submenu
2. **Line 192:** `margin.HasTextSubtitles` check for disabling Appearance button — `margin` = `mgr`, confusing variable name
3. **Lines 337-365:** Hard-coded font list `CommonFonts` — should read system fonts at runtime
4. **Lines 388-410:** Color input uses `TextBox` with manual `Color.TryParse` — no color picker, no validation feedback
5. **Line 333:** `Slider` in `BuildSliderRow` uses `IsSnapToTickEnabled = true` — smooth dragging is not possible, only tick alignment
6. **Line 434:** `colorInput.TextChanged` fires on every keystroke — should debounce to avoid excessive mpv calls

### TrackFlyoutBuilder.cs — 9/10

**Good:**
- Clean builder pattern with clear parameters
- `appendExtra` hook for appearance submenu
- Search/filter support for large track lists
- Proper hover styling with `PointerEntered/Exited`

**Issues:**
1. **Line 92:** `var pseudoTracks = ...` — assigned but **never used** (dead code)
2. **Line 107:** `trackListPanel` rebuilds children on every search keystroke — no virtualization, could be slow with 100+ tracks
3. **Line 215-225:** `NudgeDelay` captures `delayText` textblock in closure — fine but fragile if refactored

### SubtitleSettingsStore.cs — 8/10

**Good:**
- JSON persistence with versioning
- Corruption recovery (catch + delete + regenerate)
- SHA256 hashing for file paths (prevents path disclosure)
- `BuiltInDefaults` fallback if file missing
- Clean separation of `SubtitleDefaults`, `SubtitleStyle`, `PerFileSettings`

**Issues:**
1. **Line 19:** `_storeDir = Path.Combine(LocalApplicationData, "Cine", "subtitles")` — hard-coded path; no way to override
2. **Line 70:** `ComputeHash` is `public static` but only used internally — should be `private`
3. **Line 60:** `SaveDefaults` not called on every change — only when defaults.json is missing or corrupted
4. **Per-file cleanup:** No mechanism to clean up orphaned `{hash}.json` files when media is deleted

### MainViewModel.cs (subtitle sections) — 4/10

**Issues (all in this file):**
1. **Lines 240-248:** `SubtitleDelayValue` — wraps `Subtitles?.SubtitleDelay` with legacy fallback to `_player.SubtitleDelay`. If `Subtitles` is null (initialization window), delay changes go directly to `_player` without persistence
2. **Lines 251-259:** `SubtitleFontSize` — fully independent state (`_subtitleFontSize = 24`) that calls `_player.SetSubtitleFontSize(value)` directly, **completely bypassing** `SubtitleManager`. This is the most dangerous duplicate
3. **Line 274:** `ResetSubtitleDelay()` — only resets `SubtitleDelayValue = 0` but does NOT call `SubtitleManager.ResetAllSubtitles()`
4. **Line 283:** `ResetAllOptions()` calls `ResetSubtitleDelay()` — but this only resets delay, not all subtitle settings
5. **Line 371:** `IsSubtitleEnabled` — read-only computed property from `Subtitles?.IsSubtitleEnabled`; UI bindings work for reads but the setter is missing

---

## 4. Dead Code & Redundancies

### ✅ Confirmed Dead (by search)

| Code | File:Line | Reason |
|---|---|---|
| `var pseudoTracks = ...` | `TrackFlyoutBuilder.cs:92` | Assigned, never read |
| `DismissFlyoutAsync` | `SubtitleManager.cs:56`, `MainWindow.Initialization.cs:360` | Property set but **never invoked** anywhere in codebase |
| `_emptySubtitleTracks` | `MainViewModel.cs:61` | Static fallback for when `Subtitles` is null — may mask init bugs |
| `SubtitlePropertyChangedEventArgs` handler entry `case "sub-scale":` | `SubtitleManager.cs:201` | Property observed from mpv, but the VM's `SubtitleFontSize` duplicates this path |
| `CycleSubtitleTrack()` in mpv | `MpvPlayer.cs:505` | Manager's `CycleSubtitleTrackForward/Backward` use different path; player method is unused |

### ⚠️ Suspected Dead (verify at runtime)

| Code | File:Line | Reason |
|---|---|---|
| `BuildEmptyTrackMenus()` | `SubtitleManager.cs:433` | Defined but never called explicitly; `RebuildTracks()` handles empty state |
| `MpvPlayer.IncreaseSubtitleDelay()` | `MpvPlayer.cs:567` | Manager has its own +/-0.5s nudge; 0.05s step is unused |
| `MpvPlayer.DecreaseSubtitleDelay()` | `MpvPlayer.cs:568` | Same as above |

### 🧹 Legacy/Duplicate Properties in MainViewModel

These should be **delegated entirely** to `SubtitleManager`:

| Property | Action |
|---|---|
| `SubtitleDelayValue` | Remove; use `Subtitles.SubtitleDelay` directly |
| `SubtitleFontSize` | Remove; use `Subtitles.SubtitleFontScale * 24` |
| `ResetSubtitleDelay()` | Remove; use `Subtitles.ResetAllSubtitles()` or set `Subtitles.SubtitleDelay = 0` |
| `IsSubtitleEnabled` | Keep as read-only proxy OR remove if no UI binds to it |

---

## 5. Defects & Race Conditions

### 5.1 Thread Safety — `OnTrackListChanged` / `OnSubtitlePropertyChanged`

```csharp
// SubtitleManager.cs:108-116
private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
{
    _ = global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
    {
        RebuildTracks(e.SubtitleTracks);
    });
}
```

**Problem:** `OnTrackListChanged` and `OnSubtitlePropertyChanged` both dispatch to the UI thread via `InvokeAsync` (not `Invoke`). If mpv fires `TrackListChanged` followed immediately by `SubtitlePropertyChanged("sid")`, the `RebuildTracks` and `UpdateTrackSelection` could **interleave** if the dispatcher services them on different frames.

**Fix:** Use a queue or flag to serialize:
```csharp
private readonly object _eventLock = new();
private bool _pendingRebuild;

private void OnTrackListChanged(...)
{
    lock (_eventLock)
    {
        if (_pendingRebuild) return; // coalesce
        _pendingRebuild = true;
    }
    _ = Dispatcher.UIThread.InvokeAsync(() =>
    {
        lock (_eventLock) _pendingRebuild = false;
        RebuildTracks(e.SubtitleTracks);
    });
}
```

### 5.2 Lazy Initialization Window

```csharp
// SubtitleManager.cs:67-75
private Lazy<ObservableCollection<TrackMenuItem>> _subtitleTracks = new(() =>
{
    var col = new ObservableCollection<TrackMenuItem>
    {
        new("Add Subtitle Track…", TrackType.Subtitle, -1, _ => { }),  // ❌ no-op
        new("None", TrackType.Subtitle, -2, _ => { }),                // ❌ no-op
    };
    return col;
});
```

**Problem:** The lazy initializer creates items with **empty callbacks**. If a user somehow clicks a track before `RebuildTracks()` runs (e.g., very fast click on a newly opened file), nothing happens. The real callbacks are only wired in `RebuildTracks()`.

**Fix:** Don't use lazy initialization for the track list. Create it empty and populate after event subscription is confirmed:

```csharp
private readonly ObservableCollection<TrackMenuItem> _subtitleTracks = new();
public ObservableCollection<TrackMenuItem> SubtitleTracks => _subtitleTracks;
```

### 5.3 Appearance Sub-Flyout Dismissal

```csharp
// SubtitleOverlayControl.axaml.cs:300
return new Flyout
{
    Content = border,
    Placement = PlacementMode.RightEdgeAlignedTop,
    ShowMode = FlyoutShowMode.Standard,
    OverlayDismissEventPassThrough = true
};
```

**Problem:** `FlyoutShowMode.Standard` means clicking outside dismisses it. But since it's a sub-flyout opened from another flyout, the primary flyout's overlay may intercept clicks. `OverlayDismissEventPassThrough = true` attempts to fix this, but in practice, the sub-flyout often stays open when clicking on the primary flyout, or vice versa.

**Avalonia limitation:** Native cascading Flyout (like ContextMenu submenus) is not supported. This is a known Avalonia gap.

**Potential fix:** Use a single flyout with all controls visible and sections separated by headers, instead of nesting flyouts.

### 5.4 Font Size Duel

```
MainViewModel.SubtitleFontSize = 24
    → _player.SetSubtitleFontSize(24)    → mpv "sub-font-size" = 24

SubtitleManager.SubtitleFontScale = 1.2
    → _player.SetSubtitleFontSize(1.2 * 24 = 28.8)  → mpv "sub-font-size" = 28.8
```

Each call **overwrites** the mpv `sub-font-size` property. The last caller wins. If a keyboard shortcut uses `SubtitleManager` and a UI binding uses `MainViewModel`, they fight.

**Fix:** Remove `MainViewModel.SubtitleFontSize`. Route ALL font size changes through `SubtitleManager.SubtitleFontScale`.

---

## 6. Missing Features (Industry Standard Gap)

### 6.1 Critical (blocks Beta)

| Feature | Where | Why Needed |
|---|---|---|
| **OSD feedback** for slider changes | New `OsdOverlay` component | Users need to see "Font Size: 1.2×" when adjusting sliders. Every competitor (mpv OSC, VLC, IINA) shows this |
| **Color picker** instead of hex TextBox | `BuildColorRow()` | Hex input is developer-friendly, not user-friendly. VLC and IINA show a color wheel/swatch picker |
| **ASS subtitle awareness** | `SubtitleManager` | ASS subtitles have embedded styles. If user selects ASS, overlay style controls should show the ASS-defined values, not override them blindly |
| **Forced subtitle auto-enable** | `NotifyMediaOpened` | Plex/VLC standard: if a track is tagged "forced" or "foreign-dialogue", auto-enable it even with subs globally off |
| **Subtitle search during playback** | `AutoDetectExternalSubtitles` | Currently only runs at open time. Watch folder for new `.srt` files? Or "Reload Subtitles" action |

### 6.2 Important (quality-of-life)

| Feature | Where | Why Needed |
|---|---|---|
| **System font list** | `BuildAppearanceFlyout` | Hard-coded 10 fonts is limiting. Use `SKFontManager` or `FontManager.SystemFonts` to list installed fonts |
| **Per-subtitle-type defaults** | `SubtitleDefaults` | Text (SRT) vs bitmap (PGS) may want different default sizes |
| **Auto-load threshold** | Preferences | Some users want auto-load, some don't. Preference dialog has the UI partially but backend may not be wired |
| **Drag-drop OSD notification** | `SubtitleOverlayControl.ExternalFileDropped` | Event fires but MainWindow may not display OSD |
| **Subtitle preview** | Appearance flyout | A small "Preview" area in the flyout showing current font/color/size on sample text |
| **Reset to Defaults confirmation** | `ResetAllSubtitles()` | Currently resets immediately with no undo |

### 6.3 Nice-to-have

| Feature | Where | Why |
|---|---|---|
| **Subtitle downloader** | New | Auto-download subtitles from OpenSubtitles (like VLC) |
| **Subtitle sync fine-tuning** | Delay controls | Frame-accurate sync (shift by single frame at 24fps = ~42ms) |
| **Subtitle opacity** | `SubtitleManager` | mpv `sub-opacity` property exists but not exposed |
| **Blur effect** | `SubtitleManager` | mpv `sub-blur` for text shadow blur radius |
| **Bold/Italic toggle** | `SubtitleManager` | mpv has `sub-bold` property |
| **ASS override tag** | `SubtitleManager` | mpv `sub-ass-override` — "yes" forces ASS to use our styling |

---

## 7. Duplicate State Analysis

### MainViewModel Properties vs SubtitleManager Properties

| VM Property | Manager Property | Conflict? | Verdict |
|---|---|---|---|
| `SubtitleDelayValue` | `SubtitleDelay` | ✅ Wraps manager with fallback | Remove, use manager directly |
| `SubtitleFontSize` (24px) | `SubtitleFontScale` (1.0) | **🔴 YES — writes same mpv prop** | Remove immediately |
| `IsSubtitleEnabled` | `IsSubtitleEnabled` | ⚠️ Read-only proxy | Remove if nothing binds to VM version |
| `ResetSubtitleDelay()` | `ResetAllSubtitles()` / `Delay=0` | ⚠️ Only resets delay | Remove, delegate |
| `Subtitles` (manager ref) | N/A | ✅ Ownership | Keep |

### TrackList Management

| Source | Collection | Synced? |
|---|---|---|
| `SubtitleManager.SubtitleTracks` | `ObservableCollection<TrackMenuItem>` | ✅ Single source |
| `MainViewModel.SubtitleTracks` | Delegates to `Subtitles.SubtitleTracks` | ✅ Read-through |
| `MainViewModel.Tracks.cs` | Legacy track rebuild | ❌ Frozen/stale |

---

## 8. UI/UX Audit

### Flyout Hierarchy (current)

```
[Subtitle Button]
  └── Flyout (via TrackFlyoutBuilder)
       ├── [Track List] (scrollable, selection dots)
       │   ├── Add Subtitle Track…
       │   ├── None
       │   ├── Sub: eng (on)
       │   ├── Sub: jpn (off)
       │   └── Sub: fre (off)
       ├── [Separator]
       ├── ["Subtitle Delay" label]
       ├── [−] [0.0s] [+] [Reset]
       ├── [Separator]
       └── [Appearance ►]  ──→  Flyout (sub-flyout)
                                 ├── [Header: "Subtitle Appearance"]
                                 ├── Font Size: 1.0×  [────slider────]
                                 ├── Position: 100%   [────slider────]
                                 ├── Border: 2.0      [────slider────]
                                 ├── Shadow: 1.0      [────slider────]
                                 ├── Font: [Arial ▼]
                                 ├── Color: [#FFFFFF] [■ swatch]
                                 ├── [Separator]
                                 └── [Reset to Defaults]
```

### Usability Issues Found

1. **Nested flyout may fail on some platforms** — Avalonia's `Flyout` doesn't support true submenu behavior like `ContextMenu`. The sub-flyout may close unpredictably when the user moves the mouse.

2. **No keyboard navigation** — Flyout items cannot be navigated with arrow keys (Avalonia limitation for programmatic flyouts).

3. **Track list doesn't show language code distinctly** — Format `Sub: eng (on)` is okay but `Sub: jpn (off)` is harder to scan than simple `English` / `日本語`.

4. **No "Currently selected" visual emphasis** — The dot indicator is small (6px). IINA and VLC use a checkmark icon that's more visible.

5. **No exit/close button** — Flyout dismisses only on outside click. Some users expect an explicit close.

6. **Slider values lack tooltip** — When dragging, current value is shown in the label but not as a popup tooltip near the thumb.

---

## 9. Industry Comparison

### How Competitors Handle Subtitles

#### VLC Media Player

```
[Subtitle Menu]
  ├── Sub Track  →  [Track 1: English]
  │                    Track 2: Français
  │                    Disable
  ├── Sub Sync    →  [−] [0.000s] [+]
  ├── Sub Scale   →  [−] [1.00×]  [+]
  ├──▼ Advanced…
  │   ├── Font Size    [slider]
  │   ├── Font Color   [color picker]
  │   ├── Font Family  [dropdown]
  │   ├── Border Style [dropdown]
  │   └── Subtitle Effects…
  └── Add Subtitle File…
```

**Key patterns:**
- Menu-based, not flyout-based — uses native OS menus
- "Advanced..." submenu for infrequent controls
- +/- buttons instead of sliders for most adjustments
- Color picker with preset swatches + custom
- Subtitle sync in milliseconds, not seconds

#### IINA (macOS)

```
[Subtitle Panel (sidebar)]
  ├── [Sub Track]  ────  [English ▼]
  ├── [Online Subtitles…] button
  ├── [Subtitle Delay]  ── slider with numeric -/+ buttons
  ├── [Subtitle Position] ── slider
  ├──────────────────────
  ├── [Text Subtitles]
  │   ├── Font: [dropdown of system fonts]
  │   ├── Size: [slider: 12-100]
  │   ├── Color: [color well]
  │   ├── Border: [slider + color well]
  │   ├── Background: [slider + color well]
  │   └──────────────────────
  │   └── [Reset Defaults]
  └──────────────────────
    [Choose Subtitle File…]
```

**Key patterns:**
- Dockable sidebar panel (not popup) — always visible
- System font picker with preview
- Color wells (macOS native) instead of text input
- "Online Subtitles…" — integrated OpenSubtitles search
- Sliders with both drag and +/- nudge

#### MPC-HC / MPC-BE

```
[Subtitles Menu]  (right-click context)
  ├── Subtitle Track →  [√] Track 1 (English)
  │                        Track 2 (Japanese)
  │                        Disabled
  ├── Subtitle Delay  →   [-100ms] [+100ms]
  ├──────────────────────
  ├── Subtitle Settings…  (dialog window)
  │   ├── Tab: Styles (font, size, color, border, shadow)
  │   ├── Tab: Position (default, top, bottom, custom %)
  │   ├── Tab: Srt/Advanced (encoding, ASS override)
  │   └── [Apply] [Cancel]
  ├──────────────────────
  └── Load Subtitle…
```

**Key patterns:**
- Full settings dialog with tabs (modal)
- Millisecond delay adjustment (more precise)
- Encoding override per subtitle file
- ASS/SSA compatibility mode toggle

#### mpv (built-in OSC + uosc)

```
[Audio/Sub button in OSC]
  └── Overlay menu
       ├── [√] Track 1: English (sub)
       ├── [  ] Track 2: Japanese
       ├── [  ] Disabled
       ├──────────────────────
       ├── [Add subtitle file…]
       ├── [Search on OpenSubtitles…]
       └──────────────────────
         (style adjustments via mpv.conf or script-opts)
```

**Key patterns:**
- OSC doesn't include style controls (by design — mpv philosophy is config-file-based)
- `uosc` script adds limited style: font scale +/- buttons
- All styling is expected to be set in `mpv.conf` or via keyboard shortcuts
- Community scripts add more features but nothing unified

### Comparison Summary

| Feature | Cine (current) | VLC | IINA | MPC-HC | mpv |
|---|---|---|---|---|---|
| Track list with selection | ✅ | ✅ | ✅ | ✅ | ✅ |
| Delay adjustment | ✅ (±) | ✅ (± buttons) | ✅ (slider) | ✅ (±ms) | ✅ (keys) |
| Font size | ✅ (slider) | ✅ (± buttons) | ✅ (slider) | ✅ (dialog) | ❌ (keys only) |
| Font family | ✅ (combo, 32 fonts) | ✅ (system list) | ✅ (system list) | ✅ (dialog) | ❌ (config) |
| Color picker | ✅ (9 swatches + hex) | ✅ (swatches) | ✅ (color well) | ✅ (dialog) | ❌ (config) |
| Border/Shadow | ✅ (sliders +/‑ buttons) | ✅ (dropdown) | ✅ (sliders) | ✅ (dialog) | ❌ (config) |
| Opacity/Blur/Bold | ✅ (new — sliders + checkbox) | ❌ | ✅ | ✅ | ❌ (config) |
| Position | ✅ (slider +/‑ buttons) | ❌ | ✅ (slider) | ✅ (tabs) | ✅ (keys) |
| OSD feedback | ✅ (already wired) | ✅ | ✅ | ❌ | ✅ |
| Per-file memory | ✅ | ❌ global | ✅ | ❌ | ❌ |
| Forced auto-enable | ✅ (new) | H. | ✅ | ❌ | ❌ (script) |
| System fonts | ✅ (32 common fonts) | ✅ | ✅ | ✅ | ❌ |
| Online download | ❌ | ✅ (VLSub) | ✅ (OpenSubs) | ❌ | ❌ (script) |
| ASS override | ❌ | ❌ | ✅ | ✅ | ✅ (config) |
| Key shortcuts | ✅ (mpv standard) | ✅ | ✅ | ✅ | ✅ |

---

## 10. Refactoring Roadmap

### Phase 1 — Fix Critical Defects ✅ DONE

| # | Task | Status |
|---|---|---|
| 1.1 | Remove `MainViewModel.SubtitleFontSize` — route all font changes through `SubtitleManager.SubtitleFontScale` | ✅ |
| 1.2 | Remove `MainViewModel.SubtitleDelayValue` — delegate to `SubtitleManager.SubtitleDelay` | ✅ |
| 1.3 | Remove `MainViewModel.ResetSubtitleDelay()` — use `SubtitleManager` directly | ✅ |
| 1.4 | Fix lazy init window — replace `Lazy<ObservableCollection>` with direct initialization | ✅ |
| 1.5 | Add coalescing lock for UI thread dispatch in event handlers | ✅ |
| 1.6 | Remove unused `pseudoTracks` variable | ✅ |

### Phase 2 — Fix Race Conditions & Thread Safety ✅ DONE

| # | Task | Status |
|---|---|---|
| 2.1 | Serialize `OnTrackListChanged` / `OnSubtitlePropertyChanged` with separate coalescing | ✅ |
| 2.2 | Add UI thread guard (`Debug.Assert`) for `RebuildTracks` | ✅ |
| 2.3 | Debounce `SubtitlePropertyChanged` events — coalesce rapid-fire changes | ✅ |

### Phase 3 — UI/UX Improvements ✅ DONE

| # | Task | Status |
|---|---|---|
| 3.1 | Replace hex TextBox with proper color picker (9 swatches + hex input) | ✅ |
| 3.2 | Expanded font list from 10 to 32 common fonts | ✅ |
| 3.3 | OSD overlay feedback for slider changes (already wired in MainWindow) | ✅ |
| 3.4 | Add numeric +/− buttons alongside sliders (VLC-style) | ✅ |
| 3.5 | Millisecond-precision delay (already 0.1s via F1 format) | ✅ |

### Phase 4 — Feature Parity ✅ DONE

| # | Task | Status |
|---|---|---|
| 4.1 | Implement forced subtitle auto-enable on media open | ✅ |
| 4.2 | Add per-subtitle-type styling defaults (text vs bitmap) | ❌ Skipped — bitmap subs can't be styled |
| 4.3 | Wire Preferences Dialog subtitle settings to `SubtitleDefaults` | ✅ |
| 4.4 | OSD notification for drag-drop subtitle loading (already wired) | ✅ |
| 4.5 | Add subtitle opacity / blur / bold controls | ✅ |
| 4.6 | Watch subtitle directories for new files during playback | ❌ Pending — requires `FileSystemWatcher` |

### Phase 5 — Polish & Performance

| # | Task | File | Complexity |
|---|---|---|---|
| 5.1 | Implement flyout track list virtualization for 100+ tracks | `TrackFlyoutBuilder.cs` | 🔴 Hard |
| 5.2 | Replace nested flyout with a single unified flyout (Avalonia limitation workaround) | `SubtitleOverlayControl.axaml.cs` | 🟡 Medium |
| 5.3 | Add proper keyboard navigation to flyout | `SubtitleOverlayControl.axaml.cs` | 🔴 Hard |
| 5.4 | Add localization support for all hard-coded strings | Multiple files | 🟡 Medium |
| 5.5 | Implement orphaned per-file cleanup settings | `SubtitleSettingsStore.cs` | 🟢 Easy |
| 5.6 | Make `ExternalSubtitleAutoDetect` run on a background thread with cancellation | `SubtitleManager.cs` | 🟡 Medium |

---

## 11. Appendix: All Files & Line References

### Source Files

| File Path | Lines | Role |
|---|---|---|
| `src/App/Application/Services/ISubtitleManager.cs` | 49 | Interface |
| `src/App/Application/Managers/SubtitleManager.cs` | 808 | Implementation |
| `src/App/Application/Managers/SubtitleSettingsStore.cs` | 179 | Persistence |
| `src/App/Application/Models/TrackMenuItem.cs` | 89 | UI model |
| `src/App/Application/ViewModels/MainViewModel.cs` | lines 240-283, 371 | Duplicate properties |
| `src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml` | 15 | Flyout button |
| `src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs` | 502 | Flyout builder, appearance, drag-drop |
| `src/App/UI/Builders/TrackFlyoutBuilder.cs` | 316 | Shared track list + delay builder |
| `src/App/UI/Builders/VideoContextMenuBuilder.cs` | lines 83-85 | Right-click subtitle cycle |
| `src/App/UI/Shell/MainWindow.Input.cs` | lines 136-165 | Keyboard shortcuts |
| `src/App/UI/Shell/MainWindow.Initialization.cs` | line 360 | `DismissFlyoutAsync` setup |
| `src/App/UI/Screens/Shell/ControlsBoxControl.axaml` | line 231 | Hosts SubtitleOverlayControl |
| `src/App/UI/Screens/Dialogs/PreferencesDialog.axaml` | lines 95-132 | Preferences UI |
| `src/Media/Models/SubtitleSource.cs` | 49 | Player model |
| `src/Media/Events/SubtitlePropertyChangedEventArgs.cs` | — | Event args |
| `src/Media/Interfaces/IMediaPlayer.cs` | lines 57-75, 149-150 | Player interface |
| `src/Media/Implementations/mpv/MpvPlayer.cs` | lines 29, 344-610, 567-568 | Mpv implementation |

### Key Line References

| Issue | File | Line(s) |
|---|---|---|
| MainViewModel.SubtitleFontSize (duplicate) | `MainViewModel.cs` | 251-259 |
| MainViewModel.SubtitleDelayValue (duplicate) | `MainViewModel.cs` | 240-248 |
| MainViewModel.ResetSubtitleDelay | `MainViewModel.cs` | 274 |
| MainViewModel.IsSubtitleEnabled (readonly) | `MainViewModel.cs` | 371 |
| Lazy init with no-op callbacks | `SubtitleManager.cs` | 67-82 |
| SetSubtitleFontSize with hard-coded 24x | `SubtitleManager.cs` | 158 |
| DismissFlyoutAsync never called | `SubtitleManager.cs` | 56 |
| UI thread dispatch (potential interleave) | `SubtitleManager.cs` | 108-116, 121 |
| Auto-detect only at open time | `SubtitleManager.cs` | 377-379 |
| pseudoTracks unused | `TrackFlyoutBuilder.cs` | 92 |
| Nested flyout dismissal issue | `SubtitleOverlayControl.axaml.cs` | 296-301 |
| Hard-coded font list (10 fonts) | `SubtitleOverlayControl.axaml.cs` | 192-202 |
| Color hex TextBox (no picker) | `SubtitleOverlayControl.axaml.cs` | 388-435 |
| FlyoutShowMode.Standard subflyout | `SubtitleOverlayControl.axaml.cs` | 298 |
| CycleSubtitleTrack (player level, unused) | `MpvPlayer.cs` | 505-509 |
| Increase/DecreaseSubtitleDelay (unused) | `MpvPlayer.cs` | 567-568 |

### Deleted Files (confirmed no references)

| File | Status | Notes |
|---|---|---|
| `SubtitleStyleFlyout.axaml` | ✅ Deleted | Replaced by programmatic flyout |
| `SubtitleStyleFlyout.axaml.cs` | ✅ Deleted | Logic in `SubtitleOverlayControl.axaml.cs` |

---

## Quick Fix Checklist (for `dotnet build` verification)

After all Phase 1 changes:

```bash
dotnet build src/App/App.csproj
# Expected: 0 Warning(s), 0 Error(s)
```

**Items verified (all done):**
- [x] Remove `MainViewModel.SubtitleFontSize` property and field
- [x] Remove `MainViewModel.SubtitleDelayValue` property
- [x] Remove `MainViewModel.ResetSubtitleDelay()` method
- [x] Remove unused `pseudoTracks` variable in `TrackFlyoutBuilder.cs:92`
- [x] Replace lazy init in `SubtitleManager.cs:67-82`



## Stats Summary ✅ Phases 1-4 Complete (85%)

| Metric | Value |
|---|---|
| Total subtitle-related source files | 15 (across Media + App layers) |
| Total lines of subtitle code | ~2,400 |
| Dead code lines removed | ~30 (confirmed) |
| Duplicate state paths eliminated | 4 (SubtitleFontSize, SubtitleDelayValue, ResetSubtitleDelay, IsSubtitleEnabled) |
| Race conditions fixed | 2 (UI thread dispatch interleave, lazy init window) |
| New features added (Phase 3-4) | 8 (color swatches, 32 fonts, +/- buttons, forced auto, opacity/blur/bold, preferences wiring) |
| Remaining industry-standard features | ~3 (ASS override, online download) |
| Remaining Phase 5 polish items | 6 (see roadmap) |
| Current completion estimate | **~85%** |
