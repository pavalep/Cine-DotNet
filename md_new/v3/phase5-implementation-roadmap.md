# Phase 5: Implementation Roadmap

> **Version:** 1.0  
> **Date:** 2026-07-02  
> **Total Effort Estimate:** ~18 days (single developer)

---

## Roadmap Overview

The Phase 5 architecture changes are organized into **6 implementation phases**, designed to be deployed incrementally. Each phase is independently testable and can be merged without breaking existing functionality.

---

### srcv3 Strategy

All v3 architecture work lives in a **parallel `srcv3/` folder** alongside the existing `src/`. This keeps the working v2 code intact and allows us to evolve v3 incrementally.

**How it works:**
1. `src/` — untouched v2 code (saved as git commit `v2:completed`)
2. `srcv3/` — copy of `src/` taken at v2:completed; all v3 changes go here
3. Each sub-phase (5.1, 5.2, ...) is implemented **entirely within `srcv3/`**, committed with a `v3:{phase}` prefix
4. At each phase boundary we build-verify within `srcv3/`; if it compiles and runs, the phase is done
5. No files in `src/` are ever touched during v3 development

This approach:
- Eliminates rollback risk — v2 is never modified
- Makes code reviews trivial per phase (compare `src/` vs `srcv3/` for that phase's changes)
- Enables parallel experimentation without destabilising the working build
- Gives a clear git history: each `v3:5.x` commit shows exactly what changed in that phase

```
Phase 5.1 ───► DI Container + Composition Root (2 days)
    │               v3:5.1
    ▼
Phase 5.2 ───► ISP Refactor (Split IMediaPlayer) (3 days)
    │               v3:5.2
    ▼
Phase 5.3 ───► Domain Manager Refactor + Mediator (3 days)
    │               v3:5.3
    ▼
Phase 5.4 ───► Feature Flag + Licensing System (3 days)
    │               v3:5.4
    ▼
Phase 5.5 ───► Codec Plugin Architecture (3 days)
    │               v3:5.5
    ▼
Phase 5.6 ───► UI Gating + Polish (2 days)
```

**Total: ~16–18 days** (includes buffer for testing and bug fixes)

---

## Phase 5.1: DI Container + Composition Root (✓ Completed)

**Effort:** 2 days  
**Risk:** Low  
**Files affected:** ~10 files (+1 new)  
**Commit:** `v3:5.1`

### Tasks

| # | Task | Details | Status |
|---|------|---------|--------|
| 5.1.1 | Create `CompositionRoot.cs` | Static class that builds `IServiceProvider` using `Microsoft.Extensions.DependencyInjection` | ✓ |
| 5.1.2 | Register existing services | Move all `new Xxx()` calls from `MainWindow.Initialization.cs` into DI container | ✓ |
| 5.1.3 | Update `App.axaml.cs` | Call `CompositionRoot.Build()` and pass `IServiceProvider` to `MainWindow` constructor | ✓ |
| 5.1.4 | Refactor `MainWindow` constructors | Accept `IServiceProvider` via constructor injection; resolve dependencies from container | ✓ |
| 5.1.5 | Refactor `MainViewModel` constructor | Accept dependencies via DI; make all constructor params required (remove `= null` fallbacks) | ✓ |
| 5.1.6 | Remove ad-hoc `new()` | Eliminate all manual `new ServiceXxx()` in `MainWindow` partials and ViewModels | ✓ |
| 5.1.7 | Verify build + run | Ensure app starts and all services resolve correctly | ✓ |

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

## Phase 5.2: ISP Refactor — Split IMediaPlayer (✓ Completed)

**Effort:** 3 days  
**Risk:** Medium  
**Files affected:** 30+ files  
**Commit:** `v3:5.2`

### Tasks

| # | Task | Details | Status |
|---|------|---------|--------|
| 5.2.1 | Define 6 role interfaces | `IPlaybackControl`, `IAudioControl`, `IVideoControl`, `ISubtitleControl`, `IChapterNavigation`, `IPlaylistManagement` | ✓ |
| 5.2.2 | Add composite `IMediaPlayer` | Keep existing interface as a combined interface inheriting all 6 (for backward compat) | ✓ |
| 5.2.3 | Implement interfaces on `MpvPlayer` | `MpvPlayer` already implements all members — class declaration unchanged | ✓ |
| 5.2.4 | Update domain managers | Managers stay on `IMediaPlayer` (composite consumers — need `Command()` + `TrackListChanged`) | ✓ |
| 5.2.5 | Update `PlayerService` | Added 6 ISP accessor properties (`Playback`, `Audio`, `Video`, `Subtitles`, `Chapters`, `Playlist`) | ✓ |
| 5.2.6 | Update `MainViewModel` | Stays on `IMediaPlayer` (composite consumer using members from multiple roles) | ✓ |
| 5.2.7 | Update `MediaFoundationPlayer` | Fixed 5 explicit interface implementations to use role interfaces | ✓ |
| 5.2.8 | Verify build + run | `dotnet build` — 0 errors | ✓ |

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

## Phase 5.3: Domain Manager Refactor + Mediator (✓ Completed)

**Effort:** 3 days  
**Risk:** Medium  
**Files affected:** ~20 files (+8 new)  
**Commit:** `v3:5.3`

### Tasks

| # | Task | Details | Status |
|---|------|---------|--------|
| 5.3.1 | Create `DomainManager<T>` base class | Abstract common constructor pattern, disposal, `IsDisposed` guard | ✓ |
| 5.3.2 | Update `AudioManager` | Inherit `DomainManager<IMediaPlayer>`, replace `_player` → `Player`, `Dispose()` → `DisposeCore()` | ✓ |
| 5.3.3 | Update `VideoManager` | Inherit `DomainManager<IMediaPlayer>`, replace `_player` → `Player`, remove empty `Dispose()` | ✓ |
| 5.3.4 | Update `SubtitleManager` | Inherit `DomainManager<IMediaPlayer>`, replace `_player` → `Player`, `Dispose()` → `DisposeCore()` | ✓ |
| 5.3.5 | Update `PlaybackStateManager` | Inherit `DomainManager<IMediaPlayer>`, replace `_player` → `Player`, `Dispose()` → `DisposeCore()` | ✓ |
| 5.3.6 | Create `IEventBus` + `EventBus` | Typed pub/sub mediator (replaces `ICommandBus`/`CommandBus` plan — same concept, simpler) | ✓ |
| 5.3.7 | Create event types | `TrackChangedEvent`, `FlyoutDismissRequestEvent`, `FileDialogRequestEvent`, `MediaOpenedEvent`, `PlaybackStateChangedEvent` | ✓ |
| 5.3.8 | Register `EventBus` in DI | `services.AddSingleton<IEventBus, EventBus>()` in CompositionRoot | ✓ |
| 5.3.9 | Build verify | `dotnet build` — 0 errors | ✓ |

### Notes

- Generic parameter `T` = `IMediaPlayer` for all managers (they are composite consumers needing `Command()`, `TrackListChanged`, etc.)
- File dialog delegates (`RequestAudioFileAsync`, `RequestSubtitleFileAsync`) kept as-is — they require Avalonia `TopLevel` not available at DI startup
- EventBus registered but delegate wiring replacement deferred to later phase (managers currently still use `Func<Task>?` delegates)`

---

## Phase 5.4: Feature Flag + Licensing System (✓ Completed)

**Effort:** 3 days  
**Risk:** Low–Medium  
**Files affected:** ~10 files (+12 new, 1 modified)  
**Commit:** `v3:5.4`

### Tasks

| # | Task | Details | Status |
|---|------|---------|--------|
| 5.4.1 | Create feature model classes | `FeatureDefinition`, `FeatureToggleType`, `LicensingTier` | ✓ |
| 5.4.2 | Create `FeatureKeys` constants class | Compile-time safe feature key strings | ✓ |
| 5.4.3 | Create `feature-definitions.json` | Embedded resource with 16 feature definitions | ✓ |
| 5.4.4 | Implement `IFeatureStore` | Load embedded JSON + runtime overrides | ✓ |
| 5.4.5 | Implement `IFeatureService` | Cached evaluation with dependency resolution | ✓ |
| 5.4.6 | Create `FeatureGateAttribute` | Declarative feature gating for UI components | ✓ |
| 5.4.7 | Implement `ILicensingService` | Encrypted license validation, trial tracking | ✓ |
| 5.4.8 | Create `LicensingService` | AES-256 encrypted license storage + hardware binding | ✓ |
| 5.4.9 | Wire into DI container | Register `IFeatureService`, `IFeatureStore`, `ILicensingService` | ✓ |
| 5.4.10 | Add minimal trial UI | Trial banner, upgrade CTA in ControlsBox | Pending (deferred to 5.6) |
| 5.4.11 | Verify build + run | `dotnet build` — 0 errors | ✓ |

### Feature Evaluation Flow

```
IsEnabled("codecs.hdr10")
    ├──► Load feature definition from embedded JSON
    ├──► Check license tier: Pro required, user is Full → false
    ├──► Check dependencies: playback.4k is enabled? → Yes
    └──► Return false (HDR10 disabled for Full tier)
```

---

## Phase 5.5: Codec Plugin Architecture (✓ Completed)

**Effort:** 3 days  
**Risk:** Medium  
**Files affected:** ~10 files (+10 new, 3 modified)  
**Commit:** `v3:5.5`

### Tasks

| # | Task | Details | Status |
|---|------|---------|--------|
| 5.5.1 | Create codec interfaces | `ICodecProvider`, `IDecodingSession`, `CodecCapability`, `DecodingOptions` | ✓ |
| 5.5.2 | Create `DecodingSession` default impl | Wraps player instances into session with diagnostics | ✓ |
| 5.5.3 | Implement `MpvCodecProvider` | H.264/H.265/VP9/AV1 + hwdec via libmpv | ✓ |
| 5.5.4 | Implement `MFCodecProvider` | H.264/HEVC + D3D11VA via MediaFoundation | ✓ |
| 5.5.5 | Implement `SoftwareFallbackCodecProvider` | Forces `hwdec=no`, low-quality CPU fallback | ✓ |
| 5.5.6 | Create `CodecManager` | Provider selection (capability rank) + fallback logic | ✓ |
| 5.5.7 | Wire `CodecManager` into DI | Register 3 providers, `CodecManager`, `CodecPluginLoader` | ✓ |
| 5.5.8 | Update `PlayerService` | Calls `ActiveProvider.Configure()`, creates `IDecodingSession` | ✓ |
| 5.5.9 | Create `CodecPluginLoader` | Stub for future MEF-based external plugin loading | ✓ (stub) |
| 5.5.10 | Add "Codec Information" to Preferences | Show active codecs, capabilities, license status | Pending (deferred to 5.6) |
| 5.5.11 | Verify build + run | `dotnet build` — 0 errors | ✓ |

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

## Phase 5.6: UI Gating + Polish ✅

**Effort:** 2 days  
**Risk:** Low  
**Files affected:** 12 (5 new, 7 modified)  
**Commit:** `v3:5.6` (uncommitted)

### Tasks

| # | Task | Status |
|---|------|--------|
| 5.6.1 | Gate UI elements with feature flags | ✅ Equalizer + Playlist buttons gated via UpgradeCta redirect in click handlers |
| 5.6.2 | Create `TrialBanner` control | ✅ Created `TrialBanner.axaml/.cs` with trial status + Upgrade button, integrated into ControlsBox |
| 5.6.3 | Create `UpgradeCta` flyout placeholder | ✅ Created `UpgradeCtaContent.axaml/.cs` with `Show()` method, wired into gated button click handlers |
| 5.6.4 | Add trial watermark | ✅ Added translucent "TRIAL" watermark overlay in MainWindow.axaml, bound to `IsTrial` |
| 5.6.5 | Add feature status to Preferences | ✅ Added License & Features section with `FeatureStatusInfo` record and ObservableCollection binding |
| 5.6.6 | Add `IFeatureService` binding helpers | ✅ Added `IFeatureService` + `ILicensingService` to MainViewModel; discrete bool properties + indexed subscription |
| 5.6.7 | Verify build + run | ✅ `dotnet build`: 0 errors |

### Key Decisions

- **Gating approach**: Used code-behind click handler redirect rather than XAML `IsEnabled` binding, so gated buttons remain clickable and show the upgrade CTA flyout instead of being grayed out.
- **ViewModel integration**: `IFeatureService` and `ILicensingService` are optional constructor parameters for backward compat.
- **Trial state**: Exposed via discrete properties (`IsTrial`, `TrialDaysRemaining`, `LicenseLabel`, `LicenseTierDisplay`) on MainViewModel.
- **Feature status in Preferences**: Uses `ObservableCollection<FeatureStatusInfo>` populated by `RefreshFeatureStatuses()` method, updated on tier or feature state changes.

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
| 5.4 | 12 (FeatureService, Store, Licensing, GateAttr, JSON, models) | 1 (App.csproj) | 13 |
| 5.5 | 10 (interfaces, models, providers, CodecManager, loader, session) | 3 (CompositionRoot, PlayerService, roadmap) | 13 |
| 5.6 | 5 (TrialBanner axaml/cs, UpgradeCtaContent axaml/cs, FeatureStatusInfo) | 7 (MainViewModel, MainWindow.Initialization, MainWindow.axaml, ControlsBox.axaml, ControlsBox.axaml.cs, CompositionRoot, PreferencesDialog) | 12 |
| **Total** | **~37** | **~76** | **~113** |

---

> **Previous:** [phase5-codec-plugin-architecture.md](./phase5-codec-plugin-architecture.md)  
> **Back to Main:** [phase5-architecture-design.md](./phase5-architecture-design.md)
