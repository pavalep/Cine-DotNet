# UI Mismatch Tracker (Python GTK4 vs Avalonia)

Audit date: 2026-05-27  
Purpose: Single handoff file for any model/engineer to continue UI parity work.

## Quick Progress Board
- [x] P0 fully complete
- [x] P1 fully complete (Fullscreen chrome parity pending) -> now complete
- [x] P2 fully complete (Visual/Styling Parity)
- [x] P3 fully complete (Integration & Command Parity)
- [x] Start page center actions implemented
- [x] Drop indicator overlay implemented
- [x] Seek hover + wheel + click interactions implemented
- [x] Loop/shuffle bindings implemented
- [x] Subtitle/audio icon on-off switching implemented
- [x] Typed track menu selection and active styling implemented
- [x] Exact control row ordering parity achieved
- [x] Subtitle drag/drop fixed

## Scope Compared
- Python reference UI:
  - `code_for_reference/src/window.blp`
  - `code_for_reference/src/window.py`
  - `code_for_reference/src/style.css`
- Avalonia UI:
  - `src/App/UI/Views/MainWindow.axaml`
  - `src/App/UI/Views/MainWindow.axaml.cs`
  - `src/App/UI/Resources/*.axaml`
  - `src/App/Application/ViewModels/MainViewModel.cs`

## Summary
- Current Avalonia implementation is a strong foundation but still diverges from Python in layout behavior, control parity, menu behavior, interaction model, and dynamic state logic.
- High-impact gaps are in track/playlist/options workflows, progress/seek interaction behavior, overlay reveal behavior, and icon/state parity.

---

## Phase 1: Critical Parity Gaps (P0)

### 1. Missing options menu component parity
- Status: `Complete`
- Python has dedicated `$OptionsMenuButton` with rich options behavior.
- Avalonia has dedicated `OptionsMenuButton` UserControl with Contrast, Brightness, Gamma, Saturation, Hue, Subtitle Delay, Audio Delay, Playback Speed, and Screenshot.
- Files:
  - Avalonia: `src/App/UI/Components/OptionsMenuButton.axaml`, `src/App/Application/ViewModels/MainViewModel.cs`
- Task:
  - Implement an `OptionsMenuButton` component in `src/App/UI/Components`.
  - Wire actions and bindings in `MainViewModel`.
  - Progress update (2026-05-27): Replaced placeholder with full `OptionsMenuButton` containing sliders/reset commands for all supported player options.

### 2. Playlist controls behavior incomplete
- Status: `Complete`
- Python has full playlist behavior: shuffle/unshuffle, loop playlist, navigation sensitivity, playlist dialog sync.
- Avalonia has Playlist Dialog implemented with `PlaylistItemViewModel`, drag-and-drop support, "playing" visual indicator, and jump-to-play functionality.
- Files:
  - Avalonia: `src/App/UI/Views/PlaylistDialog.axaml`, `src/App/Application/ViewModels/PlaylistItemViewModel.cs`
- Task:
  - Implement full playlist state model + commands.
  - Add playlist dialog and visibility/sensitivity rules.
  - Progress update (2026-05-27): Implemented full `PlaylistDialog` with listbox, playing indicator, drop support, and wired to `MainViewModel`.

### 3. Track menu dynamic population and state icons missing
- Status: `Complete`
- Python dynamically builds subtitle/audio/video menus and toggles icon variants (`subtitles-off`, `audio-off`).
- Avalonia builds typed TrackMenuItems with "None"/"Add Track..." entries and proper bold/accent styling for selected tracks.
- Files:
  - Avalonia: `src/App/UI/Views/MainWindow.axaml`, `src/App/Application/ViewModels/MainViewModel.cs`, `TrackMenuItem.cs`
- Task:
  - Replace simple string lists with typed track menu models.
  - Implement active-selection, “None”, “Add Track” entries, and off/on icon switching.
  - Progress update (2026-05-27): Replaced string lists with `TrackMenuItem` objects. Implemented exact programmatic flyout building with `.track-item` / `.track-pseudo` classes and accent markers. Wired actions for Add/None and active track switching.

### 4. Seek/progress interaction parity missing
- Status: `Complete`
- Python supports:
  - hover chapter popover previews,
  - scroll on progress for seek actions,
  - marks with chapter labels behavior.
- Avalonia currently renders markers but lacks hover/scroll popover parity.
- Files:
  - Python: `code_for_reference/src/window.py` (`_on_progress_motion`, `_on_progress_scroll`, `chapter_popover`)
  - Avalonia: `src/App/UI/Views/MainWindow.axaml(.cs)`
- Task:
  - Add pointer-motion chapter preview popover.
  - Add wheel-based seek behavior with throttling.
  - Progress update (2026-05-27): Added chapter preview popover on seek hover and throttled wheel-seek handlers in `MainWindow` seek area. Improved chapter preview popover fidelity (centered tracking, box shadow, styling).

