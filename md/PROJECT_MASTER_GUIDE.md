# Project Master Guide

This file consolidates all project architecture, UI analysis, implementation planning, and status details.

## 2026-05-27 Architecture Update (Avalonia-First)

- Active projects are now organized under `src/`:
  - `src/App` (Avalonia UI app)
  - `src/Core` (core logic)
  - `src/Media` (media engine)
- Avalonia app internals follow a layered MVVM-oriented structure:
  - `UI/Views`, `UI/Components`, `UI/Resources`
  - `Application/ViewModels`, `Application/Converters`, `Application/Services`
  - `Infrastructure/Api`
- Legacy WinUI code was moved under `legacy/Cine.WinUI`.
- Solution and build scripts were updated to use the new paths.
- Build validation after refactor: `dotnet build Cine.sln` succeeded (0 errors).

## 2026-05-27 UI Audit Update

- A dedicated UI parity tracker has been created: `md/UI_MISMATCH_TRACKER.md`.
- It contains Python GTK4 vs Avalonia mismatch findings organized by implementation phase:
  - Phase 1 (P0): critical parity blockers
  - Phase 2 (P1): interaction/behavior parity
  - Phase 3 (P2): visual/styling parity
  - Phase 4 (P3): integration/command parity
- Use that file as the primary execution checklist for upcoming UI alignment work.

---

## CONSOLIDATED GUIDE

# Cine — Windows Native Port (Consolidated Guide)

## Purpose
Produce a **100% native Windows application** in **C# using .NET 10** (Windows Forms).

**Critical Note:**
✅ **NO Python files should remain in the final implementation**
❌ Python code is **NOT** part of the Windows Native product

**code_for_reference/ Folder Purpose:**
The `code_for_reference/` folder contains a **read-only Python snapshot** from the original Cine source:
- Used ONLY for behavioral verification and asset reference
- **Never deployed** or included in the final build
- **Never executed** — pure read-only documentation

**Final Deliverable:**
- **100% C# codebase**
- **Zero Python dependencies**
- **Zero Python files in deployment**
- **Pure Windows executable** (single-file or MSI installer)

---

## Complete Project Structure (source files only)

```
Windows-Native/
│
├── Cine.sln                                  # Solution file (VS 2026, v18.6)
│
├── Cine.Core/                                # Business logic layer (net10.0)
│   ├── Cine.Core.csproj                      # Project file
│   ├── Interfaces/
│   │   ├── IConfigService.cs                 # Configuration abstraction
│   │   └── ILoggingService.cs                # Logging abstraction
│   ├── Models/
│   │   └── AppSettings.cs                    # Settings data model
│   └── Services/
│       ├── ConfigService.cs                  # Configuration implementation
│       ├── LoggingService.cs                 # Logging implementation
│       └── StartupManager.cs                 # Startup orchestration
│
├── Cine.Media/                               # Media playback layer (net10.0-windows)
│   ├── Cine.Media.csproj                     # Project file
│   ├── Events/
│   │   ├── MediaEventArgs.cs                 # File event args (start-file, file-loaded, end-file)
│   │   ├── PlaybackStateEventArgs.cs         # Pause/unpause/stop state change args
│   │   ├── PositionChangedEventArgs.cs       # Time-pos property observer args
│   │   ├── DurationChangedEventArgs.cs       # Duration property observer args
│   │   ├── VolumeChangedEventArgs.cs         # Volume property observer args
│   ├── Interfaces/
│   │   ├── IMediaPlayer.cs                   # Player interface
│   │   ├── IVideoRenderer.cs                 # Video renderer interface
│   │   └── IAudioRenderer.cs                 # Audio renderer interface
│   ├── Models/
│   │   ├── ChapterInfo.cs                    # Chapter data model
│   │   └── PlaylistItem.cs                   # Playlist item model
│   └── Implementations/
│       ├── MediaFoundationPlayer.cs          # MF-based player
│       ├── D3D11Renderer.cs                  # D3D11 video renderer
│       ├── MfComInterop.cs                   # COM interop definitions
│       ├── MfHelper.cs                        # MF helper class
│       └── AudioRenderer.cs                  # WASAPI audio renderer
│
├── Cine.Avalonia/                            # Avalonia UI (net10.0)
│   ├── Cine.Avalonia.csproj                  # Project file
│   ├── App.axaml                             # Application definition
│   ├── App.axaml.cs                          # Application code-behind
│   ├── MainWindow.axaml                      # Main window XAML
│   ├── MainWindow.axaml.cs                  # Main window code-behind
│   ├── Controls/
│   │   ├── D3D11VideoHost.cs                 # D3D11 video host control
│   │   ├── StartPage.xaml                    # Start page with drag-and-drop
│   │   └── StartPage.xaml.cs                 # Start page code-behind
│   ├── Converters/
│   │   ├── TimeSpanToStringConverter.cs      # TimeSpan to string converter
│   │   ├── PercentConverter.cs               # Percent converter
│   │   └── ChapterMarginConverter.cs         # Chapter margin converter
│   ├── ViewModels/
│   │   ├── MainViewModel.cs                  # Main view model
│   │   └── PlayerService.cs                  # Player service wrapper
│   ├── Resources/
│   │   ├── Colors.axaml                      # Color resources (Python GTK4 matching)
│   │   ├── Typography.axaml                  # Typography resources
│   │   ├── Icons.axaml                       # Icon geometries
│   │   ├── ButtonStyles.axaml                # Button style resources
│   │   └── Effects.axaml                     # Effects and animations
│   └── Styles/
│       └── App.axaml                         # Application styles and resource includes
│
├── code_for_reference/                       # Python reference (read-only)
│   ├── src/
│   │   ├── window.ui                         # GTK4 UI definition
│   │   ├── style.css                         # Python CSS styles
│   │   └── ...                              # Other reference files
│   └── README.md                             # Reference documentation
│
├── Docs/                                     # Project documentation
│   ├── CONSOLIDATED_GUIDE.md                 # This file — everything in one place
│   ├── UI_MISMATCH_ANALYSIS.md               # UI analysis reference (10-section mismatch study)
│   ├── UI_ALIGNMENT_SOLUTIONS.md             # UI alignment solutions reference
│   └── UI_ALIGNMENT_IMPLEMENTATION_PLAN.md   # 4-week implementation plan reference
│
├── .gitignore
└── README.md
```

---

## Build & Environment

| Metric | Value |
|--------|-------|
| **Last Build** | ✅ 0 Errors, 2 Warnings (CS8500 — acceptable for interop) |
| **Build Command** | `dotnet build Cine.WinUI/Cine.WinUI.csproj` |
| **Date** | 2026-05-26 |
| **Phase 3 Status** | ✅ COMPLETED — NV12→BGRA shader pipeline implemented |

---

## Phase 1: Core Foundation (COMPLETED ✅)
- [x] Solution structure with 3 projects (Core, Media, Avalonia)
- [x] Cine.Core class library with interfaces and services
- [x] Cine.Media player engine with IMediaPlayer interface
- [x] D3D11Renderer with GPU-accelerated frame presentation
- [x] MfHelper with Source Reader pipeline
- [x] MediaFoundationPlayer with native D3D11 interop
- [x] WASAPI audio renderer
- [x] Full keyboard shortcut system (matching Python INTERNAL_BINDINGS)

## Phase 2: UI Framework Setup (COMPLETED ✅)
- [x] Cine.Avalonia project created with net10.0 target
- [x] D3D11VideoHost control for native rendering
- [x] MainViewModel with player binding
- [x] Basic window layout with video panel
- [x] PlayerService wrapper for MVVM

## Phase 3: Video Rendering Pipeline (COMPLETED ✅)
- [x] NV12→BGRA shader pipeline implemented
- [x] Auto-detection of decoder output format
- [x] Hardware decoding with D3D11 interop
- [x] Build: 0 errors, 2 warnings (CS8500 acceptable)

## Phase 4: Feature Completion (COMPLETED ✅)
- [x] Duration tracking via IMFPresentationDescriptor
- [x] Seeking via IMFMediaSource.Start()
- [x] Screenshot functionality
- [x] Chapter navigation (Next/Previous)
- [x] Rewind/Forward (Shift+←/→)
- [x] Speed control (+/- 0.1x)

## Phase 5: UI Pixel-Perfect Alignment ✅ (COMPLETED — Phase 1+2+3)

### 5.1 — Resource Dictionaries Created
- [x] `Resources/Colors.axaml` — Full palette matching Python GTK4/Adwaita
- [x] `Resources/Typography.axaml` — Consolas + Segoe UI families
- [x] `Resources/Icons.axaml` — All symbolic icon geometries
- [x] `Resources/ButtonStyles.axaml` — Circular buttons with hover/pressed/checked states
- [x] `Resources/Effects.axaml` — OSD styles, shadows, animations

### 5.2 — Layout & Controls
- [x] Overlay-based design matching Python's `GtkOverlay` structure
- [x] **Start Page / Drag & Drop** overlay with `e.DataTransfer` for Avalonia 11/12
- [x] Header bar with Open menu, PIP toggle, primary menu
- [x] Circular transport controls (Previous, Rewind, Play/Pause, Stop, Forward, Next)
- [x] Media type menu buttons (Subtitles, Audio, Video tracks)
- [x] Custom seek bar with progress fill, thumb, and chapter markers
- [x] Volume popover with mute toggle and volume slider
- [x] Position display overlay (bottom-left)
- [x] Chapter badge overlay (top-right)
- [x] OSD notification system

### 5.3 — UI Auto-Hide & Revealer Animations
- [x] **3-second timeout** matching Python GTK4 revealer behavior
- [x] **Fade-in animation**: 350ms SineEaseOut
- [x] **Fade-out animation**: 300ms SineEaseIn + 350ms delayed hide
- [x] **Hover detection**: Controls stay visible while mouse is over them
- [x] **No Media state**: UI controls stay permanently visible until a file is loaded
- [x] **Manual toggle**: `ToggleUiControls()` for keyboard shortcut support
- [x] Mouse movement tracking across entire window
- [x] Pointer enter/leave events on controls overlay

### 5.4 — Application & Styles
- [x] `App.axaml` — All resource dictionaries merged via `StyleInclude`
- [x] Global styles for Window, buttons, sliders, and text
- [x] Circular button templates with proper states (hover/pressed/checked/disabled)
- [x] Custom slider templates for seek and volume

### 5.5 — Code-Behind & ViewModel
- [x] `MainWindow.axaml.cs` — All new event handlers added
- [x] `MainViewModel.cs` — `ChapterMarkers` collection, new commands
- [x] `TimeSpanToStringConverter.cs` — `ChapterMarginConverter` added
- [x] `Cine.Avalonia.csproj` — `UseWPF=false`, resource includes

### 5.6 — Technical Specifications
- **Color System**: Exact CSS rgba values from Python converted to Avalonia Color resources
  - Header gradient: rgba(0,0,0,0.14) → rgba(0,0,0,0)
  - Controls gradient: rgba(0,0,0,0.2) → rgba(0,0,0,0)
  - OSD background: `#CC000000`
  - Button hover: rgba(255,255,255,0.17)
  - Button active: rgba(255,255,255,0.25)
- **Typography**: Consolas for numeric/time displays, Segoe UI for interface text
- **Layout**: Overlay-based with gradient backgrounds matching Python's header/controls
- **Buttons**: 40×40 circular buttons with transparent hover/active states
- **Seek Bar**: Custom-drawn trough (rgba(255,255,255,0.225)) + filled progress + circular thumb
- **Volume**: Popover-style control matching Python's menu button + scale pattern
- **Window**: Default 800×600 (matches Python), minimum 332×187
- **Transparency**: Blur transparency enabled for modern glassmorphism look
- **Auto-hide**: 3-second timeout with SineEaseIn/SineEaseOut animations
- **Revealer**: Fade-in 350ms, fade-out 300ms + 350ms delayed hide

### 5.7 — Remaining Tasks
- [ ] Implement playlist controls (shuffle, loop, playlist dialog)
- [ ] Implement options menu and PIP toggle
- [ ] Comprehensive testing and validation against Python reference

## Phase 6: Final Polish & Ship 📝
- [ ] Single-file publish: `dotnet publish -c Release -r win-x64 --self-contained true`
- [ ] MSIX or Inno Setup installer
- [ ] Integration tests (all 50+ keybindings)
- [ ] Performance profiling (GPU frame timing)
- [ ] End-user documentation

---

## Current State

