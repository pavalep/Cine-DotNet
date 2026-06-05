# Cine Media Player — Master Guide

> C# / Avalonia / mpv / D3D11 — Windows desktop media player

**Companion docs:** [MAIN_UI_GOLD_STANDARD.md](./MAIN_UI_GOLD_STANDARD.md) — visual reference design

---

## Project Structure

```
src/
├── App/                    # Avalonia UI project (entry point)
│   ├── Application/
│   │   ├── ViewModels/     # MainViewModel, PipService, PlayerService
│   │   ├── Converters/     # TimeSpan, Percent, ChapterMargin converters
│   │   └── Services/       # PipService lifecycle
│   ├── UI/
│   │   ├── Views/          # MainWindow, Dialogs (PipWindow, Preferences, About)
│   │   ├── Shell/          # MainWindow partials: Core, Media, Input, AutoHide, Pip
│   │   ├── Controls/       # D3D11VideoHost, SeekBar, Indicators, Buttons
│   │   ├── Screens/        # StartPage, ControlsBox, HeaderBar, FullscreenHeader
│   │   └── Resources/      # Colors, Typography, Icons, ButtonStyles, Effects
│   └── Infrastructure/Api/ # HTTP/API clients
├── Media/                  # Media engine
│   ├── Interfaces/         # IMediaPlayer, IVideoRenderer
│   ├── Models/             # PlaybackState, ChapterInfo
│   ├── Events/             # PositionChanged, PlaybackStateChanged, etc.
│   └── Implementations/
│       ├── mpv/            # MpvPlayer — primary backend (libmpv-2.dll)
│       └── mediafoundationplayer/ # MediaFoundationPlayer (legacy fallback)
├── Core/                   # Domain abstractions
└── md/                     # Documentation (this + MAIN_UI_GOLD_STANDARD.md)
```

---

## Build Status

| Target | Status |
|--------|--------|
| `dotnet build src/App/` | ✅ 0 errors (— warnings) |
| Framework | .NET 10, Windows |
| Renderer | D3D11 via native child HWND |

---

## Feature Dashboard

```
PIP (Picture-in-Picture)    ✅ Fixed — see [PIP Debug Context](#pip-debug-context) below
Edge Hover Zones            ✅ Done (any-move shows, 3s auto-hide)
Right-Click Context Menu    ✅ Done (Play/Pause, Aspect, Speed, Prefs)
Options Tab UI              ✅ Done (Video/Audio/Subtitles tabs)
Seek Bar Chapters           ✅ Done (markers + hover tooltip)
Audio: 200% Volume + Boost  ✅ Done (VolumeMax=200, Dialogue Boost toggle)
Play/Pause Icon Sync        ✅ Fixed (direct player state read)
Auto-Hide Reliability       ✅ Fixed (PointerEntered/Exited + WS_DISABLED)
```

---

## PIP Architecture (Debug Context)

**Approach:** Separate decoder instance (Approach A) — `PipService` creates a secondary `MpvPlayer` + `PipWindow`.

### Fixes Applied (2026-06-05)

1. **Duplicate `OnOpened` handler** — Was wired in both XAML (`Opened="OnOpened"`) AND constructor (`Opened += OnOpened`), causing `OnOpened` to run twice → `ChildWindowCreated` subscribed twice → double init attempt. **Fix:** Removed XAML attribute, kept constructor subscription only.

2. **`GetPlatformHwnd()` zero on first call** — Window native handle may not be ready when `Opened` fires. **Fix:** `RetryGetPlatformHwndAsync()` retries up to 5× with 200ms delays.

3. **`ChildWindowCreated` never fires** — If HWND is ready but bounds are still 0 when `ParentHwnd` is set, child window creation is silently skipped. **Fix:** Added 1s safety timeout — if `VideoHwnd` is still zero after delay, re-triggers `ParentHwnd` assignment.

4. **No init timeout** — If mpv init hangs, PIP window stays in loading state forever. **Fix:** Added 15-second safety timeout via linked `CancellationTokenSource`.

5. **Unhandled secondary player errors** — `Error` event on `_pipPlayer` was never subscribed, so silent failures (mpv init fail, file open fail) were invisible. **Fix:** Added `_pipPlayer.Error += (_, msg) => Trace(...)`.

6. **Trace logging to file** — All PIP initialization steps now write to `%LOCALAPPDATA%\Cine\PipWindow.log` for post-mortem analysis.

---

## PIP Architecture (Debug Context)

**Approach:** Separate decoder instance (Approach A) — `PipService` creates a secondary `MpvPlayer` + `PipWindow`.

```
MainWindow
  └─ _pipService.Initialize(mainPlayer)
      └─ EnterPip()
          ├─ _playerService.CreateSecondaryPlayer() → new MpvPlayer
          ├─ new PipWindow(secondaryPlayer, mainPlayer, filePath, pipService)
          ├─ mainPlayer.Mute(true) + mainPlayer.Pause()  ← avoid dual audio
          ├─ _pipWindow.Show()
          └─ PipWindow.OnOpened
              ├─ D3D11VideoHost created inside PipWindow
              ├─ _pipPlayer.InitializeRenderer(hwnd)
              ├─ _pipPlayer.Open(filePath)
              ├─ _pipPlayer.Seek(mainPlayer.Position)
              └─ Sync via PositionChanged events
```

**Key files:**
- [`PipService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PipService.cs) — lifecycle management
- [`PipWindow.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml) — PIP window XAML
- [`PipWindow.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml.cs) — 1000 lines, all PIP UI logic
- [`MainWindow.Pip.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Pip.cs) — MainWindow PIP handler wiring
- [`PlayerService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlayerService.cs) — `CreateSecondaryPlayer()` factory

**Why PIP may not work:**
1. `PipWindow` constructor calls `this()` (parameterless) which sets `_pipPlayer = null!` — then the parameterized constructor runs, but `InitializeComponent()` was already called in `this()` — any `FindControl` in `OnOpened` runs against the initialized component tree.
2. `D3D11VideoHost` in `PipWindow` may not get the correct `ParentHwnd` assignment before `InitializeRenderer` is called.
3. Secondary `MpvPlayer` `InitializeRenderer(hwnd)` may fail silently if the native HWND from `PipWindow.TryGetPlatformHandle()` is invalid.
4. The secondary player opened file might trigger an error that gets caught by the `OperationCanceledException` guard and never surfaces.

---

## Resource Dictionaries

| File | Contents |
|------|----------|
| `Colors.axaml` | Full palette: OsdForeground, HeaderGradient, ControlsGradient, PopoverBackground |
| `Typography.axaml` | Consolas (time), Segoe UI (UI labels) |
| `Icons.axaml` | All icon geometries (Play, Pause, Skip, Volume, etc.) |
| `ButtonStyles.axaml` | Circular 40×40 buttons, hover/pressed/checked states |
| `Effects.axaml` | OSD, spinner, drop indicator, shadows |

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Space / K / P | Play/Pause |
| F / F11 | Fullscreen toggle |
| ← / → | Seek ±5s |
| Shift+←/→ | Seek ±60s |
| ↑ / ↓ | Volume ±5 |
| ] / . | Speed +0.1 |
| [ / , | Speed -0.1 |
| Backspace | Reset speed to 1.0× |
| M | Mute toggle |
| Esc | Stop / Exit fullscreen |
| S | Screenshot |
| L | Loop file |
| Ctrl+L | Loop playlist |
| PgUp/PgDn | Previous/Next playlist item |
| P | Next chapter |
| Shift+P | Previous chapter |
| C | Cycle subtitles |
