# Cine — Production Readiness: 20% → 100% Roadmap

> **Audience**: Cine development team  
> **Scope**: Full codebase audit — architecture, dead code, UI, error handling, standards  
> **Methodology**: Internet research + Avalonia/C# best practices + deep codebase analysis  
> **Goal**: Ship a production-grade media player that is stable, clean, testable, and maintainable

---

## Executive Summary

Cine has a solid foundation — working file dialogs (after our fix), mpv render integration, PiP, playlist, chapters, subtitles, audio manager, session persistence. However, the codebase has accumulated technical debt that makes it ~20% production-ready. 

This document provides a phased, prioritized plan to reach 100%.

### Key Metrics (Current → Target)

| Metric | Current | Target |
|--------|---------|--------|
| Dead/unused XAML elements | 9 | 0 |
| Empty catch blocks | 12 | 0 (at minimum logged) |
| Partial class files (MainWindow) | 10 | 4 |
| Partial class files (MainViewModel) | 9 | 3 |
| Unused packages/imports | 2+ | 0 |
| Test coverage | 0% | >60% ViewModels |
| NRT violations | Unknown | 0 |
| Culture-aware formatting | Partial | Full |

---

## Phase 0 — Immediate Cleanup (Before Anything Else)

These are quick wins that eliminate visual noise and dead paths. **Estimated effort: 1-2 hours**.

### 0.1 Remove `#debug-point` Regions

**Files affected**: ~29 files (entire codebase)

```csharp
// ❌ Remove this pattern everywhere:
#region debug-point
// Some debug code or comment
#endregion
```

These debug regions are scaffolding markers that serve no purpose in production code. They clutter the IDE outline view and suggest the code is unfinished.

**Action**: 
```powershell
# Find and review
Select-String -Path src\App -Recurse -Pattern "#region debug-point|#debug-point|debug-point"
# Remove all matching regions (they are traces of development, not active debug code)
```

### 0.2 Remove `#if DEBUG` Developer Tools Attachment

