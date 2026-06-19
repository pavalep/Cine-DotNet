# Phase 1 — Architecture Refactoring: Detailed Implementation Plan

> **Based on**: Internet research on MVVM best practices, layered architecture patterns, C# conventions, Avalonia reference projects, and deep analysis of Cine's codebase (19,000+ lines across 100+ files).  
> **Constrained to**: Cine's existing code structure — no over-engineering, no "gold-plating."  
> **Goal**: From 19 partial files for 2 classes (MainWindow + MainViewModel) to a clean, testable, maintainable structure without changing any behavior.

---

## Table of Contents

1. [Current State Analysis](#1-current-state-analysis)
2. [Target Architecture](#2-target-architecture)
3. [Phase 1A — Service Extraction (Safe, Reversible)](#3-phase-1a--service-extraction)
4. [Phase 1B — ViewModel Decomposition](#4-phase-1b--viewmodel-decomposition)
5. [Phase 1C — View Decomposition (MainWindow)](#5-phase-1c--view-decomposition-mainwindow)
6. [Phase 1D — Interface Contracts & Dependency Injection](#6-phase-1d--interface-contracts--dependency-injection)
7. [Phase 1E — Namespace & Directory Migration](#7-phase-1e--namespace--directory-migration)
8. [Migration Order & Risk Assessment](#8-migration-order--risk-assessment)

---

## 1. Current State Analysis

### 1.1 Project Structure (Simplified)

```
src/
├── App/
│   ├── Application/
│   │   ├── Services/           # 7 files (PlayerService, FileDialogHandler, etc.)
│   │   ├── Managers/           # 7 files (AudioManager, SubtitleManager, etc.)
│   │   ├── ViewModels/         # 4 files but MainViewModel = 3 partials ~1800 lines
│   │   ├── Models/             # 2 files
│   │   ├── Converters/         # 1 file
│   │   ├── Extensions/         # 3 files
│   │   ├── Utilities/          # 1 file (RelayCommand)
│   │   └── Constants/          # 1 file
│   ├── UI/
│   │   ├── Shell/              # MainWindow = 10 partial files
│   │   ├── Views/              # MainWindow.axaml only
│   │   ├── Resources/          # 5 axaml files
│   │   ├── Builders/           # 4 files (Flyout, Menu builders)
│   │   ├── Controls/           # 14 files across 4 sub-folders
│   │   ├── Screens/            # Dialogs, Shell, Start sub-folders
│   │   └── Constants/          # 3 files
│   ├── Controls/               # 1 file (MpvVideoView.cs)
│   └── Infrastructure/         # Empty (README only)
├── Core/                       # Interfaces, Models, Services
└── Media/                      # IMediaPlayer, MpvPlayer, MfPlayer
```

### 1.2 Metric: Partial File Overload

| Class | Current Files | Current Lines | After Refactor |
|-------|-------------|---------------|----------------|
| `MainViewModel` | 3 partial files | ~1,800 lines | 1 file + 4 service classes |
| `MainWindow` | 10 partial files | ~2,400 lines | 1 file + 2 partials = 3 files |

Per [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions), partial classes are intended for:
1. Generated code (designer files, source generators)
2. Platform-specific code in multi-targeting
3. Large legacy classes under gradual refactoring

**None of these apply to Cine.** The partial classes are purely organizational — they should be real classes.

### 1.3 Current Responsibilities Per Class

**MainViewModel** (3 files, 1800 lines) handles:
- INotifyPropertyChanged + Dispose boilerplate
- Session save/load (file I/O + JSON)
- Playlist management (add, remove, shuffle, save, load)
- File operations (open, open multiple, drag-drop)
- Renderer mode switching
- Subtitle track coordination (delegates to SubtitleManager)
- Chapter list management
- Video track switching
- Audio track delegation
- Properties for SeekBar, Volume, Speed, etc.
- Debug logging

**MainWindow** (10 files, 2400 lines) handles:
- Window initialization (ANGLE + mpv + event wiring)
- Fullscreen toggle + auto-hide behavior
- PiP window management
- Pointer/key input handling
- File dialog delegate wiring
- Responsive layout breakpoints
- Drag-drop overlay
- Media event handlers (position, duration, state)
- Window control buttons (minimize, maximize, close)
- Debug logging

### 1.4 Issues with Current Architecture

| Issue | Impact | Source |
|-------|--------|--------|
| ViewModel knows about JSON format | Breaking change if serialization changes | `MainViewModel.Actions.cs` |
| ViewModel owns playlist file path | Cannot test playlist without real filesystem | `_playlistStore` |
| MainWindow initializes ANGLE + mpv | Cannot unit-test any window logic | `MainWindow.Core.cs` |
| Random `Task.Run` with no cancellation | Stale callbacks fire after window close | `MainWindow.Media.cs:35` |
| No service interfaces | Tight coupling — cannot mock | Throughout |
| ViewModel.Log() duplicates PlayerService.DebugLog() | Confusing debug output | Both files |

---

## 2. Target Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     PRESENTATION LAYER                    │
│                                                          │
│  Views/ (XAML + code-behind)                             │
│  ├── MainWindow.axaml / .cs                              │
│  ├── Dialogs/ (Playlist, Settings, etc.)                  │
│  └── Controls/ (SeekBar, Subtitles, etc.)                │
│                                                          │
│  ViewModels/                                              │
│  ├── MainViewModel.cs (Core orchestrator)                │
│  ├── MainViewModel.Session.cs (Session save/load)        │
│  └── MainViewModel.Playback.cs (Play/pause/stop cmd)     │
├─────────────────────────────────────────────────────────┤
│                    APPLICATION SERVICES                    │
│                                                          │
│  Services/                                                │
│  ├── IPlaylistService / PlaylistCoordinator              │
│  ├── ISessionService / SessionManager                    │
│  ├── IFileDialogService / FileDialogHandler              │
│  ├── IRendererService / RendererCoordinator              │
│  ├── ICrashReporting / CrashReporter                     │
│  ├── IFileAssociation / FileAssociationService           │
│  └── IPipService / PipService                            │
│                                                          │
│  Managers/ (keep, already separated)                      │
│  ├── AudioManager                                        │
│  ├── SubtitleManager                                     │
│  ├── PlaybackStateManager                                │
│  └── VideoManager                                        │
├─────────────────────────────────────────────────────────┤
│                     DOMAIN LAYER                          │
│                                                          │
│  Core/ (Models + Interfaces — already clean)             │
│  ├── IMediaPlayer (interface)                            │
│  ├── PlaybackState, LoopMode, etc.                       │
│  └── Log.ForContext<T>()                                 │
├─────────────────────────────────────────────────────────┤
│                   INFRASTRUCTURE LAYER                    │
│                                                          │
│  Media/ (MpvPlayer, MediaFoundationPlayer — OK as-is)    │
│  ├── MpvPlayer.cs                                        │
│  ├── MediaFoundationPlayer.cs                            │
│  ├── MpvInterop.cs                                       │
│  └── AngleInterop.cs                                     │
└─────────────────────────────────────────────────────────┘
```

### 2.1 Key Design Decisions

1. **ViewModel stays as partial class (minimally)** — Reducing from 3 to 2 partials. The core and session/playback logic files. This is acceptable during transition.
2. **Services extracted to real classes** — `ISessionService`, `IPlaylistService` so they can be tested without GUI.
3. **No DI container** — Services are manually instantiated and passed via constructor. DI can be added later if needed.
4. **Managers stay as-is** — `AudioManager` etc. are already well-separated. They just need interfaces.
5. **MainWindow goes from 10 -> 3 partial files** — The remaining ones are genuinely about window lifecycle.

---

## 3. Phase 1A — Service Extraction (Safe, Reversible)

**When**: Start here. These are pure C# classes with no GUI dependency.  
**Risk**: Low — can be done file-by-file.  
**Estimated effort**: 4-6 hours.

### 3.1 Extract SessionService (`IPlaylistService`, `ISessionService`)

**Problem**: Session save/load is in `MainViewModel.Actions.cs` with JSON serialization logic mixed into ViewModel.

**Current code** (`MainViewModel.Actions.cs:496-560`):
```csharp
var session = new { FilePath, Position, Playlist ... };
var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cine_session.json"), json);
```

**Target**:
```csharp
// App/Application/Services/ISessionService.cs
public interface ISessionService
{
    Task SaveAsync(string filePath, TimeSpan position, int subtitleTrack, int audioTrack);
    Task<SessionData?> LoadAsync();
}

// App/Application/Services/SessionManager.cs
public class SessionManager : ISessionService
{
    private readonly string _sessionPath;
    
    public SessionManager()
    {
        _sessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "cine_session.json");
    }

    public async Task SaveAsync(string filePath, TimeSpan position, int subtitleTrack, int audioTrack)
    {
        var data = new SessionData { FilePath = filePath, Position = position, ... };
        var json = JsonSerializer.Serialize(data, CineJsonContext.Default.SessionData);
        await File.WriteAllTextAsync(_sessionPath, json);
    }
    
    public async Task<SessionData?> LoadAsync()
    {
        if (!File.Exists(_sessionPath)) return null;
        var json = await File.ReadAllTextAsync(_sessionPath);
        return JsonSerializer.Deserialize(json, CineJsonContext.Default.SessionData);
    }
}

// ViewModel usage after extraction
public partial class MainViewModel
{
    private readonly ISessionService _session;
    
    public void LoadSession()
    {
        var data = await _session.LoadAsync();
        if (data == null) return;
        OpenFile(data.FilePath);
        if (data.Position > TimeSpan.Zero)
            _player.Seek(data.Position);
    }
}
```

**Files changed**:
- `NEW` `App/Application/Services/SessionManager.cs`
- `NEW` `App/Application/Services/ISessionService.cs`
- `MODIFY` `MainViewModel.Actions.cs` (remove session JSON, inject ISessionService)

### 3.2 Extract PlaylistService (`IPlaylistService`)

**Problem**: Playlist coordination (add, remove, move, shuffle, loop modes) is in `MainViewModel.Actions.cs` and `MainViewModel.cs` scattered across property getters.

**Current**:
```csharp
// MainViewModel.cs — scattered
private bool _isShuffleEnabled;
private bool _isLoopFileEnabled;
private bool _isLoopPlaylistEnabled;

// MainViewModel.Actions.cs — playlist management
public void OpenFiles(string[] paths) { ... }
public void ShufflePlaylist() { ... }
public void OnPlaylistItemDoubleClicked(...) { ... }
```

**Target**:
```csharp
// App/Application/Services/IPlaylistService.cs
public interface IPlaylistService
{
    ObservableCollection<string> Items { get; }
    int CurrentIndex { get; set; }
    bool IsShuffleEnabled { get; set; }
    LoopMode LoopMode { get; set; }
    
    void Add(string path);
    void AddRange(IEnumerable<string> paths);
    void RemoveAt(int index);
    void Move(int fromIndex, int toIndex);
    void Shuffle();
    string? GetNext();
    string? GetPrevious();
    void Save();
    void Load();
}

// ViewModel usage
public partial class MainViewModel
{
    private readonly IPlaylistService _playlist;
    private readonly ISessionService _session;
    
    public void OpenFiles(string[] paths)
    {
        _playlist.AddRange(paths);
        OpenFile(paths[0]);
    }
}
```

**Files changed**:
- `NEW` `App/Application/Services/IPlaylistService.cs`
- `NEW` `App/Application/Services/PlaylistCoordinator.cs` (implements IPlaylistService)
- `MODIFY` `MainViewModel.Actions.cs` (remove playlist logic, delegate to _playlist)
- `MODIFY` `MainViewModel.cs` (remove playlist properties, delegate to _playlist)

### 3.3 Extract RendererService (`IRendererService`)

**Problem**: Renderer mode switching (Native ↔ ANGLE ↔ ANGLE OOP) logic is in `MainViewModel.Actions.cs`.

**Current** (`MainViewModel.Actions.cs`):
```csharp
private string _rendererMode = "angle";
public void SwitchRenderer() { ... }
public void OnAglContextLost() { ... }
```

**Target**:
```csharp
public interface IRendererService
{
    string CurrentMode { get; }
    Task<bool> SwitchModeAsync(string mode);
    Task RecoverFromContextLossAsync();
}

public class RendererCoordinator : IRendererService
{
    // Move renderer switching logic here
    // ViewModel just calls coordinator
}
```

**Files changed**:
- `NEW` `App/Application/Services/IRendererService.cs`
- `NEW` `App/Application/Services/RendererCoordinator.cs`
- `MODIFY` `MainViewModel.Actions.cs` (remove renderer logic)

### 3.4 Extract FileOpService (`IFileOpService`)

**Problem**: `OpenFile()` in ViewModel handles: playlist bookkeeping, player open, state reset, subtitle search, history. 150 lines of orchestration.

**Current** (`MainViewModel.Actions.cs:120-270`):
```csharp
public async void OpenFile(string path)
{
    // 1. Validate path
    // 2. Update Playlist.CurrentIndex (via _playlistStore)  
    // 3. _player.Open(path)
    // 4. Reset playback state properties
    // 5. Reset chapter list
    // 6. Search for external subtitles
    // 7. Clear OSD
    // 8. Save session
    // 9. Notify UI
}
```

**Target**: Keep orchestration in ViewModel (since it touches UI state) but extract file validation + subtitle search to a service:

```csharp
public interface IMediaFileService
{
    bool IsValidMediaFile(string path);
    IEnumerable<string> FindExternalSubtitles(string mediaPath);
    Task<MediaFileInfo> GetInfoAsync(string path);
}
```

**Files changed**:
- `NEW` `App/Application/Services/IMediaFileService.cs`
- `NEW` `App/Application/Services/MediaFileService.cs`
- `MODIFY` `MainViewModel.Actions.cs` (inject IMediaFileService)

---

## 4. Phase 1B — ViewModel Decomposition

**When**: After Phase 1A is complete (services extracted).  
**Risk**: Medium — requires moving lots of code.  
**Estimated effort**: 4-6 hours.

### 4.1 New ViewModel Structure

```
ViewModels/
├── MainViewModel.cs                     # Core: construction, properties, DI wiring
├── MainViewModel.Session.cs             # Session management (delegates to ISessionService)
├── MainViewModel.Playback.cs            # Play/Pause/Stop/Seek/Volume/Commands
├── MainViewModel.FileOps.cs             # Open/OpenFiles/DropFiles (delegates to services)
├── MainViewModel.Renderer.cs            # Renderer switching (delegates to IRendererService)
├── PlaylistItemViewModel.cs             # Already standalone — keep
└── (future: MainViewModel.Playlist.cs)  # Playlist UI bindings
```

### 4.2 MainViewModel.Core.cs (NEW consolidation)

**Extract from**: `MainViewModel.cs` lines 1-80 (fields), ~80-160 (properties), 160-250 (INotifyPropertyChanged boilerplate)

**Content**: 
- Constructor (takes `IMediaPlayer`, `IPlaylistService`, `ISessionService`, etc.)
- All bindable properties (PositionText, VolumeValue, etc.)
- `INotifyPropertyChanged` implementation
- `IDisposable` implementation
- `PropertyChanged` event

**Target size**: ~150 lines (down from 300+)

### 4.3 MainViewModel.FileOps.cs (consolidated from Actions.cs)

**Content**:
- `OpenFile(string path)` — orchestration
- `OpenFiles(string[] paths)` — bulk open
- `AddFiles(string[] paths)` — add to playlist
- Drag-drop handling methods
- Session save/load calls

**Target size**: ~200 lines (down from 500+)

### 4.4 MainViewModel.Playback.cs (NEW)

**Content** (moved from `MainViewModel.cs` and `MainViewModel.Actions.cs`):
- `PlayCommand`, `PauseCommand`, `StopCommand`, `TogglePlayPauseCommand`
- `OnPlayerPositionChanged`, `OnPlayerStateChanged`
- `Seek(double position)`, `SetVolume(double)`, `SetSpeed(double)`
- `MuteCommand`

**Target size**: ~150 lines

### 4.5 MainViewModel.Renderer.cs (NEW)

**Content** (moved from `MainViewModel.Actions.cs`):
- `SwitchRendererCommand`
- `OnContextLost(..)`
- `CycleRendererMode()`

**Target size**: ~50 lines

---

## 5. Phase 1C — View Decomposition (MainWindow)

**When**: After ViewModel refactoring.  
**Risk**: Medium — touches UI code.  
**Estimated effort**: 3-4 hours.

### 5.1 Current Problem

`MainWindow` has 10 partial files (2400 lines). The worst offenders:

| File | Lines | Issue |
|------|-------|-------|
| `MainWindow.Core.cs` | ~600 | Init + ANGLE + event wiring + debug logging |
| `MainWindow.Input.cs` | ~400 | Pointer + keyboard + double-tap detection |
| `MainWindow.Media.cs` | ~350 | Media event handlers (position, duration, etc.) |
| `MainWindow.Pip.cs` | ~250 | PiP window management |
| `MainWindow.AutoHide.cs` | ~150 | Fullscreen auto-hide timing |
| `MainWindow.FileDialogs.cs` | ~50 | 5 delegate methods — already clean |
| `MainWindow.WindowControls.cs` | ~150 | Min/Max/Close + title bar behavior |
| `MainWindow.DragDrop.cs` | ~100 | Drag-drop overlay + handlers |
| `MainWindow.ResponsiveLayout.cs` | ~100 | Layout breakpoints |
| `MainWindow.App.axaml.cs` | ~50 | Template applied handler (misnamed) |

### 5.2 Target: 3 Partial Files + 1 Helper Class

```
Shell/
├── MainWindow.cs                    # Constructor + public methods (~50 lines)
├── MainWindow.Initialization.cs     # Init, ANGLE, event wiring (~200 lines)  
├── MainWindow.Input.cs              # Pointer/key input (~300 lines)
├── MainWindow.WindowControls.cs     # Fullscreen + auto-hide + min/max/close (~200 lines)
└── PipWindowManager.cs              # Extracted PiP logic as separate class (~200 lines)
```

### 5.3 Merge Plan

| Current File | → | Target | Rationale |
|-------------|---|--------|-----------|
| `MainWindow.Core.cs` | → | `MainWindow.Initialization.cs` | Renamed for clarity |
| `MainWindow.AutoHide.cs` | → | `MainWindow.WindowControls.cs` | Auto-hide is tied to fullscreen toggle |
| `MainWindow.Fullscreen.cs` | → | `MainWindow.WindowControls.cs` | Merged with auto-hide |
| `MainWindow.Media.cs` | → | `MainWindow.Initialization.cs` | Event handlers are wired in init |
| `MainWindow.FileDialogs.cs` | → | `MainWindow.Initialization.cs` | 5 delegates, not worth a file |
| `MainWindow.App.axaml.cs` | → | `MainWindow.Initialization.cs` | Template-applied handler fits in init |
| `MainWindow.ResponsiveLayout.cs` | → | `MainWindow.Input.cs` | Layout breakpoints are triggered by input/resize |
| `MainWindow.DragDrop.cs` | → | `MainWindow.Input.cs` | Drag-drop is input handling |
| `MainWindow.Pip.cs` | → | `PipWindowManager.cs` | New standalone class, not a partial |

### 5.4 PipWindowManager.cs — Extract from MainWindow.Pip.cs

**Why**: PiP management is entirely self-contained — it creates a `PipWindow`, binds player state, and handles close/reopen. It has nothing to do with MainWindow lifecycle.

```csharp
// App/Application/Services/PipWindowManager.cs
public class PipWindowManager
{
    private MainWindow _window; // owns the real parent
    private PipWindow? _pipWindow;
    private readonly IMediaPlayer _player;
    
    public bool IsOpen => _pipWindow != null;
    
    public void Open(int x, int y) { ... }  // moved from MainWindow.Pip.cs
    public void Close() { ... }             // moved from MainWindow.Pip.cs
    public void Toggle() { ... }            // moved from MainWindow.Pip.cs
    public void SyncState() { ... }         // moved from MainWindow.Pip.cs
}
```

---

## 6. Phase 1D — Interface Contracts & Dependency Injection

**When**: Throughout.  
**Risk**: Low — interfaces are additive.  
**Estimated effort**: 2-3 hours.

### 6.1 Interfaces to Create

| Interface | Implementation | Consumed By | Status |
|-----------|---------------|-------------|--------|
| `ISessionService` | `SessionManager` | `MainViewModel` | NEW |
| `IPlaylistService` | `PlaylistCoordinator` | `MainViewModel`, `PlaylistDialog` | NEW |
| `IRendererService` | `RendererCoordinator` | `MainViewModel`, `MainWindow` | NEW |
| `IMediaFileService` | `MediaFileService` | `MainViewModel.FileOps` | NEW |
| `IAudioManager` | `AudioManager` | `AudioEqualizerFlyout` | EXISTS (add interface) |
| `ISubtitleManager` | `SubtitleManager` | `SubtitleStyleFlyout` | EXISTS (add interface) |
| `ICrashReporter` | `CrashReporter` | Global | EXISTS (add interface) |

### 6.2 Constructor Injection (Simple, no DI container)

```csharp
// MainViewModel.cs — after refactoring
public partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private readonly IPlaylistService _playlist;
    private readonly ISessionService _session;
    private readonly IRendererService _renderer;
    private readonly IMediaFileService _mediaFile;
    private readonly AudioManager _audio;
    private readonly SubtitleManager _subtitles;
    private readonly PlaybackStateManager _stateManager;

    public MainViewModel(
        IMediaPlayer player,
        IPlaylistService playlist,
        ISessionService session,
        IRendererService renderer,
        IMediaFileService mediaFile,
        AudioManager audio,
        SubtitleManager subtitles,
        PlaybackStateManager stateManager)
    {
        _player = player;
        _playlist = playlist;
        _session = session;
        _renderer = renderer;
        _mediaFile = mediaFile;
        _audio = audio;
        _subtitles = subtitles;
        _stateManager = stateManager;
    }
}
```

```csharp
// MainWindow.cs — simple manual DI
private void OnWindowInitialized(object? sender, EventArgs e)
{
    var player = _viewModel!.Player; // IMediaPlayer reference
    
    var session = new SessionManager();
    var playlist = new PlaylistCoordinator();
    var renderer = new RendererCoordinator();
    var mediaFile = new MediaFileService();
    var pipManager = new PipWindowManager(this, player);
    
    _viewModel.Initialize(session, playlist, renderer, mediaFile, pipManager);
}
```

### 6.3 Why Not Use Microsoft.Extensions.DependencyInjection?

Per [Microsoft MVVM best practices](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/), DI should be used for complex apps. However:

1. Adding `ServiceCollection` requires restructuring `App.axaml.cs` and `Program.cs`
2. For a desktop media player with one window, constructor injection + factory methods is sufficient
3. DI can be added later without breaking changes (the interfaces are already there)

**Decision**: Manual DI now → Auto DI later if needed.

---

## 7. Phase 1E — Namespace & Directory Migration

**When**: Last — after code moves are verified.  
**Risk**: Low — mechanical.  
**Estimated effort**: 1 hour.

### 7.1 Current Namespace Issues

| File | Current Namespace | Target Namespace |
|------|------------------|------------------|
| `MainWindow.cs` (10 files) | `Cine.Avalonia.UI.Shell` | `Cine.Avalonia.UI.Shell` (keep) |
| `MainViewModel.cs` (3 files) | `Cine.Avalonia.ViewModels` | `Cine.Avalonia.ViewModels` (keep) |
| `AudioManager.cs` | `Cine.Avalonia.Managers` | `Cine.Avalonia.Application.Managers` |
| `CrashReporter.cs` | `Cine.Avalonia.Services` | `Cine.Avalonia.Application.Services` |

**Decision**: Keep namespaces as-is. Only fix the `Managers` → `Application.Managers` inconsistency if it's a compile error blocker. Visual Studio's "Go To Definition" works regardless of namespace naming.

### 7.2 File Moves to Avoid

❌ Do NOT move files between folders — it breaks git history tracking  
✅ Instead: **Create new files in target locations, delete old ones after**

Exception: Interface files should go next to their implementations.

---

## 8. Migration Order & Risk Assessment

### 8.1 Recommended Order

```
Week 1: Phase 1A (Service Extraction)
  Day 1:   ISessionService / SessionManager        [Low risk, pure extraction]
  Day 2:   IPlaylistService / PlaylistCoordinator  [Low risk, pure extraction]  
  Day 3:   IRendererService / RendererCoordinator  [Low risk, small change]
  Day 4:   IMediaFileService / MediaFileService     [Low risk, pure extraction]
  Day 5:   Inject + verify build                    [Integration test]

Week 2: Phase 1B (ViewModel Split)
  Day 1:   MainViewModel.Playback.cs               [Medium risk — touch commands]
  Day 2:   MainViewModel.Renderer.cs                [Low risk — small]
  Day 3:   MainViewModel.FileOps.cs -> simplify     [Medium risk — orchestration]
  Day 4:   MainViewModel.cs -> reduce to core       [Medium risk — remove dead code]
  Day 5:   Build + manual test

Week 3: Phase 1C (MainWindow Split)
  Day 1:   PipWindowManager.cs                      [Low risk — self-contained]
  Day 2:   Merge AutoHide + Fullscreen -> WindowControls [Medium risk]
  Day 3:   Merge Media + App.axaml + FileDialogs -> Init [Medium risk]
  Day 4:   Merge DragDrop + ResponsiveLayout -> Input [Medium risk]
  Day 5:   Delete old partial files, build + test
```

### 8.2 Risk Table

| Refactor | Breakage Risk | Rollback Strategy | Test Strategy |
|----------|--------------|-------------------|---------------|
| `ISessionService` | Very Low | Delete new file, restore old code | Load app, verify session resume |
| `IPlaylistService` | Low | Same as above | Open files, add/remove/reorder |
| `IRendererService` | Very Low | Same | Switch renderer modes |
| `PipWindowManager` | Low | Restore old partial file | Open/close/resize PiP |
| ViewModel Split | Medium | Can keep new+old files side-by-side | Play/pause/seek/volume |
| MainWindow Merge | Medium | Can keep new+old files side-by-side | Fullscreen, drag-drop, resize |

### 8.3 Critical Rules

1. **One extraction per PR** — never combine service extraction + ViewModel split in one change
2. **Never change behavior** — if you find a bug during refactoring, fix it in a separate PR
3. **Keep old partial files alive** until replacement is verified — delete them last
4. **Build after every file change** — not after every batch
5. **Test manually after every phase** — verify: file open, PIP, fullscreen, playlist, audio, subtitles

---

## Appendix A: Reference Projects & Patterns

### A.1 Avalonia Media Player Reference Architectures

| Project | Architecture | Notes |
|---------|-------------|-------|
| [byko-dev/music-player](https://github.com/byko-dev/music-player) | Services/ + Repository/ + Database/ + UI/ | Clean 4-layer split: UI → Services → Repository → Models |
| [VisioForge SimplePlayerMVVM](https://www.visioforge.com/help/docs/dotnet/mediaplayer/guides/avalonia-player) | ViewModels + Services + Platform projects | MainViewModel injects MediaPlayerCoreX, all playback via service |
| [Avalonia MediaPlayer (Accelerate)](https://docs.avaloniaui.net/accelerate/components/media-player/quickstart) | ViewModel + MediaPlayer backend | Uses async commands (`PlayAsync`, `PauseAsync`) |
| [Avalonia.Skia+FFmpeg](https://blog.csdn.net/gitblog_01027/article/details/151489906) | Custom control + frame rendering | Low-level approach — not applicable to Cine |

### A.2 Key Principles Applied

1. **Single Responsibility** — Per [Clean Architecture](https://www.yosuke4061.com/new_toppage/article.html?id=midori_new013), each class has exactly one reason to change
2. **Interface Segregation** — Small interfaces (`ISessionService`, `IPlaylistService`) instead of one giant `IMediaPlayerService`
3. **Dependency Inversion** — ViewModel depends on `ISessionService` (abstraction), not `SessionManager` (concrete)
4. **Layered Architecture** — Per [Fowler's layered architecture](https://woodruff.dev/stop-letting-your-controllers-talk-to-sql-layered-architecture-in-asp-net-core/): Presentation → Application → Domain → Infrastructure

### A.3 Files Not to Touch

These files are **well-separated already** and should NOT be part of this refactoring:

| File | Reason |
|------|--------|
| `MpvPlayer.cs` | Infrastructure — well-separated, already good |
| `MediaFoundationPlayer.cs` | Infrastructure — well-separated |
| `MpvInterop.cs`, `AngleInterop.cs` | P/Invoke — platform boundary |
| `PlaylistSettingsStore.cs` | Pure data persistence |
| `AudioSettingsStore.cs` | Pure data persistence |
| `SubtitleSettingsStore.cs` | Pure data persistence |
| `FileDialogHandler.cs` | Already centralized (our Phase 0 work) |
| `StartPage.axaml` | Simple start screen — doesn't need refactoring |
| `PiPWindow.axaml` | Self-contained window — doesn't need refactoring |
| `AboutDialog.axaml` | Static dialog — doesn't need refactoring |

---

## Appendix B: Before/After Comparison

### B.1 File Count

| Category | Before | After | Reduction |
|----------|--------|-------|-----------|
| MainWindow partials | 10 | 3 | -70% |
| MainViewModel partials | 3 | 5 (+1) | +66% but each is 1/3 the size |
| Service files | 7 | 13 (+6) | +85% (new abstractions) |
| Interface files | 1 | 7 (+6) | +600% |
| **Total code files** | ~75 | ~84 | +12% (acceptable) |

### B.2 Lines Per File (Average)

| File | Before | After |
|------|--------|-------|
| MainWindow partial | 240 | 200 |
| MainViewModel partial | 600 | 150 |
| Service file | 150 | 120 |
| New interface file | N/A | 15 |

### B.3 Testability Score

| Component | Before | After |
|-----------|--------|-------|
| Session persistence | 0/10 (in ViewModel) | 10/10 (ISessionService mockable) |
| Playlist logic | 0/10 (in ViewModel) | 10/10 (IPlaylistService mockable) |
| Renderer switching | 0/10 (in ViewModel) | 10/10 (IRendererService mockable) |
| Media file ops | 0/10 (in ViewModel) | 8/10 (IMediaFileService mockable) |
| Audio manager | 7/10 (no interface) | 9/10 (IAudioManager mockable) |
| Subtitle manager | 7/10 (no interface) | 9/10 (ISubtitleManager mockable) |

---

## References

1. [C# Partial Class Usage Guidelines (Microsoft)](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes)
2. [MVVM Pattern Avalonia Best Practices](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/)
3. [Fowler's Layered Architecture (Practical Example)](https://woodruff.dev/stop-letting-your-controllers-talk-to-sql-layered-architecture-in-asp-net-core/)
4. [Clean Architecture + MVVM + Repository (.NET)](https://www.yosuke4061.com/new_toppage/article.html?id=midori_new013)
5. [WPF/MVVM Practical Folder Structure (Japanese, C#)](https://prota-p.com/csharp_wpf10_mvvm2/)
6. [Avalonia Media Player SDK Architecture (VisioForge)](https://www.visioforge.com/help/docs/dotnet/mediaplayer/guides/avalonia-player)
7. [Avalonia Standard Project Structure (claude-skills)](https://github.com/markpitt/claude-skills/blob/main/skills/avalonia/SKILL.md)
8. [byko-dev/music-player — Avalonia Music Player Reference](https://github.com/byko-dev/music-player)
9. [Avalonia MediaPlayer Accelerate Docs](https://docs.avaloniaui.net/accelerate/components/media-player/quickstart)
10. [C# ASP.NET Layered Architecture Guidelines](https://blog.csdn.net/William_cl/article/details/160450171)
