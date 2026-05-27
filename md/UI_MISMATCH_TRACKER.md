# UI Mismatch Tracker (Python GTK4 vs Avalonia)

Audit date: 2026-05-27  
Purpose: Single handoff file for any model/engineer to continue UI parity work.

## Quick Progress Board
- [ ] P0 fully complete
- [ ] P1 fully complete
- [ ] P2 fully complete
- [ ] P3 fully complete
- [x] Start page center actions implemented
- [x] Drop indicator overlay implemented
- [x] Seek hover + wheel + click interactions implemented
- [x] Loop/shuffle bindings implemented
- [x] Subtitle/audio icon on-off switching implemented

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
- Status: `Partial`
- Python has dedicated `$OptionsMenuButton` with rich options behavior.
- Avalonia shows a placeholder/non-equivalent control set.
- Files:
  - Python: `code_for_reference/src/window.blp` (`$OptionsMenuButton options_menu_button`)
  - Avalonia: `src/App/UI/Views/MainWindow.axaml`
- Task:
  - Implement an `OptionsMenuButton` component in `src/App/UI/Components`.
  - Wire actions and bindings in `MainViewModel`.
  - Progress update (2026-05-27): Added `BtnOptionsMenu` with core actions (speed +/-/reset, screenshot) in `MainWindow.axaml`. Still not feature-complete relative to Python `options.py`.

### 2. Playlist controls behavior incomplete
- Status: `Partial`
- Python has full playlist behavior: shuffle/unshuffle, loop playlist, navigation sensitivity, playlist dialog sync.
- Avalonia has loop toggles but logic is TODO (`ToggleLoopFile`, `ToggleLoopPlaylist`) and no playlist dialog parity.
- Files:
  - Python: `code_for_reference/src/window.py` (`_on_shuffle_toggled`, `_on_loop_playlist_toggled`, `_update_playlist_nav_sensitivity`)
  - Avalonia: `src/App/Application/ViewModels/MainViewModel.cs`
- Task:
  - Implement full playlist state model + commands.
  - Add playlist dialog and visibility/sensitivity rules.
  - Progress update (2026-05-27): Implemented loop + shuffle toggles in `MainViewModel` and bound states in UI. Playlist dialog remains placeholder notification.

### 3. Track menu dynamic population and state icons missing
- Status: `Mostly Done`
- Python dynamically builds subtitle/audio/video menus and toggles icon variants (`subtitles-off`, `audio-off`).
- Avalonia binds plain string collections only; no “None/Add track” first entries parity, no icon state transitions.
- Files:
  - Python: `code_for_reference/src/window.py` (`_update_track_menus`, `on_sub_vis_change`, `on_aid_change`)
  - Avalonia: `src/App/UI/Views/MainWindow.axaml`, `src/App/Application/ViewModels/MainViewModel.cs`
- Task:
  - Replace simple string lists with typed track menu models.
  - Implement active-selection, “None”, “Add Track” entries, and off/on icon switching.
  - Progress update (2026-05-27): Added dynamic refresh from `TrackListChanged` with `Add ...` and `None` top entries, plus subtitle/audio icon on/off switching and video-track visibility rule (`>1`). Remaining: typed model actions/select-target parity.

### 4. Seek/progress interaction parity missing
- Status: `Partial`
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
  - Progress update (2026-05-27): Added chapter preview popover on seek hover and throttled wheel-seek handlers in `MainWindow` seek area. Remaining: exact chapter mark label fidelity and click/gesture parity.

---

## Phase 2: Interaction & Behavior Parity (P1)

### 5. Overlay reveal behavior not fully equivalent
- Status: `Partial`
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
- Status: `Partial`
- Python has dedicated drop revealer with contextual icon/text (“Play” vs “Add Subtitle Track”).
- Avalonia only changes start-page border visuals.
- Files:
  - Python: `code_for_reference/src/window.blp`, `window.py` (`_on_drop_enter`, `_on_drop_leave`)
  - Avalonia: `src/App/UI/Views/MainWindow.axaml.cs`
- Task:
  - Add drop overlay component with reveal animation and contextual label/icon.
  - Progress update (2026-05-27): Added `DropIndicatorOverlay` with contextual text/icon (`Play` vs `Add Subtitle Track`) and drag enter/leave/drop wiring. Remaining: animation and exact GTK visual parity.

### 7. Fullscreen UX parity incomplete
- Status: `Partial`
- Python updates fullscreen icon/tooltip dynamically and adjusts decoration behavior.
- Avalonia toggles fullscreen but lacks equivalent decoration-layout behavior and detailed icon/tooltip sync.
- Files:
  - Python: `window.py` (`on_fs_change`, `_set_fs_state`)
  - Avalonia: `MainWindow.axaml.cs`
- Task:
  - Add full icon/tooltip state synchronization and window chrome behavior parity.
  - Progress update (2026-05-27): Fullscreen toggle exists; deep parity (icon/tooltip/chrome behavior matching Python) still pending.