**File**: [`src/App/App.axaml.cs:32`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs#L32)

```csharp
// ❌ Current — debug tool always attached in DEBUG builds
#if DEBUG
    this.AttachDeveloperTools();
#endif

// ✅ Fix — use command-line flag instead
if (args?.Contains("--devtools") == true)
    this.AttachDeveloperTools();
```

Per [Avalonia docs](https://docs.avaloniaui.net/tools/faq/), `AvaloniaUI.DiagnosticsSupport` should be excluded from Release builds. The `#if DEBUG` approach means DevTools is always active during development — change to opt-in.

### 0.3 Remove or Implement Hidden Menu Buttons

These XAML buttons are `IsVisible="False"` with no code path that ever shows them:

| Control | File | Issue |
|---------|------|-------|
| `BtnPip` | [`HeaderBarControl.axaml:134`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml#L134) | Always hidden — PiP is handled by MainWindow, not HeaderBar |
| `BtnPrimaryMenu` | [`HeaderBarControl.axaml:145`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml#L145) | Always hidden with empty Flyout |
| `BtnFullscreenClose` | [`HeaderBarControl.axaml:163`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml#L163) | Always hidden — fullscreen close is in FullscreenHeaderControl |

**Action**: Either implement the functionality these buttons were planned for, or remove them from XAML entirely. Hidden-but-present elements still participate in layout passes and add to visual tree complexity.

### 0.4 Fix Empty/Silent Catch Blocks

12 catch blocks silently discard exceptions. At minimum, every catch should log:

```csharp
// ❌ Production code should never silently discard
catch { }  
catch { /* best-effort */ }
catch { /* silently fail */ }

// ✅ Minimum standard
catch (Exception ex)
{
    Log.ForContext<T>().Warning("Operation X failed: {Error}", ex.Message);
}
```

**Priority fixes** (these hide real bugs):

| File | Line | What it catches | Risk |
|------|------|-----------------|------|
| `PlaylistDialog.axaml.cs:233` | `catch { /* silently fail */ }` | Drag-drop validation | High — data loss possible |
| `PlaylistDialog.axaml.cs:250` | `catch { /* silently fail */ }` | Playlist operation | High |
| `ControlsBoxControl.axaml.cs:41` | `catch { }` | State comparison | Medium |
| `AudioEqualizerFlyout.axaml.cs:30` | `catch { }` | JSON deserialization | Medium |

---

## Phase 1 — Architecture Refactoring

Split the monolithic ViewModel and View into manageable units. **Estimated effort: 8-16 hours**.
> **Full detailed plan at**: [`docs/phase1-architecture-refactoring-plan.md`](file:///x:/Development/Cine_CSharp_DotNet/docs/phase1-architecture-refactoring-plan.md)
> Includes: service extraction (IPlaylistService, ISessionService, IRendererService, IMediaFileService), ViewModel decomposition from 3→5 partials (each 1/3 the size), MainWindow from 10→3 partials, PipWindowManager extraction, interface contracts, step-by-step migration order for 3 weeks.

### 1.1 Split `MainViewModel` (Currently 3 partial files, ~1800 lines)

**Current structure**:

```
MainViewModel.cs                (Core: properties, INotifyPropertyChanged, Dispose)
MainViewModel.Actions.cs        (File ops, session, playlist, renderer, cleanup)
MainViewModel.Audio.cs          (Audio proxies)
MainViewModel.Tracks.cs         (Chapter/video/subtitle track switching)
```

**Target structure**:

```
ViewModels/
├── MainViewModel.cs               (Core: properties, init, dispose — ~200 lines)
├── MainViewModel.Commands.cs      (All RelayCommand handlers — ~250 lines)
├── MainViewModel.FileOps.cs       (OpenFile, OpenFiles, session save/load — ~150 lines)
├── Services/
│   ├── PlaylistCoordinator.cs     (Playlist logic: add, remove, shuffle, loop, save)
│   ├── SessionManager.cs          (Session save/load — separated from ViewModel)
│   ├── TrackCoordinator.cs        (Chapter/video/subtitle track switching)
│   └── RendererCoordinator.cs     (Renderer mode switching)
├── Managers/
│   ├── AudioManager.cs            (Already exists — move to Services/)
│   └── SubtitleManager.cs        (Already exists — move to Services/)
```

**Why**: 9 partial files for one class is a code smell per [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions). Partial classes should be used sparingly (generated code, platform-specific code). Data operations + presentation logic in one class violates Single Responsibility.

### 1.2 Split `MainWindow` (Currently 10 partial files)

**Current**:
```
MainWindow.App.axaml.cs           (Window template wiring — misnamed!)
MainWindow.cs                     (Constructor)
MainWindow.Core.cs                (Init, Loaded, cleanup, video container)
MainWindow.AutoHide.cs            (Fullscreen auto-hide)
MainWindow.FileDialogs.cs         (Dialog delegates)
MainWindow.Fullscreen.cs          (Fullscreen toggle)
MainWindow.Input.cs               (Pointer/key events)
MainWindow.Media.cs               (Media event handlers)
MainWindow.Pip.cs                 (PiP management)
MainWindow.WindowControls.cs      (Min/max/close, title bar)
```

**Target**:
```
Shell/
├── MainWindow.cs                  (Constructor only)
├── MainWindow.Initialization.cs   (InitPlayer, OnWindowLoaded, OnWindowClosed)
├── MainWindow.Fullscreen.cs       (Toggle + auto-hide)
├── MainWindow.Pip.cs              (PiP management)
├── MainWindow.Input.cs            (Pointer/key events)
└── MainWindow.WindowControls.cs   (Min/max/close, title bar)
```

**Removed/merged**:
- `MainWindow.App.axaml.cs` → rename to `MainWindow.Template.cs` (it handles template-applied, not App logic)
- `MainWindow.FileDialogs.cs` → merge into `Initialization.cs` (it's just 5 one-liner delegates)
- `MainWindow.Media.cs` → move event handlers to `Initialization.cs`
- `MainWindow.AutoHide.cs` → merge into `Fullscreen.cs`

### 1.3 Remove Unnecessary Imports

**File**: [`src/App/App.csproj:25-26`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.csproj#L25-L26)

```xml
<!-- ✅ Keep — Core and Media are essential -->
<ProjectReference Include="..\Core\Core.csproj" />
<ProjectReference Include="..\Media\Media.csproj" />
```

**File**: [`src/App/App.csproj:20-21`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.csproj#L20-L21)

```xml
<!-- ✅ Essential packages -->
<PackageReference Include="Avalonia" Version="12.0.3" />
<PackageReference Include="Avalonia.Desktop" Version="12.0.3" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.3" />
<PackageReference Include="Material.Icons.Avalonia" Version="3.0.2" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.3" />
<!-- ⚠️ Check: Is DI container actually used? If not, remove it -->
```

Check if `Microsoft.Extensions.DependencyInjection` is actually used. If services are manually instantiated, remove the package — it's large and complex for simple use cases.

---

## Phase 2 — Error Handling Standardization

**Estimated effort: 3-4 hours**.

### 2.1 Standardize Catch Policy

| Scenario | Pattern |
|----------|---------|
| **User-facing operation** (file open, save) | Log warning + return null/empty |
| **Background operation** (state sync, cleanup) | Log error, never throw |
| **Dispose** | Log error, never throw |
| **Critical invariant** (init, db write) | Log error + throw (fail fast) |

```csharp
// ✅ Reference implementation
public async Task<string[]?> OpenFilesAsync()
{
    try
    {
        // ...
    }
    catch (Exception ex)
    {
        Log.ForContext<FileDialogHandler>()
            .Warning(ex, "OpenFiles dialog failed");
        return null;
    }
}
```

### 2.2 Replace All Silent Catches

Run this to find all candidates:

```powershell
Select-String -Path src\App -Recurse -Pattern 'catch\s*\{\s*/\*|catch\s*\{\s*\}' 
```

Every match needs either:
1. A log statement, OR
2. A comment explaining why the exception is genuinely ignorable + what conditions cause it

### 2.3 Add Global Unhandled Exception Handler

**File**: [`src/App/App.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs)

```csharp
public override void OnFrameworkInitializationCompleted()
{
    // Global exception handler — writes to log before crash
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    {
        Log.ForContext<App>().Fatal(e.ExceptionObject as Exception, "Unhandled exception");
    };

    TaskScheduler.UnobservedTaskException += (_, e) =>
    {
        Log.ForContext<App>().Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    };

    // ... rest of init
}
```

---

## Phase 3 — UI Polish & Layout Fixes

**Estimated effort: 4-6 hours**.

### 3.1 Standardize Spacing & Padding

Per [Avalonia styling best practices](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/) and Material Design guidelines:

**Issue**: The codebase mixes hardcoded margins (`Margin="0,2,0,0"`, `Margin="0,0,4,0"`) with `{StaticResource}` references. This makes it impossible to globally adjust spacing.

**Fix**: Move all spacing to `Spacing.axaml` resources and reference by key:

```xml
<!-- Spacing.axaml — add these keys -->
<Thickness x:Key="space-xs">4</Thickness>
<Thickness x:Key="space-sm">8</Thickness>
<Thickness x:Key="space-md">12</Thickness>
<Thickness x:Key="space-lg">16</Thickness>
<Thickness x:Key="space-xl">24</Thickness>

<!-- XAML usage -->
<Button Margin="{StaticResource space-sm}" />
```

### 3.2 Fix Tooltip Accessibility

Many buttons have `ToolTip.Tip` but no `AutomationProperties.Name`. Every interactive element must have both:

```xml
<!-- ❌ Missing AutomationProperties.Name -->
<Button ToolTip.Tip="Open Files" />

<!-- ✅ Screen reader accessible -->
<Button ToolTip.Tip="Open Files"
        AutomationProperties.Name="Open media files" />
```

### 3.3 Fix Hidden Overlay Elements

Several overlays use `Opacity="0"` + `IsVisible="False"` — redundant:

```xml
<!-- ❌ Both Opacity and IsVisible set → one is useless -->
<Border IsVisible="False" Opacity="0" />

<!-- ✅ Use IsVisible only (it skips layout pass) -->
<Border IsVisible="False" />
```

**Affected files**:
- [`OsdNotificationControl.axaml:26`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/OsdNotificationControl.axaml#L26)
- [`PauseOverlayControl.axaml:7`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/PauseOverlayControl.axaml#L7)
- [`SpinnerOverlayControl.axaml:21-22`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/SpinnerOverlayControl.axaml#L21-L22)

### 3.4 PiP Window — Verify Aspect Ratio on Resize

The PiP window uses custom resize edge handlers but doesn't call `ApplyAspectRatio()` on every resize step — only after the resize ends. This means the window can go out of aspect ratio mid-drag. Call `ApplyAspectRatio()` in `OnEdgePointerMoved` during active resize.

### 3.5 StartPage — Ghost TextBlock

**File**: [`StartPage.axaml:29`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Start/StartPage.axaml#L29)

```xml
<TextBlock IsVisible="False">...</TextBlock>
```

An invisible TextBlock still allocates memory and text layout resources. If it's genuinely unused, remove it. If it's toggled by code, ensure the toggle actually works.

---

## Phase 4 — Performance & Memory

**Estimated effort: 4-6 hours**.

### 4.1 JsonSerializer Source Generation

Per [Microsoft docs](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation), source generation provides up to **40% startup time reduction** and eliminates reflection-based trimming issues.

**Current**: The codebase uses `JsonSerializer.Serialize(obj)` / `JsonSerializer.Deserialize<T>(json)` with reflection (no source-gen context):

```csharp
// ❌ Reflection-based — slow startup, trim-unfriendly
var json = JsonSerializer.Serialize(session);
var state = JsonSerializer.Deserialize<PipState>(json);

// ✅ Source-gen — AOT-compatible, fast
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SessionData))]
[JsonSerializable(typeof(PipState))]
[JsonSerializable(typeof(AudioSettings))]
internal partial class CineJsonContext : JsonSerializerContext { }

// Usage
var json = JsonSerializer.Serialize(session, CineJsonContext.Default.SessionData);
```

**Affected files**:
- `MainViewModel.Actions.cs` — session persistence  
- `PipWindow.axaml.cs` — PiP state save/load
- `AudioSettingsStore.cs` — audio EQ settings
- `PlaylistSettingsStore.cs` — playlist persistence

### 4.2 Thread Safety Audit

Three places use `Task.Run` or `new Thread`:

| File | Thread creation | Purpose | Issue |
|------|----------------|---------|-------|
| [`MpvVideoView.cs:136`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Controls/MpvVideoView.cs#L136) | `new Thread(RenderLoop)` | mpv render | OK — required by mpv |
| [`MainWindow.Input.cs:251`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs#L251) | `Task.Run(Delay)` | Double-tap detection | OK — fire-and-forget |
| [`MainWindow.Media.cs:35`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Media.cs#L35) | `Task.Run(Delay)` | Playlist UI update | ⚠️ No cancellation — can still fire after window closed |

**Fix for MainWindow.Media.cs:35**: Use `CancellationTokenSource` tied to window lifetime so delayed UI updates don't fire after close.

### 4.3 SeekBar and Position Update Frequency

Position updates come from mpv every frame (~60 Hz). The SeekBar `Slider` control fires `ValueChanged` for every update, causing unnecessary layout passes.

**Fix**: Use `IsSeeking` flag to suppress programmatic position updates while the user is dragging the seek bar. (Already partially implemented — verify it works.)

### 4.4 NativeAOT Readiness

If NativeAOT compilation is planned, remove:

1. All reflection-based JSON serialization (→ source-gen, Phase 4.1)
2. `System.Xml.Linq` usage if any
3. Verify `Material.Icons.Avalonia` is trim-compatible

---

## Phase 5 — Code Standards & Naming

**Estimated effort: 2-3 hours**.

### 5.1 Nullable Reference Types

Ensure all projects have NRT enabled:

```xml
<!-- .csproj -->
<Nullable>enable</Nullable>
<WarningsAsErrors>nullable</WarningsAsErrors>
```

Then fix all NRT warnings. Common fixes:

```csharp
// ❌ Nullable warning (no NRT annotation on field assigned via InitVideoRenderer)
private MpvVideoView _videoView;
// → Assign in constructor or mark as nullable
private MpvVideoView? _videoView;  // or = null! if guaranteed initialized

// ❌ Nullable warning (no null check before dereference)
_dialogHandler!.OpenFilesAsync()
// → Use null-conditional
_dialogHandler?.OpenFilesAsync()
```

### 5.2 Naming Conventions

| Issue | Current | C# Standard |
|-------|---------|-------------|
| Private fields | `_fieldName` ✅ | `_camelCase` |
| Methods | `PascalCase` ✅ | `PascalCase` |
| XAML control names | `BtnOpenMenu` ✅ | `PascalCase` |
| Resource keys | `space-h-2` ⚠️ | `kebab-case` for XAML keys is OK |
| File names | `MainWindow.AutoHide.cs` ⚠️ | Unconventional — prefer `MainWindow+AutoHide.cs` (VS convention) or just split to separate class |

### 5.3 Remove Commented-Out Code

```csharp
// ❌ Never commit commented-out code — it's what git history is for
// _viewModel?.PlayPause();
// Icon updates via PlaybackStateManager.StateChanged — no optimistic toggle
```

Run: `Select-String -Path src\App -Recurse -Pattern '^\s*//.*;'` to find lines that look like commented-out code (non-documentation comments ending with `;`).

---

## Phase 6 — Testing Infrastructure

**Estimated effort: 8-12 hours**.

### 6.1 Test Project Setup

```xml
<!-- tests/Cine.Avalonia.Tests/Cine.Avalonia.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.0" />
    <PackageReference Include="Moq" Version="4.20.70" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\App\App.csproj" />
  </ItemGroup>
</Project>
```

### 6.2 Priority Test Targets

| Test | What it covers | Priority |
|------|---------------|----------|
| `PlaylistCoordinatorTests` | Add/remove/reorder/shuffle | 🔴 P0 |
| `SessionManagerTests` | Save/load session JSON round-trip | 🔴 P0 |
| `AudioManagerTests` | Volume clamping, EQ save/load | 🟡 P1 |
| `SubtitleManagerTests` | Track selection, delay adjustment | 🟡 P1 |
| `FileDialogHandlerTests` | Path normalization, null handling | 🟢 P2 |
| `MainViewModel.FileOpsTests` | OpenFile, OpenFiles, session resume | 🔴 P0 |

### 6.3 Test Example

```csharp
public class PlaylistCoordinatorTests
{
    [Fact]
    public void Add_DuplicatePath_ShouldNotAddTwice()
    {
        var coordinator = new PlaylistCoordinator();
        coordinator.Add("C:\\video.mp4");
        coordinator.Add("C:\\video.mp4");
        Assert.Single(coordinator.Items);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_ShouldPreserveOrder()
    {
        var coordinator = new PlaylistCoordinator();
        coordinator.Add("C:\\a.mp4");
        coordinator.Add("C:\\b.mp4");
        
        coordinator.Save();
        coordinator.Clear();
        coordinator.Load();
        
        Assert.Equal(2, coordinator.Items.Count);
        Assert.Equal("C:\\a.mp4", coordinator.Items[0]);
    }
}
```

---

## Phase 7 — Documentation

**Estimated effort: 2-3 hours**.

### 7.1 Required Docs

| Doc | Content |
|-----|---------|
| `README.md` | Project overview, build instructions, architecture diagram |
| `docs/ARCHITECTURE.md` | Component diagram, data flow, threading model |
| `docs/BUILD.md` | Prerequisites, build steps, dependencies |
| `docs/CONTRIBUTING.md` | Code style, PR process, branch strategy |

### 7.2 XML Doc Comments

Every public method and property should have `<summary>` XML doc:

```csharp
/// <summary>Opens a media file via mpv, updating all UI state.</summary>
/// <param name="path">Absolute path to the media file.</param>
/// <remarks>Callers: File menu, drag-drop, session resume, recent files, CLI args.</remarks>
public async void OpenFile(string path) { ... }
```

Current coverage: ~30%. Target: >90% of public API.

---

## Phase 8 — Release Engineering

**Estimated effort: 3-4 hours**.

### 8.1 Versioning

Add to `App.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <InformationalVersion>1.0.0</InformationalVersion>
  <ApplicationIcon>Assets\cine.ico</ApplicationIcon>
</PropertyGroup>
```

### 8.2 Build Configurations

| Config | Purpose | Defines |
|--------|---------|---------|
| Debug | Development with DevTools | `DEBUG;DEVELOPER_TOOLS` |
| DebugNoTools | Development without DevTools | `DEBUG` |
| Release | Production | (none) |

### 8.3 Windows Installer (MSIX/Squirrel)

For production deployment, use `dotnet publish` with single-file:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>false</SelfContained>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

---

## Implementation Priority Matrix

```
                        Impact
                 Low       Medium     High
            ┌──────────┬──────────┬──────────┐
    Low     │ 0.3      │ 3.2      │ 0.1      │
            │ Hidden   │ Tooltips │ Debug    │
            │ buttons  │          │ regions  │
            ├──────────┼──────────┼──────────┤
Effort      │ 5.2      │ 2.3      │ 0.4      │
    Medium  │ Naming   │ Unhandled│ Empty    │
            │          │ handler  │ catches  │
            ├──────────┼──────────┼──────────┤
    High    │ 1.1      │ 4.1      │ 6.1      │
            │ Split VM │ JSON src │ Test     │
            │          │ gen      │ infra    │
            └──────────┴──────────┴──────────┘
```

**Recommended order**:
1. Phase 0 (1-2 hrs) — Immediate cleanup, highest ROI
2. Phase 2 (3-4 hrs) — Error handling standardization
3. Phase 3 (4-6 hrs) — UI polish
4. Phase 4 (4-6 hrs) — Performance (JSON source-gen)
5. Phase 1 (8-16 hrs) — Architecture refactoring
6. Phase 6 (8-12 hrs) — Testing
7. Phase 5 (2-3 hrs) — Code standards
8. Phase 7 (2-3 hrs) — Documentation
9. Phase 8 (3-4 hrs) — Release engineering

---

## References

1. [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
2. [Avalonia MVVM Pattern Best Practices](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/)
3. [CommunityToolkit.Mvvm Documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
4. [System.Text.Json Source Generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
5. [Avalonia Developer Tools FAQ](https://docs.avaloniaui.net/tools/faq/)
6. [Avalonia Deployment Best Practices](https://docs.avaloniaui.net/docs/deployment/)
7. [mpv render.h — Thread Safety Documentation](https://github.com/mpv-player/mpv/blob/master/include/mpv/render.h)
8. [Avalonia #18969 — Flyout + StorageProvider Freeze](https://github.com/AvaloniaUI/Avalonia/issues/18969)
9. [C# nullable reference types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)
10. [.NET ReadyToRun compilation](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)
