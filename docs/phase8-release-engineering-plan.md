# Phase 8 — Release Engineering: Setup Wizard, Runtime Download & Distribution

> **Goal**: Ship Cine as a polished setup experience with a proper wizard (Next/Back/Install/Finish), automatic .NET runtime download with progress, and in-app native DLL download with progress bar on first launch.

---

## Current State (Quantified)

| Item | Status | Problem |
|------|--------|---------|
| WiX Bootstrapper (`CineSetup.exe`) | ✅ Exists | Only checks .NET — doesn't **download** it |
| Custom bootstrapper theme | ✅ Dark theme | ✅ Adequate but wizard flow is flat (no Next/Back) |
| .NET Runtime detection | ✅ Registry search | ❌ Just shows link — user must manually download |
| .NET Runtime download | ❌ None | Must automate this with progress bar |
| In-app native DLL download | ✅ `RuntimeDownloader.cs` | ❌ No UI wired — no progress bar visible to user |
| MSI/package | ✅ WiX MSI | Functional but heavy |
| MSIX packaging | ⚠️ Partial | `Package.appxmanifest` exists, not built in CI |
| CI/CD pipeline | ❌ None | All builds manual |

---

## Overall Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    DEPLOYMENT PIPELINE                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────┐    ┌────────────────────────────────┐  │
│  │ CineSetup.exe     │    │ Cine.Avalonia (first launch)   │  │
│  │ (WiX Bootstrapper)│    │                                │  │
│  │                   │    │  ┌──────────────────────────┐  │  │
│  │ 1. Welcome page   │    │  │ RuntimeDownloader         │  │  │
│  │ 2. License agree  │    │  │ • libmpv-2.dll (~45MB)   │  │  │
│  │ 3. Install opts   │    │  │ • libEGL.dll (~0.5MB)    │  │  │
│  │ 4. Download .NET  │───▶│  │ • libGLESv2.dll (~8MB)   │  │  │
│  │    ★ progress bar │    │  │ ★ Progress bar in UI     │  │  │
│  │ 5. Install MSI    │    │  └──────────────────────────┘  │  │
│  │ 6. Finish         │    │                                │  │
│  └──────────────────┘    └────────────────────────────────┘  │
│                                                              │
│  User sees: [Next →] [Next →] [Downloading...] [Install]     │
│             [Finish ✓]                                       │
└─────────────────────────────────────────────────────────────┘
```

---

## 8A — Setup Wizard Flow (Bootstrapper Enhancement)

The current bootstrapper has a single-step "Install" page. We need a **multi-page wizard**:

```
┌──────────────────────────────────────────────────────────────┐
│                     SETUP WIZARD FLOW                         │
│                                                              │
│  ┌─────────┐    ┌─────────┐    ┌──────────────┐             │
│  │ PAGE 1  │    │ PAGE 2  │    │ PAGE 3       │             │
│  │ Welcome │───▶│ License │───▶│ Install      │             │
│  │         │    │ Agree   │    │ Options      │             │
│  │ [Next]  │    │ [Back]  │    │ [Back]       │             │
│  │         │    │ [Next]  │    │ [Install]    │             │
│  └─────────┘    └─────────┘    └──────┬───────┘             │
│                                       │                      │
│                                       ▼                      │
│  ┌──────────────────┐    ┌──────────────────────┐           │
│  │ PAGE 5           │    │ PAGE 4               │           │
│  │ Complete ✓       │◀───│ Download + Install   │           │
│  │                  │    │ ★ Progress bar       │           │
│  │ [Finish]         │    │ ★ Status text        │           │
│  │ [✓ Launch Cine]  │    │ [Cancel]             │           │
│  └──────────────────┘    └──────────────────────┘           │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Implementation: Enhanced Theme.xml Pages

The existing `Theme.xml` already has Install/Progress/Success/Failure pages. Key changes needed:

#### 8A.1 — Add License Page

