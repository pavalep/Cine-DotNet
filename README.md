# Cine Media Player

A modern, high-performance media player for Windows built with **Avalonia UI** and **libmpv**. Features hardware-accelerated video rendering via D3D11/ANGLE, a full-featured playlist with shuffle/loop, Picture-in-Picture mode, extensive subtitle and audio track support, and video filter adjustments (contrast, brightness, gamma, crop, rotation).

## Features

- **Dual playback engine**: libmpv (primary) and Media Foundation (fallback)
- **Hardware acceleration**: D3D11 rendering via ANGLE/OpenGL ES
- **Picture-in-Picture (PiP)**: detached overlay window with aspect-ratio-locked resize
- **Playlist management**: drag-to-reorder, shuffle, loop modes (file/playlist), M3U export
- **Subtitle support**: SRT/ASS/SSA/VTT, external loading, delay adjustment, style customization
- **Audio support**: multi-track selection, external audio loading, delay adjustment, EQ
- **Video filters**: contrast, brightness, gamma, saturation, hue, zoom, aspect ratio, crop, rotation, flip
- **Session resume**: remembers last-played file, position, subtitle/audio tracks, renderer mode
- **Keyboard shortcuts**: 50+ bindings with scope-aware routing (dialog/fullscreen/PiP modes)
- **File association**: one-click registration as default player for 30+ formats
- **Crash recovery**: automatic log dumps, corrupted-config restoration, six-layer exception defense
- **Screenshot capture**: with/without subtitles, configurable output directory
- **Custom theming**: dark UI theme with Material Design icons

## Screenshots

*(screenshots to be added)*

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                    Cine.sln                          │
│  ┌────────────┐  ┌────────────┐  ┌───────────────┐  │
│  │  App.dll   │  │  Core.dll  │  │  Media.dll    │  │
│  │ (Avalonia  │  │ (Logging,  │  │ (mpv/MF      │  │
│  │  UI + VM)  │  │  Config)   │  │  backends)    │  │
│  └─────┬──────┘  └─────┬──────┘  └──────┬────────┘  │
│        │               │                │           │
│        └───────────────┴────────────────┘           │
│                          │                          │
│                    ┌─────┴─────┐                    │
│                    │  libmpv   │                    │
│                    │  (native) │                    │
│                    └───────────┘                    │
└─────────────────────────────────────────────────────┘
```

- **App.dll**: Avalonia UI shell, ViewModels, service layer, managers
- **Core.dll**: Logging (`Log.ForContext<T>()`), configuration, shared models
- **Media.dll**: `IMediaPlayer` interface + mpv and Media Foundation implementations

## Prerequisites

| Dependency | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.x | [Download](https://dotnet.microsoft.com/download) |
| Windows | 10+ | Required for Media Foundation + ANGLE |
| libmpv | 2.x | Bundled in `resources/libmpv-2_x86-64/` |

## Quick Start

```powershell
git clone https://github.com/user/Cine
cd Cine

# Restore & build
dotnet restore src\App\App.csproj
dotnet build src\App\App.csproj

# Run
dotnet run --project src\App\App.csproj

# Run tests
dotnet test tests\Cine.Tests\Cine.Tests.csproj

# Run benchmarks
dotnet run --project tests\Cine.Benchmarks\Cine.Benchmarks.csproj -c Release
```

## Project Structure

```
src/
├── App/                          # Avalonia UI application
│   ├── Application/              #   ViewModels, Services, Managers
│   │   ├── Managers/             #     AudioManager, SubtitleManager, VideoManager
│   │   ├── Services/             #     PlaylistCoordinator, FileDialogHandler, PipService
│   │   ├── Serialization/        #     CineJsonContext (source-gen), PipState
│   │   └── ViewModels/           #     MainViewModel (7 partial files)
│   ├── Controls/                 #   MpvVideoView (native rendering host)
│   ├── UI/                       #   XAML views, styles, resources
│   │   ├── Controls/             #     SeekBar, SubtitleOverlay, AudioSelector
│   │   ├── Screens/              #     StartPage, PipWindow, PlaylistDialog
│   │   ├── Shell/                #     MainWindow partials (8 files)
│   │   └── Styles/               #     Colors, Typography, Elevation, Spacing
│   └── App.axaml.cs              #   Entry point with DI container
├── Core/                         # Shared infrastructure
│   ├── Interfaces/               #   IConfigService, ILoggingService
│   ├── Models/                   #   AppSettings
│   └── Services/                 #   LoggingService, ConfigService, FileLogger
├── Media/                        # Media playback backends
│   ├── Interfaces/               #   IMediaPlayer (152-line contract)
│   ├── Implementations/
│   │   ├── mpv/                  #   MpvPlayer (ANGLE/D3D11 rendering)
│   │   └── mediafoundationplayer/#   MediaFoundationPlayer (native MF path)
│   ├── Events/                   #   15 event arg types
│   └── Models/                   #   PlaybackState, VideoTrackInfo, etc.
├── MediaSmoke/                   # Quick media-playback integration test
tests/
├── Cine.Tests/                   # 270 xUnit tests
│   ├── Headless/                 #   Avalonia headless UI tests
│   ├── Infrastructure/           #   HeadlessFixture
│   ├── Managers/                 #   Audio, PlaybackState, Playlist, Subtitle, Video
│   ├── Services/                 #   PlaylistCoordinator, SessionManager, CrashReporter
│   └── ViewModels/               #   MainViewModel, PlaylistDialogHelpers
└── Cine.Benchmarks/              # BenchmarkDotNet performance benchmarks
```

## Key Technologies

| Technology | Purpose |
|---|---|
| [Avalonia 12.0](https://www.avaloniaui.net/) | Cross-platform UI framework |
| [libmpv](https://mpv.io/) | Video playback engine |
| [ANGLE](https://chromium.googlesource.com/angle/angle/) | OpenGL ES → D3D11 translation |
| [Material.Icons](https://github.com/AvaloniaUtils/Material.Icons.Avalonia) | Icon set |
| [xUnit](https://xunit.net/) | Unit testing |
| [NSubstitute](https://nsubstitute.github.io/) | Mocking |
| [Shouldly](https://docs.shouldly.org/) | Assertion library |
| [BenchmarkDotNet](https://benchmarkdotnet.org/) | Performance benchmarking |
| [WiX v4](https://wixtoolset.org/) | Windows installer |

## Keyboard Shortcuts

| Key | Action |
|---|---|
| Space | Play/Pause |
| F | Toggle fullscreen |
| M | Toggle mute |
| Up/Down | Volume ±10% |
| Left/Right | Seek ±5s |
| Ctrl+Left/Right | Seek ±30s |
| Ctrl+O | Open files |
| Ctrl+P | Open folder |
| Escape | Exit fullscreen / Close PiP |

See [docs/keyboard-shortcuts.md](docs/keyboard-shortcuts.md) for the full list.

## Build Configurations

| Configuration | Purpose | Defines |
|---|---|---|
| `Debug` | Development with debug tools | `DEBUG` |
| `Release` | Production build | (none) |

## License

This project is licensed under the terms of the LICENSE file included in the repository.
