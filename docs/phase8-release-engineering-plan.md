# Phase 8 — Release Engineering: MSIX Packaging & Distribution

> **Goal**: Ship Cine as a lightweight, self-updating MSIX package with a gorgeous custom installer UI, framework-dependent deployment (no bundled runtime), and fully automated CI/CD pipeline.

---

## Current State (Quantified)

| Item | Status | Problem |
|------|--------|---------|
| MSIX packaging | ❌ None | No package identity → no clean install/uninstall, no Store option |
| WiX MSI | ✅ Exists (`installer/`) | Legacy; heavier, no differential updates, manual .NET dependency |
| `.appinstaller` auto-update | ❌ None | Users must manually download new versions |
| Code signing | ⚠️ None | MSI unsigned; MSIX requires signing |
| CI/CD pipeline | ❌ None | All builds are local; no automated releases |
| Custom installer UX | ❌ None | Default Windows installer dialog |
| App icon branding | ⚠️ Partial | Icons exist but unoptimized for MSIX tile sizes |
| File association | ⚠️ WiX only | MSIX offers superior file association (declarative in manifest) |
| Package size | ⚠️ ~100MB (self-contained) | Currently bundles .NET runtime in folder; MSIX should be ~8MB |

---

## 8A — Framework-Dependent MSIX Strategy (No Bundled Runtime)

### Why Framework-Dependent?

```
Self-Contained MSIX           Framework-Dependent MSIX
┌─────────────────────┐       ┌─────────────────────┐
│ App.dll              │       │ App.dll              │
│ Core.dll             │       │ Core.dll             │
│ Media.dll            │       │ Media.dll            │
│ libmpv-2.dll          │       │ libmpv-2.dll          │
│ libEGL.dll            │       │ libEGL.dll            │
│ libGLESv2.dll         │       │ libGLESv2.dll         │
│ .NET Runtime (60MB)  │       │ ── NOT INCLUDED ──  │
│ Avalonia DLLs (15MB) │       │ Avalonia DLLs (15MB) │
└─────────────────────┘       └─────────────────────┘
        ~100 MB                       ~15 MB
```

The `.NET 10 Desktop Runtime` is declared as a **framework package dependency** in `Package.appxmanifest`. Windows downloads it automatically on first install from Microsoft's CDN. This is analogous to how a web browser downloads video codecs on demand.

### Dependency Declaration

```xml
<!-- Package.appxmanifest -->
<Dependencies>
    <!-- Windows downloads these from Microsoft CDN automatically -->
    <TargetDeviceFamily Name="Windows.Desktop"
        MinVersion="10.0.19041.0"
        MaxVersionTested="10.0.26100.0" />

    <!-- .NET 10 Desktop Runtime → ~60MB download, ONCE per machine -->
    <PackageDependency Name="Microsoft.NET.Runtime.10.0"
        MinVersion="10.0.100.46302"
        Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />

    <!-- VCLibs → already on most machines -->
    <PackageDependency Name="Microsoft.VCLibs.140.00"
        MinVersion="14.0.32530.0"
        Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
</Dependencies>
```

### Package Size Breakdown

| Component | Framework-Dependent | Self-Contained |
|---|---|---|
| App binaries (Cine.*.dll) | ~2 MB | ~2 MB |
| Avalonia NuGet DLLs | ~15 MB | ~15 MB |
| Native DLLs (mpv, ANGLE) | ~45 MB | ~45 MB |
| .NET Runtime | **NOT INCLUDED** | ~60 MB |
| **Total MSIX** | **~15 MB** | **~125 MB** |

> **Critical**: Native mpv DLLs (45MB) cannot be excluded — they have no framework package equivalent. Investigate compression via MSIX's built-in block-level compression. If still too large, consider a post-install download step for mpv DLLs similar to how VS Code downloads its C++ tools.

### What Windows Downloads On Demand

| Framework Package | Size | Source | Downloaded When |
|---|---|---|---|
| .NET 10 Desktop Runtime | ~60 MB | Microsoft CDN | First app install that requires it |
| VCLibs 140 | ~5 MB | Microsoft CDN | Already on 95% of Windows 10/11 machines |
| Windows App SDK Runtime | ~35 MB | Microsoft CDN | First WinAppSDK app install |