---

## Phase 2: Interaction & Behavior Parity (P1)

### 5. Overlay reveal behavior not fully equivalent
- Status: `Complete`
- Python reveal logic checks active popovers/buttons and pointer containment before hiding.
- Avalonia has simpler timer + bounds check; may hide in cases Python keeps UI visible.
- Files:
  - Python: `code_for_reference/src/window.py` (`_hide_ui`, `_hide_ui_timeout`)
  - Avalonia: `src/App/UI/Views/MainWindow.axaml.cs`
- Task:
  - Track open flyouts/popovers and integrate into hide conditions.
  - Match Python timing defaults and behavior under fullscreen/dialog states.
  - Progress update (2026-05-27): Added flyout open/close tracking and integrated active-flyout/drop-overlay checks into auto-hide logic so controls do not hide while user interacts with menus/popovers.

### 6. Drag/drop indicator parity missing
- Status: `Complete`
- Python has dedicated drop revealer with contextual icon/text (“Play” vs “Add Subtitle Track”).
- Avalonia only changes start-page border visuals.
- Files:
  - Python: `code_for_reference/src/window.blp`, `window.py` (`_on_drop_enter`, `_on_drop_leave`)
  - Avalonia: `src/App/UI/Views/MainWindow.axaml.cs`
- Task:
  - Add drop overlay component with reveal animation and contextual label/icon.
  - Progress update (2026-05-27): Added `DropIndicatorOverlay` with contextual text/icon (`Play` vs `Add Subtitle Track`) and drag enter/leave/drop wiring. Implemented fade-in/out animations and exact GTK visual parity.

### 7. Fullscreen UX parity incomplete
- Status: `Complete`
- Python updates fullscreen icon/tooltip dynamically and adjusts decoration behavior.
- Avalonia toggles fullscreen but lacks equivalent decoration-layout behavior and detailed icon/tooltip sync.
- Files:
  - Python: `window.py` (`on_fs_change`, `_set_fs_state`)
  - Avalonia: `MainWindow.axaml.cs`
- Task:
  - Add full icon/tooltip state synchronization and window chrome behavior parity.
  - Progress update (2026-05-27): Fullscreen toggle exists. Implemented deep parity: HeaderBar dynamically hides title and standard menus, showing only the fullscreen close button when active.

### 8. Button set/order mismatch in controls row
- Status: `Complete`
- Python control row has specific ordering and includes playlist button + options button + dedicated visibility behaviors.
- Avalonia exact match achieved: removed Stop/Screenshot from transport, fixed Play/Forward/Next ordering, placed Volume/Track/Playlist/Options correctly.
- Files:
  - Avalonia: `MainWindow.axaml`
- Task:
  - Reorder to Python baseline and restore missing controls/visibility rules.
  - Progress update (2026-05-27): Transport controls re-ordered to exact Python layout parity. Removed redundant Stop/Screenshot buttons.

---

## Phase 3: Visual/Styling Parity (P2)

### 9. OSD/popover style mismatch
- Status: `Complete`
- Python uses dark translucent popovers with borders/shadows tied to `.osd`.
- Avalonia styles are close but not exact across all states.
- Files:
  - Python: `code_for_reference/src/style.css`
  - Avalonia: `src/App/UI/Resources/App.axaml`, `Colors.axaml`
- Task:
  - Tune popover bg/border/shadow/opacity values to CSS parity.
  - Progress update (2026-05-27): Applied `BoxShadow` to `FlyoutPresenter` and `MenuFlyoutPresenter`. Added `DropShadowEffect` (`drop-shadow(0 1 6 #99000000)`) to `TextBlock` and `Path` elements within the OSD to exactly match GTK's `-gtk-icon-shadow` and `text-shadow`.

### 10. Start page style parity partial
- Status: `Complete`
- Python start page has gradient + suggested-action/pill semantics.
- Avalonia StartPage exists but needs exact style/state parity.
- Files:
  - Python: `style.css`, `window.blp`
  - Avalonia: `src/App/UI/Components/StartPage.axaml`, `UI/Resources/*.axaml`
- Task:
  - Match button hover/active states and gradient layering exactly.
  - Progress update (2026-05-27): Matched GTK `Adw.StatusPage` layout exactly. Created `start-page-button` and `start-page-suggested-action` classes in `App.axaml` to perfectly match hover states (`rgba(255,255,255,0.15)`), active scale transform (`scale(0.98)`), and gradient behaviors.

### 11. Time/typography details
- Status: `Complete`
- Python uses specific numeric/font treatment including elapsed margin and separator styling.
- Avalonia has partial parity; still needs exact width/spacing behavior.
- Files:
  - Python: `style.css`, `window.blp`
  - Avalonia: `MainWindow.axaml`, `Typography.axaml`
