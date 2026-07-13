# Phase 5: Implementation Roadmap

> **Version:** 1.0  
> **Date:** 2026-07-02  
> **Total Effort Estimate:** ~18 days (single developer)

---

## Working Copy Strategy

The v3 architecture is built alongside the existing v2 codebase using a **parallel folder strategy**:

```
Cine_CSharp_DotNet/
├── src/           ← v2 reference (unchanged, git-committed as "v2:completed")
├── srcv3/         ← v3 working copy (built file-by-file during implementation)
└── md_new/Phase5/ ← Architecture docs
```

**Workflow:**
1. When a file needs modification for v3, first **copy** it from `src\` to `srcv3\` at the same relative path
2. Then **modify** the copy in `srcv3\`
3. New v3-only files (e.g., `CompositionRoot.cs`) are created directly in `srcv3\`
4. `src\` remains untouched as the v2 stable reference
5. All builds and tests run from `srcv3\`

**Commit convention:** `v3: <phase> - <description>`

**Example:**
```
v3: 5.1 - Add CompositionRoot with DI container
v3: 5.2 - Split IMediaPlayer into role interfaces
```

---

## Roadmap Overview

The Phase 5 architecture changes are organized into **6 implementation phases**, designed to be deployed incrementally. Each phase is independently testable.

```
Phase 5.1 ───► DI Container + Composition Root (2 days)
    │
    ▼
Phase 5.2 ───► ISP Refactor (Split IMediaPlayer) (3 days)
    │
    ▼
Phase 5.3 ───► Domain Manager Refactor + Mediator (3 days)
    │
    ▼
Phase 5.4 ───► Feature Flag + Licensing System (3 days)
    │
    ▼
Phase 5.5 ───► Codec Plugin Architecture (3 days)
    │
    ▼
Phase 5.6 ───► UI Gating + Polish (2 days)
```

**Total: ~16–18 days** (includes buffer for testing and bug fixes)

---

## Phase 5.1: DI Container + Composition Root

**Effort:** 2 days  
**Risk:** Low  
**Files affected:** ~10 files (+1 new)

### Tasks

| # | Task | Details |
|---|------|---------|
| 5.1.1 | Create `CompositionRoot.cs` | Static class that builds `IServiceProvider` using `Microsoft.Extensions.DependencyInjection` |
| 5.1.2 | Register existing services | Move all `new Xxx()` calls from `MainWindow.Initialization.cs` into DI container |
| 5.1.3 | Update `App.axaml.cs` | Call `CompositionRoot.Build()` and pass `IServiceProvider` to `MainWindow` constructor |
| 5.1.4 | Refactor `MainWindow` constructors | Accept `IServiceProvider` via constructor injection; resolve dependencies from container |
| 5.1.5 | Refactor `MainViewModel` constructor | Accept dependencies via DI; make all constructor params required (remove `= null` fallbacks) |
| 5.1.6 | Remove ad-hoc `new()` | Eliminate all manual `new ServiceXxx()` in `MainWindow` partials and ViewModels |
| 5.1.7 | Verify build + run | Ensure app starts and all services resolve correctly |

### Key Code Changes

```csharp
// ── Before (MainWindow.Initialization.cs) ──
_playerService ??= new PlayerService();
_playerService.Initialize();
_audioManager = new AudioManager(player);
_flyoutManager = new FlyoutManager();
_controlsBox.FlyoutManager = _flyoutManager;
_viewModel = new MainViewModel(player, null, null, _audioManager, ...);

// ── After ──
// MainWindow constructor:
public MainWindow(IServiceProvider services)
{
    _services = services;
    InitializeComponent();
}

