# Cine Architecture

## Layered Architecture

Cine follows a strict 3-layer architecture:

```
┌──────────────────────────────────────────────────────────────┐
│                   UI Layer (Avalonia)                         │
│  MainWindow (8 partials)   Controls   Dialogs   StartPage    │
│  HeaderBar   ControlsBox   PipWindow   PlaylistDialog        │
├──────────────────────────────────────────────────────────────┤
│                  ViewModel / Service Layer                    │
│  MainViewModel (7 partials)   Services   Managers            │
│  PlaylistCoordinator   SessionManager   PipWindowManager     │
│  FileDialogHandler     InputRoutingService                   │
├──────────────────────────────────────────────────────────────┤
│                   Media Abstraction Layer                     │
│  IMediaPlayer ← MpvPlayer / MediaFoundationPlayer            │
│  Events: PositionChanged, PlaybackStateChanged, etc.         │
├──────────────────────────────────────────────────────────────┤
│                   Native / OS Layer                           │
│  libmpv (DLL)   ANGLE (D3D11/OpenGL)   Windows Registry      │
└──────────────────────────────────────────────────────────────┘
```

### Layer Rules

1. **UI Layer** knows about ViewModels only (MVVM data-binding). No direct service access.
2. **ViewModel Layer** orchestrates services. No Avalonia control references.
3. **Service Layer** contains business logic. Tests use mocks for all dependencies.
4. **Media Layer** exposes `IMediaPlayer` interface. All playback goes through this.

## Project Dependencies

```
App (net10.0-windows)
├── Core (net10.0)          — Logging, Config, Shared Models
├── Media (net10.0-windows) — IMediaPlayer + Implementations
│   └── (no deps)
└── NuGet Packages:
    ├── Avalonia 12.0.3
    ├── Avalonia.Desktop 12.0.3
    ├── Avalonia.Themes.Fluent 12.0.3
    ├── Material.Icons.Avalonia 3.0.2
    ├── Microsoft.Extensions.DependencyInjection 9.0.3
    └── Native DLLs: libmpv-2.dll, libEGL.dll, libGLESv2.dll

Tests (net10.0-windows)
├── App, Core, Media (ProjectReferences)
├── xUnit 2.9.3
├── NSubstitute 5.3.0
├── Shouldly 4.3.0
└── Avalonia.Headless 12.0.3
```

## MainViewModel — Partial File Layout

The MainViewModel is split across 7 partial files for maintainability:

| File | Responsibility |
|------|---------------|
| `MainViewModel.cs` | Constructor, INPC properties, command declarations |
| `MainViewModel.Actions.cs` | OpenFile, OpenFiles, drag-drop, session resume |
| `MainViewModel.Playback.cs` | Play/Pause/Stop, seek, speed, volume, mute |
| `MainViewModel.Playlist.cs` | Playlist CRUD, navigation, persistence, recent files |
| `MainViewModel.Renderer.cs` | Renderer mode switching |
| `MainViewModel.Tracks.cs` | Audio/subtitle track selection, loading |
| `MainViewModel.Video.cs` | Video filters: contrast, brightness, crop, rotation, zoom |

## MainWindow — Partial File Layout

| File | Responsibility |
|------|---------------|
| `MainWindow.Core.cs` | Constructor, service DI, event wiring, flag definitions |
| `MainWindow.Initialization.cs` | OnOpened, OnWindowInitialized, InitVideoRenderer |
| `MainWindow.Input.cs` | Keyboard shortcut registration (50+ bindings) |
| `MainWindow.MediaEvents.cs` | MediaOpened, PositionChanged, PlaybackStateChanged |
| `MainWindow.Pip.cs` | PiP toggle, frame forwarding, event sync |
| `MainWindow.State.cs` | Window state persistence, property watchers, OSD |
| `MainWindow.WindowControls.cs` | Min/Max/Close, fullscreen toggle |
| `MainWindow.axaml` | XAML layout |

## Service Architecture

### Service Interfaces (all in `Cine.Avalonia.Services`)