| Component | Status | Notes |
|-----------|--------|-------|
| **Solution/project scaffolding** | ✅ Done | Cine.sln with 3 projects |
| **Cine.Core** (business logic) | ✅ Built | net10.0, 0 errors |
| **Cine.Media** (playback layer) | ✅ Built | net10.0-windows, 0 errors, 0 warnings |
| **Cine.Media player engine** | ✅ Built | 737+ lines, 50+ methods, 30+ properties, 15+ events |
| **Cine.Avalonia** (Avalonia UI) | 🔄 In Progress | New `Cine.Avalonia` project with Avalonia.Desktop target |
| **D3D11 Renderer** | ✅ Complete | GPU-accelerated with NV12→BGRA shader pipeline |
| **Media Foundation Pipeline** | ✅ Complete | Source Reader, COM interop, WASAPI audio |
| **Keyboard Shortcuts** | ✅ Complete | All 20+ shortcuts matching Python bindings |
| **UI Resource Dictionaries** | ✅ Complete | Colors, Typography, Icons, ButtonStyles, Effects |
| **UI Layout (MainWindow)** | ✅ Complete | Overlay-based design matching Python GTK4 |
| **UI Auto-Hide** | ✅ Complete | 3-second timeout with revealer-style fade animations |
| **Revealer Animations** | ✅ Complete | Fade-in 350ms, fade-out 300ms with delayed hide |
| **Hover Detection** | ✅ Complete | Controls stay visible while mouse is over them |
| **UI Mismatch Analysis** | ✅ Complete | Comprehensive analysis of Python vs Avalonia UI differences |
| **UI Alignment Solutions** | ✅ Complete | Complete technical implementation guide for pixel-perfect matching |
| **UI Implementation Plan** | ✅ Complete | 4-week phased plan for UI alignment |
| **Build pipeline (CLI)** | ✅ Verified | `dotnet build Cine.sln` → 0 errors, 0 warnings |
| **Type safety** | ✅ Achieved | Nullable enabled throughout |

---

## Technology Choices

| Layer | Technology | Reason |
|-------|-----------|--------|
| **UI** | Avalonia UI (net10.0) | Pixel-perfect cross-platform rendering, native HWND interop for D3D11, modern XAML/C#, Fluent design language |
| **Media** | Native MediaFoundation + D3D11 | Hardware decoding (DXVA2/D3D11VA), GPU shader pipeline, WASAPI low-latency audio |
| **Core logic** | .NET 10 class library | Cross-platform, no Windows-only dependencies |
| **Build** | `dotnet build` CLI | Works reliably; VS 2026 at custom path missing AppX targets |
| **Python Reference** | GTK4/Adwaita | UI behavior and visual design reference only — never deployed |

---

## NV12→BGRA Shader Pipeline Details

### Dual Rendering Paths in `D3D11Renderer.cs`
- **BGRA-direct path** (default): decoder outputs RGB32/BGRA → memcpy to back buffer
- **NV12→BGRA shader path**: decoder outputs NV12 → pixel shader converts YUV to BGRA
- `UseNv12ShaderPath` property toggles between paths (must be set before `Initialize()`)

### Shader Pipeline Components
- **Vertex shader**: full-screen quad with UV coordinates
- **Pixel shader**: NV12 → RGB conversion using BT.601 color matrix
- **Input layout**: vertex position + texture coordinates
- **Vertex buffer**: 4 vertices for triangle strip rendering
- **Shader resource views**: separate SRVs for Y and UV planes
- **Sampler state**: linear filtering with clamp addressing

### Texture Management
- **Default textures** (GPU): `_yDefaultTex`, `_uvDefaultTex` for shader sampling
- **Staging textures** (CPU write): `_yStagingTex`, `_uvStagingTex` for NV12 upload
- **Dynamic resizing**: textures recreated when video dimensions change

### COM Interface Updates in `MfComInterop.cs`
- Added missing structs: `D3D11_INPUT_ELEMENT_DESC`, `D3D11_SUBRESOURCE_DATA`, `D3D11_SHADER_RESOURCE_VIEW_DESC`, `D3D11_TEX2D_SRV`
- Added missing COM methods: `CreateShaderResourceView` to `ID3D11Device`, `VSSetShader` to `ID3D11DeviceContext`
- Fixed `CreateInputLayout` method signature to match native D3D11

### Technical Details
- **NV12 format**: Y plane (full resolution, R8_UNORM) + interleaved UV plane (half resolution, R8G8_UNORM)
- **Shader compilation**: inline HLSL compiled at runtime via `D3DCompile` from `d3dcompiler_47.dll`
- **Upload pipeline**: `IMFSample` → lock buffer → copy Y/UV planes to staging textures → `CopyResource` to GPU textures
- **Rendering**: set shaders, SRVs, sampler → draw 4-vertex triangle strip → present swap chain

### Build Status
- **Errors**: 0 ✅
- **Warnings**: 2 (CS8500 — pointers to managed types in `fixed` statement) — acceptable for interop code
- **Functionality**: Complete NV12→BGRA conversion pipeline ready for testing

### Next Steps
- **Auto-detection**: Add logic to choose shader vs RGB32 path based on decoder output format
- **Testing**: Verify color accuracy with various video files
- **Optimization**: Profile GPU texture upload and shader performance

---

## UI Auto-Hide Implementation Details

### Architecture
```
MainWindow.axaml.cs
├── InitializeAutoHide()
│   ├── DispatcherTimer (3s interval)
│   ├── PointerMoved event handler
│   ├── PointerEnter on UiControlsOverlay
│   └── PointerLeave on UiControlsOverlay
├── ShowUiControls() ──→ Fade-in 350ms (SineEaseOut)
├── HideUiControls() ──→ Fade-out 300ms (SineEaseIn) + 350ms delay
├── SetUiControlsVisibility(bool) ──→ Direct set (no animation)
└── ToggleUiControls() ──→ Manual show/hide
```

### Timer Behavior
| Event | Action |
|-------|--------|
| Mouse moves > 1px | Show UI, restart timer |
| Timer expires (3s) | Hide UI (if not hovering) |
| Mouse enters controls overlay | Cancel timer, keep UI visible |
| Mouse leaves controls overlay | Start timer, schedule hide |
| Fullscreen mode | Behavior unchanged (timer continues) |

### Animation Parameters
| Animation | Duration | Easing | Delay |
|-----------|----------|--------|-------|
| Fade-in (show) | 350ms | SineEaseOut | 0ms |
| Fade-out (hide) | 300ms | SineEaseIn | +350ms (stays visible) |
| Total hide cycle | 650ms | — | UI invisible at 650ms |

### Event Wiring
```csharp
// In InitializeAutoHide():
PointerMoved += OnWindowPointerMoved;
UiControlsOverlay.PointerEnter += OnControlsPointerEnter;
UiControlsOverlay.PointerLeave += OnControlsPointerLeave;
```

---