public void OnWindowInitialized()
{
    _playerService = _services.GetRequiredService<IPlayerService>();
    _audioManager = _services.GetRequiredService<IAudioManager>();
    _viewModel = _services.GetRequiredService<MainViewModel>();
    _controlsBox.FlyoutManager = _services.GetRequiredService<IFlyoutService>();
}
```

### Rollback Strategy

If issues arise, revert `MainWindow` constructors to manual `new()` calls. All changes in this phase are contained to wiring code — no business logic changes.

---

## Phase 5.2: ISP Refactor (Split IMediaPlayer)

**Effort:** 3 days  
**Risk:** Medium  
**Files affected:** 30+ files

### Tasks

| # | Task | Details |
|---|------|---------|
| 5.2.1 | Define 6 role interfaces | `IPlaybackControl`, `IAudioControl`, `IVideoControl`, `ISubtitleControl`, `IChapterNavigation`, `IPlaylistManagement` |
| 5.2.2 | Add composite `IMediaPlayer` | Keep existing interface as a combined interface inheriting all 6 (for backward compat) |
| 5.2.3 | Implement interfaces on `MpvPlayer` | `MpvPlayer` already implements all members — just update class declaration to `: IMediaPlayer` (implicit) |
| 5.2.4 | Update domain managers | `AudioManager` → inject `IAudioControl`, `VideoManager` → inject `IVideoControl`, etc. |
| 5.2.5 | Update `PlayerService` | Expose split interfaces instead of single `IMediaPlayer` |
| 5.2.6 | Update `MainViewModel` | Subscribe to specific role interfaces, not full `IMediaPlayer` |
| 5.2.7 | Remove direct `IMediaPlayer` references from ViewModel | Delegate player events through domain managers via ICommandBus |
| 5.2.8 | Verify build + run | Test all playback features |

### ISP Interface Structure

```csharp
public interface IPlaybackControl { /* ~25 members */ }
public interface IAudioControl { /* ~15 members */ }
public interface IVideoControl { /* ~15 members */ }
public interface ISubtitleControl { /* ~20 members */ }
public interface IChapterNavigation { /* ~8 members */ }
public interface IPlaylistManagement { /* ~10 members */ }

// Backward compatibility composite:
public interface IMediaPlayer : IPlaybackControl, IAudioControl, IVideoControl,
    ISubtitleControl, IChapterNavigation, IPlaylistManagement { }
```

### Risk Mitigation

- Keep the `IMediaPlayer` composite interface throughout 5.2 — all existing code continues to compile
- Phase 5.2.7 (direct ViewModel subscriber removal) can be deferred if too risky
- Test each manager independently after interface change

---

## Phase 5.3: Domain Manager Refactor + Mediator

**Effort:** 3 days  
**Risk:** Medium  
**Files affected:** ~20 files (+8 new)

### Tasks

| # | Task | Details |
|---|------|---------|
| 5.3.1 | Create `DomainManager<T>` base class | Abstract common constructor pattern, logger, disposal |
| 5.3.2 | Update `AudioManager` | Inherit `DomainManager<IAudioControl>`, inject `IAudioSettingsStore` via constructor |
| 5.3.3 | Update `VideoManager` | Inherit `DomainManager<IVideoControl>`, inject settings store |
| 5.3.4 | Update `SubtitleManager` | Inherit `DomainManager<ISubtitleControl>`, inject settings store |
| 5.3.5 | Update `PlaybackStateManager` | Inherit `DomainManager<IPlaybackControl>` |
| 5.3.6 | Create `ICommandBus` + `CommandBus` | Mediator implementation with event handler registration |
| 5.3.7 | Create event types | `FlyoutDismissingEvent`, `FileDialogRequestingEvent`, `MediaOpenedEvent`, `TrackChangedEvent` |
| 5.3.8 | Create event handlers | `FlyoutDismissalHandler`, `SessionResumeHandler`, `TrackChangeHandler` |
| 5.3.9 | Replace delegate wiring | Replace property-injection delegates (`DismissFlyoutAsync`) with command/event bus |
| 5.3.10 | Verify build + run | Test flyout, file dialog, and session interactions |

### Event Handler Pattern

```csharp
// Instead of:
_audioManager.DismissFlyoutAsync += (key, cb) => { ... };

