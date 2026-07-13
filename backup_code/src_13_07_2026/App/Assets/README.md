# MSIX App Icon Assets

These icons are used by the MSIX package for the Windows Start menu, taskbar, installer dialog, and splash screen.

## Generate from Source

```powershell
# Run from src/App/ directory
.\generate-app-icons.ps1
```

This converts `UI/Resources/AppIcon.ico` into 9 PNG assets at the required MSIX sizes.

## Required Sizes

| File | Size | Used In |
|------|------|---------|
| `CineLogo44x44.png` | 44×44 | Start menu tile (small) |
| `CineLogo71x71.png` | 71×71 | Start menu tile (medium) |
| `CineLogo75x75.png` | 75×75 | Package visual elements |
| `CineLogo124x124.png` | 124×124 | Custom App Installer UX |
| `CineLogo150x150.png` | 150×150 | Start menu tile |
| `CineLogo310x150.png` | 310×150 | Start menu tile (wide) |
| `CineLogo310x310.png` | 310×310 | Start menu tile (large) |
| `CineSplash.png` | 620×300 | Launch splash screen |
| `CineBadge24x24.png` | 24×24 | Taskbar badge |

## Notes

- These files are **NOT** committed to Git (too many binary files)
- They are regenerated during CI/CD before MSIX packaging
- Source: `UI/Resources/AppIcon.ico`
