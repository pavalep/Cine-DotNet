# Cine Codebase — Comprehensive Remaining Work Analysis

> **Codebase completion estimate: ~20%**  
> **Date**: 2026-07-01  
> **Scope**: Full application audit — code quality, architecture, testing, documentation

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Completed Fixes (Phase 1–3)](#2-completed-fixes)
3. [Remaining Bugs & Code Quality Issues](#3-remaining-bugs)
4. [Architecture & Design Debt](#4-architecture-debt)
5. [Testing & QA Gaps](#5-testing-gaps)
6. [Documentation Gaps](#6-documentation-gaps)
7. [Build, CI & DevOps](#7-build-ci)
8. [Performance & Optimization](#8-performance)
9. [Accessibility & Localization](#9-accessibility)
10. [Security Considerations](#10-security)
11. [Priority Matrix & Phase Plan](#11-priority-matrix)

---

## 1. Executive Summary

The Cine media player has reached functional maturity for core playback, but significant work remains across code quality, testing, documentation, and polish. This document catalogs every known gap, organized by severity and phase.

### Codebase Facts
- **~200+ source files** across App, Media, and Core layers
- **Primary language**: C# 12 / .NET 8 / Avalonia UI
- **Media engines**: mpv (OpenGL), MediaFoundation (D3D11)
- **UI pattern**: MVVM with code-behind, partial classes

### Completion Breakdown

| Area | Completion | Notes |
|------|-----------|-------|
| Core playback pipeline | 30% | mpv path functional; MF path needs work |
| UI shell & chrome | 50% | Main chrome done; settings/prefs partial |
| Flyout/menu system | 70% | Fixed overlay system; some menus still stub |
| Settings & preferences | 20% | Basic storage; no UI for most settings |
| Audio DSP & effects | 25% | Basic equalizer; no convolver/visualizer |
| Subtitle engine | 30% | Core rendering done; styling/search limited |
| Testing & CI | 5% | No automated test suite |
| Documentation | 25% | Architecture docs exist; API docs missing |
| Build & packaging | 15% | MSIX partially configured |
| Accessibility | 5% | Keyboard nav added; screen reader support absent |
| Performance optimization | 20% | GPU-rendered path working; CPU usage unoptimized |

---

## 2. Completed Fixes (Phase 1–3)

All items marked ✅ have been applied and committed.

| # | Fix | Tier | Status |
|---|-----|------|--------|
| 1 | FlyoutOverlay ZIndex `10→50` | T1 Critical | ✅ |
| 2 | Volume close delegate fix | T1 Critical | ✅ |
| 3 | Double border removal | T2 High | ✅ |
| 4a | BtnOpenMenu Flyout (XAML) | T2 High | ✅ |
| 4b | BtnOpenMenu wiring (.cs) | T2 High | ✅ |
| 5 | PauseLog removal | T3 Medium | ✅ |
| 6 | Console.WriteLine removal | T3 Medium | ✅ |
| 7 | Keyboard navigation | T4 Low | ✅ |
| 8 | Shuffle repeat bug | T4 Low | ✅ |
| 9 | Silent catch{} in Startup | Extra | ✅ |
| 10 | Silent catch{} in PlaylistDialog | Extra | ✅ |
| 11 | Silent catch{} in RuntimeDownloader | Extra | ✅ |
| 12 | Duplicate event wiring removal | Extra | ✅ |

---

## 3. Remaining Bugs & Code Quality Issues

### 3.1 UI Bugs

| # | Severity | File | Issue |
|---|----------|------|-------|
| B1 | 🔴 Critical | `SeekBarControl.axaml.cs` | Seek bar thumb position jumps on window resize due to unclamped pixel conversion |
| B2 | 🔴 Critical | `FullscreenHeaderControl.axaml.cs` | Fullscreen toggle does not update `HeaderBarControl.Visibility` — header remains visible in fullscreen |
| B3 | 🟠 High | `ControlsBoxControl.axaml` | `BtnVolumeMenu` click handler name is inconsistent between XAML and code-behind declaration |
| B4 | 🟠 High | `PlaylistDialog.axaml.cs` | Playlist items don't refresh after drag-and-drop reorder — requires restart of playback |
| B5 | 🟠 High | `MainWindow.Input.cs` | Global shortcuts not deactivated when modal dialog open — can trigger playback while dialog focused |
| B6 | 🟡 Medium | `SubtitleOverlayControl.axaml.cs` | Subtitle position slider tooltip shows raw float value instead of formatted percentage |
| B7 | 🟡 Medium | `AudioTrackSelectorControl.axaml` | Track list not scrollable when more than 20 tracks — overflows window bounds |
| B8 | 🟡 Medium | `PipWindow.axaml` | PiP window does not respect system DPI scaling on multi-monitor setups |

### 3.2 Engine Bugs

| # | Severity | File | Issue |
|---|----------|------|-------|
| B9 | 🟠 High | `MpvPlayer.cs` | `Seek()` called during `LoadFile()` race condition — can cause mpv crash on rapid file switching |
| B10 | 🟠 High | `AudioManager.cs` | Channel layout mismatch between mpv output and system audio causes silent playback on some configurations |
| B11 | 🟡 Medium | `VideoManager.cs` | `SetAspectRatio()` does not persist across sessions — always resets to "Default" |
| B12 | 🟡 Medium | `MediaFoundationPlayer.cs` | `MFMediaType` handling broken for HEVC on Windows 10 — falls back to software decode silently |
| B13 | 🟡 Medium | `MpvConfig.cs` | Hardware decoder selection (`hwdec`) not adaptive — always uses `auto` regardless of GPU availability |

### 3.3 Code Quality

| # | Severity | Location | Issue |
|---|----------|----------|-------|
| B14 | 🟡 Medium | `SubtitleManager.cs` | 48 KB file — violates SRP; handles parsing, selection, styling, and search in a single class |
| B15 | 🟡 Medium | `ControlsBoxControl.axaml.cs` | Still 26 KB — should split XAML code-behind into separate command/state handlers |
| B16 | 🟡 Medium | `PlaylistCoordinator.cs` | No thread safety on `_items` list — can cause `InvalidOperationException` during concurrent access |
| B17 | 🟡 Medium | `MpvVideoView.cs` | Frame statistics logging uses string concatenation instead of structured logging |
| B18 | 🟢 Low | Multiple | Hardcoded pixel values (87 instances across 30+ axaml files) — should use design tokens |
| B19 | 🟢 Low | `App.axaml.cs` | `#if DEBUG` blocks lack `#else` clarity — some debug paths compiled into release |

---

## 4. Architecture & Design Debt

### 4.1 Structural Debt

| ID | Area | Description | Effort | Risk |
|----|------|-------------|--------|------|
| D1 | **SubtitleManager decomposition** | Split 48 KB monolith into `EmbeddedSubtitleService`, `ExternalSubtitleService`, `SubtitleStyleService`, `SubtitleSearchService` | 2–3 weeks | Medium — public API change |
| D2 | **Design token system** | Create centralized `DesignTokens.axaml` with color palette, spacing scales, typography anchors, border radii, elevation tokens | 1 week | Low |
| D3 | **IView abstraction for MVVM** | Current code-behind pattern tightly couples UI to logic; introduce `IView<TViewModel>` with bindings for testability | 3–4 weeks | High — architectural shift |
| D4 | **Command routing system** | Replace scattered `Click` handlers with centralized `ICommandService` supporting undo/redo, shortcut binding, and context-sensitive availability | 2 weeks | Medium |
| D5 | **Settings persistence refactor** | Move from `SubtitleSettingsStore` + per-file JSON to unified `SettingsRegistry` with versioned migrations | 1 week | Low |

### 4.2 Missing Infrastructure

| ID | Area | Description |
|----|------|-------------|
| D6 | **Unit test project** | No automated tests exist — need NUnit/xUnit project with coverage targets |
| D7 | **UI automation** | No accessibility test harness — need Axe-based or FlaUI test pipeline |
| D8 | **CI/CD pipeline** | Build-only CI exists; need full pipeline: build → unit tests → UI smoke tests → package |
| D9 | **Error reporting service** | `CrashReporter` exists but has no remote collection endpoint or dashboard |

---

## 5. Testing & QA Gaps

| ID | Area | Description | Priority |
|----|------|-------------|----------|
| T1 | **Unit tests** | Zero existing unit tests — need at minimum: PlaylistCoordinator, AudioManager, SubtitleSettingsStore | 🔴 Critical |
| T2 | **Integration tests** | Playback pipeline needs end-to-end test scenarios: open → play → seek → switch → close | 🔴 Critical |
| T3 | **UI smoke tests** | Core user flows: open file, toggle fullscreen, change audio/subtitle track, resize window | 🟠 High |
| T4 | **Performance benchmarks** | FPS stability, memory leak detection, startup time measurement | 🟡 Medium |
| T5 | **Cross-monitor DPI** | Test on multi-monitor setups with mixed DPI scaling | 🟡 Medium |
| T6 | **Accessibility audit** | Screen reader (NVDA/JAWS) testing, keyboard-only navigation completeness | 🟡 Medium |

---

## 6. Documentation Gaps

### 6.1 Code Documentation
| ID | Area | Status |
|----|------|--------|
| D1 | **Public API XML docs** | Most public methods/interfaces lack `<summary>` docs |
| D2 | **Architecture decision records (ADRs)** | No formal ADRs — decisions scattered across commit messages and chat |
| D3 | **Onboarding guide** | No new-developer setup document |

### 6.2 User Documentation
| ID | Area | Status |
|----|------|--------|
| D4 | **Keyboard shortcuts reference** | Not documented outside source code |
| D5 | **User guide** | No help/documentation for end users |
| D6 | **Troubleshooting** | Common issues FAQ needed |

---

## 7. Build, CI & DevOps

| ID | Area | Description |
|----|------|-------------|
| D7 | **MSIX signing** | Package needs code-signing configuration for distribution |
| D8 | **Dependency pinning** | NuGet packages should use locked versions (Directory.Build.props) |
| D9 | **Static analysis** | Add StyleCop or Roslyn analyzers to CI pipeline |
| D10 | **Coverage gates** | Set minimum test coverage threshold (target: 70% on core modules) |
| D11 | **Localization infra** | Internationalization scaffolding needed — `resx` or `locxl` for multi-language support |

---

## 8. Performance & Optimization

| ID | Area | Description | Priority |
|----|------|-------------|----------|
| P1 | **GPU utilization monitoring** | No GPU usage tracking — could overheat mobile GPUs | Medium |
| P2 | **Memory management** | Large files not explicitly disposed, relying on GC — could OOM with very large media | Medium |
| P3 | **Startup time** | App launches in ~2s on SSD; target <1s with lazy subsystem initialization | Low |
| P4 | **Playlist pre-loading** | Next file in playlist should pre-buffer while current file plays | Low |
| P5 | **Audio buffer tuning** | Audio latency ~200ms — can reduce with smaller buffer size in the low-latency tuning mode | Low |

---

## 9. Accessibility & Localization

| ID | Area | Description |
|----|------|-------------|
| A1 | **Screen reader support** | All UI controls need proper `AutomationProperties` names and roles |
| A2 | **High contrast mode** | Application does not respond to Windows High Contrast settings |
| A3 | **Color blindness support** | Color-coded elements (waveform, track types) need pattern differentiation |
| A4 | **Keyboard shortcut documentation** | Need in-app help for keyboard shortcuts |
| A5 | **Localization framework** | Prepare all user-facing strings for translation |
| A6 | **Right-to-left support** | No RTL layout handling for Arabic/Hebrew languages |

---

## 10. Security Considerations

| ID | Area | Description |
|----|------|-------------|
| S1 | **Path traversal** | Media file path input is not sanitized — potential `../../../etc/passwd` attack on file operations |
| S2 | **DLL loading** | Native DLL loading (`mpv-1.dll`) from application directory without signature verification |
| S3 | **External subtitle parsing** | Subtitle parsers may have vulnerabilities with malformed subtitle files |
| S4 | **Crash dump privacy** | Crash dumps may contain file paths, system info — need user consent dialog |
| S5 | **HTTP requests** | Update check mechanism uses HTTP — should be HTTPS |

---

## 11. Priority Matrix & Phase Plan

### Phase 4 — Foundations (Weeks 1–3)
**Goal**: Build the infrastructure needed for reliable development

| Item | ID | Effort |
|------|----|--------|
| Unit test project + core tests | T1, T2 | 1 week |
| Design token system | D2 | 1 week |
| CI pipeline with static analysis | D8, D9 | 1 week |
| SubtitleManager decomposition | D1 | Ongoing |

### Phase 5 — Hardening (Weeks 4–8)
**Goal**: Fix high-severity bugs and reach production quality

| Item | ID | Effort |
|------|----|--------|
| SeekBar resize bug | B1 | 1 day |
| Fullscreen header visibility | B2 | 1 day |
| Seek bar overflow (many tracks) | B7 | 2 days |
| Race condition on file switch | B9 | 2 days |
| Performance profiling and burst fixes | P1–P5 | 1 week |
| Security hardening | S1–S5 | 1 week |

### Phase 6 — Polish & Innovation (Weeks 9–14)
**Goal**: Ship-worthy product with polished UX

| Item | ID | Effort |
|------|----|--------|
| Accessibility audit + fixes | A1–A6 | 2 weeks |
| Localization framework | A5, S5 | 1 week |
| User documentation | D4, D5, D6 | 1 week |
| Error reporting service | D9 | 1 week |
| **Innovation features** (See [innovate.md](../../Phase3/innovate.md) Part 2) | F1–F19 | 1–2 weeks |

### Appendix A: Hidden Functionality Gaps (19 items)

These were discovered by auditing every flyout, panel, builder, and control in the App layer.

| # | File | Missing Feature | Impact |
|---|------|----------------|--------|
| FA1 | `AudioTrackSelectorControl.*`, `SubtitleOverlayControl.*` | No "Now Playing" indicator — active track not highlighted | User confusion: can't tell which track is active |
| FA2 | `AudioTrackSelectorControl.axaml.cs` | No `SetFontSize()` method | Broken accessibility/font-scaling for track buttons |
| FA3 | `AudioTrackSelectorControl.axaml.cs` | No delay reset button | User must manually drag delay slider back to 0 |
| FA4 | `AudioEqualizerFlyout.axaml` | Preset buttons hardcoded in XAML | New presets added to manager won't appear in UI |
| FA5 | `SubtitleOverlayControl.axaml.cs` | `SubtitleSettingsDialog` referenced but file path not found | Subtitle gear button may fail or open nothing |
| FA6 | `SeekBarControl.axaml.cs` | Chapter markers lack tooltips | No way to preview chapter names without scrubbing |
| FA7 | `MainWindow.Input.cs` | `GoToTimeDialog` exists but has no keyboard shortcut | No keyboard path to jump to specific timestamp |
| FA8 | `PlaylistDialog.*` | No search/filter for long playlists | Manual scrolling through 500+ items |
| FA9 | `FullscreenHeaderControl.*` | No track selectors in fullscreen | Must exit fullscreen to change audio/subtitle tracks |
| FA10 | `HeaderBarControl.axaml.cs` | Recent files section empty in Open menu | Always shows just File/Open + empty separator |
| FA11 | `AudioTrackSelectorControl.*` | No loading indicator when switching tracks | No feedback during slow track load |
| FA12 | `TrackFlyoutBuilder.cs` | All tracks look identical regardless of codec | AC3 vs DTS-HD indistinguishable |
| FA13 | `AudioTrackSelectorControl.axaml` | No drag-over visual feedback | User can't see button is active drop target |
| FA14 | `OsdNotificationControl.axaml.cs` | `NotificationClicked` event never subscribed to | Clicking OSD does nothing |
| FA15 | `ControlsBoxControl.axaml.cs` | No crossfade status indicator | User can't tell if crossfade is active |
| FA16 | `FirstLaunchDialog.*`, `App.axaml.cs` | Integration unclear — may be dead/half-coded | First-run experience possibly broken |
| FA17 | `SeekBarControl.axaml.cs` | Chapter preview popover can overflow window bounds | Popover renders off-screen in some cases |
| FA18 | `AudioEqualizerFlyout.axaml` | Equalizer sliders lack numeric input | Can't type exact dB value like "+2.5" |
| FA19 | `TrackFlyoutBuilder.cs` | No visual distinction for default tracks | User can't tell which track came from container metadata |

---

*Document version: 3.0 — Updated 2026-07-01*
*Appendix A: 19 hidden functionality gaps — merged from innovte.md appendix*
*[UI Refinement doc: see innovate.md for complete premium polish plan]*

---

## Phase Plan (Updated)

| Phase | Scope | Duration | Priority |
|-------|-------|----------|----------|
| **Phase 4** — Spacing & Transparency | P1 (tokens), P2 (transparency), P5 (dividers) | 2–3 days | High — foundation for everything else |
| **Phase 5** — Functional Completeness | F1–F19 (19 functional fixes) + bug fixes | 1–2 weeks | High — makes every feature actually work |
| **Phase 6** — Polish & Responsiveness | P3 (typography), P4 (button states), F15 (animations), F16 (focus), I1, I2 | 1 week | Medium — quality feel, responsive breakpoints |
| **Phase 7** — Testing & Release | Unit tests, CI pipeline, accessibility audit | 1–2 weeks | Required before any public release |

### Presentation Layer (UI)

| Category | Files | Status |
|----------|-------|--------|
| Shell controls | `HeaderBarControl.*`, `ControlsBoxControl.*`, `FullscreenHeaderControl.*` | Partially fixed |
| Windows/Dialogs | `MainWindow.*`, `PlaylistDialog.*`, `PreferencesDialog.*`, `FirstLaunchDialog.*`, `GoToTimeDialog.*`, `KeyboardShortcutsDialog.*`, `PipWindow.*`, `CommandPaletteDialog.*`, `MediaInfoDialog.*` | Some stubs |
| Custom controls | `FlyoutOverlayControl.*`, `SeekBarControl.*`, `AudioEqualizerFlyout.*`, `AudioTrackSelectorControl.*`, `SubtitleOverlayControl.*`, `NowPlayingInfoControl.*` | Partial |
| Converters | `TimeSpanToStringConverter.*`, `BoolToVisibilityConverter.*`, etc. | Existing |
| Builders | `TrackFlyoutBuilder.*`, `PrimaryMenuBuilder.*`, `VideoContextMenuBuilder.*` | Partially fixed |
| Start page | `StartPage.*` | Stub |

### Application Layer (Logic)

| Category | Files | Status |
|----------|-------|--------|
| Managers | `AudioManager.*`, `VideoManager.*`, `SubtitleManager.*`, `PlaybackStateManager.*` | Needs decomposition |
| Services | `FlyoutManager.*`, `InputRoutingService.*`, `PlaylistCoordinator.*`, `RuntimeDownloader.*`, `SessionManager.*`, `ResumeService.*`, `ScreenshotService.*`, `ThemeService.*`, `PipService.*`, `PipWindowManager.*`, `RendererCoordinator.*`, `RenderThrottleService.*`, `MediaFileService.*`, `FileDialogService.*`, `FileAssociationService.*` | Partially implemented |
| Models | `Result.*`, `TrackDisplayHelper.*`, `PlaylistItem.*`, etc. | Existing |
| Helpers | `TrackGroupCollectionExtensions.*`, etc. | Existing |

### Media Layer (Playback)

| Category | Files | Status |
|----------|-------|--------|
| mpv engine | `MpvPlayer.*`, `MpvConfig.*`, `MpvVideoView.*`, `MarshalHelper.*`, `AngleGlContext.*` | Functional |
| MF engine | `MediaFoundationPlayer.*`, `MfComInterop.*`, `MfHelper.*`, `AudioRenderer.*` | Partial |
| Models | `AudioTrackInfo.*`, `VideoTrackInfo.*`, `SubtitleSource.*`, `ChapterInfo.*`, `MediaFile.*` | Existing |

### Core Layer (Infrastructure)

| Category | Files | Status |
|----------|-------|--------|
| Logging | `FileLogger.*`, `ILogger.*` | Functional |
| Config | `ConfigService.*`, `IConfigService.*` | Existing |
| Services | `CrashReporter.*`, `ScreenRecorder.*` | Existing |

---

*Document version: 1.0 — Generated by code audit*
*Next review: After Phase 4 completion*