**User experience**: First install shows "Downloading required framework..." with a progress bar. Subsequent installs (or other apps using the same framework) are instant.

---

## 8B — Custom App Installer UX (Funky/Cool Design)

### The Default vs. Custom Experience

```
┌─ DEFAULT (boring) ─────────────────────┐   ┌─ CUSTOM (what we want) ───────────────┐
│  [small gray icon]                      │   │  [Large Cine logo, 124x124, centered]  │
│  Cine                                    │   │                                        │
│  Publisher: Unknown                      │   │         █▀▀▀▀▀▀█                      │
│  Version: 1.0.0                         │   │         █ CINE █                      │
│                                          │   │         █▄▄▄▄▄▄█                      │
│  ┌──────────┐                           │   │                                        │
│  │  Install │  ← gray, boring            │   │   A modern media player                │
│  └──────────┘                           │   │   Powered by libmpv + Avalonia          │
│                                          │   │                                        │
│  ────────────────────────               │   │   ┌──────────────────────────────────┐ │
│  Terms & Conditions                     │   │   │  ⚡ Install Cine  ──▶            │ │
│                                          │   │   └──────────────────────────────────┘ │
│                                          │   │                                        │
│                                          │   │   ☑ Launch when ready                  │
│                                          │   │   ── Terms · Privacy · GitHub ──      │
└──────────────────────────────────────────┘   └────────────────────────────────────────┘
```

### Implementation: `MSIXAppInstallerData.xml`

