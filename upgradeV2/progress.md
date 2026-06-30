# Upgrade V2 Progress Tracker

Last updated: 2026-06-29

## Current Phase
- Phase 0: Premium design principles and quality contract - **completed**
- Phase 1: UI forensics and visual debt audit - **completed**
- Phase 2: Playback smoothness and rendering stability - **completed** (mpv OpenGL path only)
- Phase 3: Focus and input architecture rebuild - **completed**
- Phase 4: Shell and windowing model redesign - **completed**
- Phase 5: Motion, micro-interactions, and delight layer - **completed**
- Phase 6: Menu and control consolidation - **completed**
- Phase 7: Audio-visual craft pass - **completed**
- Phase 8: Accessibility and inclusive premium - **completed**
- Phase 9: Reliability, recovery, and trust layer - **completed**
- Phase 10: Performance budget and instrumentation - **completed**
- Phase 11: Signature innovations - **completed**
- Phase 12: Premium release hardening - **pending**

## Completed Work
- Renamed the shared artifact folder from `artifacts/` to `upgradeV2/`.
- Updated `upgradeV2/README.md` and internal references.
- Created the premium product transformation masterplan in `upgradeV2/md/action-plan.md`.
- Renamed the UI canvas artifact to `upgradeV2/canvas/action-plan.canvas`.
- Added progress tracking guidance for immediate next-step visibility.

### Phase 2 Deliverables — Playback Smoothness & Rendering Stability (mpv OpenGL only)
- **`src/App/Application/Services/PerformanceMonitor.cs`** — Frame pacing and drop detection. Tracks rendered fps, logs warnings when < 50fps via `CrashReporter.LogError()`.
- **`src/App/Application/Services/RenderThrottleService.cs`** — Throttles render submissions to ~60fps cap using Stopwatch-based timing. Prevents frame-ready flood from overloading the render pipeline.
- **`src/Media/Implementations/mpv/MpvConfig.cs`** — Added `GetPremiumTuningOptions()` with low-latency audio buffer (100ms), early OpenGL flush, cache disabled, display-resample sync, and hwdec=auto.
- **`src/Media/Implementations/mpv/MpvPlayer.cs`** — `InitializeRenderApi()` now applies premium tuning after base options.
- **`src/App/Controls/MpvVideoView.cs`** — Integrated `RenderThrottleService.ShouldRender()` in render loop; calls `PerformanceMonitor.OnFrameRendered()` after display; STATS logging includes throttle/perf summaries.
- **`src/App/UI/Shell/MainWindow.Startup.cs`** — Wires performance services via `MpvVideoView.SetPerformanceServices()`.
- **`upgradeV2/md/perf-instrumentation.md`** — Documentation of the instrumentation architecture.

### Phase 4 Deliverables — Shell and Windowing Model Redesign (Flyout Standardization)

- **`src/App/UI/Builders/TrackFlyoutBuilder.cs`** — Standardized flyout container to match equalizer:
  - Container padding: `Thickness(4)` → `Thickness(14, 12)` (equalizer standard)
  - Added header with title + close button (equalizer pattern)
  - Separator: `new Separator()` → styled `Border Height=1` with `Opacity=0.5`
  - Delay label: removed raw `Opacity=0.5`/`LetterSpacing=0.8`, uses `Classes="md3-caption"`
  - Removed redundant `+` icon from "Add Subtitles…"/"Add Audio Track…" pseudo-entries
  - Root StackPanel now uses `Spacing=10` for consistent spacing
  - Added optional `title` parameter for flyout header

- **`src/App/Application/Helpers/TrackDisplayHelper.cs`** (new) — Shared language name resolution and track formatting:
  - `GetLanguageName()`: Converts ISO 639 codes to user-friendly names (500+ languages)
  - `FormatTrack()`: Consistent format — subtitle tracks show `"English (External, Forced)"`, audio/video show `"English (Audio)"`
  - Replaces 4 duplicated `FormatTrack` implementations across managers