## Keyboard Shortcuts (Complete Reference)
| Key | Action | Python Match |
|-----|--------|-------------|
| Space | Play/Pause | ✅ |
| F / F11 | Fullscreen | ✅ |
| M | Mute | ✅ |
| ← / → | Seek ±5s | ✅ |
| Shift+←/→ | Seek ±60s | ✅ |
| ↑ / ↓ | Volume ±5 | ✅ |
| ] / . | Speed +0.1 | ✅ |
| [ / , | Speed -0.1 | ✅ |
| Backspace | Reset speed | ✅ |
| L | Loop file | ✅ |
| Ctrl+L | Loop playlist | ✅ |
| S | Screenshot | ✅ |
| P | Next chapter | ✅ |
| Shift+P | Previous chapter | ✅ |
| PgDown | Next playlist item | ✅ |
| PgUp | Previous playlist item | ✅ |
| Esc | Stop (normal) / Exit fullscreen | ✅ |
| ←/→ on controls visible | Seek (no fullscreen needed) | ✅ |

---

## Resource Dictionary Reference

### Colors.axaml — Semantic Color Mapping
| Semantic Name | Value | Python CSS Match |
|---------------|-------|------------------|
| `Black` | `#000000` | `black` |
| `White` | `#FFFFFF` | `white` |
| `Gray100` | `#E5E5E5` | `--light-color` |
| `Gray800` | `#2D2D30` | `--darkest-color` |
| `Gray900` | `#1E1E1E` | `--dark-color` |
| `Gray950` | `#131313` | `--inverted-color` |
| `Accent` | `#0078D4` | `--accent-color` |
| `OsdForeground` | `#FFFFFF` (opaque) | OSD text |
| `OsdBackground` | `#CC000000` | OSD bg rgba(0,0,0,0.8) |
| `HeaderGradient` | Linear gradient | header-gradient |
| `ControlsGradient` | Linear gradient | controls-gradient |
| `ButtonHoverBackground` | `#2BFFFFFF` | button-hover |
| `ButtonActiveBackground` | `#40FFFFFF` | button-active |
| `ProgressTroughBackground` | `#39FFFFFF` | trough |
| `PopoverBackground` | `#F2F2F2` | popover bg |
| `PopoverBorder` | `#BFBFBF` | popover border |
| `StatusBarBackgroundColor` | `#CC000000` | statusbar bg rgba(0,0,0,0.8) |
| `TimeSeparatorBackground` | `#DDDDDD` | time separator |

### Typography.axaml — Font Specifications
| Style | Font Family | Size | Notes |
|-------|-------------|------|-------|
| `time-label` | Consolas, Courier New, monospace | 13px | Time display |
| `time-elapsed` | (inherits time-label) | 13px | With right margin -7px |
| `time-duration` | (inherits time-label) | 13px | With left margin -7px |
| `chapter-badge` | Segoe UI, system-ui | 12px | Chapter name display |
| `header-title` | Segoe UI, system-ui | 14px Medium | Window title |
| `speed-display` | Consolas, Courier New, monospace | 12px | Speed indicator |
| `toolbar-text` | Segoe UI, system-ui | 11px | Toolbar labels |
| `status-text` | Consolas, Courier New, monospace | 11px | Status bar |

### Icons.axaml — Glyph Reference
| Icon Name | Glyph Path | Usage |
|-----------|------------|-------|
| `PlayIcon` | `M 8 5V 19L 16 12L 8 5 Z` | Play button |
| `PauseIcon` | `M 8 4H 12V 20H 8V 4 Z M 16 4H 20V 20H 16V 4 Z` | Pause button |
| `StopIcon` | `M 5 4H 9V 20H 5V 4 Z M 13 4H 17V 20H 13V 4 Z` | Stop button |
| `SkipBackwardIcon` | `M 15.41 7.41L 14 6L 8 12L 14 18L 15.41 16.59L 10.83 12L 15.41 7.41 Z` | Previous chapter |
| `SkipForwardIcon` | `M 10 6L 8.59 7.41L 13.17 12L 8.59 16.59L 10 18L 16 12L 10 6 Z` | Next chapter |
| `PreviousChapterIcon` | `M 16 5V 19L 8 12L 16 5 Z` | Previous (large) |
| `NextChapterIcon` | `M 8 5V 19L 16 12L 8 5 Z` | Next (large) |
| `VolumeMaxIcon` | Complex path | Volume high |
| `VolumeMediumIcon` | Complex path | Volume medium |
| `VolumeLowIcon` | Complex path | Volume low |
| `VolumeMuteIcon` | Complex path | Volume muted |
| `ScreenshotIcon` | `M 4 4H 20V 20H 4V 4 Z...` | Screenshot |
| `FullscreenEnterIcon` | Complex path | Enter fullscreen |
| `FullscreenExitIcon` | Complex path | Exit fullscreen |
| `PipIcon` | `M 6 4H 22V 16H 22V 4H 6V 4 Z M 10 8H 18V 12H 10V 8 Z` | Picture-in-Picture |
| `MenuIcon` | Three ellipses | More menu |
| `RewindIcon` | Complex path | Rewind (Shift+←) |
| `FastForwardIcon` | Complex path | Fast forward (Shift+→) |

### ButtonStyles.axaml — Circular Button Specs
| Property | Value |
|----------|-------|
| `Width` | 40px |
| `Height` | 40px |
| `CornerRadius` | 20px (circular) |
| `Background` | Transparent |
| `BorderThickness` | 0 |
| `Padding` | 0 |
| `HorizontalContentAlignment` | Center |
| `VerticalContentAlignment` | Center |
| **Hover state** | `#2BFFFFFF` (rgba(255,255,255,0.17)) |
| **Pressed state** | `#40FFFFFF` (rgba(255,255,255,0.25)) |
| **Checked (toggle)** | `#FFFFFFFF` (white bg) + black icon |
| **Disabled** | Opacity 0.4 |

### Effects.axaml — OSD Overlay Styles
| Element | Style |
|---------|-------|
| Pause indicator | Centered, 48px icon, bg rgba(0,0,0,0.5), corner radius 8 |
| Loading spinner | Centered, 90px diameter, white foreground |
| Drop indicator | Full size, rgba(0,0,0,0.6), blue border |
| OSD notification | Bottom-center, rgba(0,0,0,0.8), 16px padding |
| Video position overlay | Bottom-left, rgba(0,0,0,0.73), 8px padding |
| Chapter badge overlay | Top-right, rgba(0,0,0,0.73), 8px padding |
| Drop shadow | rgba(0,0,0,0.5), offset 1px, blur 2px |

---

## Implementation Architecture
```
Cine.Avalonia/
├── App.axaml ────────────── Merges all ResourceDictionaries
│   ├── Colors.axaml ─────── Semantic color definitions
│   ├── Typography.axaml ─── Font families & text styles
│   ├── Icons.axaml ───────── Path geometries for all icons
│   ├── ButtonStyles.axaml ─ Circular button control templates
│   └── Effects.axaml ─────── OSD overlays, shadows, animations
│
├── MainWindow.axaml ─────── Overlay-based layout
│   ├── VideoHost ─────────── D3D11 rendering surface
│   ├── PauseIndicator ────── Centered pause icon (hidden)
│   ├── UiControlsOverlay ─── Auto-hiding controls container
│   │   ├── HeaderBar ─────── Open menu, title, PIP, menu buttons
│   │   └── ControlsBox ───── Transport row + seek bar + time labels
│   ├── PositionOverlay ───── Bottom-left time display
│   ├── ChapterBadge ──────── Top-right chapter name
│   └── OSDNotification ───── Fade-in/out system messages
│
├── MainWindow.axaml.cs ──── Window code-behind
│   ├── InitializeAutoHide() ─ Timer + pointer tracking
│   ├── ShowUiControls() ──── Fade-in animation
│   ├── HideUiControls() ──── Fade-out animation + delayed hide
│   ├── ToggleUiControls() ── Manual toggle via keyboard
│   └── All button click handlers
│
├── MainViewModel.cs ─────── MVVM ViewModel
│   ├── ChapterMarkers ────── DoubleCollection for seek bar
│   ├── RefreshState() ────── Populates chapters + markers
│   └── All player commands
│
└── Converters/
    ├── TimeSpanToStringConverter.cs ── TimeSpan → HH:MM:SS
    ├── PercentConverter.cs ──────────── Double → % string or Thickness
    └── ChapterMarginConverter.cs ────── Double → Thickness for seek bar
```

---

## Build Commands
```bash
# Build entire solution
dotnet build Cine.sln

# Build specific project
dotnet build Cine.Avalonia/Cine.Avalonia.csproj

# Run
dotnet run --project Cine.Avalonia/Cine.Avalonia.csproj

# Publish (single-file)
dotnet publish -c Release -r win-x64 --self-contained true
```


---

## UI MISMATCH ANALYSIS

# UI Mismatch Analysis: Python (GTK4) vs Avalonia

## Executive Summary
This document analyzes the UI differences between the Python (GTK4/Adwaita) reference implementation and the current Avalonia implementation. The goal is to identify mismatches and provide solutions for pixel-perfect alignment.

## 1. Layout Structure Mismatches

### Python (GTK4) Layout
- **Overlay-based design**: Uses `GtkOverlay` with revealers for UI elements
- **Responsive breakpoints**: `AdwBreakpoint` at 495sp for mobile/tablet adaptation
- **Gradient backgrounds**: Linear gradients for header and controls
- **OSD (On-Screen Display) style**: Semi-transparent overlays with text shadows
- **Video area**: Pure black background with overlay controls

### Avalonia Layout
- **Fixed toolbar layout**: Static top toolbar, video area, bottom seek bar
- **No responsive design**: Fixed minimum sizes (640x360)
- **Flat color backgrounds**: Solid colors (#1E1E1E, #2D2D30, #E0000000)
- **Separated controls**: Toolbar at top, seek bar at bottom
- **Video area**: Black background with position/chapter overlays

## 2. Control Placement & Hierarchy

### Python Control Hierarchy
```
AdwApplicationWindow
└── AdwToastOverlay
    └── GtkWindowHandle
        └── GtkOverlay (video_overlay)
            ├── GtkRevealer (pause_indicator) [overlay]
            ├── AdwSpinner [overlay]
            ├── GtkRevealer (ui) [overlay]
            │   └── GtkBox (header-and-controls)
            │       ├── AdwHeaderBar
            │       │   ├── GtkMenuButton (open_menu_button)
            │       │   ├── GtkToggleButton (pip_button)
            │       │   └── GtkMenuButton (primary_menu_button)
            │       ├── GtkSeparator (spacer)
            │       └── GtkBox (controls_box)
            │           ├── AdwWrapBox (control buttons)
            │           └── GtkBox (progress controls)
            ├── AdwStatusPage (start_page) [overlay]
            └── GtkRevealer (drop_indicator) [overlay]
```

### Avalonia Control Hierarchy
```
Window
└── Grid (3 rows)
    ├── Border (top toolbar)
    │   └── StackPanel (horizontal buttons)
    ├── Grid (video area)
    │   ├── D3D11VideoHost
    │   ├── Border (position overlay)
    │   └── Border (chapter badge)
    └── Border (seek bar)
        └── Grid (3 columns)
            ├── TextBlock (position)
            ├── Slider (seek)
            └── TextBlock (duration)
```

## 3. Visual Design Differences

### Color Scheme
| Component | Python (GTK4) | Avalonia |
|-----------|---------------|----------|
| Window Background | Transparent/Theme | #1E1E1E |
| Toolbar Background | Gradient: rgba(0,0,0,0.14) → transparent | #2D2D30 |
| Controls Background | Gradient: rgba(0,0,0,0.2) → transparent | #E0000000 (80% black) |
| Text Color | White with shadow | White (no shadow) |
| Button Hover | rgba(255,255,255,0.17) | #3E3E40 |
| Button Active | rgba(255,255,255,0.25) | #5A5A5A |

### Typography
| Element | Python (GTK4) | Avalonia |
|---------|---------------|----------|
| Time Labels | `heading` + `numeric` classes, Consolas | Consolas, 13px |
| General Text | System font with shadows | System font, 14px |
| Button Icons | Symbolic icons (cine-* names) | Path data (SVG-like) |

### Spacing & Sizing
| Component | Python (GTK4) | Avalonia |
|-----------|---------------|----------|
| Button Size | Circular, ~40px diameter | Rectangular, 32x28px |
| Button Spacing | 4px child-spacing | 4-8px margins |
| Progress Bar Height | Custom scale with 20px slider | Standard slider, 32px height |
| Header Height | AdwHeaderBar auto | ~40px (Border + StackPanel) |

## 4. Icon System Mismatch

### Python Icon System
- **Symbolic icons**: Uses `icon-name` property with `cine-*` prefix
- **Standardized sizes**: `-gtk-icon-size` CSS property
- **Shadow effects**: `-gtk-icon-shadow` for depth
- **Icon set**: Comprehensive cine-specific icons (volume, playback, playlist, etc.)

### Avalonia Icon System
- **Path data**: Inline SVG-like path definitions
- **No standardization**: Each icon defined individually
- **No shadows**: Flat fill colors
- **Limited set**: Basic transport controls only

## 5. Interactive Behavior Differences

### UI Visibility
| Behavior | Python (GTK4) | Avalonia |
|----------|---------------|----------|
| UI Auto-hide | Revealer with 300ms transition | Always visible |
| Pause Indicator | Revealer with 350ms transition | Not implemented |
| Drop Indicator | Revealer with 200ms transition | Not implemented |
| Start Page | AdwStatusPage overlay | Not implemented |

### Control States
| State | Python (GTK4) | Avalonia |
|-------|---------------|----------|
| Disabled Buttons | Icon shadow only | Standard disabled state |
| Toggle Buttons | White background when checked | No visual difference |
| Hover Effects | Subtle transparency | Solid color change |
| Active Effects | Slightly darker | Different solid color |

## 6. Missing Features in Avalonia

### Complete UI Components Missing
1. **Start Page**: Drag-and-drop area with "Open" buttons
2. **Menu System**: File menu, primary menu button
3. **Volume Popover**: Mute toggle + volume scale in popover
4. **Track Menus**: Subtitles, audio tracks, video tracks menus
5. **Playlist Controls**: Shuffle, loop, playlist dialog button
6. **Options Menu**: Comprehensive settings menu
7. **Picture-in-Picture**: PIP toggle button
8. **Spinner**: Loading animation overlay
9. **Breakpoint System**: Responsive design adaptation

### Visual Effects Missing
1. **Gradients**: Linear gradient backgrounds
2. **Shadows**: Text and icon shadows for depth
3. **Transitions**: Smooth revealer animations
4. **OSD Style**: On-screen display aesthetic
5. **Circular Buttons**: Rounded transport controls

## 7. Detailed Component Comparison

### Progress/Seek Bar
**Python**:
- Custom `GtkScale` with white slider (20px diameter)
- Trough: rgba(255,255,255,0.225)
- Marks with white color and shadow
- Integrated time labels (elapsed/total) with separator

**Avalonia**:
- Standard `Slider` control
- Solid colors (default theme)
- Separate time labels in Grid columns
- No visual styling customization

### Volume Control
**Python**:
- MenuButton with popover containing:
  - Mute toggle button (circular)
  - Volume scale (180px width, 0-130 range)
- Icon changes based on volume level

**Avalonia**:
- Inline slider (120px width, 0-150 range)
- Separate mute button
- Static icon regardless of volume

### Transport Controls
**Python**:
- Previous/PlayPause/Next buttons in `AdwWrapBox`
- Circular buttons with flat style
- Icons: `cine-skip-*-symbolic`
- Tooltips with keyboard shortcuts

**Avalonia**:
- Play/Pause, Stop, Seek Back/Forward buttons
- Rectangular buttons with path icons
- Custom path data for each icon
- Tooltips with descriptions only

## 8. Screen Real Estate Analysis

### Python Default Layout
- Window: 800x600 (default), 332x187 (minimum)
- Video area: Full window minus overlay margins
- Controls: Overlay (disappears when not needed)
- Efficient use of space with responsive design

### Avalonia Default Layout
- Window: 1280x720 (default), 640x360 (minimum)
- Video area: Middle row of Grid
- Controls: Fixed toolbars (always visible)
- Less efficient space usage, especially at smaller sizes

## 9. Accessibility Considerations

### Python Advantages
- **Text shadows**: Better contrast on video backgrounds
- **Larger touch targets**: Circular buttons (40px diameter)
- **Keyboard navigation**: Full menu system with accelerators
- **Screen reader support**: GTK4 built-in accessibility

### Avalonia Limitations
- **No text shadows**: Potential contrast issues
- **Smaller buttons**: 32x28px rectangular targets
- **Limited keyboard support**: Basic transport controls only
- **Unknown accessibility**: Custom controls may lack proper support

## 10. Platform Consistency

### Python (GTK4/Adwaita)
- Follows GNOME Human Interface Guidelines
- Consistent with Linux desktop ecosystems
- Adaptive to system theme preferences
- Standardized component behavior

### Avalonia (Custom)
- Windows-centric design patterns
- Custom styling not aligned with any platform guidelines
- Mixed metaphors (some GTK-like, some Windows-like)
- Inconsistent component behavior

## Conclusion

The Avalonia implementation lacks the sophistication, polish, and feature completeness of the Python reference implementation. Key areas requiring alignment include:

1. **Visual design**: Gradients, shadows, and OSD styling
2. **Layout structure**: Overlay-based UI with revealers
3. **Component completeness**: Missing menus, popovers, and specialized controls
4. **Interactive behavior**: Transitions, auto-hide, and responsive design
5. **Icon system**: Symbolic icons with standardized sizing

The following sections will provide detailed solutions for achieving pixel-perfect matching between the two implementations.

---

## UI ALIGNMENT SOLUTIONS

# UI Alignment Solutions: Pixel-Perfect Matching

## Overview
This document provides detailed solutions to align the Avalonia implementation with the Python (GTK4) reference implementation. Each solution includes specific implementation steps, code examples, and visual specifications.

## 1. Foundation: Color System & Typography

### Color Palette Implementation
Create a centralized color resource file:

```xml
<!-- Colors.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Base Colors -->
    <Color x:Key="Black">#000000</Color>
    <Color x:Key="White">#FFFFFF</Color>
    <Color x:Key="Gray100">#E5E5E5</Color>
    <Color x:Key="Gray800">#202021</Color>
    <Color x:Key="Gray900">#19191B</Color>
    
    <!-- OSD (On-Screen Display) Colors -->
    <SolidColorBrush x:Key="OsdForeground" Color="{StaticResource White}" />
    <LinearGradientBrush x:Key="HeaderGradient" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Offset="0" Color="#24000000" />   <!-- rgba(0,0,0,0.14) -->
        <GradientStop Offset="0.4" Color="#14000000" /> <!-- rgba(0,0,0,0.08) -->
        <GradientStop Offset="1" Color="#00000000" />   <!-- transparent -->
    </LinearGradientBrush>
    
    <LinearGradientBrush x:Key="ControlsGradient" StartPoint="0,1" EndPoint="0,0">
        <GradientStop Offset="0" Color="#33000000" />   <!-- rgba(0,0,0,0.2) -->
        <GradientStop Offset="0.4" Color="#1A000000" /> <!-- rgba(0,0,0,0.1) -->
        <GradientStop Offset="1" Color="#00000000" />   <!-- transparent -->
    </LinearGradientBrush>
    
    <!-- Button States -->
    <SolidColorBrush x:Key="ButtonHoverBackground" Color="#2BFFFFFF" /> <!-- rgba(255,255,255,0.17) -->
    <SolidColorBrush x:Key="ButtonActiveBackground" Color="#40FFFFFF" /> <!-- rgba(255,255,255,0.25) -->
    <SolidColorBrush x:Key="ToggleButtonCheckedBackground" Color="{StaticResource White}" />
    
    <!-- Progress/Seek Bar -->
    <SolidColorBrush x:Key="ProgressTroughBackground" Color="#39FFFFFF" /> <!-- rgba(255,255,255,0.225) -->
    <SolidColorBrush x:Key="ProgressSliderBackground" Color="{StaticResource White}" />
    <SolidColorBrush x:Key="TimeSeparatorBackground" Color="#DDDDDD" />
    
</ResourceDictionary>
```

### Typography System
```xml
<!-- Typography.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Font Families -->
    <FontFamily x:Key="NumericFont">Consolas, Courier New, monospace</FontFamily>
    <FontFamily x:Key="SystemFont">Segoe UI, system-ui, sans-serif</FontFamily>
    
    <!-- Text Styles -->
    <Style Selector="TextBlock.time-label">
        <Setter Property="FontFamily" Value="{StaticResource NumericFont}" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="Foreground" Value="{StaticResource OsdForeground}" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>
    
    <Style Selector="TextBlock.time-elapsed">
        <Setter Property="Style" Value="{StaticResource time-label}" />
        <Setter Property="Margin" Value="0,0,-7,0" />
    </Style>
    
    <Style Selector="TextBlock.heading">
        <Setter Property="FontWeight" Value="Medium" />
        <Setter Property="Foreground" Value="{StaticResource OsdForeground}" />
    </Style>
    
</ResourceDictionary>
```

## 2. Layout Reconstruction: Overlay-Based Design

### Main Window Structure
Replace current Grid layout with Overlay-based design:

```xml
<!-- MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Cine.Avalonia.ViewModels"
        xmlns:conv="using:Cine.Avalonia.Converters"
        xmlns:ctrl="using:Cine.Avalonia.Controls"
        x:Class="Cine.Avalonia.MainWindow"
        x:DataType="vm:MainViewModel"
        Title="Cine"
        Width="800" Height="600"
        MinWidth="332" MinHeight="187"
        Background="Transparent"
        WindowStartupLocation="CenterScreen"
        ExtendClientAreaToDecorationsHint="True"
        TransparencyLevelHint="Blur">

    <Window.Resources>
        <conv:TimeSpanToStringConverter x:Key="TimeSpanToString" />
        <conv:PercentConverter x:Key="PercentConverter" />
    </Window.Resources>

    <!-- Main Overlay Container -->
    <Overlay x:Name="MainOverlay" Background="Black">
        
        <!-- Video Host (Primary Content) -->
        <ctrl:D3D11VideoHost x:Name="VideoHost" />
        
        <!-- Pause Indicator (Overlay) -->
        <Border x:Name="PauseIndicator" 
                HorizontalAlignment="Center" VerticalAlignment="Center"
                Opacity="0" IsVisible="False"
                CornerRadius="8" Padding="16"
                Background="#80000000">
            <Path Data="M 8 5V 19L 16 12L 8 5 Z"
                  Width="72" Height="72" Stretch="Uniform"
                  Fill="White" />
        </Border>
        
        <!-- Loading Spinner (Overlay) -->
        <ProgressRing x:Name="LoadingSpinner"
                      Width="90" Height="90"
                      HorizontalAlignment="Center" VerticalAlignment="Center"
                      IsVisible="False"
                      Foreground="White" />
        
        <!-- UI Controls (Overlay with Revealer Behavior) -->
        <Border x:Name="UiControls" 
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                Opacity="0" IsVisible="False"
                Background="Transparent">
            
            <!-- Combined Header & Controls Gradient -->
            <Border Background="{StaticResource HeaderAndControlsGradient}">
                
                <!-- Header Bar -->
                <Border x:Name="HeaderBar" 
                        Height="50" VerticalAlignment="Top"
                        Background="{StaticResource HeaderGradient}">
                    <Grid Margin="12,0">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        
                        <!-- Open Menu Button -->
                        <Button x:Name="BtnOpenMenu" Grid.Column="0"
                                Style="{StaticResource CircularMenuButton}"
                                Content="Open"
                                IsVisible="{Binding !IsStartPageVisible}">
                            <Button.Flyout>
                                <MenuFlyout Placement="Bottom">
                                    <MenuItem Header="Open Files" 
                                              Command="{Binding OpenFilesCommand}" />
                                    <MenuItem Header="Open Folder" 
                                              Command="{Binding OpenFolderCommand}" />
                                    <MenuItem Header="Add Files" 
                                              Command="{Binding AddFilesCommand}" />
                                    <Separator />
                                    <MenuItem Header="Add Subtitle Track" 
                                              Command="{Binding AddSubtitleCommand}" />
                                    <MenuItem Header="Add Audio Track" 
                                              Command="{Binding AddAudioCommand}" />
                                </MenuFlyout>
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Window Title (Centered) -->
                        <TextBlock Grid.Column="1" 
                                   Text="Cine"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Foreground="White"
                                   FontSize="14" FontWeight="Medium" />
                        
                        <!-- PIP Button -->
                        <ToggleButton x:Name="BtnPip" Grid.Column="2"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Picture-in-Picture">
                            <Path Data="{StaticResource PipIcon}" />
                        </ToggleButton>
                        
                        <!-- Primary Menu Button -->
                        <Button x:Name="BtnPrimaryMenu" Grid.Column="3"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Main Menu">
                            <Path Data="{StaticResource MenuIcon}" />
                            <Button.Flyout>
                                <MenuFlyout Placement="Bottom">
                                    <MenuItem Header="New Window" 
                                              Command="{Binding NewWindowCommand}" />
                                    <MenuItem Header="Preferences" 
                                              Command="{Binding PreferencesCommand}" />
                                    <Separator />
                                    <MenuItem Header="Keyboard Shortcuts" 
                                              Command="{Binding ShortcutsCommand}" />
                                    <MenuItem Header="About Cine" 
                                              Command="{Binding AboutCommand}" />
                                </MenuFlyout>
                            </Button.Flyout>
                        </Button>
                    </Grid>
                </Border>
                
                <!-- Spacer -->
                <Rectangle Height="1" Margin="0" Opacity="0" />
                
                <!-- Controls Box -->
                <Border x:Name="ControlsBox"
                        Height="120" VerticalAlignment="Bottom"
                        Background="{StaticResource ControlsGradient}"
                        Padding="0,0,0,20">
                    
                    <!-- Transport Controls -->
                    <WrapPanel HorizontalAlignment="Center" VerticalAlignment="Top"
                               Margin="13,10,13,0" Spacing="4">
                        
                        <!-- Previous Button -->
                        <Button x:Name="BtnPrevious" 
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Previous (Ctrl+Left)">
                            <Path Data="{StaticResource SkipBackwardIcon}" />
                        </Button>
                        
                        <!-- Play/Pause Button -->
                        <Button x:Name="BtnPlayPause" 
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Play/Pause (Space)">
                            <Path Data="{StaticResource PlayIcon}" 
                                  x:Name="PlayPauseIcon" />
                        </Button>
                        
                        <!-- Next Button -->
                        <Button x:Name="BtnNext" 
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Next (Ctrl+Right)">
                            <Path Data="{StaticResource SkipForwardIcon}" />
                        </Button>
                        
                        <!-- Volume Menu Button -->
                        <Button x:Name="BtnVolumeMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Volume">
                            <Path Data="{StaticResource VolumeMaxIcon}" 
                                  x:Name="VolumeIcon" />
                            <Button.Flyout>
                                <Popup Placement="Top">
                                    <Border Background="{StaticResource PopoverBackground}"
                                            CornerRadius="6" Padding="12"
                                            BorderThickness="1" 
                                            BorderBrush="{StaticResource PopoverBorder}">
                                        <StackPanel Spacing="8">
                                            <ToggleButton x:Name="BtnMuteToggle"
                                                          Style="{StaticResource CircularToggleButton}"
                                                          Content="M" />
                                            <Slider x:Name="VolumeSlider"
                                                    Width="180"
                                                    Minimum="0" Maximum="130"
                                                    Value="{Binding Volume}" />
                                        </StackPanel>
                                    </Border>
                                </Popup>
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Subtitles Menu Button -->
                        <Button x:Name="BtnSubtitlesMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Subtitles">
                            <Path Data="{StaticResource SubtitlesIcon}" />
                            <Button.Flyout>
                                <MenuFlyout ItemsSource="{Binding SubtitleTracks}" />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Audio Tracks Menu Button -->
                        <Button x:Name="BtnAudioTracksMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Audio Tracks">
                            <Path Data="{StaticResource AudioIcon}" />
                            <Button.Flyout>
                                <MenuFlyout ItemsSource="{Binding AudioTracks}" />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Video Tracks Menu Button -->
                        <Button x:Name="BtnVideoTracksMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Video Tracks">
                            <Path Data="{StaticResource VideoIcon}" />
                            <Button.Flyout>
                                <MenuFlyout ItemsSource="{Binding VideoTracks}" />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Separator (Expands) -->
                        <Rectangle Width="1" Opacity="0" />
                        
                        <!-- Playlist Shuffle Toggle -->
                        <ToggleButton x:Name="BtnPlaylistShuffle"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Shuffle Playlist">
                            <Path Data="{StaticResource PlaylistShuffleIcon}" />
                        </ToggleButton>
                        
                        <!-- Playlist Loop Toggle -->
                        <ToggleButton x:Name="BtnPlaylistLoop"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Loop Playlist">
                            <Path Data="{StaticResource PlaylistRepeatIcon}" />
                        </ToggleButton>
                        
                        <!-- File Loop Toggle -->
                        <ToggleButton x:Name="BtnFileLoop"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Loop File">
                            <Path Data="{StaticResource RepeatFileIcon}" />
                        </ToggleButton>
                        
                        <!-- Playlist Button -->
                        <Button x:Name="BtnPlaylist"
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Playlist"
                                Command="{Binding OpenPlaylistCommand}">
                            <Path Data="{StaticResource PlaylistIcon}" />
                        </Button>
                        
                        <!-- Options Menu Button -->
                        <Button x:Name="BtnOptionsMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Options">
                            <Path Data="{StaticResource OptionsIcon}" />
                            <Button.Flyout>
                                <OptionsFlyout />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Fullscreen Button -->
                        <Button x:Name="BtnFullscreen"
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Fullscreen (F)"
                                Command="{Binding ToggleFullscreenCommand}">
                            <Path Data="{StaticResource FullscreenIcon}" />
                        </Button>
                        
                    </WrapPanel>
                    
                    <!-- Progress Bar & Time -->
                    <Grid Margin="8,0,20,0" VerticalAlignment="Bottom">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        
                        <!-- Time Elapsed -->
                        <TextBlock Grid.Column="0"
                                   Text="{Binding PositionText}"
                                   Style="{StaticResource time-elapsed}" />
                        
                        <!-- Progress Scale -->
                        <Border Grid.Column="1" 
                                Margin="8,0,3,0"
                                VerticalAlignment="Center">
                            <Slider x:Name="ProgressSlider"
                                    Background="Transparent"
                                    Minimum="0" Maximum="1"
                                    Value="{Binding Progress}">
                                <Slider.Styles>
                                    <Style Selector="Slider">
                                        <Setter Property="Template">
                                            <ControlTemplate>
                                                <Grid>
                                                    <!-- Trough -->
                                                    <Border Name="PART_Track"
                                                            Height="4" CornerRadius="2"
                                                            Background="{StaticResource ProgressTroughBackground}" />
                                                    
                                                    <!-- Progress Fill -->
                                                    <Border Name="PART_Fill"
                                                            Height="4" CornerRadius="2"
                                                            Background="White" />
                                                    
                                                    <!-- Thumb -->
                                                    <Border Name="PART_Thumb"
                                                            Width="20" Height="20" CornerRadius="10"
                                                            Background="White"
                                                            BorderThickness="0"
                                                            HorizontalAlignment="Left"
                                                            VerticalAlignment="Center">
                                                        <Border.Shadow>
                                                            <BoxShadow Blur="4" Color="Black" Opacity="0.3" />
                                                        </Border.Shadow>
                                                    </Border>
                                                </Grid>
                                            </ControlTemplate>
                                        </Setter>
                                    </Style>
                                </Slider.Styles>
                            </Slider>
                        </Border>
                        
                        <!-- Time Separator -->
                        <Rectangle Grid.Column="1" 
                                   Width="2" Height="16"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Fill="{StaticResource TimeSeparatorBackground}"
                                   Opacity="0.4" />
                        
                        <!-- Time Total -->
                        <TextBlock Grid.Column="2"
                                   Text="{Binding DurationText}"
                                   Style="{StaticResource time-label}" />
                        
                    </Grid>
                </Border>
            </Border>
        </Border>
        
        <!-- Start Page (Overlay) -->
        <Border x:Name="StartPage"
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                Background="{StaticResource StartPageGradient}"
                IsVisible="{Binding IsStartPageVisible}">
            
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                        Spacing="12">
                
                <TextBlock Text="Drag and Drop Files Here"
                           FontSize="24" FontWeight="Medium"
                           Foreground="{StaticResource Gray100}" />
                
                <StackPanel Orientation="Horizontal" Spacing="12"
                            HorizontalAlignment="Center">
                    
                    <Button Content="Open…"
                            Style="{StaticResource PillButtonSuggested}"
                            Command="{Binding OpenFilesCommand}" />
                    
                    <Button Content="Open Folder"
                            Style="{StaticResource PillButton}"
                            Command="{Binding OpenFolderCommand}" />
                    
                </StackPanel>
            </StackPanel>
        </Border>
        
        <!-- Drop Indicator (Overlay) -->
        <Border x:Name="DropIndicator"
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                Margin="12" Padding="24"
                Background="{StaticResource DropIndicatorBackground}"
                BorderBrush="{StaticResource AccentColor}"
                BorderThickness="2" BorderDashArray="4,4"
                CornerRadius="7"
                Opacity="0" IsVisible="False">
            
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                        Spacing="12">
                
                <Path Data="{StaticResource DropIcon}"
                      Width="64" Height="64"
                      Fill="{StaticResource AccentColor}" />
                
                <TextBlock x:Name="DropLabel"
                           FontSize="20" FontWeight="Medium"
                           Foreground="{StaticResource AccentColor}" />
                
            </StackPanel>
        </Border>
        
    </Overlay>
</Window>
```

## 3. Component Styles: Button System

### Circular Button Styles
```xml
<!-- ButtonStyles.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Base Circular Button -->
    <Style Selector="Button.circular">
        <Setter Property="Width" Value="40" />
        <Setter Property="Height" Value="40" />
        <Setter Property="CornerRadius" Value="20" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="HorizontalContentAlignment" Value="Center" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        
        <!-- OSD Text Shadow -->
        <Setter Property="TextBlock.Foreground" Value="White" />
        <Setter Property="TextBlock.Shadow">
            <Shadow Blur="6" Color="Black" Opacity="0.6" OffsetX="0" OffsetY="1" />
        </Setter>
        
        <!-- Icon Shadow -->
        <Setter Property="Path.Shadow">
            <Shadow Blur="6" Color="Black" Opacity="0.6" OffsetX="0" OffsetY="1" />
        </Setter>
    </Style>
    
    <!-- Circular Flat Button (Transport Controls) -->
    <Style Selector="Button.circular.flat" BasedOn="{StaticResource circular}">
        <Setter Property="Background" Value="Transparent" />
    </Style>
    
    <Style Selector="Button.circular.flat:hover">
        <Setter Property="Background" Value="{StaticResource ButtonHoverBackground}" />
    </Style>
    
    <Style Selector="Button.circular.flat:pressed">
        <Setter Property="Background" Value="{StaticResource ButtonActiveBackground}" />
    </Style>
    
    <Style Selector="Button.circular.flat:disabled">
        <Setter Property="Opacity" Value="0.5" />
        <Setter Property="Path.Shadow">
            <Shadow Blur="5" Color="Black" Opacity="1" OffsetX="0" OffsetY="1" />
        </Setter>
    </Style>
    
    <!-- Circular Toggle Button -->
    <Style Selector="ToggleButton.circular.flat" BasedOn="{StaticResource circular}">
        <Setter Property="Background" Value="Transparent" />
    </Style>
    
    <Style Selector="ToggleButton.circular.flat:hover">
        <Setter Property="Background" Value="{StaticResource ButtonHoverBackground}" />
    </Style>
    
    <Style Selector="ToggleButton.circular.flat:pressed">
        <Setter Property="Background" Value="{StaticResource ButtonActiveBackground}" />
    </Style>
    
    <Style Selector="ToggleButton.circular.flat:checked">
        <Setter Property="Background" Value="{StaticResource ToggleButtonCheckedBackground}" />
        <Setter Property="TextBlock.Foreground" Value="Black" />
        <Setter Property="Path.Fill" Value="Black" />
        <Setter Property="Path.Shadow" Value="{x:Null}" />
        <Setter Property="TextBlock.Shadow" Value="{x:Null}" />
        <Setter Property="BoxShadow">
            <BoxShadow Blur="3" Color="Black" Opacity="0.2" OffsetY="1" />
        </Setter>
    </Style>
    
    <!-- Circular Menu Button -->
    <Style Selector="Button.circular.menu" BasedOn="{StaticResource circular}">
        <Setter Property="MinWidth" Value="80" />
        <Setter Property="Height" Value="32" />
        <Setter Property="CornerRadius" Value="16" />
        <Setter Property="Padding" Value="12,0" />
        <Setter Property="Background" Value="Transparent" />
    </Style>
    
    <!-- Pill Button (Start Page) -->
    <Style Selector="Button.pill">
        <Setter Property="Height" Value="40" />
        <Setter Property="CornerRadius" Value="20" />
        <Setter Property="Padding" Value="24,0" />
        <Setter Property="Background" Value="#1FFFFFFF" /> <!-- rgba(255,255,255,0.12) -->
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Foreground" Value="{StaticResource Gray100}" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontWeight" Value="Medium" />
    </Style>
    
    <Style Selector="Button.pill:hover">
        <Setter Property="Background" Value="#26FFFFFF" /> <!-- rgba(255,255,255,0.15) -->
    </Style>
    
    <Style Selector="Button.pill:pressed">
        <Setter Property="RenderTransform">
            <ScaleTransform ScaleX="0.98" ScaleY="0.98" />
        </Setter>
    </Style>
    
    <!-- Suggested Action Pill Button -->
    <Style Selector="Button.pill.suggested">
        <Setter Property="Background" Value="{StaticResource Gray100}" />
        <Setter Property="Foreground" Value="Black" />
    </Style>
    
    <Style Selector="Button.pill.suggested:hover">
        <Setter Property="Background" Value="White" />
    </Style>
    
</ResourceDictionary>
```

## 4. Icon System Implementation

### Icon Resource Dictionary
```xml
<!-- Icons.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Playback Icons -->
    <Geometry x:Key="PlayIcon">M 8 5V 19L 16 12L 8 5 Z</Geometry>
    <Geometry x:Key="PauseIcon">M 5 4H 9V 20H 5V 4 Z M 13 4H 17V 20H 13V 4 Z</Geometry>
    <Geometry x:Key="StopIcon">M 4 4H 20V 20H 4V 4 Z</Geometry>
    
    <!-- Skip Icons -->
    <Geometry x:Key="SkipBackwardIcon">M 15.41 7.41L 14 6L 8 12L 14 18L 15.41 16.59L 10.83 12L 15.41 7.41 Z</Geometry>
    <Geometry x:Key="SkipForwardIcon">M 10 6L 8.59 7.41L 13.17 12L 8.59 16.59L 10 18L 16 12L 10 6 Z</Geometry>
    
    <!-- Volume Icons (Multiple levels) -->
    <Geometry x:Key="VolumeMuteIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z</Geometry>
    <Geometry x:Key="VolumeLowIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z</Geometry>
    <Geometry x:Key="VolumeMidIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z M 18 9L 22 5V 19L 18 15Z</Geometry>
    <Geometry x:Key="VolumeMaxIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z M 18 9L 22 5V 19L 18 15Z M 23 9L 27 5V 19L 23 15Z</Geometry>
    <Geometry x:Key="VolumeOverampIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z M 18 9L 22 5V 19L 18 15Z M 23 9L 27 5V 19L 23 15Z M 28 9L 32 5V 19L 28 15Z</Geometry>
    
    <!-- Track Icons -->
    <Geometry x:Key="SubtitlesIcon">M 4 4H 20V 20H 4V 4 Z M 8 8H 10V 16H 8V 8 Z M 12 8H 14V 16H 12V 8 Z M 16 8H 18V 16H 16V 8 Z</Geometry>
    <Geometry x:Key="AudioIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z</Geometry>
    <Geometry x:Key="VideoIcon">M 4 4H 20V 20H 4V 4 Z M 8 8H 10V 16H 8V 8 Z M 12 8H 14V 16H 12V 8 Z M 16 8H 18V 16H 16V 8 Z</Geometry>
    
    <!-- Playlist Control Icons -->
    <Geometry x:Key="PlaylistShuffleIcon">M 10.59 9.17L 5.41 4L 4 5.41L 9.17 10.58L 10.59 9.17 Z M 14.5 4L 16.54 6.04L 4 18.59L 5.41 20L 17.96 7.46L 20 9.5V 4H 14.5 Z</Geometry>
    <Geometry x:Key="PlaylistRepeatIcon">M 7 7H 17V 10L 21 6L 17 2V 5H 5V 11H 7V 7 Z M 17 17H 7V 14L 3 18L 7 22V 19H 19V 13H 17V 17 Z</Geometry>
    <Geometry x:Key="RepeatFileIcon">M 17 17H 7V 14L 3 18L 7 22V 19H 19V 13H 17V 17 Z</Geometry>
    <Geometry x:Key="PlaylistIcon">M 4 4H 20V 20H 4V 4 Z M 8 8H 10V 16H 8V 8 Z M 12 8H 14V 16H 12V 8 Z M 16 8H 18V 16H 16V 8 Z</Geometry>
    
    <!-- Menu & Options Icons -->
    <Geometry x:Key="OptionsIcon">M 3 18H 21V 16H 3V 18 Z M 3 13H 21V 11H 3V 13 Z M 3 6V 8H 21V 6H 3 Z</Geometry>
    <Geometry x:Key="MenuIcon">M 3 18H 21V 16H 3V 18 Z M 3 13H 21V 11H 3V 13 Z M 3 6V 8H 21V 6H 3 Z</Geometry>
    
    <!-- View Icons -->
    <Geometry x:Key="FullscreenIcon">M 7 14H 5V 19H 10V 17H 7V 14 Z M 5 10H 7V 7H 10V 5H 5V 10 Z M 17 17H 14V 19H 19V 14H 17V 17 Z M 14 5V 7H 17V 10H 19V 5H 14 Z</Geometry>
    <Geometry x:Key="RestoreIcon">M 7 14H 5V 19H 10V 17H 7V 14 Z M 5 10H 7V 7H 10V 5H 5V 10 Z M 17 17H 14V 19H 19V 14H 17V 17 Z M 14 5V 7H 17V 10H 19V 5H 14 Z</Geometry>
    <Geometry x:Key="PipIcon">M 19 11H 13V 5H 19V 11 Z M 19 19H 13V 13H 19V 19 Z M 11 11H 5V 5H 11V 11 Z M 11 19H 5V 13H 11V 19 Z</Geometry>
    
    <!-- Drop & Status Icons -->
    <Geometry x:Key="DropIcon">M 19 13C 19.7 13 20.37 13.13 21 13.35V 8L 14 2H 6C 4.9 2 4.01 2.9 4.01 4L 4 20C 4 21.1 4.89 22 5.99 22H 13.54C 12.58 20.94 12 19.54 12 18C 12 15.24 14.24 13 17 13C 17.65 13 18.27 13.1 18.86 13.28L 19 13 Z M 14 3.5L 18.5 8H 14V 3.5 Z M 23 18C 23 20.21 21.21 22 19 22S 15 20.21 15 18C 15 15.79 16.79 14 19 14S 23 15.79 23 18 Z M 20.5 18.5L 18 21L 15.5 18.5L 16.21 17.79L 18 19.59L 19.79 17.79L 20.5 18.5 Z</Geometry>
    
    <!-- Icon Style for Consistent Sizing -->
    <Style Selector="Path.icon">
        <Setter Property="Width" Value="24" />
        <Setter Property="Height" Value="24" />
        <Setter Property="Stretch" Value="Uniform" />
        <Setter Property="Fill" Value="{DynamicResource SystemControlForegroundBaseHighBrush}" />
    </Style>
    
</ResourceDictionary>
```

## 5. Animation & Transition System

### Revealer Animation Behaviors
```csharp
// RevealerBehavior.cs
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;

namespace Cine.Avalonia.Behaviors
{
    public class RevealerBehavior : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsRevealedProperty =
            AvaloniaProperty.RegisterAttached<RevealerBehavior, Control, bool>(
                "IsRevealed", defaultValue: false);
        
        public static readonly AttachedProperty<int> TransitionDurationProperty =
            AvaloniaProperty.RegisterAttached<RevealerBehavior, Control, int>(
                "TransitionDuration", defaultValue: 300);
        
        public static readonly AttachedProperty<RevealerTransitionType> TransitionTypeProperty =
            AvaloniaProperty.RegisterAttached<RevealerBehavior, Control, RevealerTransitionType>(
                "TransitionType", defaultValue: RevealerTransitionType.SlideUp);
        
        static RevealerBehavior()
        {
            IsRevealedProperty.Changed.AddClassHandler<Control>(OnIsRevealedChanged);
        }
        
        public static bool GetIsRevealed(Control element) => element.GetValue(IsRevealedProperty);
        public static void SetIsRevealed(Control element, bool value) => element.SetValue(IsRevealedProperty, value);
        
        public static int GetTransitionDuration(Control element) => element.GetValue(TransitionDurationProperty);
        public static void SetTransitionDuration(Control element, int value) => element.SetValue(TransitionDurationProperty, value);
        
        public static RevealerTransitionType GetTransitionType(Control element) => element.GetValue(TransitionTypeProperty);
        public static void SetTransitionType(Control element, RevealerTransitionType value) => element.SetValue(TransitionTypeProperty, value);
        
        private static async void OnIsRevealedChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            var isRevealed = (bool)args.NewValue!;
            var duration = GetTransitionDuration(control);
            var transitionType = GetTransitionType(control);
            
            // Set initial state
            if (!isRevealed)
            {
                control.Opacity = 0;
                control.IsVisible = false;
                return;
            }
            
            // Make visible before animation
            control.IsVisible = true;
            
            // Create animation based on transition type
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(duration),
                Easing = new CubicEaseOut()
            };
            
            switch (transitionType)
            {
                case RevealerTransitionType.SlideUp:
                    var translateY = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
                    control.RenderTransform = translateY;
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters =
                            {
                                new Setter(Visual.OpacityProperty, 1.0),
                                new Setter(TranslateTransform.YProperty, 0.0)
                            }
                        });
                    break;
                    
                case RevealerTransitionType.Fade:
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters = { new Setter(Visual.OpacityProperty, 0.0) }
                        });
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(duration),
                            Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                        });
                    break;
                    
                case RevealerTransitionType.SlideDown:
                    var translateY2 = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
                    control.RenderTransform = translateY2;
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters = 
                            {
                                new Setter(Visual.OpacityProperty, 0.0),
                                new Setter(TranslateTransform.YProperty, -20.0)
                            }
                        });
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(duration),
                            Setters =
                            {
                                new Setter(Visual.OpacityProperty, 1.0),
                                new Setter(TranslateTransform.YProperty, 0.0)
                            }
                        });
                    break;
            }
            
            // Run animation
            await animation.RunAsync(control);
        }
    }
    
    public enum RevealerTransitionType
    {
        SlideUp,
        SlideDown,
        Fade,
        Crossfade
    }
}
```

### UI Auto-hide Behavior
```csharp
// UiAutoHideBehavior.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cine.Avalonia.Behaviors
{
    public class UiAutoHideBehavior : AvaloniaObject
    {
        private static CancellationTokenSource? _hideCts;
        private static DateTime _lastInteractionTime = DateTime.Now;
        
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<UiAutoHideBehavior, Control, bool>(
                "IsEnabled", defaultValue: false);
        
        public static readonly AttachedProperty<int> HideDelayProperty =
            AvaloniaProperty.RegisterAttached<UiAutoHideBehavior, Control, int>(
                "HideDelay", defaultValue: 2000); // 2 seconds
        
        public static readonly AttachedProperty<Control?> UiControlsProperty =
            AvaloniaProperty.RegisterAttached<UiAutoHideBehavior, Control, Control?>(
                "UiControls", defaultValue: null);
        
        static UiAutoHideBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }
        
        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);
        
        public static int GetHideDelay(Control element) => element.GetValue(HideDelayProperty);
        public static void SetHideDelay(Control element, int value) => element.SetValue(HideDelayProperty, value);
        
        public static Control? GetUiControls(Control element) => element.GetValue(UiControlsProperty);
        public static void SetUiControls(Control element, Control? value) => element.SetValue(UiControlsProperty, value);
        
        private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            var isEnabled = (bool)args.NewValue!;
            
            if (isEnabled)
            {
                // Attach event handlers
                control.PointerMoved += OnPointerMoved;
                control.PointerPressed += OnPointerPressed;
                control.KeyDown += OnKeyDown;
                
                // Start hide timer
                StartHideTimer(control);
            }
            else
            {
                // Remove event handlers
                control.PointerMoved -= OnPointerMoved;
                control.PointerPressed -= OnPointerPressed;
                control.KeyDown -= OnKeyDown;
                
                // Cancel hide timer
                CancelHideTimer();
            }
        }
        
        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            _lastInteractionTime = DateTime.Now;
            ShowUiControls(sender as Control);
            RestartHideTimer(sender as Control);
        }
        
        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _lastInteractionTime = DateTime.Now;
            ShowUiControls(sender as Control);
            RestartHideTimer(sender as Control);
        }
        
        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            _lastInteractionTime = DateTime.Now;
            ShowUiControls(sender as Control);
            RestartHideTimer(sender as Control);
        }
        
        private static void ShowUiControls(Control? control)
        {
            if (control == null) return;
            
            var uiControls = GetUiControls(control);
            if (uiControls != null)
            {
                RevealerBehavior.SetIsRevealed(uiControls, true);
            }
        }
        
        private static void HideUiControls(Control? control)
        {
            if (control == null) return;
            
            var uiControls = GetUiControls(control);
            if (uiControls != null)
            {
                RevealerBehavior.SetIsRevealed(uiControls, false);
            }
        }
        
        private static void StartHideTimer(Control control)
        {
            CancelHideTimer();
            
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;
            
            Task.Run(async () =>
            {
                await Task.Delay(GetHideDelay(control), token);
                
                if (!token.IsCancellationRequested)
                {
                    // Check if enough time has passed since last interaction
                    var timeSinceInteraction = DateTime.Now - _lastInteractionTime;
                    if (timeSinceInteraction.TotalMilliseconds >= GetHideDelay(control))
                    {
                        await control.Dispatcher.InvokeAsync(() =>
                        {
                            HideUiControls(control);
                        });
                    }
                }
            }, token);
        }
        
        private static void RestartHideTimer(Control? control)
        {
            if (control != null)
            {
                StartHideTimer(control);
            }
        }
        
        private static void CancelHideTimer()
        {
            _hideCts?.Cancel();
            _hideCts?.Dispose();
            _hideCts = null;
        }
    }
}
```

## 6. Responsive Design Implementation

### Breakpoint System
```csharp
// BreakpointBehavior.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace Cine.Avalonia.Behaviors
{
    public class BreakpointBehavior : AvaloniaObject
    {
        public static readonly AttachedProperty<double> MaxWidthProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, double>(
                "MaxWidth", defaultValue: 495);
        
        public static readonly AttachedProperty<Control?> TargetControlProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, Control?>(
                "TargetControl", defaultValue: null);
        
        public static readonly AttachedProperty<AvaloniaProperty?> TargetPropertyProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, AvaloniaProperty?>(
                "TargetProperty", defaultValue: null);
        
        public static readonly AttachedProperty<object?> TargetValueProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, object?>(
                "TargetValue", defaultValue: null);
        
        static BreakpointBehavior()
        {
            MaxWidthProperty.Changed.AddClassHandler<Control>(OnMaxWidthChanged);
        }
        
        public static double GetMaxWidth(Control element) => element.GetValue(MaxWidthProperty);
        public static void SetMaxWidth(Control element, double value) => element.SetValue(MaxWidthProperty, value);
        
        public static Control? GetTargetControl(Control element) => element.GetValue(TargetControlProperty);
        public static void SetTargetControl(Control element, Control? value) => element.SetValue(TargetControlProperty, value);
        
        public static AvaloniaProperty? GetTargetProperty(Control element) => element.GetValue(TargetPropertyProperty);
        public static void SetTargetProperty(Control element, AvaloniaProperty? value) => element.SetValue(TargetPropertyProperty, value);
        
        public static object? GetTargetValue(Control element) => element.GetValue(TargetValueProperty);
        public static void SetTargetValue(Control element, object? value) => element.SetValue(TargetValueProperty, value);
        
        private static void OnMaxWidthChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            // Listen to window size changes
            if (control.GetVisualRoot() is Window window)
            {
                window.PropertyChanged += (sender, e) =>
                {
                    if (e.Property == Window.ClientSizeProperty)
                    {
                        UpdateBreakpoint(control, window);
                    }
                };
                
                // Initial update
                UpdateBreakpoint(control, window);
            }
        }
        
        private static void UpdateBreakpoint(Control control, Window window)
        {
            var maxWidth = GetMaxWidth(control);
            var targetControl = GetTargetControl(control);
            var targetProperty = GetTargetProperty(control);
            var targetValue = GetTargetValue(control);
            
            if (targetControl == null || targetProperty == null || targetValue == null)
                return;
            
            // Convert DIP to pixels (assuming 96 DPI)
            var currentWidth = window.ClientSize.Width;
            
            if (currentWidth <= maxWidth)
            {
                // Apply breakpoint condition
                targetControl.SetValue(targetProperty, targetValue);
            }
            else
            {
                // Revert to default (clear the value)
                targetControl.ClearValue(targetProperty);
            }
        }
    }
}
```

### Responsive Layout Adjustments
```xml
<!-- In MainWindow.axaml -->
<Window ...>
    
    <!-- Breakpoint for controls separator -->
    <Window.Styles>
        <Style Selector="Border#ControlsSeparatorBreakpoint">
            <Setter Property="behaviors:BreakpointBehavior.MaxWidth" Value="495" />
            <Setter Property="behaviors:BreakpointBehavior.TargetControl" Value="{Binding ElementName=ControlsSeparator}" />
            <Setter Property="behaviors:BreakpointBehavior.TargetProperty" Value="{x:Static Border.IsVisibleProperty}" />
            <Setter Property="behaviors:BreakpointBehavior.TargetValue" Value="False" />
        </Style>
    </Window.Styles>
    
    <!-- In UI controls section -->
    <Rectangle x:Name="ControlsSeparator"
               Grid.Column="1" 
               Width="1" Height="16"
               HorizontalAlignment="Center" VerticalAlignment="Center"
               Fill="{StaticResource TimeSeparatorBackground}"
               Opacity="0.4"
               classes="ControlsSeparatorBreakpoint" />
    
</Window>
```

## 7. Implementation Priority & Phasing

### Phase 1: Foundation (Week 1)
1. **Color System**: Implement centralized color resources
2. **Typography**: Set up font system and text styles
3. **Basic Layout**: Convert to overlay-based structure
4. **Button Styles**: Implement circular button system

### Phase 2: Core Components (Week 2)
1. **Icon System**: Implement symbolic icon resources
2. **Progress Bar**: Custom slider with Python styling
3. **Menu System**: File and primary menu buttons
4. **Volume Control**: Popover with mute toggle

### Phase 3: Advanced Features (Week 3)
1. **Animation System**: Revealer behaviors and transitions
2. **Auto-hide**: UI visibility with mouse/keyboard tracking
3. **Responsive Design**: Breakpoint system
4. **Start Page**: Drag-and-drop interface

### Phase 4: Polish & Integration (Week 4)
1. **Visual Effects**: Gradients, shadows, OSD styling
2. **Accessibility**: Keyboard navigation, screen reader support
3. **Performance**: Optimize animations and rendering
4. **Testing**: Cross-platform validation

## 8. Testing & Validation Checklist

### Visual Consistency Tests
- [ ] Color matching against Python screenshots
- [ ] Typography alignment (font, size, weight)
- [ ] Button sizing and spacing
- [ ] Icon sizing and positioning
- [ ] Gradient rendering quality
- [ ] Shadow effects and opacity

### Functional Tests
- [ ] UI auto-hide/show behavior
- [ ] Revealer animations (duration, easing)
- [ ] Menu popovers and flyouts
- [ ] Volume control interaction
- [ ] Progress bar dragging
- [ ] Responsive breakpoints

### Performance Tests
- [ ] Animation smoothness (60fps target)
- [ ] Memory usage with multiple overlays
- [ ] GPU acceleration for gradients
- [ ] Startup time with resource loading

### Accessibility Tests
- [ ] Keyboard navigation (Tab, Arrow keys)
- [ ] Screen reader compatibility
- [ ] High contrast mode support
- [ ] Focus indicators visibility

## 9. Resources & Assets

### Required Asset Files
1. **Icon SVGs**: Convert Python symbolic icons to SVG paths
2. **Color Swatches**: Extract exact colors from Python CSS
3. **Gradient Definitions**: Recreate linear gradients
4. **Font Files**: Ensure Consolas/Courier New availability

### Reference Materials
1. **Python Screenshots**: `window.png`, `video.png`, `options.png`, `preferences.png`
2. **GTK4 Documentation**: Adwaita component specifications
3. **Avalonia Documentation**: Custom control templates
4. **Design Specifications**: Pixel measurements from reference

## 10. Success Metrics

### Quantitative Metrics
- **Visual Accuracy**: 95%+ pixel matching with reference
- **Performance**: <16ms frame time for animations
- **Memory**: <50MB additional overhead
- **Load Time**: <100ms for resource initialization

### Qualitative Metrics
- **User Experience**: Intuitive interaction patterns
- **Visual Polish**: Professional, polished appearance
- **Platform Consistency**: Feels native on Windows
- **Accessibility**: Fully accessible to all users

## Conclusion

This comprehensive solution set provides everything needed to achieve pixel-perfect alignment between the Avalonia implementation and the Python reference. By following this phased approach, the team can systematically address each mismatch while maintaining code quality and performance.

The key to success is starting with the foundation (colors, typography, layout) and progressively building up to the more complex features (animations, responsive design). Regular testing against the Python screenshots will ensure visual accuracy throughout the implementation process. 
                            {
                                new Setter(Visual.OpacityProperty, 0.0),
                                new Setter(TranslateTransform.YProperty, 20.0)
                            }
                        });
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(duration),
                            Setters =