// Use:
public class FlyoutDismissalHandler : IEventHandler<FileDialogRequestingEvent>
{
    private readonly IFlyoutService _flyoutService;
    public Task Handle(FileDialogRequestingEvent @event, CancellationToken ct)
    {
        _flyoutService.CloseAll();
        return Task.CompletedTask;
    }
}
```

---

## Phase 5.4: Feature Flag + Licensing System

**Effort:** 3 days  
**Risk:** Low–Medium  
**Files affected:** ~10 files (+5 new)

### Tasks

| # | Task | Details |
|---|------|---------|
| 5.4.1 | Create feature model classes | `FeatureDefinition`, `FeatureToggleType`, `LicensingTier` |
| 5.4.2 | Create `FeatureKeys` constants class | Compile-time safe feature key strings |
| 5.4.3 | Create `feature-definitions.json` | Embedded resource with all feature definitions |
| 5.4.4 | Implement `IFeatureStore` | Load embedded JSON + runtime overrides from config |
| 5.4.5 | Implement `IFeatureService` | Cached evaluation with dependency resolution |
| 5.4.6 | Create `FeatureGateAttribute` | Declarative feature gating for UI components |
| 5.4.7 | Implement `ILicensingService` | Encrypted license validation, trial tracking |
| 5.4.8 | Create `LicensingService` | AES-256-GCM encrypted license storage + hardware binding |
| 5.4.9 | Wire into DI container | Register `IFeatureService`, `IFeatureStore`, `ILicensingService` |
| 5.4.10 | Add minimal trial UI | Trial banner, upgrade CTA in ControlsBox |
| 5.4.11 | Verify build + run | Test feature toggles by tier |

### Feature Evaluation Flow

```
IsEnabled("codecs.hdr10")
    ├──► Load feature definition from embedded JSON
    ├──► Check license tier: Pro required, user is Full → false
    ├──► Check dependencies: playback.4k is enabled? → Yes
    └──► Return false (HDR10 disabled for Full tier)
```

---

## Phase 5.5: Codec Plugin Architecture

**Effort:** 3 days  
**Risk:** Medium  
**Files affected:** ~10 files (+8 new)

### Tasks

| # | Task | Details |
|---|------|---------|
| 5.5.1 | Create codec interfaces | `ICodecProvider`, `IDecodingSession`, `CodecCapability`, `DecodingOptions` |
| 5.5.2 | Create `DecodingSession` default impl | Wraps player instances into session with diagnostics |
| 5.5.3 | Implement `MpvCodecProvider` | Wraps `MpvPlayer` with H.264/H.265/VP9/AV1 capabilities |
| 5.5.4 | Implement `MFCodecProvider` | Wraps `MediaFoundationPlayer` |
| 5.5.5 | Implement `SoftwareFallbackCodecProvider` | Forces `hwdec=no` fallback |
| 5.5.6 | Create `CodecManager` | Provider selection + fallback logic |
| 5.5.7 | Wire `CodecManager` into DI | Register providers + manager |
| 5.5.8 | Update `PlayerService` | Use `CodecManager` instead of directly creating `MpvPlayer` |
| 5.5.9 | Create `CodecPluginLoader` | MEF-based external plugin loading (future) |
| 5.5.10 | Add "Codec Information" to Preferences | Show active codecs, capabilities, license status |
| 5.5.11 | Verify build + run | Test provider selection with known file types |

### Key Integration Point

```csharp
// ── Before ──
public void OpenMedia(string path)
{
    var player = new MpvPlayer();
    player.Open(path);
}

