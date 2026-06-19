# Phase 2 — Error Handling Standardization

> **Goal**: Eliminate silent `catch { }` patterns, unify on structured logging, and make `ErrorBoundary` the standard way to wrap async event handlers.

---

## Current Anti-Patterns (Quantified)

| Pattern | Count | Severity | Impact |
|---------|-------|----------|--------|
| `catch { }` (bare empty) | **32** | 🔴 | Bugs invisible, silent failures |
| `DebugLog($"... {ex}")` for errors | **12** | 🟡 | Console-only, lost in prod |
| `ErrorBoundary.Run()` usage | **1** | 🟢 | Should be 20+ |
| `Result<T>` usage | **1** | 🟢 | Should be 5+ |
| Inconsistent `Log.ForContext` | **8** | 🟡 | Mixed string/type patterns |

---

## Implementation Plan

### 2A — Fix Silent `catch { }` (32 sites → 0)

**Strategy**: Replace every bare `catch { }` with one of:
- `catch { /* suppress — expected */ }` + comment WHY it's safe  
- `catch (Exception ex) { Log.ForContext<T>().Warning(ex, "..."); }`  
- `ErrorBoundary.Run(...)` for event handlers

**Worst offenders** (fix first):

| File | Lines | Fix |
|------|-------|-----|
| `MpvVideoView.cs` | 6 empty catches | Log + rethrow for dispose failures |
| `PipService.cs` | 2 empty catches | Log on close failures |
| `CrashReporter.cs` | 4 empty catches | Already crash-safe, add Warning log |
| `AudioSettingsStore.cs` | 3 empty catches | Log + degrade gracefully |
| `SubtitleSettingsStore.cs` | 4 empty catches | Log + degrade gracefully |
| `PlaylistSettingsStore.cs` | 1 empty catch | Log + degrade gracefully |

### 2B — Standardize on Structured Logging (not DebugLog)

**Strategy**: Replace `DebugLog($"... {ex}")` error paths with `Log.ForContext<T>().Error(ex, ...)`.

| File | Lines | Fix |
|------|-------|-----|
| `MainWindow.Core.cs` | `DebugLog($"Player init FAILED: {ex}")` | → `Log.ForContext<MainWindow>().Error(ex, "Player init failed")` |
| `MainWindow.Core.cs` | `DebugLog($"InitVideoRenderer FAILED: {ex}")` | → structured log |
| `PlayerService.cs` | `DebugLog($"... failed: {ex}")` | → structured log |

### 2C — Promote `ErrorBoundary` Usage

**Strategy**: Wrap all `async void` event handlers with `ErrorBoundary.Run()`.

| File | Handler | Fix |
|------|---------|-----|
| `MainWindow.WindowControls.cs` | `FadeHeaderAndControls` | ✅ Already done |
| `MainWindow.Core.cs` | `OnMediaOpened` | → Wrap with ErrorBoundary |
| `MainWindow.Core.cs` | `OnPlaybackStateChanged` | → Wrap with ErrorBoundary |
| `MainWindow.Core.cs` | `OnMediaEnded` | → Wrap with ErrorBoundary |
| `MainWindow.Input.cs` | Keyboard handler | → Wrap with ErrorBoundary |

### 2D — Adopt `Result<T>` for File I/O Operations

**Strategy**: Return `Result<T>` from all file read/write operations instead of silent catch.

| File | Method | Fix |
|------|--------|-----|
| `AudioSettingsStore.cs` | `Save` / `Load` | → `Result.From(...)` |
| `PlaylistSettingsStore.cs` | `SavePlaylist` / `LoadPlaylist` | → `Result.From(...)` |
| `SubtitleSettingsStore.cs` | `Save` / `Load` | → `Result.From(...)` |
| `SessionManager.cs` | `Save` / `Load` | → `Result.From(...)` |