---

## UI ALIGNMENT IMPLEMENTATION PLAN

# UI Alignment Implementation Plan

## Project Overview
This plan outlines the step-by-step implementation process to achieve pixel-perfect alignment between the Avalonia implementation and the Python (GTK4) reference implementation of Cine.

## Phase 1: Foundation Setup (Week 1)

### Day 1-2: Resource System Setup
**Objective**: Create centralized resource dictionaries for colors, typography, and styles.

**Tasks**:
1. Create `Resources/` directory structure:
   ```
   Resources/
   ├── Colors.axaml
   ├── Typography.axaml
   ├── ButtonStyles.axaml
   ├── IconResources.axaml
   └── GradientBrushes.axaml
   ```

2. Implement `Colors.axaml`:
   - Extract exact color values from Python CSS
   - Define color resources with semantic names
   - Create gradient brush resources

3. Implement `Typography.axaml`:
   - Define font families (Consolas, system fonts)
   - Create text styles (heading, time-label, etc.)
   - Set up font sizing system

**Deliverables**:
- Complete color resource dictionary
- Complete typography resource dictionary
- Resource loading in App.axaml

### Day 3-4: Basic Layout Reconstruction
**Objective**: Convert from Grid layout to Overlay-based design.

**Tasks**:
1. Modify `MainWindow.axaml` structure:
   - Replace Grid with Overlay container
   - Set up video host as primary content
   - Create overlay containers for UI controls