| Interface | Implementation | Purpose |
|---|---|---|
| `IPlaylistService` | `PlaylistCoordinator` | Playlist CRUD, navigation, shuffle, persistence |
| `ISessionService` | `SessionManager` | Save/load session: file path, position, tracks |
| `IAudioManager` | `AudioManager` | Volume, EQ, audio track selection |
| `ISubtitleManager` | `SubtitleManager` | Track selection, delay, font, position |
| `IRendererService` | `RendererCoordinator` | Renderer mode switching (Auto/Software) |
| `IMediaFileService` | `MediaFileService` | File validation, media filtering, screenshot paths |
| `IFileDialogService` | `FileDialogService` | File picker dialogs (wraps FileDialogHandler) |
| `IPipService` | `PipService` | PiP lifecycle, frame sharing |
| `IPipWindow` | `PipWindow` | PiP window abstraction |
| `IRegistryService` | `WindowsRegistryService` | File association registry operations |

### Manager classes (in `Cine.Avalonia.Managers`)

| Class | Purpose |
|---|---|
| `AudioManager` | Volume, EQ band adjustment, track selection |
| `SubtitleManager` | Delay, font, track cycling, external loading |
| `VideoManager` | Video filter coordination |
| `PlaybackStateManager` | UI state machine for play/pause/buffering/error |
| `PlaylistSettingsStore` | Playlist JSON persistence |
| `AudioSettingsStore` | EQ settings JSON persistence |
| `SubtitleSettingsStore` | Subtitle defaults JSON persistence |

## Threading Model

```
┌────────────────────────────────────────────────────┐
│                  UI Thread                           │
│  (Dispatcher.UIThread)                               │
│  All Avalonia controls, ViewModel properties,        │
│  commands, MainWindow events                        │
├────────────────────────────────────────────────────┤
│              mpv Render Thread                       │
│  (Dedicated Thread in MpvVideoView)                  │
│  OpenGL context, frame rendering, ANGLE swap         │
│  Required by libmpv render API                       │
├────────────────────────────────────────────────────┤
│              .NET ThreadPool                         │
│  Task.Run() for:                                     │
│  - File I/O (dialog results, playlist save/load)    │
│  - Screenshot capture                                │
│  - Debounced UI updates (double-tap detection)      │
│  - Popup auto-close timer (PlaylistDialog)          │
├────────────────────────────────────────────────────┤
│              mpv Event Loop (MpvPlayer)              │
│  Dedicated Task reading mpv event queue via          │
│  mpv_wait_event().                                  │
│  Dispatches to UI thread via Post.                  │
└────────────────────────────────────────────────────┘
```

### Thread Safety Rules

1. **Always use `Dispatcher.UIThread.Post/InvokeAsync`** when updating UI-bound properties from non-UI threads.
2. **`MpvVideoView.RenderLoop`** is thread-affine to the mpv render context — never call mpv render API from other threads.
3. **CancellationTokenSource** tied to window lifetime prevents delayed UI updates after close.
4. **PlayerService** uses timeout + `CancellationToken` for player init/shutdown.
5. **ConfigService** uses `lock()` for thread-safe config reads/writes.

## Data Flow — File Open

```
User clicks "Open" or Ctrl+O
        │
        ▼
MainWindow.Input.cs
  → InputRoutingService.TryHandle
  → MainViewModel.OpenCommand
        │
        ▼
MainViewModel.Actions.cs
  → IFileDialogService.OpenFilesAsync()
  → FileDialogHandler → StorageProvider (native OS dialog)
  → returns string[] paths
        │
        ▼
  → IMediaFileService.FilterMediaFiles(paths)
  → MediaFileService validates extensions
        │
        ▼
  → InsertAfterCurrent(path) (playlist)
  → OpenFile(path)
        │
        ▼
  → _player.Open(path)
  → MpvPlayer.Open() → mpv command string
  → mpv loads file → fires FILE_LOADED event
        │
        ▼
  → MainWindow.MediaEvents.OnMediaOpened
  → Updates UI: title, position, duration, chapters
  → Starts playback
```

## Data Flow — PiP Mode