// ── After ──
public async Task OpenMediaAsync(string path)
{
    var session = await _codecManager.OpenMediaAsync(path);
    if (session == null) throw new UnsupportedMediaException(path);
    _playback = session.Playback;
    _audio = session.Audio;
    // ...
}
```

---

## Phase 5.6: UI Gating + Polish

**Effort:** 2 days  
**Risk:** Low  
**Files affected:** 15+ files

### Tasks

| # | Task | Details |
|---|------|---------|
| 5.6.1 | Gate UI elements with `[FeatureGate]` | Add attributes to controls: `AudioEqualizerFlyout`, `ShaderSettingsPanel`, `PlaylistDialog`, etc. |
| 5.6.2 | Create `TrialBanner` control | Persistent banner in ControlsBox for trial users with upgrade link |
| 5.6.3 | Create `UpgradeCta` flyout placeholder | Show "Upgrade to Pro" CTA in gated flyouts |
| 5.6.4 | Add trial watermark | OSD watermark in video area during trial |
| 5.6.5 | Add feature status to Preferences | Show which features are enabled/disabled and why |
| 5.6.6 | Add `IFeatureService` binding helpers | XAML-friendly `IsEnabled[featureKey]` binding |
| 5.6.7 | Verify build + run | Test UI gating for all tiers |

### UI Gating Examples

```xml
<!-- Before: Always visible -->
<local:AudioEqualizerFlyout />

<!-- After: Gated by feature -->
<local:AudioEqualizerFlyout
    IsVisible="{Binding Source={x:Static features:FeatureService.Instance},
                        Path=IsEnabled[equalizer.advanced]}" />
```

---

## Dependency Map Between Phases

```
5.1 (DI Container)
 │
 ├──► 5.2 (ISP Refactor) — depends on DI container for service resolution
 │      │
 │      └──► 5.3 (Managers + Mediator) — depends on ISP interfaces
 │             │
 │             └──► 5.6 (UI Gating) — depends on FeatureService from 5.4
 │
 └──► 5.4 (Feature Flags) — standalone, can run in parallel with 5.2/5.3
        │
        └──► 5.5 (Codec Plugin) — depends on FeatureService for licensing
               │
               └──► 5.6 (UI Gating) — final integration
```

**Parallelism opportunities:**
- 5.2 (ISP) + 5.4 (Feature Flags) can be done in parallel by different developers
- 5.3 (Managers) can start after 5.2 interfaces are defined
- 5.5 (Codec) can start after 5.4 FeatureService is available
- 5.6 (UI) depends on all previous phases being complete

---

## Per-Phase Testing Strategy

| Phase | Test Focus | Test Type |
|-------|-----------|-----------|
| 5.1 | All services resolve from container without errors | Integration (app startup) |
| 5.2 | Domain managers accept ISP interfaces; all VM bindings work | Integration + Manual |
| 5.3 | Events publish and handlers execute correctly | Unit + Integration |
| 5.4 | Feature toggles return correct values per tier; license validation | Unit + Manual |
| 5.5 | CodecManager selects correct provider; fallback works | Unit + Integration |
| 5.6 | Gated UI elements hide/show correctly per tier; trial banner shows | Manual |

---

## Success Criteria

| Criterion | Measurement |
|-----------|-------------|
| **Zero breaking changes** | All existing playback features work identically |
| **DI container used everywhere** | No `new ServiceXxx()` calls outside composition root (grep count = 0) |
| **IMediaPlayer split** | No code references `IMediaPlayer` directly except for backward compat shim |
| **Feature flags working** | Changing license tier in config disables/enables corresponding features |
| **Codec fallback works** | Playing an AV1 file on a system without AV1 GPU decode falls back to software |
| **Build + run success** | `dotnet build` succeeds; app loads and plays media |

---

## Appendix A: File Change Inventory

| Phase | New Files | Modified Files | Total |
|-------|-----------|----------------|-------|
| 5.1 | 1 (CompositionRoot.cs) | ~9 | ~10 |
| 5.2 | 6 (ISP interfaces) | ~25 | ~31 |
| 5.3 | 8 (events, handlers, base class) | ~12 | ~20 |
| 5.4 | 5 (FeatureService, Store, Licensing, GateAttr, JSON) | ~5 | ~10 |
| 5.5 | 8 (interfaces, providers, CodecManager, loader) | ~3 | ~11 |
| 5.6 | 2 (TrialBanner, CTA controls) | ~13 | ~15 |
| **Total** | **~30** | **~67** | **~97** |

---

> **Previous:** [phase5-codec-plugin-architecture.md](./phase5-codec-plugin-architecture.md)  
> **Back to Main:** [phase5-architecture-design.md](./phase5-architecture-design.md)