```xml
<Page Name="LicenseAgreement">
    <!-- Background image -->
    <Image Control="BackgroundImage" X="0" Y="0" Width="520" Height="440"
           ImageFile="Background.bmp" ScaleMode="uniformToFill" />

    <Rectangle X="0" Y="0" Width="220" Height="440" Fill="1A1A2E" />

    <Text X="0" Y="60" Width="220" Height="40"
          FontId="2" DisablePrefix="yes" Center="yes">Cine</Text>
    <Image Control="LogoControl" X="70" Y="120" Width="80" Height="80"
           ImageFile="Logo.png" ScaleMode="uniform" />

    <Text X="250" Y="30" Width="240" Height="30"
          FontId="1" DisablePrefix="yes">License Agreement</Text>

    <!-- Scrollable license text -->
    <ScrollableText Control="LicenseText"
                     X="250" Y="70" Width="240" Height="200"
                     FontId="0" TabStop="yes"
                     Hex="yes" />

    <!-- Accept checkbox -->
    <Checkbox Control="AcceptCheckbox"
              X="250" Y="290" Width="240" Height="18"
              FontId="0" TabStop="yes"
              Hex="yes">I accept the terms in the License Agreement</Checkbox>

    <!-- Bottom bar -->
    <Rectangle X="0" Y="390" Width="520" Height="50" Fill="141428" />

    <Button Control="BackButton"   X="220" Y="400" Width="85" Height="30"
            FontId="1" TabStop="yes" Hex="yes">Back</Button>
    <Button Control="NextButton"   X="320" Y="400" Width="85" Height="30"
            FontId="1" TabStop="yes" Hex="yes" Default="yes">Next</Button>
    <Button Control="CancelButton" X="415" Y="400" Width="85" Height="30"
            FontId="1" TabStop="yes" Hex="yes">Cancel</Button>
</Page>
```

#### 8A.2 — Enhance Install Options Page

The current `Install` page already has folder selection + checkboxes. Add:

```xml
<!-- .NET Runtime status with download progress -->
<Text Control="DotNetStatus"
      X="250" Y="295" Width="240" Height="16"
      FontId="3" DisablePrefix="yes">Checking .NET Runtime...</Text>

<!-- Runtime download progress bar (hidden until needed) -->
<Progressbar Control="DotNetProgressBar"
             X="250" Y="315" Width="200" Height="6"
             TabStop="yes" Visible="no" />

<Text Control="DotNetProgressText"
      X="250" Y="325" Width="240" Height="14"
      FontId="3" DisablePrefix="yes" Visible="no" />
```

### 8A.3 — Bundle.wxs: Add .NET Download Step

The current `Bundle.wxs` only has a `Condition` that blocks install with a URL. Replace with an **actual download step**:

```xml
<Bundle Name="Cine Media Player"
        Version="!(bind.packageVersion.CineMsi)"
        Manufacturer="Cine"
        UpgradeCode="B5A1C2D3-E4F5-6789-ABCD-EF0123456789">

    <BootstrapperApplication>
        <bal:WixStandardBootstrapperApplication
            ThemeFile="Theme\Theme.xml"
            LocalizationFile="Theme\Theme.wxl" />
    </BootstrapperApplication>

    <!-- =========================================================================
         .NET 10 Runtime Detection
         ========================================================================= -->
    <util:RegistrySearch Id="DotNet10DesktopSearch"
                         Root="HKLM"
                         Key="SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\10.0.0"
                         Value="Version"
                         Variable="DotNet10DesktopVersion"
                         Result="value" />

    <!-- =========================================================================
         .NET Runtime Download (when missing)
         Uses WiX built-in http download via ExePackage
         ========================================================================= -->
    <Chain>
        <!-- Step 1: Download .NET Runtime if missing -->
        <ExePackage Id="DotNetRuntime"
                    Name="Microsoft .NET 10 Desktop Runtime"
                    DisplayName=".NET 10 Desktop Runtime"
                    Description="Required by Cine Media Player"
                    Cache="no"
                    Compressed="no"
                    Permanent="yes"
                    Vital="yes"
                    InstallCommand="/install /quiet /norestart"
                    RepairCommand="/repair /quiet /norestart"
                    DetectCondition="DotNet10DesktopVersion"
                    DownloadUrl="https://download.visualstudio.microsoft.com/download/.../dotnet-desktop-runtime-10.0.0-win-x64.exe"
                    PrereqPackage="yes"
                    Protocol="netfx4">

            <!-- Progress is reported to the bootstrapper UI automatically -->
            <ExitCode Behavior="forceReboot" />

        </ExePackage>

        <!-- Step 2: Install the MSI -->
        <MsiPackage Id="CineMsi"
                    SourceFile="..\CineMsi\bin\Release\Cine.msi"
                    DisplayInternalUI="no"
                    Vital="yes"
                    Compressed="yes"
                    After="DotNetRuntime" />
    </Chain>

    <!-- Variables -->
    <Variable Name="InstallFolder"
              Type="string"
              Value="[ProgramFiles64Folder]Cine" />

    <Log PathVariable="%TEMP%\CineSetup.log" Prefix="CineSetup_" Extension=".log" />
</Bundle>
```