### 8. Button set/order mismatch in controls row
- Status: `Partial`
- Python control row has specific ordering and includes playlist button + options button + dedicated visibility behaviors.
- Avalonia includes extra stop/screenshot placement differences and missing playlist button parity.
- Files:
  - Python: `window.blp`
  - Avalonia: `MainWindow.axaml`
- Task:
  - Reorder to Python baseline and restore missing controls/visibility rules.
  - Progress update (2026-05-27): Added missing shuffle/playlist/options controls and playlist/video visibility conditions. Exact button ordering and extra-control parity (rewind/stop/forward/screenshot placement vs Python baseline) still need final pass.

---

## Phase 3: Visual/Styling Parity (P2)

### 9. OSD/popover style mismatch
- Status: `Partial`
- Python uses dark translucent popovers with borders/shadows tied to `.osd`.
- Avalonia styles are close but not exact across all states.
- Files:
  - Python: `code_for_reference/src/style.css`
  - Avalonia: `src/App/UI/Resources/App.axaml`, `Colors.axaml`
- Task:
  - Tune popover bg/border/shadow/opacity values to CSS parity.
  - Progress update (2026-05-27): Popover colors moved to dark translucent + border values closer to Python CSS. Remaining: full shadow/text-shadow/icon-shadow parity.

### 10. Start page style parity partial
- Status: `Partial`
- Python start page has gradient + suggested-action/pill semantics.
- Avalonia StartPage exists but needs exact style/state parity.
- Files:
  - Python: `style.css`, `window.blp`
  - Avalonia: `src/App/UI/Components/StartPage.axaml`, `UI/Resources/*.axaml`
- Task:
  - Match button hover/active states and gradient layering exactly.
  - Progress update (2026-05-27): Start page now includes both center actions (`Open...`, `Open Folder`), gradient background key, and button tone adjustments toward GTK baseline. Remaining: exact hover/active animation/token parity.

### 11. Time/typography details
- Status: `Pending`
- Python uses specific numeric/font treatment including elapsed margin and separator styling.
- Avalonia has partial parity; still needs exact width/spacing behavior.
- Files:
  - Python: `style.css`, `window.blp`
  - Avalonia: `MainWindow.axaml`, `Typography.axaml`
- Task:
  - Match dynamic time width-chars behavior and separator spacing/opacity.

---

## Phase 4: Integration & Command Parity (P3)

### 12. Action/accelerator parity
- Status: `Pending`
- Python has action model (`Gio.SimpleAction`) and broad accelerator coverage.
- Avalonia has subset key handling; several action-level routes are absent.
- Files:
  - Python: `window.py` (`_setup_actions`, `_on_key_pressed`)
  - Avalonia: `MainWindow.axaml.cs`, `MainViewModel.cs`
- Task:
  - Add command registry abstraction and map full shortcut/action surface.

### 13. Observer/event parity surface
- Status: `Partial`
- Python watches many mpv properties (`idle-active`, `track-list`, `playlist-pos`, etc.) and updates UI states accordingly.
- Avalonia updates fewer states and relies more on manual refresh.
- Files:
  - Python: `window.py` (`_setup_observers`)
  - Avalonia: `MainViewModel.cs`, player events integration
- Task:
  - Extend event observers and UI state synchronization.
  - Progress update (2026-05-27): Added observers for track/playlist/loop updates in `MainViewModel`; broader mpv parity surface still pending.

---

## Implementation Order (Recommended)
1. Phase 1 (P0) items 1-4
2. Phase 2 (P1) items 5-8
3. Phase 3 (P2) items 9-11
4. Phase 4 (P3) items 12-13

## Completion Snapshot (2026-05-27)

### Completed in this cycle
- Added start-page center actions (`Open...`, `Open Folder`) and wiring.
- Added drop indicator overlay with contextual text/icon.
- Added seek hover chapter preview.
- Added throttled wheel-seek and click-to-seek.
- Added overlay auto-hide guard for active flyouts/popovers.
- Added playlist/loop/shuffle state bindings and basic logic.
- Added subtitle/audio icon on-off switching.
- Added playlist/video visibility rules.

### Not completed yet (must continue)
- Exact Python control ordering parity in transport row.
- Typed action-driven track menu model parity (not string-only list parity).
- Full options menu feature parity (`options.py` behavior).
- Exact visual parity for shadows/text-shadows/icon-shadows and hover/active motion.
- Full command/action accelerator parity surface from Python.
- Full observer parity surface from Python (`idle-active`, richer state sync).

### Next-session first tasks
1. Finish control row exact ordering and visibility parity.
2. Replace string track menu lists with typed selectable models/actions.
3. Complete options menu behavior parity.

## Done Definition Per Item
- UI element exists and is visible/hidden under same conditions as Python.
- Tooltip/icon/menu state matches Python behavior.
- Keyboard/mouse interactions match baseline behavior.
- Any new binding has unit-level or interaction test coverage where feasible.