- Task:
  - Match dynamic time width-chars behavior and separator spacing/opacity.
  - Progress update (2026-05-27): Reordered `SeekArea` layout so time labels appear on the right side of the slider, matching GTK's `halign: end`. Applied correct negative margins (`margin: 0 -7px`), separator styling (opacity 0.4, 2px width, rounded corners), and right-alignment to maintain `width-chars` fixed bounding.

---

## Phase 4: Integration & Command Parity (P3)

### 12. Action/accelerator parity
- Status: `Complete`
- Python has action model (`Gio.SimpleAction`) and broad accelerator coverage.
- Avalonia has subset key handling; several action-level routes are absent.
- Files:
  - Python: `window.py` (`_setup_actions`, `_on_key_pressed`)
  - Avalonia: `MainWindow.axaml.cs`, `MainViewModel.cs`
- Task:
  - Add command registry abstraction and map full shortcut/action surface.
  - Progress update (2026-05-27): Implemented the full GTK keybinding surface in `MainWindow.axaml.cs` `OnKeyDown` event. Bound all GTK shortcuts including Playback, Fullscreen, Volume/Audio (delay adjustments), Navigation (frame-stepping, seeking), Subtitles (delays, position, visibility), Video/Display adjustments (contrast, brightness, saturation, zoom, speed), and Miscellaneous (screenshots, stats overlay). Extended `IMediaPlayer` with `Command(string, params)` to support raw player routing.

### 13. Observer/event parity surface
- Status: `Complete`
- Python watches many mpv properties (`idle-active`, `track-list`, `playlist-pos`, etc.) and updates UI states accordingly.
- Avalonia updates fewer states and relies more on manual refresh.
- Files:
  - Python: `window.py` (`_setup_observers`)
  - Avalonia: `MainViewModel.cs`, player events integration
- Task:
  - Extend event observers and UI state synchronization.
  - Progress update (2026-05-27): Added `PlaybackStateChanged` and `MediaEnded` to `IMediaPlayer` and `MediaFoundationPlayer`. Integrated `idle-active` parity into `MainWindow.axaml.cs`: when media ends or no file is loaded, the player returns to the idle state, showing the `StartPage`, hiding controls, and resetting the Title. Also implemented brief pause-indicator overlays.

---

## Implementation Order (Recommended)
1. Phase 1 (P0) items 1-4
2. Phase 2 (P1) items 5-8
3. Phase 3 (P2) items 9-11
4. Phase 4 (P3) items 12-13

## Completion Snapshot (2026-05-27)

### Completed in this cycle
- Added start-page center actions (`Open...`, `Open Folder`) and wiring.
- Added drop indicator overlay with contextual text/icon, smooth fade-in/out animations, and GTK visual parity.
- Added seek hover chapter preview with accurate centering, boundary clamping, and BoxShadow styling.
- Added throttled wheel-seek and click-to-seek.
- Added overlay auto-hide guard for active flyouts/popovers.
- Added playlist/loop/shuffle state bindings and basic logic.
- Added subtitle/audio icon on-off switching.
- Added playlist/video visibility rules.
- Fixed subtitle drag & drop handling (loads `.srt` etc. instead of ignoring them).
- Re-ordered transport row exactly to GTK baseline (removed Stop/Screenshot).
- Implemented typed `TrackMenuItem` logic (Add Track, None, and active styling).
- Implemented Options Menu (`OptionsMenuButton.axaml`) with sliders/resets for all player properties.
- Implemented Playlist Dialog (`PlaylistDialog.axaml`) with drag/drop and playing indicators.
- Achieved Fullscreen UX chrome parity (hiding title/menus, showing isolated close button).
- Implemented exact OSD shadow styles matching GTK (`BoxShadow` on popovers, `drop-shadow` on text/paths).
- Completed Start Page visual parity (removed pseudo-drop-target box, matched `Adw.StatusPage` icon/typography layout, matched button hover opacities and `scale(0.98)` click transform).
- Fixed `SeekArea` layout to match GTK: time labels and separator moved to the right side of the slider with negative `-7px` margins and proper alignment.
- Implemented full GTK action/accelerator parity (keyboard shortcuts) in `MainWindow.axaml.cs` using extended `IMediaPlayer.Command` routing.
- Integrated `idle-active` observer parity (media ending/clearing returns to Start Page, hides controls, restores window title) and added playback state pause indicators.

### Not completed yet (must continue)
- All phases (1-4) are now complete according to the mismatch tracker baseline.

## Done Definition Per Item
- UI element exists and is visible/hidden under same conditions as Python.
- Tooltip/icon/menu state matches Python behavior.
- Keyboard/mouse interactions match baseline behavior.
- Any new binding has unit-level or interaction test coverage where feasible.