**How the download progress appears to the user**:

| Bootstrapper Page | What User Sees |
|---|---|
| Install options | "Checking .NET Runtime..." → "Downloading .NET Runtime... (45 MB / 60 MB)" |
| Progress page | Two progress bars: top = overall install, bottom = .NET download progress |
| Completion | "✔ .NET Runtime installed" + "✔ Cine installed" |

---

## 8B — In-App Native DLL Download (RuntimeDownloader + Progress UI)

The [`RuntimeDownloader.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/RuntimeDownloader.cs) already exists and downloads native DLLs with `IProgress<string>` — but **no UI is wired to it**. On first launch, the app should show a download screen.

### 8B.1 — First-Launch Detection Dialog

```xml
<!-- UI/Screens/Dialogs/FirstLaunchDialog.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:materialIcons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
        Title="Cine — First-Time Setup"
        Width="480" Height="320"
        WindowStartupLocation="CenterScreen"
        CanResize="False"
        ExtendClientAreaToDecorationsHint="True"
        Background="{StaticResource AppDialogSurface}">

    <Grid RowDefinitions="*,Auto,Auto,Auto,Auto" Margin="32">

        <!-- Header -->
        <StackPanel Grid.Row="0" VerticalAlignment="Center" HorizontalAlignment="Center">
            <materialIcons:MaterialIcon Kind="Download" Width="48" Height="48"
                Foreground="{StaticResource AppAccent}" />
            <TextBlock Text="Setting up Cine for first use"
                       FontSize="20" FontWeight="SemiBold"
                       Foreground="{StaticResource AppTextPrimary}"
                       HorizontalAlignment="Center" Margin="0,12,0,4" />
            <TextBlock Text="Cine needs to download native media components (~54 MB total)"
                       FontSize="13"
                       Foreground="{StaticResource AppTextOnDarkSecondary}"
                       HorizontalAlignment="Center"
                       TextWrapping="Wrap" />
        </StackPanel>

        <!-- File progress list -->
        <ItemsControl Grid.Row="1" ItemsSource="{Binding Downloads}" Margin="0,16,0,0">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Grid ColumnDefinitions="*,Auto" Margin="0,2">
                        <TextBlock Text="{Binding FileName}" FontSize="12"
                                   Foreground="{StaticResource AppTextOnDarkSecondary}" />
                        <TextBlock Grid.Column="1" Text="{Binding Status}" FontSize="12"
                                   Foreground="{Binding StatusColor}" />
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <!-- Overall progress bar -->
        <ProgressBar Grid.Row="2" Value="{Binding OverallProgress}"
                     Maximum="100" Height="6" Margin="0,12,0,4"
                     Foreground="{StaticResource AppAccent}"
                     Background="{StaticResource AppHoverSubtle}" />
        <TextBlock Grid.Row="3" Text="{Binding StatusText}" FontSize="11"
                   Foreground="{StaticResource AppTextOnDarkHint}"
                   HorizontalAlignment="Center" />

        <!-- Download / Launch button -->
        <Button Grid.Row="4" Content="{Binding ButtonText}"
                Command="{Binding DownloadCommand}"
                HorizontalAlignment="Center" Margin="0,16,0,0"
                Width="160" Height="36"
                Classes="start-page-suggested-action" />
    </Grid>
</Window>
```

### 8B.2 — FirstLaunchViewModel

```csharp
public class FirstLaunchViewModel : ViewModelBase
{
    private readonly IProgress<(string FileName, string Status, double Percent)> _progress;

    public FirstLaunchViewModel()
    {
        _progress = new Progress<(string FileName, string Status, double Percent)>(UpdateProgress);
    }

    public ObservableCollection<DownloadItem> Downloads { get; } = new();
    public double OverallProgress { get; set; }
    public string StatusText { get; set; } = "Preparing download...";
    public string ButtonText { get; set; } = "Download";

    private async Task ExecuteDownloadAsync()
    {
        ButtonText = "Downloading...";

        // RuntimeDownloader.EnsureRuntimeAsync uses IProgress<string>
        var runtimeProgress = new Progress<string>(msg =>
        {
            StatusText = msg;
            // Parse messages like "Downloading libmpv-2.dll..."
            // and "  libmpv-2.dll: 50% (22 / 45 MB)"
        });

        var runtimeDir = await RuntimeDownloader.EnsureRuntimeAsync(
            runtimeProgress, CancellationToken.None);

        // Store the runtime path for the player service
        PlayerService.ConfigureNativeLibraryPath(runtimeDir);

        ButtonText = "Launch Cine";
        // Close dialog → proceed to MainWindow
    }
}
```

### 8B.3 — Wire in App.axaml.cs

```csharp
public override void OnFrameworkInitializationCompleted()
{
    // ... existing initialization ...

    if (!RuntimeDownloader.IsRuntimeReady())
    {
        // Show first-launch download dialog instead of main window
        var downloadVm = new FirstLaunchViewModel();
        var downloadDialog = new FirstLaunchDialog { DataContext = downloadVm };
        downloadDialog.Show();
        downloadDialog.Closed += async (_, _) =>
        {
            // After download completes, show MainWindow
            ShowMainWindow();
        };
    }
    else
    {
        ShowMainWindow();
    }
}
```

### 8B.4 — User Experience Flow

```
First Launch                          Subsequent Launches
─────────────────                    ────────────────────

┌─ Cine Setup ─────────────────┐     ┌─ Cine ─────────────────┐
│                              │     │                        │
│  🔽 Downloading Components  │     │  Main Window loads     │
│                              │     │  instantly             │
│  □ libmpv-2.dll   ████░░ 45%│     │                        │
│  □ libEGL.dll     ██████ 100%│     │                        │
│  □ libGLESv2.dll  ███░░░ 30%│     │                        │
│                              │     │                        │
│  ════════════════════ 48%   │     │                        │
│  Downloading: 26 / 54 MB    │     │                        │
│                              │     │                        │
│  [     Cancel    ]           │     │                        │
└──────────────────────────────┘     └────────────────────────┘
```

---

## 8C — Full Installer Build Pipeline (CiCd)

### Build Script (installer/build.bat)

```bat
@echo off
setlocal enabledelayedexpansion

echo ════════════════════════════════════════════
echo   Cine Installer Build
echo ════════════════════════════════════════════

:: 1. Determine version from Git tag
for /f %%a in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%a
if "%VERSION%"=="" set VERSION=0.0.1
set VERSION=%VERSION:v=%
echo Version: %VERSION%

:: 2. Publish framework-dependent
echo.
echo [1/5] Publishing application (framework-dependent)...
dotnet publish ..\src\App\App.csproj
    --configuration Release
    --runtime win-x64
    --self-contained false
    -p:Version=%VERSION%
    -o ..\src\App\bin\Release\net10.0-windows\win-x64\publish

:: 3. Harvest files for MSI
echo [2/5] Harvesting files...
wix harvest ..\src\App\bin\Release\net10.0-windows\win-x64\publish
    -o CineMsi\Package.wxs
    -ag "-src:..\src\App\bin\Release\net10.0-windows\win-x64\publish"
    -culture en-US
    -t:CineMsi

:: 4. Build MSI
echo [3/5] Building MSI package...
wix build CineMsi\CineMsi.wixproj
    -configuration Release
    -p:Version=%VERSION%

:: 5. Build Bootstrapper (CineSetup.exe)
echo [4/5] Building bootstrapper...
wix build CineBootstrapper\CineBootstrapper.wixproj
    -configuration Release
    -p:Version=%VERSION%

:: 6. Sign if certificate available
if exist signing.pfx (
    echo [5/5] Signing installer...
    signtool sign /fd SHA256
        /f signing.pfx
        /tr http://timestamp.digicert.com
        /td SHA256
        CineBootstrapper\bin\Release\CineSetup.exe
) else (
    echo [5/5] Skipping signing — no signing.pfx found
)

echo.
echo ════════════════════════════════════════════
echo   Build Complete!
echo   Output: CineBootstrapper\bin\Release\CineSetup.exe
echo ════════════════════════════════════════════
```

### Build Outputs

| Output | Size | Purpose |
|--------|------|---------|
| `CineSetup.exe` | ~12 MB | **Primary deliverable** — self-extracting bundle w/ .NET check + download |
| `Cine.msi` | ~10 MB | MSI package (embedded in bootstrapper) |
| `Cine_1.0.0.0_x64.msix` | ~15 MB | MSIX for Store / enterprise (optional) |

---

## 8D — GitHub Actions CI/CD, With Installer

### Workflow Stages

```
  Commit/Tag Push
       │
       ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ STAGE 1      │    │ STAGE 2      │    │ STAGE 3      │
│ Build & Test │───▶│ Package      │───▶│ Release      │
│ • dotnet     │    │ • MSI        │    │ • GitHub     │
│   restore    │    │ • CineSetup  │    │   Release    │
│ • dotnet     │    │   .exe       │    │ • Upload     │
│   build      │    │ • MSIX       │    │   assets     │
│ • dotnet     │    │ • Sign       │    │ • MS App     │
│   test       │    │              │    │   Installer  │
└──────────────┘    └──────────────┘    │   URL        │
       ~2 min            ~3 min         └──────────────┘
                                              ~1 min
```

### Full release.yml

```yaml
name: Release — Build, Sign, Deploy

on:
  push:
    tags: ['v*']

env:
  DOTNET_VERSION: 10.0.x
  SOLUTION: Cine.sln
  APP_PROJECT: src/App/App.csproj
  CONFIG: Release

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Setup WiX
        run: dotnet tool install --global wix

      # ── Stage 1: Build ──
      - name: Restore
        run: dotnet restore ${{ env.SOLUTION }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION }}
          --configuration ${{ env.CONFIG }}
          --no-restore

      - name: Test
        run: dotnet test tests/Cine.Tests/Cine.Tests.csproj
          --configuration ${{ env.CONFIG }}
          --no-build
          --logger trx

      # ── Stage 2: Package ──
      - name: Publish (framework-dependent)
        run: >
          dotnet publish ${{ env.APP_PROJECT }}
          --configuration ${{ env.CONFIG }}
          --runtime win-x64
          --self-contained false
          -p:Version=${{ github.ref_name }}
          -o publish/

      - name: Import signing certificate
        run: |
          $cert = [Convert]::FromBase64String('${{ secrets.BASE64_ENCODED_PFX }}')
          [IO.File]::WriteAllBytes('${{ runner.temp }}\cine.pfx', $cert)

      - name: Build MSI
        run: |
          cd installer
          wix harvest ..\publish -o CineMsi\Package.wxs -ag -culture en-US
          wix build CineMsi\CineMsi.wixproj -${{ env.CONFIG }}

      - name: Build Bootstrapper (CineSetup.exe)
        run: |
          cd installer
          wix build CineBootstrapper\CineBootstrapper.wixproj -${{ env.CONFIG }}

      - name: Sign Installer
        run: |
          & 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe' sign
            /fd SHA256
            /f '${{ runner.temp }}\cine.pfx'
            /p '${{ secrets.PFX_PASSWORD }}'
            /tr http://timestamp.digicert.com
            /td SHA256
            installer/CineBootstrapper/bin/${{ env.CONFIG }}/CineSetup.exe

      # ── Stage 3: Release ──
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: |
            installer/CineBootstrapper/bin/${{ env.CONFIG }}/CineSetup.exe
            installer/CineMsi/bin/${{ env.CONFIG }}/Cine.msi
          body: |
            ## Cine ${{ github.ref_name }}

            ### 📥 Downloads
            - **CineSetup.exe** — Recommended. Self-extracting installer with .NET runtime auto-download.
            - **Cine.msi** — Standalone MSI (requires .NET 10 pre-installed).

            ### 🔧 Installation
            1. Download `CineSetup.exe`
            2. Run it — the wizard will guide you through setup
            3. .NET 10 Runtime is downloaded automatically if missing
            4. On first launch, native video components are downloaded (~54 MB)

            ### ✨ What's New
            *See commit history for details*
          draft: false
          prerelease: ${{ contains(github.ref_name, 'preview') }}