2. Implement base styles:
   - Window transparency and blur effects
   - Black video background
   - OSD (On-Screen Display) styling

3. Create custom `Overlay` control if needed:
   - Handle multiple overlay children
   - Manage z-index ordering

**Deliverables**:
- Overlay-based main window structure
- Proper video area with black background
- Basic OSD styling applied

### Day 5: Button System Implementation
**Objective**: Implement circular button styles matching Python design.

**Tasks**:
1. Create `ButtonStyles.axaml`:
   - Circular button base style
   - Flat button variant (transport controls)
   - Toggle button styles
   - Menu button styles

2. Implement hover and active states:
   - Match Python's rgba transparency values
   - Add shadow effects for depth
   - Implement disabled state styling

3. Test button system:
   - Visual consistency with Python screenshots
   - Interaction feedback quality
   - Accessibility features

**Deliverables**:
- Complete button style system
- All button variants implemented
- Visual testing results

## Phase 2: Core Components (Week 2)

### Day 6-7: Icon System Implementation
**Objective**: Create symbolic icon system matching Python's iconography.

**Tasks**:
1. Extract icon paths from Python source:
   - Analyze `window.ui` for icon names
   - Convert symbolic icons to SVG path data
   - Create comprehensive icon resource dictionary

2. Implement `IconResources.axaml`:
   - Playback icons (play, pause, stop)
   - Skip icons (backward, forward)
   - Volume icons (mute, low, mid, max, overamp)
   - Track icons (subtitles, audio, video)
   - Control icons (shuffle, loop, playlist, options)