Customize via the [MSIX App Installer UX API](https://learn.microsoft.com/en-us/windows/msix/app-installer/how-to-create-custom-app-installer-ux). This file is placed in a `Msix.AppInstaller.Data` folder inside the MSIX package.

```xml
<?xml version="1.0" encoding="utf-8"?>
<AppInstallerUX xmlns="http://schemas.microsoft.com/msix/appinstallerux"
                xmlns:ux="http://schemas.microsoft.com/msix/appinstallerux"
                xmlns:ux2="http://schemas.microsoft.com/msix/appinstallerux/2"
                IgnorableNamespaces="ux ux2"
                Version="1.0.0">
  <UX
    AccentColor="#6C5CE7"           <!-- Cine purple accent -->
    FontFamily="Segoe UI Variable"
    AllowUserInteraction="true"
    BackgroundColor="#1A1A2E"        <!-- Dark cinematic background -->
    AppNameInTitle="true"
    HyperLinkFontSize="12">

    <!-- Large, centered app icon -->
    <Icon
      HorizontalAlignment="center"
      Logo="Images\CineLogo124x124.png"
      TopMargin="60"/>

    <!-- Stylish install button with extra text -->
    <Buttons
      HorizontalAlignment="center"
      Text="Light up your screen"
      IsSecondaryButtonAccent="true"/>

    <!-- "Launch when ready" checkbox -->
    <LaunchWhenReady HorizontalAlignment="center"/>

    <!-- Additional info shown as a flyout -->
    <AppInformation Mode="flyout" />

    <!-- Links row -->
    <HyperLinks TopMargin="24">
      <HyperLink
        Text="Terms &amp; conditions"
        Url="https://cine.app/terms"
        HorizontalAlignment="center"/>
      <HyperLink
        Text="Privacy policy"
        Url="https://cine.app/privacy"
        HorizontalAlignment="center"/>
      <HyperLink
        Text="GitHub"
        Url="https://github.com/user/cine"
        HorizontalAlignment="center"/>
    </HyperLinks>
  </UX>
</AppInstallerUX>
```

### Design Token Reference

| Token | Value | Effect |
|---|---|---|
| `AccentColor` | `#6C5CE7` | Purple install button, progress bar, highlights |
| `BackgroundColor` | `#1A1A2E` | Dark background (matches app's dark theme) |
| `FontFamily` | `Segoe UI Variable` | Windows 11 native font with optical sizing |
| `Icon::Logo` | `Images\CineLogo124x124.png` | 124x124 PNG inside MSIX package |
| `Icon::TopMargin` | `60` | Pushes logo down from top for breathing room |

### Visual Assets (Required Icons)

| Asset | Size | File | Purpose |
|---|---|---|---|
| App Icon | 44x44 | `CineLogo44x44.png` | Start menu tile (small) |
| App Icon | 71x71 | `CineLogo71x71.png` | Start menu tile (medium) |
| App Icon | 150x150 | `CineLogo150x150.png` | Start menu tile (wide) |
| App Icon | 310x150 | `CineLogo310x150.png` | Start menu tile (large/hero) |
| Store Logo | 75x75 | `CineLogo75x75.png` | Package manifest visual elements |
| Splash Screen | 620x300 | `CineSplash.png` | Launch splash screen |
| Badge Logo | 24x24 | `CineBadge24x24.png` | Taskbar / notification area |
| Installer UX Logo | 124x124 | `CineLogo124x124.png` | Custom App Installer dialog |

> **Recommendation**: Generate all sizes from a single 1024x1024 source PNG via a script. Use the existing Cine iconography with the purple accent (#6C5CE7) on a gradient background.

---

## 8C — Web Hosting with `.appinstaller` Auto-Update

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│              https://releases.cine.app/                    │
│                                                            │
│  Cine.appinstaller   ← XML points to latest MSIX          │
│  Cine_1.2.0_x64.msix                                      │
│  Cine_1.2.0_x64.msixbundle                                │
│  Cine_1.1.0_x64.msix    ← kept for rollback               │
│  index.html                                                │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│                      User's PC                             │
│                                                            │
│  1. User clicks "Install" on website                      │
│  2. App Installer downloads Cine.appinstaller              │
│  3. Resolves latest MSIX URL from .appinstaller            │
│  4. Downloads MSIX (differential, ~15 MB)                  │
│  5. Resolves framework deps, downloads .NET if needed      │
│  6. Installs → registers package identity                  │
│                                                            │
│  Subsequent launches:                                       │
│  → Checks .appinstaller on launch                          │
│  → If newer version exists, downloads diff blocks only     │
│  → Updates silently (ShowPrompt="false")                   │
└──────────────────────────────────────────────────────────┘
```

### `.appinstaller` File

```xml
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
    xmlns="http://schemas.microsoft.com/appx/appinstaller/2021"
    Version="1.2.0.0"
    Uri="https://releases.cine.app/Cine.appinstaller">

    <MainPackage
        Name="Cine.CineMediaPlayer"
        Publisher="CN=CineApp"
        Version="1.2.0.0"
        ProcessorArchitecture="x64"
        Uri="https://releases.cine.app/Cine_1.2.0.0_x64.msix" />

    <UpdateSettings>
        <OnLaunch
            HoursBetweenUpdateChecks="12"
            ShowPrompt="false"
            UpdateBlocksActivation="false" />
    </UpdateSettings>

    <ForceUpdateFromAnyVersion>true</ForceUpdateFromAnyVersion>
</AppInstaller>
```

### Update Behavior

| Setting | Value | Effect |
|---|---|---|
| `HoursBetweenUpdateChecks` | `12` | Checks every 12 hours (not on every launch) |
| `ShowPrompt` | `false` | Silent background download — no popup |
| `UpdateBlocksActivation` | `false` | App launches immediately, update applies next launch |
| `ForceUpdateFromAnyVersion` | `true` | Allows downgrading if needed (rollback support) |

### Hosting Options

| Option | Cost | Best For |
|---|---|---|
| **GitHub Releases** | Free | Open source; built-in CDN, no server needed |
| **Azure Static Web Apps** | Free tier | Custom domain + CDN + auto-SSL |
| **Cloudflare R2** | Free (10GB) | S3-compatible, global CDN, very fast |
| **Netlify** | Free | One-click deploy, custom domain |
| **S3 + CloudFront** | ~$1/mo | Full control, enterprise-grade |

**Recommendation**: Start with **GitHub Releases** (free, no infra). Promote to **Cloudflare R2** when you want `releases.cine.app` custom domain with global CDN.

---

## 8D — CI/CD Pipeline (GitHub Actions)

### Build Matrix

```yaml
strategy:
  matrix:
    architecture: [x64]
    configuration: [Release]
```

### Pipeline Stages

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  RESTORE  │───▶│  BUILD   │───▶│  TEST    │───▶│  PACKAGE │───▶│  DEPLOY  │
│  dotnet   │    │  dotnet  │    │  dotnet  │    │  dotnet  │    │  upload  │
│  restore  │    │  build   │    │  test    │    │  publish │    │  release │
│           │    │  --no-   │    │  270+    │    │  + MSIX  │    │          │
│           │    │  restore │    │  tests   │    │  signing │    │          │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     ~30s            ~45s           ~5s            ~120s           ~10s

                                    Total: ~3.5 minutes
```

### Full Workflow YAML

```yaml
name: CI/CD — Build, Test, Package, Release

on:
  push:
    branches: [main, develop]
    tags: ['v*']
  pull_request:
    branches: [main, develop]

env:
  SOLUTION: Cine.sln
  APP_PROJECT: src/App/App.csproj
  DOTNET_VERSION: '10.0.x'
  MSIX_NAME: Cine.CineMediaPlayer
  MSIX_VERSION: 1.0.0.0

jobs:
  build:
    runs-on: windows-latest
    strategy:
      matrix:
        arch: [x64]

    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0  # required for GitVersion

    # -----------------------------------------------
    # Stage 1: Setup
    # -----------------------------------------------
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Setup MSBuild
      uses: microsoft/setup-msbuild@v2

    # -----------------------------------------------
    # Stage 2: Restore
    # -----------------------------------------------
    - name: Restore dependencies
      run: dotnet restore ${{ env.SOLUTION }}

    # Cache: NuGet packages + obj folders
    - uses: actions/cache@v4
      with:
        path: |
          ~/.nuget/packages
          **/obj
        key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}

    # -----------------------------------------------
    # Stage 3: Build
    # -----------------------------------------------
    - name: Build
      run: >
        dotnet build ${{ env.SOLUTION }}
        --configuration Release
        --no-restore
        -p:Platform=x64

    # -----------------------------------------------
    # Stage 4: Test
    # -----------------------------------------------
    - name: Run tests
      run: dotnet test tests/Cine.Tests/Cine.Tests.csproj
        --configuration Release
        --no-build
        --logger "trx;LogFileName=test-results.trx"

    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: test-results-${{ matrix.arch }}
        path: tests/Cine.Tests/TestResults/

    # -----------------------------------------------
    # Stage 5: Package (MSIX) — only on tag push
    # -----------------------------------------------
    - name: Publish framework-dependent
      if: startsWith(github.ref, 'refs/tags/v')
      run: >
        dotnet publish ${{ env.APP_PROJECT }}
        --configuration Release
        --runtime win-${{ matrix.arch }}
        --no-build
        -p:PublishSingleFile=false
        -p:SelfContained=false
        -p:WindowsPackageType=None
        -o publish/win-${{ matrix.arch }}

    - name: Import signing certificate
      if: startsWith(github.ref, 'refs/tags/v')
      run: |
        $certBytes = [Convert]::FromBase64String('${{ secrets.BASE64_ENCODED_PFX }}')
        [IO.File]::WriteAllBytes('${{ runner.temp }}\cine.pfx', $certBytes)

    - name: Create MSIX package
      if: startsWith(github.ref, 'refs/tags/v')
      run: >
        dotnet run --project tools/Cine.Packager/Cine.Packager.csproj
        -- --source publish/win-${{ matrix.arch }}
           --output dist/
           --version ${{ github.ref_name | replace 'v' '' }}
           --cert '${{ runner.temp }}\cine.pfx'
           --arch ${{ matrix.arch }}

    - name: Sign MSIX
      if: startsWith(github.ref, 'refs/tags/v')
      run: |
        signtool sign /fd SHA256
          /f '${{ runner.temp }}\cine.pfx'
          /p '${{ secrets.PFX_PASSWORD }}'
          /tr http://timestamp.digicert.com
          /td SHA256
          dist/Cine_${{ github.ref_name }}_${{ matrix.arch }}.msix

    # -----------------------------------------------
    # Stage 6: Deploy
    # -----------------------------------------------
    - name: Upload MSIX to GitHub Release
      if: startsWith(github.ref, 'refs/tags/v')
      uses: softprops/action-gh-release@v2
      with:
        files: |
          dist/Cine_*.msix
          dist/Cine.appinstaller
        body: |
          ## Cine ${{ github.ref_name }}

          ### Installation
          1. Download `Cine_${{ github.ref_name }}_x64.msix`
          2. Double-click to install
          3. Windows will automatically download .NET 10 Runtime if needed

          ### What's New
          - *(automated release — see commit history)*

          [Full changelog](https://github.com/user/Cine/compare/...)
        draft: false
        prerelease: ${{ contains(github.ref_name, 'preview') || contains(github.ref_name, 'rc') }}
```

### Secrets Required

| Secret Name | Description | How to Get |
|---|---|---|
| `BASE64_ENCODED_PFX` | Base64 of code signing `.pfx` | `[Convert]::ToBase64String((Get-Content cert.pfx -AsByteStream))` |
| `PFX_PASSWORD` | Password for the `.pfx` (empty for dev cert) | Self-signed dev cert: empty; production: CA-provided password |

### When Triggers Fire

| Trigger | What Happens |
|---|---|
| `push` to `main`/`develop` | Full build + test (no packaging) |
| `pull_request` to `main`/`develop` | Full build + test (no packaging) |
| Tag push `v1.0.0` | Full build + test + package + sign + create GitHub Release |
| Tag push `v1.0.0-preview1` | Same as above but marked as prerelease |

---

## 8E — Code Signing Strategy

### Development (Self-Signed)

```powershell
# Generate a self-signed certificate (valid 1 year)
New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=CineApp" `
  -KeyUsage DigitalSignature `
  -FriendlyName "Cine Dev Certificate" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

# Export as PFX (no password for CI)
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=CineApp" }
Export-PfxCertificate -Cert $cert -FilePath cine-dev.pfx -Password (ConvertTo-SecureString -String "" -AsPlainText -Force)

# Install on dev machine
Import-PfxCertificate -FilePath cine-dev.pfx -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

**Limitation**: Self-signed MSIX only installs on dev machines. Users see "Windows protected your PC" SmartScreen warning.

### Production (Trusted CA)

For public distribution, obtain a code signing certificate from:

| Provider | Type | Price | Notes |
|---|---|---|---|
| [DigiCert](https://www.digicert.com/code-signing/) | EV Code Signing | ~$300/yr | SmartScreen reputation instantly |
| [Sectigo](https://sectigo.com/code-signing) | OV Code Signing | ~$200/yr | Builds reputation over time |
| [SSL.com](https://www.ssl.com/certificates/code-signing/) | OV Code Signing | ~$150/yr | Budget option |
| [Azure Key Vault](https://azure.microsoft.com/services/key-vault/) | Managed HSM signing | ~$3/mo + cert | Best for CI/CD; cert never leaves Azure |

**Recommendation**: Start with **Azure Key Vault** managed signing. Cert never touches CI runners — extremely secure. For Phase 8 launch, use self-signed with a clear install instructions page.

### Signing in the Pipeline

```yaml
# Option A: PFX file (simpler, less secure)
- name: Sign MSIX
  run: |
    signtool sign /fd SHA256
      /f '${{ runner.temp }}\cine.pfx'
      /p '${{ secrets.PFX_PASSWORD }}'
      /tr http://timestamp.digicert.com
      /td SHA256
      dist/Cine_*.msix

# Option B: Azure Key Vault (production)
- name: Sign MSIX (Azure Key Vault)
  uses: azure/signtool-action@v1
  with:
    key-vault-name: cine-codesign
    certificate-name: CineCodeSign
    files: dist/Cine_*.msix
    timestamp-server: http://timestamp.digicert.com
```

---

## 8F — File Associations via MSIX Manifest

MSIX file associations are **declarative** — no registry writes, clean uninstall, no leftovers:

```xml
<!-- Package.appxmanifest -->
<Extensions>
  <uap3:Extension Category="windows.fileTypeAssociation">
    <uap3:FileTypeAssociation Name="cine-media"
                              Parameters=""%1"">
      <uap:SupportedFileTypes>
        <uap:FileType ContentType="video/mp4">.mp4</uap:FileType>
        <uap:FileType ContentType="video/x-matroska">.mkv</uap:FileType>
        <uap:FileType ContentType="video/x-msvideo">.avi</uap:FileType>
        <uap:FileType ContentType="video/quicktime">.mov</uap:FileType>
        <uap:FileType ContentType="video/webm">.webm</uap:FileType>
        <uap:FileType ContentType="video/MP2T">.ts</uap:FileType>
        <uap:FileType ContentType="video/mpeg">.mpg</uap:FileType>
        <uap:FileType ContentType="video/mpeg">.mpeg</uap:FileType>
        <uap:FileType ContentType="video/x-ms-wmv">.wmv</uap:FileType>
        <uap:FileType ContentType="video/mp4">.m4v</uap:FileType>

        <!-- Audio formats -->
        <uap:FileType ContentType="audio/mpeg">.mp3</uap:FileType>
        <uap:FileType ContentType="audio/flac">.flac</uap:FileType>
        <uap:FileType ContentType="audio/x-wav">.wav</uap:FileType>
        <uap:FileType ContentType="audio/ogg">.ogg</uap:FileType>
        <uap:FileType ContentType="audio/aac">.aac</uap:FileType>
        <uap:FileType ContentType="audio/wma">.wma</uap:FileType>
        <uap:FileType ContentType="audio/x-ms-wma">.wma</uap:FileType>
        <uap:FileType ContentType="audio/m4a">.m4a</uap:FileType>
        <uap:FileType ContentType="audio/opus">.opus</uap:FileType>

        <!-- Subtitle formats -->
        <uap:FileType ContentType="text/plain">.srt</uap:FileType>
        <uap:FileType ContentType="text/plain">.ass</uap:FileType>
        <uap:FileType ContentType="text/plain">.ssa</uap:FileType>
        <uap:FileType ContentType="text/plain">.vtt</uap:FileType>
        <uap:FileType ContentType="text/plain">.sub</uap:FileType>

        <!-- Playlist formats -->
        <uap:FileType ContentType="text/plain">.m3u</uap:FileType>
        <uap:FileType ContentType="text/plain">.m3u8</uap:FileType>
      </uap:SupportedFileTypes>
    </uap3:FileTypeAssociation>
  </uap3:Extension>
</Extensions>
```

**Benefits over registry-based file association**:
- No admin rights needed for file association
- Clean uninstall removes all associations
- Windows manages the "Open With" menu automatically
- Works with Windows 10/11 default app settings

---

## 8G — Project Structure Changes

### New Files to Create

```
src/
├── App/
│   ├── Package.appxmanifest        ← NEW: MSIX identity + file associations + deps
│   ├── Package.StoreAssociation.xml │ NEW: Microsoft Store (optional, later)
│   ├── Assets/                      ← NEW: App icons (8 sizes) + splash screen
│   │   ├── CineLogo44x44.png
│   │   ├── CineLogo71x71.png
│   │   ├── CineLogo150x150.png
│   │   ├── CineLogo310x150.png
│   │   ├── CineLogo75x75.png
│   │   ├── CineSplash.png
│   │   ├── CineBadge24x24.png
│   │   └── CineLogo124x124.png
│   ├── Msix.AppInstaller.Data/     ← NEW: Custom installer UX
│   │   └── MSIXAppInstallerData.xml
│   └── App.csproj                  ← MODIFY: add WindowsPackageType=MSIX + signing config

tools/
└── Cine.Packager/                  ← NEW: dotnet tool wrapping MakeAppx + signtool
    ├── Cine.Packager.csproj
    └── Program.cs

docs/
└── phase8-release-engineering-plan.md ← THIS FILE

.github/
└── workflows/
    └── release.yml                 ← NEW: CI/CD workflow (from Section 8D)

web/                                ← NEW: Landing page for web install
├── index.html                      ← "Install Cine" button with ms-appinstaller URI
└── Cine.appinstaller               ← Template (version injected by CI)
```

### `.csproj` Changes

```xml
<!-- src/App/App.csproj additions -->
<PropertyGroup>
  <!-- Enable MSIX packaging -->
  <WindowsPackageType>MSIX</WindowsPackageType>

  <!-- Framework-dependent = no bundled runtime -->
  <SelfContained>false</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>

  <!-- Package identity -->
  <AppxPackageDir>$(SolutionDir)dist\</AppxPackageDir>
  <GenerateAppxPackageOnBuild>false</GenerateAppxPackageOnBuild>
  <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>

  <!-- Branding -->
  <ApplicationTitle>Cine</ApplicationTitle>
  <ApplicationIcon>Assets\CineLogo44x44.png</ApplicationIcon>
  <AssemblyName>Cine.Avalonia</AssemblyName>

  <!-- Version from Git tag -->
  <Version>1.0.0.0</Version>
</PropertyGroup>

<!-- App icons as content (included in MSIX) -->
<ItemGroup>
  <Content Include="Assets\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="Msix.AppInstaller.Data\MSIXAppInstallerData.xml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

## 8H — Web Install Page

A simple HTML page at `cine.app` that triggers MSIX web install with a single click:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Install Cine — Modern Media Player</title>
    <style>
        :root {
            --bg: #1A1A2E;
            --accent: #6C5CE7;
            --text: #E0E0E0;
            --card: #16213E;
        }
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI Variable', 'Segoe UI', sans-serif;
            background: var(--bg);
            color: var(--text);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            text-align: center;
        }
        .card {
            background: var(--card);
            border-radius: 16px;
            padding: 48px 64px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.5);
            max-width: 480px;
        }
        .logo {
            width: 124px;
            height: 124px;
            margin-bottom: 24px;
            filter: drop-shadow(0 0 20px rgba(108,92,231,0.3));
        }
        h1 { font-size: 28px; margin-bottom: 8px; }
        .subtitle { color: #888; margin-bottom: 32px; font-size: 14px; }
        .features {
            text-align: left;
            margin-bottom: 32px;
            font-size: 13px;
            color: #999;
            line-height: 1.8;
        }
        .features span { color: var(--accent); margin-right: 8px; }
        .install-btn {
            display: inline-block;
            background: var(--accent);
            color: white;
            padding: 14px 48px;
            border-radius: 8px;
            text-decoration: none;
            font-size: 16px;
            font-weight: 600;
            transition: transform 0.15s, box-shadow 0.15s;
            box-shadow: 0 4px 20px rgba(108,92,231,0.4);
        }
        .install-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 30px rgba(108,92,231,0.6);
        }
        .note {
            margin-top: 24px;
            font-size: 11px;
            color: #666;
        }
    </style>
</head>
<body>
    <div class="card">
        <img class="logo" src="CineLogo124x124.png" alt="Cine Logo">
        <h1>Cine Media Player</h1>
        <p class="subtitle">A modern, high-performance media player for Windows</p>

        <div class="features">
            <p><span>▶</span> Hardware-accelerated playback via libmpv + ANGLE</p>
            <p><span>▶</span> Picture-in-Picture with aspect-ratio-locked resize</p>
            <p><span>▶</span> 50+ keyboard shortcuts. Full playlist management</p>
            <p><span>▶</span> SRT/ASS/VTT subtitles. Multi-track audio. EQ</p>
            <p><span>▶</span> Session resume. Auto-updates via App Installer</p>
        </div>

        <!-- ms-appinstaller: protocol triggers Windows App Installer -->
        <a class="install-btn"
           href="ms-appinstaller:?source=https://releases.cine.app/Cine.appinstaller">
            ⚡ Install Cine
        </a>

        <p class="note">
            Requires Windows 10 (19041+) or Windows 11.
            .NET 10 Desktop Runtime will be downloaded automatically if needed.
        </p>
    </div>
</body>
</html>
```

The `ms-appinstaller:` URI protocol is **built into Windows 10 1809+**. No extra software needed — clicking the link opens the App Installer dialog with the custom UX from Section 8B.

---

## 8I — Implementation Checklist

### Phase 8a — Foundation (Setup)
- [ ] **8.1** Create `Package.appxmanifest` with identity, file associations, framework deps
- [ ] **8.2** Generate app icon assets (8 sizes) from source SVG/PNG
- [ ] **8.3** Create `MSIXAppInstallerData.xml` with custom UX
- [ ] **8.4** Update `App.csproj` with MSIX properties (`WindowsPackageType=MSIX`, `SelfContained=false`)
- [ ] **8.5** Configure framework-dependent publish: `dotnet publish -p:SelfContained=false`

### Phase 8b — Packaging Tooling
- [ ] **8.6** Create `tools/Cine.Packager/` dotnet tool wrapping MSIX creation
- [ ] **8.7** Generate `.appinstaller` file with auto-update URL and update settings
- [ ] **8.8** Test local MSIX install: `Add-AppxPackage -Path Cine.msix`
- [ ] **8.9** Verify framework download works on clean VM (no .NET 10 preinstalled)

### Phase 8c — CI/CD
- [ ] **8.10** Create `.github/workflows/release.yml` (build + test + package + sign + release)
- [ ] **8.11** Generate self-signed certificate for CI
- [ ] **8.12** Add `BASE64_ENCODED_PFX` + `PFX_PASSWORD` GitHub secrets
- [ ] **8.13** Test CI pipeline: push tag, verify MSIX artifact uploaded to GitHub Release
- [ ] **8.14** Test web install flow: `ms-appinstaller:` URI → App Installer → launch

### Phase 8d — Polish
- [ ] **8.15** Create web install page (`web/index.html`) with proper branding
- [ ] **8.16** Test auto-update: install v1.0.0, push v1.1.0 to CDN, verify silent update
- [ ] **8.17** Test file association: double-click .mp4 → Cine opens as default
- [ ] **8.18** Test clean uninstall: `Remove-AppxPackage`, verify no files/registry left
- [ ] **8.19** Document install instructions on README and website

### Phase 8e — Production (Future)
- [ ] **8.20** Obtain trusted CA code signing certificate
- [ ] **8.21** Migrate signing to Azure Key Vault
- [ ] **8.22** Submit to Microsoft Store (optional — reaches more users)
- [ ] **8.23** Submit to `winget` package repository
- [ ] **8.24** Set up custom domain `releases.cine.app` with CDN

---

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| mpv native DLLs too large (~45MB) | High | Medium | Block-level compression in MSIX; post-install download as fallback |
| .NET 10 Runtime not yet in Microsoft framework repo | Low | High | Fall back to self-contained package (larger) until CDN available; add bootstrapper |
| Self-signed cert causes SmartScreen warning | High | Medium | Clear install instructions; "Run anyway" link; migrate to EV cert after validation |
| MSIX incompatible with Windows 10 LTSC | Low | Low | Keep WiX MSI as fallback for enterprise; document limitation |
| `ms-appinstaller:` protocol blocked by IT policy | Low | Medium | Provide direct `.msix` download link as alternative |

---

## Success Metrics

| Metric | Target | Measurement |
|---|---|---|
| MSIX package size | &lt; 25 MB | Check `.msix` file size after build |
| Install time (cold, no .NET) | &lt; 2 minutes | Stopwatch on clean Windows VM |
| Install time (warm, .NET cached) | &lt; 10 seconds | Stopwatch on second install |
| Auto-update success rate | &gt; 95% | Telemetry / app-insights |
| CI pipeline duration | &lt; 5 minutes | GitHub Actions run log |
| 0 uninstall leftovers | Verified | `Get-AppxPackage` + registry scan |
| File association works | 30+ formats | Manual testing with each format |
