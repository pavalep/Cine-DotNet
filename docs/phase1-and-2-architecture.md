# Phase 1 + 2 — Architecture Refactoring & Error Handling

> **Audit date**: 2026-06-18
> **Build status**: ✅ 0 errors, 0 warnings
> **Project**: Cine media player (Avalonia UI + mpv)

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Phase 1A — Service Extraction](#2-phase-1a--service-extraction)
3. [Phase 1B — ViewModel Decomposition](#3-phase-1b--viewmodel-decomposition)
4. [Phase 1C — MainWindow Decomposition](#4-phase-1c--mainwindow-decomposition)
5. [Phase 1D — Interface Contracts](#5-phase-1d--interface-contracts)
6. [Phase 1E — Namespace Cleanup](#6-phase-1e--namespace-cleanup)
7. [Phase 2 — Error Handling Standardization](#7-phase-2--error-handling-standardization)
8. [Remaining Work & Risks](#8-remaining-work--risks)
9. [Architecture Diagram](#9-architecture-diagram)
10. [File Inventory](#10-file-inventory)

---

## 1. Architecture Overview

The application follows a layered architecture:

```
┌────────────────────────────────────────────────┐
│  MainWindow (4 partials)                       │
│  ├── Core.cs        —  init, DI, lifecycle     │
│  ├── Input.cs       —  keyboard/mouse/drag     │
│  ├── Pip.cs         —  PiP delegation          │
│  └── WindowControls —  auto-hide, fullscreen   │
├────────────────────────────────────────────────┤
│  Controls                                       │
│  ├── MpvVideoView   —  ANGLE/OpenGL renderer   │
│  ├── SeekBarControl —  seek/progress           │
│  └── ...                                        │
├────────────────────────────────────────────────┤
│  ViewModels (6 partials)                        │
│  ├── MainViewModel.cs       —  core/properties  │
│  ├── Actions.cs             —  file ops/handlers│
│  ├── Playback.cs            —  playback cmd     │
│  ├── Playlist.cs            —  playlist/session │
│  ├── Renderer.cs            —  renderer mode    │
│  └── Tracks.cs              —  audio/subtitles  │
├────────────────────────────────────────────────┤
│  Managers (7 files)                             │
│  ├── AudioManager           —  audio state     │
│  ├── SubtitleManager        —  subtitle state  │
│  ├── PlaybackStateManager   —  state machine   │
│  ├── VideoManager           —  video tracks    │
│  ├── AudioSettingsStore     —  persistence     │
│  ├── SubtitleSettingsStore  —  persistence     │
│  └── PlaylistSettingsStore  —  persistence     │
├────────────────────────────────────────────────┤
│  Services (14 files)                            │
│  ├── Interfaces:            IAudioManager,      │
│  │                         ISubtitleManager,    │
│  │                         IPlaylistService,    │
│  │                         ISessionService      │
│  ├── PipWindowManager      —  PiP orchestrator │
│  ├── PipService            —  PiP lifecycle    │
│  ├── PlayerService         —  mpv wrapper      │
│  ├── SessionManager        —  session resume   │
│  ├── PlaylistCoordinator   —  playlist logic   │
│  ├── FileDialogHandler     —  file dialogs     │
│  ├── ErrorBoundary          —  safe execution  │
│  ├── CrashReporter          —  dump writer     │
│  ├── ScreenshotService      —  screenshots     │
│  └── FileAssociationService —  file types      │
└────────────────────────────────────────────────┘
```

---

## 2. Phase 1A — Service Extraction

**Goal**: Extract standalone service classes for playlist logic, session management, and file operations from MainViewModel.

### What Changed

| Service | File | Lines | Replaces |
|---------|------|-------|----------|
| [`PlaylistCoordinator`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlaylistCoordinator.cs) | `Services/` | 161 | Inline playlist logic in `MainViewModel` |
| [`SessionManager`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/SessionManager.cs) | `Services/` | 95 | Inline session save/load in `MainViewModel` |

### Integration Points

- `MainViewModel` constructor accepts `IPlaylistService?` and `ISessionService?` (both optional with defaults)
- `MainViewModel.Actions.cs` and `MainViewModel.Playlist.cs` delegate all playlist operations to `_playlistCoordinator`
- All fields duplicated between ViewModel and coordinator were removed (see [diff](#))

### Key Design Decisions

- **Optional constructor parameters** — backward-compatible, no DI container required
- `PlaylistCoordinator` handles navigation, shuffle, persistence, and loop state
- `SessionManager` handles file-level session resume (position, track IDs, delays)

---

## 3. Phase 1B — ViewModel Decomposition

**Goal**: Split the monolithic `MainViewModel` into focused partial files by domain.

### Before → After

| File | Lines | Responsibility |
|------|-------|----------------|
| [`MainViewModel.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs) | **586** | Core properties, fields, constructor, INPC, disposal |
| [`MainViewModel.Actions.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Actions.cs) | **348** | File operations (Open/Add), event handlers (position/volume/playlist), EQ presets, `RefreshState` |
| [`MainViewModel.Playback.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Playback.cs) | **135** | **NEW** — PlayPause, Stop, Seek, Volume, Fullscreen, Loop/Shuffle, Speed, Screenshot, Audio Normalization |
| [`MainViewModel.Playlist.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Playlist.cs) | **268** | **NEW** — PlaylistPosition, PlayNext/Prev, InsertAfterCurrent, Sort, Save/Load, Session, Recent Files |
| [`MainViewModel.Renderer.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Renderer.cs) | **29** | **NEW** — `RendererType` enum + `RendererMode` / `IsHardwareAccelerationEnabled` |
| [`MainViewModel.Tracks.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Tracks.cs) | **212** | Unchanged — subtitle/audio track selection UI |

### Total Lines

- Before: ~1,310 (2 files)
- After: **1,641** (6 files) — +25% due to added separation boundaries + comments
- Logical code volume is similar; the increase comes from file headers, region markers, and XML doc

---

## 4. Phase 1C — MainWindow Decomposition

**Goal**: Reduce 10 partial files to 4 by merging tightly-coupled concerns.

### Before → After

| Removed (merged into) | Lines | Target | Lines |
|----------------------|-------|--------|-------|
| `AutoHide.cs` | 217 | `WindowControls.cs` | 317 |
| `DragDrop.cs` | 89 | `Input.cs` | 406 |
| `FileDialogs.cs` | 27 | `Core.cs` | 968 |
| `Media.cs` | 219 | `Core.cs` | — |
| `Fullscreen.cs` | 54 | `WindowControls.cs` | — |
| `ResponsiveLayout.cs` | 38 | `Input.cs` | — |

**Kept as-is**:
| File | Lines | Purpose |
|------|-------|---------|
| `Core.cs` | 968 | Init, DI, dispose, startup sequence, media event handlers, file dialog delegates |
| `Input.cs` | 406 | Keyboard shortcuts, mouse events, context menu, drag-and-drop, responsive layout |
| `Pip.cs` | 36 | Delegates to `PipWindowManager` |
| `WindowControls.cs` | 317 | Auto-hide, fullscreen, fade animations, window chrome |

### PipWindowManager Extraction

[`PipWindowManager`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipWindowManager.cs) (185 lines) was extracted from the original `MainWindow.Pip.cs` (121 lines → 36 lines). It:

- Owns `PipService` lifecycle
- Bridges PiP events back to `MainWindow`
- Accepts all dependencies via constructor injection

---

## 5. Phase 1D — Interface Contracts

**Goal**: Define interfaces for all major service classes to enable testing and loose coupling.

### Interfaces Created

| Interface | File | Members | Implemented By | Used By |
|-----------|------|---------|----------------|---------|
| [`IAudioManager`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/IAudioManager.cs) | `Services/` | 25 | `AudioManager` | `MainViewModel`, `AudioEqualizerFlyout` |
| [`ISubtitleManager`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ISubtitleManager.cs) | `Services/` | 20 | `SubtitleManager` | `MainViewModel`, `SubtitleStyleFlyout`, `SubtitleOverlayControl` |
| [`IPlaylistService`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/IPlaylistService.cs) | `Services/` | 16 | `PlaylistCoordinator` | `MainViewModel` |
| [`ISessionService`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ISessionService.cs) | `Services/` | 10 | `SessionManager` | `MainViewModel` |

### Wiring Changes

| File | Before | After |
|------|--------|-------|
| [`MainViewModel.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs) | `AudioManager Audio` | `IAudioManager Audio` |
| [`MainViewModel.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs) | `SubtitleManager Subtitles` | `ISubtitleManager Subtitles` |
| [`MainViewModel.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs) | `PlaylistCoordinator` (ctor param) | `IPlaylistService` (ctor param) |
| [`MainViewModel.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs) | `_playlistCoordinator` (field) | `IPlaylistService _playlistCoordinator` |
| [`AudioManager.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs) | `: INotifyPropertyChanged, IDisposable` | `: IAudioManager` |
| [`SubtitleManager.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs) | `: INotifyPropertyChanged, IDisposable` | `: ISubtitleManager` |
| [`PlaylistCoordinator.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlaylistCoordinator.cs) | `class PlaylistCoordinator` | `class PlaylistCoordinator : IPlaylistService` |
| [`SubtitleStyleFlyout.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleStyleFlyout.axaml.cs) | `SubtitleManager` (all refs) | `ISubtitleManager` (all refs) |
| [`AudioEqualizerFlyout.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioEqualizerFlyout.axaml.cs) | `AudioManager` | `IAudioManager` |

---

## 6. Phase 1E — Namespace Cleanup

**Goal**: Fix files whose namespace doesn't match their directory.

### Fixed

| File | Directory | Old Namespace | New Namespace |
|------|-----------|---------------|---------------|
| [`PlayerService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlayerService.cs) | `Services/` | `Cine.Avalonia.ViewModels` | `Cine.Avalonia.Services` |
| [`PipService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs) | `Services/` | `Cine.Avalonia.ViewModels` | `Cine.Avalonia.Services` |

### Verified Clean (no changes needed)

- All `Services/*.cs` → `Cine.Avalonia.Services` ✅
- All `Managers/*.cs` → `Cine.Avalonia.Managers` ✅
- All `ViewModels/*.cs` → `Cine.Avalonia.ViewModels` ✅
- All `Shell/*.cs` → `Cine.Avalonia` ✅
- `Controls/*.cs` → `Cine.Avalonia.Controls` ✅

---

## 7. Phase 2 — Error Handling Standardization

**Goal**: Eliminate silent failures, standardize on structured logging, wrap async void handlers.

### 2A — Silent `catch { }` Elimination

**32 sites → 0**. All empty catches now have either:
- A comment explaining why suppression is safe (e.g., `/* best-effort during shutdown */`)
- A `Log.ForContext<T>().Error(ex, "...")` call

| File | Catches Fixed | Strategy |
|------|--------------|----------|
| [`AudioSettingsStore.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioSettingsStore.cs) | 3 | `Log.Error(ex, "...")` |
| [`SubtitleSettingsStore.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleSettingsStore.cs) | 4 | `Log.Error(ex, "...")` |
| [`PlaylistSettingsStore.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/PlaylistSettingsStore.cs) | 1 | `Log.Error(ex, "...")` |
| [`MpvVideoView.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Controls/MpvVideoView.cs) | 6 | Comments (shutdown path) |
| [`CrashReporter.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/CrashReporter.cs) | 4 | Comments (IS the crash writer) |
| [`PipService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs) | 2 | Comments (disposed window) |
| [`MainViewModel.Playlist.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Playlist.cs) | 1 | Comment (recent files non-critical) |

### 2B — DebugLog → Structured Logging

| File | Before | After |
|------|--------|-------|
| `MainWindow.Core.cs` (L180) | `DebugLog($"Player init FAILED: {ex}")` | `Log.ForContext<MainWindow>().Error(ex, "Player init failed")` |
| `MainWindow.Core.cs` (L302) | `DebugLog($"InitVideoRenderer FAILED: {ex}")` | `Log.ForContext<MainWindow>().Error(ex, "Video renderer init failed")` |

**Remaining `DebugLog` calls** (kept intentionally):
- `PlayerService.cs` — 5 calls for startup timing (debug-only)
- `MainWindow.Core.cs` — 9 calls for lifecycle trace (debug-only)
- These are low-volume, high-value debug traces. The `DebugLog` method writes to a file in `%LOCALAPPDATA%\Cine\`.

### 2C — ErrorBoundary Promotion

| Handler | File | Before | After |
|---------|------|--------|-------|
| `OnMediaOpened` | `MainWindow.Core.cs` | `async void` (unhandled) | Wrapped with `ErrorBoundary.Run(async () => ...)` |
| `FadeHeaderAndControls` | `WindowControls.cs` | ✅ Already wrapped (Phase 1) | Still wrapped |

### Structured Logging API

```
Log.ForContext<T>().Error(Exception ex, string message, params object?[] args)
Log.ForContext<T>().Warning(string message, params object?[] args)
Log.ForContext<T>().Info(string message, params object?[] args)
Log.ForContext<T>().Debug(string message, params object?[] args)
```

Note: `Warning()` does NOT accept Exception. Use `Error(ex, ...)` for exception logging.

### Current Logging Coverage

```
Services/  —  Log.ForContext<PlayerService>.Error()    ✅
           —  Log.ForContext<CrashReporter>.Error()     ✅
Managers/  —  Log.ForContext<AudioSettingsStore>.Error() ✅
           —  Log.ForContext<SubtitleSettingsStore>.Error() ✅
           —  Log.ForContext<PlaylistSettingsStore>.Error() ✅
Shell/     —  Log.ForContext<MainWindow>.Error()        ✅
```

---

## 8. Remaining Work & Risks

### Deferred Items (low value / high risk)

| Item | Reason | Effort if done later |
|------|--------|---------------------|
| `IRendererService` interface | 1 enum + 2 properties = over-engineering | 10 min |
| `IMediaFileService` | File validation is ~2 lines | 5 min |
| PIP → `PipWindowManager` (extraction) | 8+ field dependencies in MainWindow | Deferred permanently |
| `MainWindow.App.axaml.cs` merge | Already gone (was merged earlier) | 0 min |
| Namespace `Cine.Avalonia.Managers` → `Application.Managers` | Visual Studio navigates fine | Cosmetic only |

### Remaining Anti-Patterns (low priority)

| Pattern | Count | Location |
|---------|-------|----------|
| `DebugLog()` calls | ~14 | PlayerService, MainWindow.Core — intentional startup tracing |
| `catch { /* best-effort comment */ }` | ~6 | MpvVideoView, CrashReporter — justified (shutdown/crash paths) |
| `catch (Exception ex)` with no `when` filter | ~20 | All over — acceptable for general error handling |

---

## 9. Architecture Diagram

```
                          ┌──────────────────────────────┐
                          │       MainWindow (4 files)    │
                          │  Core | Input | Pip | WndCtrl │
                          └──────────┬───────────────────┘
                                     │ owns
                          ┌──────────▼───────────────────┐
                          │   MpvVideoView (ANGLE/GL)    │
                          └──────────┬───────────────────┘
                                     │ events
              ┌──────────────────────┼──────────────────────┐
              │                      │                      │
   ┌──────────▼────────┐  ┌─────────▼────────┐  ┌─────────▼────────┐
   │  MainViewModel    │  │  PipService      │  │  PlayerService   │
   │  (6 partials)     │  │  (frame sharing) │  │  (mpv wrapper)   │
   └──────────┬────────┘  └──────────────────┘  └──────────────────┘
              │ uses
     ┌────────┼────────┬──────────┬──────────┐
     │        │        │          │          │
┌────▼───┐ ┌─▼─────┐ ┌▼──────┐ ┌─▼──────┐ ┌▼──────┐
│IAudio  │ │ISub   │ │IPlay  │ │ISession│ │Error  │
│Manager │ │Manager│ │Service│ │Service │ │Boundary│
└────────┘ └───────┘ └───────┘ └────────┘ └───────┘
     │         │         │          │
┌────▼───┐ ┌──▼────┐ ┌──▼────────┐ │
│Audio   │ │Subtitle│ │Playlist  │ │
│Manager │ │Manager │ │Coordinator│ │
└────────┘ └────────┘ └──────────┘ │
                              ┌────▼────────┐
                              │SessionManager│
                              └─────────────┘

Settings Stores (file I/O):
  AudioSettingsStore → %LOCALAPPDATA%\Cine\audio_settings.json
  SubtitleSettingsStore → %LOCALAPPDATA%\Cine\subtitle_*.json
  PlaylistSettingsStore → %LOCALAPPDATA%\Cine\playlist.json
```

---

## 10. File Inventory

### Shell (MainWindow partials) — 4 files, 1,727 total lines

| File | Lines | Path |
|------|-------|------|
| Core.cs | 968 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Core.cs) |
| Input.cs | 406 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) |
| WindowControls.cs | 317 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.WindowControls.cs) |
| Pip.cs | 36 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Pip.cs) |

### ViewModels — 7 files, 1,641 total lines

| File | Lines | Path |
|------|-------|------|
| MainViewModel.cs | 586 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.cs) |
| MainViewModel.Actions.cs | 348 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Actions.cs) |
| MainViewModel.Playlist.cs | 268 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Playlist.cs) |
| MainViewModel.Tracks.cs | 212 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Tracks.cs) |
| MainViewModel.Playback.cs | 135 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Playback.cs) |
| PlaylistItemViewModel.cs | 63 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/PlaylistItemViewModel.cs) |
| MainViewModel.Renderer.cs | 29 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Renderer.cs) |

### Services — 14 files, 1,651 total lines

| File | Lines | Path |
|------|-------|------|
| FileDialogHandler.cs | 303 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FileDialogHandler.cs) |
| PipWindowManager.cs | 185 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipWindowManager.cs) |
| PlaylistCoordinator.cs | 161 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlaylistCoordinator.cs) |
| FileAssociationService.cs | 148 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FileAssociationService.cs) |
| PipService.cs | 131 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs) |
| CrashReporter.cs | 119 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/CrashReporter.cs) |
| PlayerService.cs | 112 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlayerService.cs) |
| SessionManager.cs | 95 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/SessionManager.cs) |
| ErrorBoundary.cs | 63 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ErrorBoundary.cs) |
| ScreenshotService.cs | 60 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ScreenshotService.cs) |
| IAudioManager.cs | 59 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/IAudioManager.cs) |
| ISubtitleManager.cs | 48 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ISubtitleManager.cs) |
| IPlaylistService.cs | 32 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/IPlaylistService.cs) |
| ISessionService.cs | 30 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ISessionService.cs) |

### Managers — 7 files, 2,337 total lines

| File | Lines | Path |
|------|-------|------|
| SubtitleManager.cs | 697 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs) |
| AudioManager.cs | 561 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs) |
| PlaybackStateManager.cs | 391 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/PlaybackStateManager.cs) |
| VideoManager.cs | 214 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/VideoManager.cs) |
| AudioSettingsStore.cs | 192 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioSettingsStore.cs) |
| SubtitleSettingsStore.cs | 178 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleSettingsStore.cs) |
| PlaylistSettingsStore.cs | 104 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/PlaylistSettingsStore.cs) |

### Controls — 1 file, 380 lines

| File | Lines | Path |
|------|-------|------|
| MpvVideoView.cs | 380 | [link](file:///x:/Development/Cine_CSharp_DotNet/src/App/Controls/MpvVideoView.cs) |

---

## Appendix A: File Reduction Summary

| Area | Before (files) | After (files) | Reduction |
|------|---------------|---------------|-----------|
| MainWindow partials | 10 | 4 | **−60%** |
| MainViewModel partials | 3 | 6 | **+100%** (more focused) |
| Service interfaces | 0 | 4 | **+∞** (new) |
| **Total tracked** | 13 | 14 | comparable |

## Appendix B: Phase 1 Plan vs Actual

| Phase | Planned | Actual | Variance |
|-------|---------|--------|----------|
| 1A Service Extraction | 4 services | 2 services | −2 (IRendererService, IMediaFileService deferred) |
| 1B ViewModel Split | 5 partials | 6 partials | +1 (Renderer.cs) |
| 1C MainWindow Merge | 10→3 files | 10→4 files | Pip kept separate |
| 1D Interface Contracts | 7 interfaces | 4 interfaces | −3 (deferred low-value) |
| 1E Namespace Cleanup | Full audit | 2 files fixed | PlayerService + PipService |

## Appendix C: Build Verification

```powershell
dotnet build src\App\App.csproj --no-restore
# Result: Build succeeded. 0 errors, 0 warnings.
# Projects: Media, Core, App
# Total time: ~10-15s
```
