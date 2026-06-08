# Cine Media Player — Documentation

> Minimal, current documentation for active development

---

## Core Documentation

| Document | Purpose |
|----------|---------|
| [`PROJECT_MASTER_GUIDE.md`](./PROJECT_MASTER_GUIDE.md) | Architecture, build status, feature dashboard, troubleshooting |

---

## Project Structure

```
src/
├── App/         # Avalonia UI (entry point, ViewModels, Controls)
├── Media/       # mpv + MediaFoundation player backends
└── Core/        # Domain services (Config, Logging)
```

**Build:** `dotnet build src/App/`  
**Framework:** .NET 10, Windows 10/11  
**Renderer:** D3D11 via native child HWND + DWM thumbnail

---

## Key Features

- **Player Backend:** mpv (primary), MediaFoundation (legacy)
- **Rendering:** D3D11 + DWM thumbnail compositing
- **PIP:** Picture-in-Picture with screenshot polling (~30fps)
- **UI Layer:** Avalonia with Material Design 3 styling
- **Auto-hide:** Smart UI hiding with hover zones
- **Session:** Automatic save/restore of playlist and position

---

## Architecture Highlights

- **MainWindow:** Split into 8 partial classes (Core, Input, Media, AutoHide, etc.)
- **PlayerService:** Manages mpv player instance lifecycle
- **D3D11VideoHost:** Hidden child window for mpv rendering, shown via DWM
- **Controls:** Standalone overlay controls (Subtitle, Audio, SeekBar, etc.)

---

## Development Notes

- Debug logging writes to `%LOCALAPPDATA%\Cine\*.log`
- PIP diagnostics in `PipWindow.log`
- DWM thumbnail debug in `cine_d3d11.log`
- Startup sequence logged in `cine_startup.log`

---

*Last Updated: 2026-06-08*