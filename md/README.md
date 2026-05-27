# Documentation

## Canonical Docs
- `PROJECT_MASTER_GUIDE.md`: Full technical and implementation history
- `PYTHON_REFERENCE_README.md`: Python reference project notes/assets
- `UI_MISMATCH_TRACKER.md`: Current Python-vs-Avalonia UI parity audit with phased task list

## Current Project Structure (Avalonia-First)
```
X:\Development\Cine_C#_Dot
├── src
│   ├── App                      # Avalonia UI entry project
│   │   ├── App.axaml
│   │   ├── App.axaml.cs
│   │   ├── app.manifest
│   │   ├── App.csproj
│   │   ├── UI
│   │   │   ├── Views            # Windows/pages (AXAML + code-behind)
│   │   │   ├── Components       # Reusable controls/components
│   │   │   └── Resources        # Styles, colors, icons, typography
│   │   ├── Application
│   │   │   ├── ViewModels       # MVVM view models and commands
│   │   │   ├── Converters       # Value converters for bindings
│   │   │   └── Services         # UI-facing app services
│   │   └── Infrastructure
│   │       └── Api              # HTTP/API clients + DTO mapping
│   ├── Core                     # Domain/application core abstractions
│   └── Media                    # Media engine and platform interop
├── code_for_reference           # Read-only Python reference code
├── md                           # Documentation
└── Cine.sln                     # Solution entry
```

## Why This Structure
- Keeps Avalonia as the primary app entry point.
- Follows MVVM separation (`UI` vs `Application` vs domain/service layers).
- Keeps backend integration concerns (`Infrastructure/Api`) out of view/viewmodel code.
- Keeps reference code isolated from active app code.