3. Create icon styling:
   - Consistent sizing (24px default)
   - Shadow effects matching Python
   - Color states (normal, hover, active, disabled)

**Deliverables**:
- Complete icon resource dictionary
- All reference icons implemented
- Icon styling system

### Day 8-9: Progress Bar & Timeline
**Objective**: Implement custom progress slider with Python styling.

**Tasks**:
1. Create custom `ProgressSlider` control:
   - Custom control template
   - White slider thumb (20px diameter)
   - Semi-transparent trough (rgba(255,255,255,0.225))
   - Shadow effects for depth

2. Implement time display system:
   - Elapsed time (left-aligned, negative margin)
   - Time separator (2px width, #DDD, 40% opacity)
   - Total time (right-aligned)
   - Consolas font family, 13px size

3. Add interaction features:
   - Drag behavior
   - Click-to-seek
   - Keyboard navigation (arrow keys)

**Deliverables**:
- Custom progress slider control
- Complete time display system
- Full interaction implementation

### Day 10: Menu System Implementation
**Objective**: Implement file menu and primary menu system.

**Tasks**:
1. Create `OpenMenuButton`:
   - Circular menu button style
   - Flyout with menu items:
     - Open Files
     - Open Folder
     - Add Files
     - Add Subtitle Track
     - Add Audio Track

2. Create `PrimaryMenuButton`:
   - Three-dot menu icon
   - Flyout with menu items:
     - New Window
     - Preferences
     - Keyboard Shortcuts
     - About Cine

3. Implement menu behaviors:
   - Show/hide based on start page visibility
   - Keyboard shortcuts
   - Accessibility labels

**Deliverables**:
- Complete menu button system
- All menu items implemented
- Menu behavior testing

## Phase 3: Advanced Features (Week 3)

### Day 11-12: Animation System
**Objective**: Implement revealer animations and transitions.

**Tasks**:
1. Create `RevealerBehavior`:
   - Attached property system
   - Transition types (slide-up, slide-down, fade, crossfade)
   - Duration and easing controls

2. Implement `UiAutoHideBehavior`:
   - Mouse/keyboard interaction tracking
   - Auto-hide timer system
   - Smooth show/hide transitions

3. Create animation utilities:
   - Cubic easing functions
   - Parallel animation support
   - Animation cancellation

**Deliverables**:
- Complete animation behavior system
- Auto-hide functionality
- Performance-optimized animations

### Day 13: Volume Control Popover
**Objective**: Implement volume control matching Python design.

**Tasks**:
1. Create `VolumeMenuButton`:
   - Circular button with volume icon
   - Icon changes based on volume level
   - Popover flyout behavior

2. Implement volume popover content:
   - Mute toggle button (circular)
   - Volume slider (180px width, 0-130 range)
   - Popover styling (background, border, shadow)

3. Add volume behavior:
   - Mute toggle functionality
   - Volume level icon updates
   - Keyboard shortcuts (M, arrow keys)

**Deliverables**:
- Complete volume control system
- Popover styling matching Python
- Full functionality implementation

### Day 14: Track Selection Menus
**Objective**: Implement subtitles, audio, and video track menus.

**Tasks**:
1. Create track menu buttons:
   - Subtitles menu button
   - Audio tracks menu button
   - Video tracks menu button
   - Circular button styling

2. Implement dynamic menu population:
   - Bind to view model collections
   - Track selection functionality
   - Current track indication (bold text)

3. Add menu styling:
   - Popover background and borders
   - Menu item styling
   - Hover and selection states

**Deliverables**:
- Complete track menu system
- Dynamic menu population
- Visual consistency with Python

## Phase 4: Polish & Integration (Week 4)

### Day 15-16: Start Page Implementation
**Objective**: Implement drag-and-drop start page.

**Tasks**:
1. Create `StartPage` overlay:
   - Gradient background matching Python
   - "Drag and Drop Files Here" title
   - Open buttons (Open…, Open Folder)

2. Implement drag-and-drop behavior:
   - File drop handling
   - Drop indicator animations
   - Visual feedback during drag

3. Add start page styling:
   - Button styles (pill buttons)
   - Suggested action styling
   - Typography and spacing

**Deliverables**:
- Complete start page implementation
- Drag-and-drop functionality
- Visual design matching Python

### Day 17: Playlist Controls
**Objective**: Implement shuffle, loop, and playlist controls.

**Tasks**:
1. Create playlist control buttons:
   - Shuffle toggle button
   - Playlist loop toggle button
   - File loop toggle button
   - Playlist dialog button

2. Implement toggle behaviors:
   - Visual checked states (white background)
   - Tooltip text
   - Keyboard shortcuts

3. Add playlist dialog:
   - Dialog window design
   - Playlist management
   - Current playing item highlighting

**Deliverables**:
- Complete playlist control system
- Toggle button functionality
- Playlist dialog implementation

### Day 18: Options Menu & PIP
**Objective**: Implement options menu and picture-in-picture.

**Tasks**:
1. Create `OptionsMenuButton`:
   - Circular button with options icon
   - Flyout with settings options
   - Styling matching Python

2. Implement `PipToggleButton`:
   - Picture-in-picture toggle
   - Icon state changes
   - Window behavior

3. Add options flyout content:
   - Settings controls
   - Layout and styling
   - Interaction behaviors

**Deliverables**:
- Complete options menu system
- PIP functionality
- Visual design consistency

### Day 19-20: Testing & Validation
**Objective**: Comprehensive testing and quality assurance.

**Tasks**:
1. Visual consistency testing:
   - Pixel-by-pixel comparison with Python screenshots
   - Color accuracy validation
   - Typography alignment checking

2. Functional testing:
   - All interactive components
   - Animation performance
   - Responsive design breakpoints

3. Accessibility testing:
   - Keyboard navigation
   - Screen reader compatibility
   - High contrast mode

4. Performance testing:
   - Memory usage profiling
   - Animation frame rates
   - Startup time optimization

**Deliverables**:
- Complete test suite
- Performance benchmarks
- Accessibility audit report
- Visual consistency report

## Technical Specifications

### File Structure Updates
```
Cine.Avalonia/
├── Resources/
│   ├── Colors.axaml
│   ├── Typography.axaml
│   ├── ButtonStyles.axaml
│   ├── IconResources.axaml
│   └── GradientBrushes.axaml
├── Behaviors/
│   ├── RevealerBehavior.cs
│   ├── UiAutoHideBehavior.cs
│   └── BreakpointBehavior.cs
├── Controls/
│   ├── CustomProgressSlider.axaml
│   ├── CustomProgressSlider.axaml.cs
│   └── OverlayControl.axaml
├── Styles/
│   └── App.axaml (updated)
├── MainWindow.axaml (completely rewritten)
└── MainWindow.axaml.cs (updated)
```

### Color System Specifications
| Resource Name | Python Value | Avalonia Value | Usage |
|---------------|--------------|----------------|-------|
| OsdForeground | `white` | `#FFFFFF` | All OSD text and icons |
| HeaderGradient | `rgba(0,0,0,0.14) → transparent` | Linear gradient | Header bar background |
| ControlsGradient | `rgba(0,0,0,0.2) → transparent` | Linear gradient | Controls box background |
| ButtonHoverBackground | `rgba(255,255,255,0.17)` | `#2BFFFFFF` | Button hover state |
| ButtonActiveBackground | `rgba(255,255,255,0.25)` | `#40FFFFFF` | Button active state |
| ProgressTroughBackground | `rgba(255,255,255,0.225)` | `#39FFFFFF` | Progress slider trough |
| TimeSeparatorBackground | `#DDDDDD` (40% opacity) | `#DDDDDD` with 0.4 opacity | Time display separator |

### Typography Specifications
| Style Name | Font Family | Size | Weight | Usage |
|------------|-------------|------|--------|-------|
| time-label | Consolas, monospace | 13px | Normal | Time display labels |
| time-elapsed | Consolas, monospace | 13px | Normal | Elapsed time (special margin) |
| heading | System font | 14px | Medium | Headers and titles |
| button-label | System font | 14px | Medium | Button text labels |

### Button Specifications
| Button Type | Size | Corner Radius | Icon Size | Spacing |
|-------------|------|---------------|-----------|---------|
| Circular Transport | 40x40px | 20px | 24x24px | 4px between buttons |
| Circular Menu | 32x32px | 16px | 24x24px | 4px between buttons |
| Circular Toggle | 40x40px | 20px | 24x24px | 4px between buttons |
| Pill Button | 40px height | 20px | N/A | 12px between buttons |

### Animation Specifications
| Animation Type | Duration | Easing | Usage |
|----------------|----------|--------|-------|
| UI Reveal/Hide | 300ms | CubicEaseOut | Main UI controls |
| Pause Indicator | 350ms | CubicEaseOut | Play/pause overlay |
| Drop Indicator | 200ms | CubicEaseOut | File drag feedback |
| Button Hover | 150ms | CubicEaseOut | Button state transitions |

## Risk Mitigation

### Technical Risks
1. **Performance Impact**: Overlay system with multiple animations may affect performance.
   - **Mitigation**: Implement lazy loading, optimize animation rendering, use GPU acceleration.

2. **Cross-Platform Compatibility**: Avalonia behaviors may work differently across platforms.
   - **Mitigation**: Test on Windows, Linux, and macOS during development.

3. **Resource Loading**: Multiple resource dictionaries may increase startup time.
   - **Mitigation**: Implement asynchronous loading, combine resources where possible.

### Design Risks
1. **Visual Inconsistency**: Difficult to achieve exact pixel matching.
   - **Mitigation**: Regular comparison with Python screenshots, use pixel measurement tools.

2. **Interaction Differences**: Avalonia and GTK4 have different interaction paradigms.
   - **Mitigation**: Custom event handling, user testing for interaction feedback.

### Schedule Risks
1. **Complexity Underestimation**: Some features may be more complex than anticipated.
   - **Mitigation**: Buffer time in schedule, prioritize core features first.

2. **Integration Issues**: New components may not integrate smoothly with existing code.
   - **Mitigation**: Incremental integration, comprehensive testing at each phase.

## Success Criteria

### Phase 1 Success Criteria
- [ ] Color system implemented and visually matches Python
- [ ] Typography system implemented with correct fonts and sizes
- [ ] Overlay-based layout structure working
- [ ] Circular button styles implemented and functional

### Phase 2 Success Criteria
- [ ] Icon system complete with all reference icons
- [ ] Custom progress slider with Python styling
- [ ] Menu system implemented with all items
- [ ] Volume control popover functional

### Phase 3 Success Criteria
- [ ] Revealer animations working smoothly
- [ ] UI auto-hide behavior implemented
- [ ] Track selection menus functional
- [ ] Responsive breakpoints working

### Phase 4 Success Criteria
- [ ] Start page with drag-and-drop functionality
- [ ] Playlist controls implemented
- [ ] Options menu and PIP functional
- [ ] All tests passing (visual, functional, performance, accessibility)

## Measurement & Reporting

### Daily Standups
- **Time**: 9:00 AM daily
- **Duration**: 15 minutes
- **Format**: What was done yesterday, what's planned today, any blockers

### Weekly Reviews
- **Time**: Friday 4:00 PM
- **Duration**: 1 hour
- **Content**: 
  - Progress against plan
  - Demo of completed features
  - Adjustments for following week
  - Risk assessment update

### Quality Gates
Each phase has specific quality gates that must be passed before proceeding:
1. **Phase 1 Gate**: Visual consistency score ≥ 90%
2. **Phase 2 Gate**: Functional test coverage ≥ 85%
3. **Phase 3 Gate**: Animation performance ≥ 50fps
4. **Phase 4 Gate**: All success criteria met

## Conclusion

This implementation plan provides a structured approach to achieving pixel-perfect alignment between the Avalonia and Python implementations of Cine. By following this phased approach with clear deliverables and success criteria, the team can systematically address the UI mismatches while maintaining code quality and performance.

Regular testing against the Python reference implementation will ensure visual accuracy throughout the development process, and the risk mitigation strategies will help address potential challenges as they arise.

---

## TASK STATUS

# Cine Project — Task Status

## 🔧 Setup — First Steps (Done!)

### 0. Initialize Git Repository & Gitignore
**Status:** ✅ **DONE**

The project now has a git repository initialized in `Windows-Native/` with a proper `.gitignore`.

**What was done:**
1. Initialized git repo: `git init` in `Windows-Native/`
2. Created `.gitignore` for Windows-Native (build outputs, IDE files, OS files, NuGet, compiled outputs)
3. Committed all 40 source files: `git commit -m "Initial state — Windows-Native port with D3D11 renderer, Media Foundation pipeline, and WPF UI rewrite"`

**Note:** The git repo is scoped to `Windows-Native/` only (not the whole project root) because the root contains mixed Linux/Flatpak content that should not be tracked together with the Windows-native code.

---

## Completed ✅

### 1. Fixed MfComInterop.cs GUID encoding issue
- **File:** `Cine.Media/Implementations/MfComInterop.cs`
- **Lines 35 and 170:** The GUID `DF598931-F10C-4E71-86AB-34BE-8F8F-8CE9` had **6 dashes** instead of the required 4 (standard `8-4-4-4-12` format). The last 12 hex digits were incorrectly split as `34BE-8F8F-8CE9` with embedded dashes.
- **Fix:** Replaced with correct IID: `DF598931-F10C-4E71-86AB-34BE8F8F8CE9`
- **Also fixed:** `IMFMediaType` interface now includes all inherited `IMFAttributes` vtable methods (GetCount, GetItemByIndex, SetItem, SetGUID, GetGUID, GetUINT64, etc.) — without these, every vtable call after the missing slots would hit the wrong method.
- **Also fixed:** `MappedSubresource` struct wrapped in `#pragma warning disable CS0649`
- **Also fixed:** Added `ID3D11ShaderResourceView`, `ID3D11VertexShader`, `ID3D11PixelShader`, `ID3D11InputLayout`, `ID3D11Buffer`, `ID3D11SamplerState`, `ID3DBlob`, `ID3D11BlendState` COM interfaces for D3D11 shader pipeline
- **Build result:** 0 errors, 0 warnings

### 2. Created D3D11Renderer class
- **File:** `Cine.Media/Implementations/D3D11Renderer.cs`
- **What it does:** Manages a Direct3D 11 GPU device, DXGI swap chain, render target view, and frame presentation pipeline.
- **Key methods:**
  - `Initialize()` — creates D3D11 device + context, DXGI factory, swap chain bound to an HWND, and render target view
  - `Present(IMFSample)` — copies a decoded video sample into the back buffer and flips to screen (vsync on)
  - `ResizeBuffers(width, height)` — recreates back buffer when the panel resizes
  - `ClearToBlack()` — clears to opaque black and presents (for initial/error state)
  - `TakeScreenshot(outputPath)` — captures the current back buffer and saves it to a PNG file
- **NV12→BGRA Shader Pipeline:**
  - Compiles inline HLSL shaders (VS + PS) for YUV to RGB conversion
  - Creates GPU textures for Y and UV planes with staging textures for CPU upload
  - Fullscreen quad rendering through NV12→BGRA pixel shader
- **Design decisions:**
  - Uses `Marshal.GetObjectForIUnknown()` to wrap raw COM pointers as managed interface types — type-safe method calls via vtable while keeping manual `Marshal.Release()` control over COM lifetime
  - Hardware device first, WARP software fallback if no GPU available
  - All COM objects released in reverse creation order in `Dispose()`

### 3. Created MfHelper class
- **File:** `Cine.Media/Implementations/MfHelper.cs`
- **What it does:** The Media Foundation pipeline bridge — opens media files, enumerates streams, decodes video samples, and dispatches them to D3D11Renderer.
- **Key methods:**
  - `Initialize()` — calls `CoInitializeEx` + `MFStartup` (MTA apartment for background threading)
  - `OpenFile(path)` — creates `IMFSourceReader`, discovers video/audio streams, configures output type
  - `StartPlayback()` — begins background reading loop on a thread-pool task
  - `StopPlayback()` / `Pause()` / `Resume()` — playback control
  - `GetVideoStreamInfo()` — queries current media type for width, height, frame rate, pixel format subtype
- **Threading model:**
  - Main thread: UI + control calls
  - Background thread: `ReadSample` loop reads decoded frames and fires `SampleReady` event
- **Events dispatched:** `MediaOpened`, `SampleReady`, `PlaybackEnded`, `Error`

### 4. Created AudioRenderer class
- **File:** `Cine.Media/Implementations\AudioRenderer.cs`
- **What it does:** WASAPI shared-mode audio output for low-latency PCM audio playback.
- **Key methods:**
  - `Initialize(waveFormat)` — sets up WASAPI client with specified wave format
  - `Write(data, offset, count)` — writes PCM audio data to the render buffer
  - `Stop()` / `Dispose()` — cleanup

### 5. Created MediaFoundationPlayer class
- **File:** `Cine.Media/Implementations\MediaFoundationPlayer.cs`
- **What it does:** Integrates video and audio rendering with Media Foundation pipeline.
- **Key features:**
  - Wires `MfHelper` with `D3D11Renderer` and `AudioRenderer`
  - Auto-detects video format and configures shader vs RGB32 path
  - Handles `MediaOpened` event to configure renderer before first frame
  - Cleanup and reinitialize renderer when video format changes between files

### 6. Implemented Auto-Detection for Video Format
- **File:** `Cine.Media\Implementations\MediaFoundationPlayer.cs`
- **What it does:** Automatically selects between NV12→BGRA shader path and BGRA-direct path based on decoder output format.
- **Detection logic:**
  - Checks `VideoFormat` string from `MediaOpenedEventArgs`
  - NV12 format detected by GUID substring `3231564E`
  - I420 format detected by GUID substring `30323449`
  - YUY2 format detected by GUID substring `32595559`
- **Renderer reinitialization:** When format changes between files, the renderer is disposed and recreated with the correct shader path setting.

### 7. Updated MediaFoundationPlayer.TakeScreenshot
- **File:** `Cine.Media/Implementations\MediaFoundationPlayer.cs`
- **What changed:** Replaced `throw new NotImplementedException(...)` with `_renderer?.TakeScreenshot(outputPath)`
- **Note:** Requires `D3D11Renderer.TakeScreenshot()` to be implemented (see Issue section below)

### 8. ✅ Git Repository Initialized (Windows-Native only)
- **Scope:** `Windows-Native/` subdirectory only
- **Reason:** Root project contains mixed Linux/Flatpak content not related to Windows build
- **40 source files committed** as initial snapshot
- **`.gitignore`** covers: `bin/`, `obj/`, `Debug/`, `Release/`, `publish/`, `.vs/`, `*.suo`, `*.user`, NuGet caches, build scripts, compiled outputs

---

## 🔴 CURRENT ISSUE — Application Crash on Startup (NullReferenceException)

**Status:** ✅ **FIXED**

**Problem:** The application crashed with `NullReferenceException` on line 93 of `MainApp.cs` because `playerPanel.Resize` event was being subscribed before `playerPanel` was created.

**Root Cause:** In the `MainForm` constructor, `playerPanel.Resize += OnPlayerPanelResize;` was called before `InitializeUI()` which creates `playerPanel`.

**Fix:** Moved `playerPanel.Resize += OnPlayerPanelResize;` to after `InitializeUI();` call.

**Files changed:**
- `Cine.WinUI\MainApp.cs` — Reordered event subscription after UI initialization

**Additional fixes applied:**
- D3D11Renderer.cs — Added debug diagnostics that were later removed
- MfComInterop.cs — PreserveSig attributes already fixed in prior commits

---

## 🔴 CURRENT ISSUE — Basic UI Only (Not Feature-Complete)

**Status:** 🔄 **IN PROGRESS**

**Problem:** The current WinForms UI is a basic implementation with only essential controls. It does not match the feature set of the Python UI (window.py).

**What's currently implemented:**
- Basic WinForms layout with video panel, playlist sidebar, transport controls
- MediaFoundationPlayer integration with native D3D11 rendering
- Open file dialog and basic playlist

**What's missing (compared to Python UI):**
- Menu bar with File, Playback, View, Help menus
- Proper seek bar with time display
- Volume slider with mute toggle
- Speed control
- Subtitle track selection
- Audio track selection
- Fullscreen toggle with proper UI
- Drag and drop support
- Auto-hide UI in fullscreen mode
- Keyboard shortcuts (50+ bindings from Python)
- Chapter navigation
- Video filters (contrast, brightness, gamma, saturation)
- Proper status bar with playback state

**To implement:**
1. Design UI layout matching Python reference (window.py:1186-1345)
2. Implement all missing controls and their event handlers
3. Add keyboard shortcuts
4. Implement auto-hide UI for fullscreen mode

---

## Remaining ❌

| # | Task | Status | Notes |
|---|------|--------|-------|
| 1 | Fix NullReferenceException in MainForm | ✅ Done | Moved playerPanel.Resize after InitializeUI() |
| 2 | Build with 0 errors | ✅ Done | Build succeeds with 0 errors, 0 warnings |
| 3 | Application runs without crash | ✅ Done | Exits cleanly with code 0 |
| 4 | Implement full UI (match Python) | 🔄 In Progress | Basic UI exists, needs full feature set |
| 5 | Implement NextChapter/PreviousChapter | Not started | Stubs exist in `MediaFoundationPlayer` |
| 6 | Implement video filters | Not started | Stubs exist, need GPU shader pipeline |
| 7 | Fullscreen auto-hide UI | Not started | Toggle works, UI doesn't auto-hide |
| 8 | Drag & drop support | Not started | Not implemented yet |
| 9 | Testing with various video files | Not started | Verify color accuracy, format detection |
| 10 | Screenshot UI integration | Not started | `TakeScreenshot` method ready, need UI button |

---

## Phase Progress

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1: Video Rendering | ✅ Complete | D3D11Renderer with GPU-accelerated frame presentation |
| Phase 2: Audio + Seeking | ✅ Complete | WASAPI audio output, duration tracking, seeking support |
| Phase 3: YUV→RGB Conversion | ✅ Complete | NV12→BGRA shader pipeline, auto-detection of format |
| Phase 4: UI Implementation | 🔄 In Progress | Basic WinForms UI, needs full feature set |
| Phase 5: Feature Completion | ⏳ Pending | Screenshot, chapters, filters, keyboard shortcuts |
| Phase 6: Testing & Polish | ⏳ Pending | Cross-format testing, performance optimization |

