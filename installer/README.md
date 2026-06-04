# Cine Installer — Platform & Distribution (P11)

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| WiX Toolset | v4.0+ | `dotnet tool install --global wix` |
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com) |
| PowerShell | 5.1+ | Built into Windows |

## Project Structure

```
installer/
├── Assets/                          # Installer images (auto-generated)
│   ├── Background.bmp               #   Main background (520×440)
│   ├── BackgroundSmall.bmp          #   Small background (300×200)
│   ├── Banner.bmp                   #   MSI banner (493×58)
│   └── Logo.png                     #   App logo from AppIcon.ico
├── CineMsi/                         # MSI package project
│   ├── CineMsi.wixproj              #   WiX project file
│   ├── Product.wxs                  #   Product definition + file assoc
│   ├── Package.wxs                  #   Harvested file list (auto-gen)
│   └── License.rtf                  #   EULA shown during install
├── CineBootstrapper/                # Burn bootstrapper project
│   ├── CineBootstrapper.wixproj     #   WiX project file
│   ├── Bundle.wxs                   #   Bundle with .NET runtime check
│   └── Theme/                       #   Custom dark theme
│       ├── Theme.xml                #     Layout (welcome/progress/success/failure)
│       └── Theme.wxl                #     English locale strings
├── build.bat                        # Full build script
├── generate-assets.ps1              # Regenerate placeholder images
└── README.md                        # This file
```

## Build

```cmd
cd installer
build.bat
```

### What it does

1. **Publish** — `dotnet publish` with `--self-contained false` (framework-dependent)
2. **Harvest** — `wix harvest` scans publish output for files
3. **Compile MSI** — `wix build CineMsi.wixproj` → `Cine.msi`
4. **Compile Bootstrapper** — `wix build CineBootstrapper.wixproj` → `CineSetup.exe`
5. **Sign** — If `signing.pfx` found, sign with SHA256

## Output

- `installer/CineBootstrapper/bin/Release/CineSetup.exe` (~10 MB)
  - Self-extracting bundle
  - Checks for .NET 10 Desktop Runtime
  - If missing → shows message with download link
  - If present → extracts and runs the MSI

## Custom Theme

The installer has a custom dark theme (`CineBootstrapper/Theme/Theme.xml`):

- **Welcome page** — Left panel with app name + logo, right panel with install options (folder, shortcuts, file associations)  
- **Progress page** — Progress bar with status text
- **Success page** — Completion message with "Launch Cine" checkbox
- **Failure page** — Error details + GitHub issues link

To customize the background image, replace `Assets/Background.bmp` (520×440) with your own image and re-run `generate-assets.ps1`.

## Replacing Assets

```cmd
# Regenerate placeholder gradients
powershell -ExecutionPolicy Bypass -File generate-assets.ps1 -Force

# Or place your own:
#   Assets/Background.bmp        — 520×440 BMP
#   Assets/BackgroundSmall.bmp   — 300×200 BMP
#   Assets/Banner.bmp            — 493×58 BMP
#   Assets/Logo.png              — App icon PNG
```