```
User presses Ctrl+P or PiP button
        │
        ▼
PipWindowManager.TogglePip()
  → IPipService.EnterPip()
  → Creates PipWindow
  → Starts frame forwarding timer
        │
        ▼
Frame Sharing (every ~33ms / ~30fps throttled)
  → MpvVideoView provides raw byte[] frame
  → PipService.SendFrame() → Dispatcher.UIThread.Post
  → PipWindow.UpdateFrame(byte[] pixels, width, height)
  → WriteableBitmap.Lock() + Buffer.MemoryCopy
  → Image.Source = bitmap
```

## JSON Serialization Strategy

Reflection-free serialization via `System.Text.Json` source generation:

```csharp
// CineJsonContext.cs — source-generated context
[JsonSerializable(typeof(PlaylistData))]
[JsonSerializable(typeof(PipState))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class CineJsonContext : JsonSerializerContext { }

// Usage — no reflection
JsonSerializer.Serialize(data, CineJsonContext.Default.PlaylistData);
JsonSerializer.Deserialize(json, CineJsonContext.Default.PipState);
```

## Error Handling Strategy

Six-layer exception defense in `App.Main()`:

| Layer | Handler | Behavior |
|---|---|---|
| 4 | `AppDomain.UnhandledException` | Dumps crash info, process terminates |
| 3 | `TaskScheduler.UnobservedTaskException` | Logs, marks observed — prevents teardown |
| 2 | `async void` handlers | Caught in `OpenFile` try/catch → `OnError` event |
| 1 | `try/catch` in every user-facing command | Logs warning, returns null/fallback |
| 0 | Catch blocks in Disposers | Log error, never throw |
| -1 | CrashReporter | Writes `crash_*.txt` with stack trace + context |

### Service-level policy

- **User-initiated operations**: catch → log warning → return null/fallback
- **Background operations**: catch → log error → do not rethrow
- **Dispose/cleanup**: catch → log error → never throw (would crash finalizer)
- **Critical invariants**: throw after logging

## Testing Strategy

### Test categories

| Category | Location | Count | Tech |
|---|---|---|---|
| Service unit tests | `tests/Cine.Tests/Services/` | ~100 | NSubstitute + Shouldly |
| Manager unit tests | `tests/Cine.Tests/Managers/` | ~80 | NSubstitute + Shouldly |
| ViewModel unit tests | `tests/Cine.Tests/ViewModels/` | ~40 | NSubstitute + Shouldly |
| Headless UI tests | `tests/Cine.Tests/Headless/` | ~10 | Avalonia.Headless |
| Perf benchmarks | `tests/Cine.Benchmarks/` | ~20 | BenchmarkDotNet |

### Key testing patterns

- **Service interfaces** enable full mocking: all 10+ service interfaces have mock-friendly contracts
- **FileDialogHandler** wraps native `StorageProvider` — `TopLevel` can be stubbed in tests
- **PlayerService** uses `IPlayerFactory` — tests inject `MpvPlayerFactory` or custom mock
- **HeadlessFixture** initializes Avalonia platform once per test collection via `HeadlessUnitTestSession`

## Keyboard Shortcut System

`InputRoutingService` provides scope-aware routing:

```csharp
// Registration (in MainWindow.Input.cs)
_router.Register(Key.Space, () => _viewModel?.TogglePlayPause(), scope: InputScope.Normal);

// Scope prevents shortcuts when dialog is open
_router.CurrentScope = InputScope.DialogOpen; // blocks all Normal-scoped shortcuts
```

### Scopes

| Scope | Active When |
|---|---|
| `Normal` | Default playback |
| `DialogOpen` | Any modal dialog open |
| `Fullscreen` | Fullscreen mode |
| `PipActive` | PiP window active |

## Build and Packaging

### Development
```powershell
dotnet build src\App\App.csproj        # Debug build
dotnet run --project src\App\App.csproj # Launch
dotnet test tests\Cine.Tests\           # Run all 270+ tests
```

### Release packaging via WiX
- **MSI**: `installer/CineMsi/` — product.wxs with file associations
- **Bootstrapper**: `installer/CineBootstrapper/` — checks for .NET runtime
- **Custom theme**: Dark theme for installer UI

### CI/CD (GitHub Actions)
- Build and test on push/PR to main/develop
- Windows-latest runner with .NET 10
- Test results uploaded as build artifacts