- **Managers format track consolidation:**
  - `AudioManager.FormatTrack`: `"Audio: eng (on)"` → `"English (Audio)"`
  - `VideoManager.FormatTrack`: `"Video: eng (on)"` → `"English (Video)"`
  - `SubtitleManager.FormatTrack`: Now uses shared helper (language name + tags)
  - `MainViewModel.Actions.FormatTrack`: Now uses shared helper
  - Removed 500+ lines of duplicated `LanguageNames` dictionary from SubtitleManager

### Phase 7 Deliverables — Audio-Visual Craft Pass
- **`src/App/Application/Managers/SubtitleSettingsStore.cs`** — Premium subtitle defaults: FontScale 1.1x (was 1.0), BorderSize 2.5 (was 2.0), ShadowOffset 1.5 (was 1.0), Font "Segoe UI" (was "Arial"). Version 2→3 migration.
- **`src/App/Application/Services/ISubtitleManager.cs`** — Added `TrackChangedMessage` callback property for OSD feedback.
- **`src/App/Application/Managers/SubtitleManager.cs`** — Fires `TrackChangedMessage` on track selection. Added `TrackChangedMessage` property.
- **`src/App/Application/Managers/AudioManager.cs`** — Added `TrackChangedMessage` property, fires on audio track selection with display name.
- **`src/App/UI/Shell/MainWindow.Initialization.cs`** — Wires both manager callbacks to `ShowOsdNotification()` with ClosedCaption/Music icons.
- **`src/App/Application/Services/ScreenshotService.cs`** — Filenames now include media name (`Cine_MyMovie_2026-06-29_120000_1.png`). Added `ScreenshotSaved` callback, configurable `Format`, `MediaName` property, sanitized filenames.

### Phase 9 Deliverables — Reliability, Recovery, and Trust Layer
- **`src/App/Application/ViewModels/MainViewModel.Actions.cs`** — Hardened `OpenFile`: file existence check before opening, structured error logging with `Log.ForContext`.
- **`src/App/Application/Services/SessionManager.cs`** — Session backup before overwrite (`session.json.bak`). Load fallback: corrupted/missing session auto-restores from `.bak`. Specific `JsonException` handling distinguishes corruption from other failures.
- **`src/App/App.axaml.cs`** — Startup catch block now writes `CrashReporter.Dump()` and cleanly shuts down instead of rethrowing an unhandled crash.

### Phase 11 Deliverables — Signature Innovations
- **`src/App/UI/Screens/Dialogs/CommandPaletteDialog.axaml`** + **`.axaml.cs`** — Searchable command palette (Ctrl+K). 35+ commands across Playback, Navigation, View, Speed, Audio, Subtitles, Screenshots, Dialogs, Zoom, Loop. Real-time filtering with result count. Click or Enter to execute.
- **`src/App/UI/Controls/Indicators/NowPlayingInfoControl.axaml`** + **`.axaml.cs`** — Media metadata overlay (Ctrl+D). Shows file name, resolution, frame rate, video codec, audio (language + codec + channels), duration. Bottom-right position over video.
- **`src/App/UI/Views/MainWindow.axaml`** — Added `FocusModeIndicator` (thin accent line) and `controls:NowPlayingInfoControl` element.
- **`src/App/UI/Shell/MainWindow.Core.cs`** — Added `_isFocusMode` and `_paletteCommands` fields.
- **`src/App/UI/Shell/MainWindow.Input.cs`** — 3 new shortcuts (Ctrl+K palette, Ctrl+D now playing, Ctrl+. focus mode). `PopulatePaletteCommands()` with 35 entries. `ToggleFocusMode()` hides/shows all chrome. `ToggleNowPlayingInfo()` refreshes player metadata. `PaletteCommandEntry` uses `TrackDisplayHelper` for language names.

## Next Actions
1. Phase 12: Premium Release Hardening — cross-hardware matrix testing, long-session soak, UX freeze, release candidate triage.
2. Keep documentation up to date.

## Notes for contributors and models
- Read `upgradeV2/README.md` first, then `upgradeV2/progress.md`.
- Use `upgradeV2/md/action-plan.md` for the full plan and phase definitions.
- Update this file after each phase or major milestone.
- Add short progress notes inside the relevant phase section of `upgradeV2/md/action-plan.md` when work changes scope or status.
