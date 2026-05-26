# Cine — Windows Native Port (Consolidated Guide)

## Purpose
Produce a **100% native Windows application** in **C# using .NET 10** (Windows Forms).

**Critical Note:**
✅ **NO Python files should remain in the final implementation**
❌ Python code is **NOT** part of the Windows Native product

**Reference/ Folder Purpose:**
The `Reference/` folder contains a **read-only Python snapshot** from the original Cine source:
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
├── Reference/                                # Python reference (read-only)
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