```

---

## 8E — Developer Install Experience

### Building Locally

```powershell
# Quick: publish + run
dotnet run --project src\App\App.csproj

# Full: build installer
cd installer
.\build.bat
.\CineBootstrapper\bin\Release\CineSetup.exe
```

### Testing the Runtime Download UI

```powershell
# Clear cached runtime → forces download on next launch
Remove-Item "$env:LOCALAPPDATA\Cine\runtime" -Recurse -Force

# Launch app — should show download dialog
dotnet run --project src\App\App.csproj
```

---

## 8F — Complete Implementation Checklist

### Phase 8.1 — Setup Wizard (Bootstrapper)
- [ ] **8.1.1** Add License Agreement page to `Theme.xml`
- [ ] **8.1.2** Add .NET download progress bar + status text to Install page
- [ ] **8.1.3** Update `Bundle.wxs`: add `ExePackage` for .NET runtime download with `DownloadUrl`
- [ ] **8.1.4** Wire Next/Back navigation between Welcome → License → Install pages
- [ ] **8.1.5** Test on clean VM: no .NET → download with progress → MSI install → launch

### Phase 8.2 — In-App Runtime Download (First Launch)
- [ ] **8.2.1** Create [`FirstLaunchDialog.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/FirstLaunchDialog.axaml) — download progress UI
- [ ] **8.2.2** Create `FirstLaunchViewModel` — drives download + progress binding
- [ ] **8.2.3** Wire `RuntimeDownloader.EnsureRuntimeAsync()` to the ViewModel
- [ ] **8.2.4** Update `App.axaml.cs` — detect first launch, show download dialog before main window
- [ ] **8.2.5** After download, configure `PlayerService` to use the runtime directory
- [ ] **8.2.6** Test: delete runtime dir, launch app, verify progress bar + completion

