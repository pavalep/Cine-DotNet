# Build Guide

## Prerequisites

| Dependency | Version | Installation |
|---|---|---|
| Windows SDK | 10.0.20348+ | Via Visual Studio Installer or [standalone SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) |
| .NET SDK | 10.0.301+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| Git | 2.40+ | [git-scm.com](https://git-scm.com/) |
| Visual Studio 2022 (optional) | 17.12+ | For debugging with .NET MAUI/desktop workloads |

## Clone & Quick Start

```powershell
git clone https://github.com/user/Cine
cd Cine

# Restore dependencies
dotnet restore

# Build the application
dotnet build src\App\App.csproj

# Run the application (Debug mode)
dotnet run --project src\App\App.csproj

# Run all tests
dotnet test tests\Cine.Tests\Cine.Tests.csproj
```

## Building from Command Line

### Debug Build

```powershell
dotnet build src\App\App.csproj -c Debug
```

Output: `src\App\bin\Debug\net10.0-windows\App.dll`

### Release Build

```powershell
dotnet build src\App\App.csproj -c Release
dotnet publish src\App\App.csproj -c Release -o .\publish
```

Output: `.\publish\Cine.Avalonia.exe` (single-file publish candidate)

## Running Tests

### Full test suite

```powershell
dotnet test tests\Cine.Tests\Cine.Tests.csproj
```

### Filter by category

```powershell
dotnet test tests\Cine.Tests\Cine.Tests.csproj --filter "FullyQualifiedName~PlaylistCoordinator"
dotnet test tests\Cine.Tests\Cine.Tests.csproj --filter "FullyQualifiedName~SessionManager"
dotnet test tests\Cine.Tests\Cine.Tests.csproj --filter "FullyQualifiedName~MainViewModel"
```

### With code coverage

```powershell
dotnet test tests\Cine.Tests\Cine.Tests.csproj --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:Html
```

## Running Benchmarks

```powershell
dotnet run --project tests\Cine.Benchmarks\Cine.Benchmarks.csproj -c Release
```

**Important**: Benchmarks must be built first before running:

```powershell
dotnet build tests\Cine.Benchmarks\Cine.Benchmarks.csproj -c Release
dotnet run --project tests\Cine.Benchmarks\Cine.Benchmarks.csproj -c Release --no-build
```

Benchmark results are written to `BenchmarkDotNet.Artifacts/results/`.

## Building the Installer

### Prerequisites

```powershell
dotnet tool install --global wix
```

### Generate assets (icons → installer images)

```powershell
.\installer\generate-assets.ps1
```

### Build MSI

```powershell
dotnet build installer\CineMsi\CineMsi.wixproj -c Release
```

### Build Bootstrapper (MSI + runtime check)

```powershell
dotnet build installer\CineBootstrapper\CineBootstrapper.wixproj -c Release
```

Output: `installer\CineBootstrapper\bin\Release\CineSetup.exe`

## Native Dependencies

The following native DLLs are bundled in `resources/libmpv-2_x86-64/`:

| DLL | Source | Purpose |
|---|---|---|
| `libmpv-2.dll` | [mpv-winbuild](https://github.com/shinchiro/mpv-winbuild) | Video playback engine |
| `libEGL.dll` | ANGLE project | EGL entry points for OpenGL ES |
| `libGLESv2.dll` | ANGLE project | OpenGL ES 2.0 implementation over D3D11 |

These are copied to the output directory on build via `<Content>` items in `App.csproj`.

## Solution Structure

```
Cine.sln
├── src/App/              — Avalonia UI application
├── src/Core/             — Shared infrastructure (logging, config)
├── src/Media/            — Media playback backends (mpv, MF)
├── src/MediaSmoke/       — Quick integration test
├── tests/Cine.Tests/     — xUnit test suite (270+ tests)
├── tests/Cine.Benchmarks/— BenchmarkDotNet performance benchmarks
└── installer/            — WiX installer projects
```

## Troubleshooting

### Build fails: "SDK not found"
Install .NET 10 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

### Build fails: "libmpv-2.dll not found"
Ensure the native DLLs exist at `resources/libmpv-2_x86-64/`. If missing, download from the mpv-winbuild releases page.

### Tests fail: "Avalonia headless not initialized"
Headless tests require the `Headless` collection. Verify test class has `[Collection("Headless")]` attribute.

### Test fails: "Window constructor requires Avalonia platform"
Window-based tests must run in the `Headless` collection. Use `NSubstitute` mocks for pure unit tests that don't need the headless platform.