### Phase 8.3 — Packaging & CI
- [ ] **8.3.1** Verify `installer/build.bat` produces working `CineSetup.exe`
- [ ] **8.3.2** Create `.github/workflows/release.yml`
- [ ] **8.3.3** Add `BASE64_ENCODED_PFX` + `PFX_PASSWORD` secrets to GitHub
- [ ] **8.3.4** Test CI pipeline: push tag `v1.0.0`, verify Release artifact

### Phase 8.4 — MSIX (Optional / Store)
- [ ] **8.4.1** Verify `Package.appxmanifest` references `.NET 10` framework dependency
- [ ] **8.4.2** Generate `Build-MSIX.ps1` script
- [ ] **8.4.3** Test MSIX install: `Add-AppxPackage -Path Cine.msix`

### Phase 8.5 — Web & Distribution
- [ ] **8.5.1** Create web install page (`web/index.html`) with `CineSetup.exe` download button
- [ ] **8.5.2** Test: download → run → wizard flow complete
- [ ] **8.5.3** Update `README.md` with install instructions

---

## 8G — Premium Visual Design Language

The bootstrapper theme ([`Theme.xml`](file:///x:/Development/Cine_CSharp_DotNet/installer/CineBootstrapper/Theme/Theme.xml)) implements an **Apple-level aesthetic** with these design principles:

### Visual Identity

```
┌──────────────────────────────────────────────────────────────────┐
│                         DESIGN LANGUAGE                           │
│                                                                  │
│  Background:  Deep navy (#0A0A1A) with gradient overlay         │
│  Cards:       Elevated glass panel (#141430) with alpha glow    │
│  Accent:      Vibrant purple (#8866FF / #4422AA alpha)          │
│  Success:     Emerald green (#00BB88)                           │
│  Error:       Warm coral (#DD6644)                              │
│  Text:        White (#FFFFFF) → silver (#AAAAAA) → hint (#888888)│
│  Dividers:    Subtle (#2A2A50)                                  │
│  Controls:    Flat minimal, no heavy borders                    │
│  Progress:    Ultra-thin (2-4px) bars, accent colored           │
│  Typography:  SF Pro scale (Bold 20px → Semibold 15px →        │
│               Regular 11px → Caption 9px)                      │
│  Window:      560×480 — wider, more breathing room              │
│  Decorations: macOS traffic-light dots (top-left)               │
│  Animations:  None (WiX limitation) — compensated by            │
│               clean layout transitions via page structure        │
└──────────────────────────────────────────────────────────────────┘
```

### UX Flow — What the user sees at each step

```
  ┌─ Welcome ─────────────────────────────────────────────────┐
  │                                                            │
  │              ● (glow)                                      │
  │              [LOGO]                                        │
  │              Cine                                          │
  │          Media Player                                      │
  │          [ v1.0.0 ]                                        │
  │                                                            │
  │  A modern, high-performance media player for Windows       │
  │  ─────────────────────────────────────────────────────     │
  │         [Cancel]         [Next →]                          │
  └────────────────────────────────────────────────────────────┘

  ┌─ License ─────────────────────────────────────────────────┐
  │  License Agreement                                        │
  │  ─────────────────────────────────────────────────────     │
  │  ┌──────────────────────────────────────────────────────┐  │
  │  │  MIT License                                         │  │
  │  │  Copyright (c) 2025 Cine                             │  │
  │  │  Permission is hereby granted...                     │  │
  │  │  (scrollable)                                        │  │
  │  └──────────────────────────────────────────────────────┘  │
  │  ☐ I accept the terms of the agreement                    │
  │  ─────────────────────────────────────────────────────     │
  │  [Cancel]    [Back]    [Next →]                            │
  └────────────────────────────────────────────────────────────┘

  ┌─ Install Options ────────────────────────────────────────┐
  │  Install Options                                         │
  │  ─────────────────────────────────────────────────────     │
  │  INSTALL LOCATION                                         │
  │  [C:\Program Files\Cine________________________] [Browse] │
  │                                                           │
  │  OPTIONS                                                  │
  │  ☑ Create desktop shortcut                                │
  │  ☑ Associate video files (.mp4, .mkv, .avi)               │
  │                                                           │
  │  REQUIREMENTS                                             │
  │  ●  Checking .NET Runtime...                              │
  │  [████████░░░░░░░░░░] 52%  (when downloading)             │
  │  ─────────────────────────────────────────────────────     │
  │  [Cancel]    [Back]    [Install ✓]                        │
  └────────────────────────────────────────────────────────────┘

  ┌─ Progress ───────────────────────────────────────────────┐
  │                    ●                                      │
  │               Setting up Cine                             │
  │               Installing...                               │
  │               ████████████░░░░░░ 68%                      │
  │               ██████████████████  (dotnet)                │
  │               Downloading .NET Runtime...                 │
  │               ─────────────────────                      │
  │                     [Cancel]                              │
  └────────────────────────────────────────────────────────────┘

  ┌─ Success ────────────────────────────────────────────────┐
  │                    ✓ (green glow)                         │
  │              Installation Complete                        │
  │              Cine has been installed on your computer.    │
  │              ────────────────────────────────             │
  │              ● Hardware-accelerated ● Keyboards ● Subs   │
  │              ☑ Launch Cine                                │
  │              ─────────────────────                        │
  │                          [Close]  [Finish ✓]             │
  └────────────────────────────────────────────────────────────┘
```

### Asset Requirements

For the premium look to render correctly, these asset files must be updated:

| Asset | Current | Recommended | Purpose |
|-------|---------|-------------|---------|
| `Background.bmp` | 560×480 flat color | **Dark premium gradient** (navy → deep purple vignette, centered glow) | Full-window backdrop |
| `Logo.png` | Current logo | **High-res vector export** (PNG with transparency, 80×80+) | Brand icon on Welcome page |
| `Banner.bmp` | 560×120 | **Dark gradient banner** matching the theme | Optional bundle banner |

For the absolute best visual quality, generate `Background.bmp` with:
- Center radial gradient: `#1A1050` (vibrant purple) at center
- Outer edge: `#0A0A1A` (deep navy black)
- Subtle noise/grain texture overlay (reduces banding)
- Color stops: `#1A1050` @ 30% → `#0D0D22` @ 70% → `#0A0A1A` @ 100%

---

## Success Metrics

| Metric | Target | How to Measure |
|--------|--------|---------------|
| Installer size | < 15 MB | Check file size of `CineSetup.exe` |
| .NET download progress | Real-time % | Visual inspection on clean VM |
| DLL download progress | Real-time per-file | Visual inspection on first launch |
| First-run to usable | < 2 minutes (54 MB @ 5 MB/s) | Stopwatch |
| CI pipeline time | < 8 minutes | GitHub Actions run log |
| SmartScreen warnings | None (signed) | Test on clean Windows VM |
| File association | 20+ formats | Double-click .mp4 → opens in Cine |
| Clean uninstall | No leftovers | Registry + file scan after uninstall |
