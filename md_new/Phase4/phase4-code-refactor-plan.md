# Phase 4 — Code Refactor & SOLID Architecture Plan

> **Goal:** Transform the UI codebase into a clean, SOLID‑adhering component architecture with zero duplication, consistent structure, and clear boundaries — "Google‑level" quality.
>
> **Scope:** `src/App/UI/` (controls, screens, builders, views, shell, constants) + `src/App/Application/Services/Token.cs`
>
> **Pre‑requisite:** AF1–AF5 (architectural flaws) already applied.

---

## Table of Contents

1. [Principles & Guiding Rules](#1-principles--guiding-rules)
2. [Current Pain Points](#2-current-pain-points)
3. [Refactoring Phases](#3-refactoring-phases)
   - [Phase R1 — Component Cohesion](#r1--component-cohesion)
   - [Phase R2 — Flyout Abstractions](#r2--flyout-abstractions)
   - [Phase R3 — ControlsBox Decomposition](#r3--controlsbox-decomposition)
   - [Phase R4 — Service Layer Clarity](#r4--service-layer-clarity)
   - [Phase R5 — SOLID Hardening](#r5--solid-hardening)
4. [File Tree — Target State](#4-target-file-tree)
5. [Verification & Rollout](#5-verification--rollout)

---

## 1. Principles & Guiding Rules

| # | Rule | Description |
|---|------|-------------|
| P1 | **One component = one folder** | Every UI component gets its own subfolder containing `.axaml`, `.axaml.cs`, and any **exclusively** related code (helpers, builders, state classes). |
| P2 | **No suffix redundancy** | `ChaptersFlyoutControl` → `ChaptersFlyout`. The folder name provides the context; the class name is the component name. |
| P3 | **No pattern duplication** | Every flyout source follows the same interface → register → show → hide → dismiss lifecycle. Extract this into a shared abstraction. |
| P4 | **Boundaries are physical** | A component folder is the unit of reuse. If code is shared across components, it belongs in a `Shared/` or `Common/` folder, not duplicated. |
| P5 | **Services at the edge** | Services (FlyoutManager, ThemeService) stay in `Application/Services/`. UI‑only helpers (Token, color maps) move to `UI/Constants/`. |
| P6 | **Don't break what works** | Each phase produces a **buildable** state with **zero behavior change**. Refactoring is reorganisation + abstraction extraction only. |

---

## 2. Current Pain Points

| ID | Pain Point | Location | Violates |
|----|-----------|----------|----------|
| PP1 | Mixed organisation — some controls have folders, others don't | `Controls/ChaptersFlyoutControl.axaml` vs `Controls/Audio/AudioTrackSelectorControl.axaml` | P1 |
| PP2 | Flyout show/hide/dismiss pattern repeated in 5+ controls | `VolumeFlyoutControl`, `ChaptersFlyoutControl`, `AudioTrackSelectorControl`, `SubtitleOverlayControl`, `ControlsBoxControl` | P3, SRP |
| PP3 | ControlsBoxControl still owns equaliser wiring | `OpenEqualizerFlyout()` + `CloseAction` callback | SRP |
| PP4 | TrackFlyoutBuilder lives isolated in `Builders/` | Only used by track‑selection controls, should live with them | P1 |
| PP5 | MainWindow.axaml in `Views/` but partials in `Shell/` | Cross‑directory partial class | P1 |
| PP6 | `Token.cs` (UI design tokens) lives in `Application/Services/` | Clearly UI‑only concern | P4 |
| PP7 | No `IFlyoutSource` interface — each control implements flyout lifecycle ad‑hoc | P3, DIP |
| PP8 | `Application/Managers/` vs `Application/Services/` boundary unclear | `AudioManager`, `VideoManager`, `SubtitleManager` are state managers mixed with service folder | P4 |

---

## 3. Refactoring Phases

### R1 — Component Cohesion

**Goal:** Every component owns its folder under `Components/`. Consistent naming. Self‑contained. All file moves done in one pass.

**IMPORTANT — Cross‑cutting rules for every rename:**
- Update `x:Class` in the `.axaml` file (e.g. `x:Class="Cine.Avalonia.Controls.ChaptersFlyoutControl"` → `Cine.Avalonia.Components.Chapters.ChaptersFlyout`)
- Update `xmlns:controls` → `xmlns:components` and `clr-namespace:Cine.Avalonia.Controls` → `clr-namespace:Cine.Avalonia.Components.*` in all consuming XAML files
- Update `using Cine.Avalonia.Controls` → `using Cine.Avalonia.Components.*` in all consuming `.cs` files
- Update C# references: property types, method calls, static method calls

**Steps:**

| # | Step | Files Affected | Effort | Risk |
|---|------|---------------|--------|------|
| R1.1 | Move `Controls/ChaptersFlyoutControl.axaml(.cs)` → `Components/Chapters/ChaptersFlyout.axaml(.cs)` + rename class | 2 moved, 1 renamed + update ControlsBox XAML tag + C# refs | Small | Medium |
| R1.2 | Move `Controls/FlyoutOverlayControl.axaml(.cs)` → `Components/Flyout/FlyoutOverlay.axaml(.cs)` + rename class | 2 moved, 1 renamed + update MainWindow GetOverlay() return type | Small | Medium |
| R1.3 | Move `Views/MainWindow.axaml(.cs)` → `Shell/MainWindow.axaml(.cs)` | 2 moved | Small | Low |
| R1.4 | Rename `Controls/SeekBar/SeekBarControl.axaml(.cs)` → `Components/SeekBar/SeekBar.axaml(.cs)` + rename class | 2 renamed + update ControlsBox XAML tag + all `SeekBarControl.FormatTimeSpan()` calls | Small | Medium |
| R1.5 | Move `Builders/TrackFlyoutBuilder.cs` → `Components/TrackSelection/TrackFlyoutBuilder.cs` + update namespace | 1 moved + update `using Cine.Avalonia.Builders` → `using Cine.Avalonia.Components.TrackSelection` in 4 files | Small | Low |
| R1.6 | Move `Controls/Audio/VolumeFlyoutControl.axaml(.cs)` → `Components/Volume/VolumeFlyout.axaml(.cs)` + rename class | 2 moved, 1 renamed + update ControlsBox XAML tag + MainWindow refs | Small | Medium |
| R1.7 | Move `Controls/Audio/AudioTrackSelectorControl.axaml(.cs)` → `Components/Audio/AudioTrackSelector.axaml(.cs)` + rename class | 2 moved, 1 renamed + update ControlsBox + MainWindow refs | Small | Medium |
| R1.8 | Move `Controls/Audio/AudioEqualizerFlyout.axaml(.cs)` → `Components/Audio/AudioEqualizerFlyout.axaml(.cs)` (no rename) | 2 moved + update ControlsBox refs | Small | Low |
| R1.9 | Move `Screens/Shell/ControlsBoxControl.axaml(.cs)` → `Components/Shell/ControlsBox.axaml(.cs)` + rename class | 2 moved, 1 renamed + update MainWindow `_controlsBox` refs across 9 partial files | Medium | Medium |
| R1.10 | Move `Screens/Shell/HeaderBarControl.axaml(.cs)` → `Components/Shell/HeaderBar.axaml(.cs)` + rename class | 2 moved, 1 renamed | Small | Low |
| R1.11 | Move `Screens/Shell/FullscreenHeaderControl.axaml(.cs)` → `Components/Shell/FullscreenHeader.axaml(.cs)` + rename class | 2 moved, 1 renamed | Small | Low |
| R1.12 | Move `Screens/Start/StartPage.axaml(.cs)` → `Components/Start/StartPage.axaml(.cs)` + update namespace | 2 moved | Small | Low |
| R1.13 | Move all `Controls/Indicators/*Control.axaml(.cs)` → `Components/Indicators/*.axaml(.cs)` + rename classes | 12 files (6 controls), 6 renamed | Medium | Low |
| R1.14 | Move `Controls/Subtitle/SubtitleOverlayControl.axaml(.cs)` → `Components/Subtitle/SubtitleOverlay.axaml(.cs)` + rename class | 2 moved, 1 renamed | Small | Low |
| R1.15 | Move `Screens/Dialogs/` → `Dialogs/` + update namespace `Cine.Avalonia.Views.Dialogs` → `Cine.Avalonia.Dialogs` | 18 files (9 dialogs) + update `using` in 4+ files | Medium | Medium |
| R1.16 | Move `Builders/PrimaryMenuBuilder.cs` → `Components/Shell/PrimaryMenuBuilder.cs` + update namespace | 1 moved + update references in Shell controls | Small | Low |
| R1.17 | Move `Builders/VideoContextMenuBuilder.cs` → `Components/Shell/VideoContextMenuBuilder.cs` + update namespace | 1 moved + update references | Small | Low |
| R1.18 | Delete empty `Controls/`, `Screens/`, `Builders/`, `Views/` directories | 4 empty folders | Tiny | None |

**Build verification:** `dotnet build` — expected 0 errors after updating namespaces and project inclusions.

**Key reference update checklist for every rename:**
1. `.axaml` — `x:Class`, `xmlns:` mappings
2. `.axaml.cs` — `namespace` declaration, all references to the renamed class
3. All XAML files that use the component (update tag name + xmlns)
4. All `.cs` files that reference the class (update using + type references)
5. `MainWindow.*.cs` partials for `_controlsBox.*` property accesses

---

### R2 — Flyout Abstractions

**Goal:** Single interface for all flyout sources. Eliminate 5× duplicated show/hide/dismiss patterns.

**Steps:**

| # | Step | Files Affected | Effort | Risk |
|---|------|---------------|--------|------|
| R2.1 | Define `IFlyoutSource` interface in `Controls/Flyout/` | New file | Tiny | None |
| R2.2 | Add extension method `FlyoutManagerExtensions.ShowFlyoutFor<T>(...)` | New file | Small | None |
| R2.3 | Implement `IFlyoutSource` on `VolumeFlyoutControl` | 1 file | Small | Low |
| R2.4 | Implement `IFlyoutSource` on `ChaptersFlyoutControl` → `ChaptersFlyout` | 1 file | Small | Low |
| R2.5 | Implement `IFlyoutSource` on `AudioTrackSelectorControl` | 1 file | Small | Low |
| R2.6 | Implement `IFlyoutSource` on `SubtitleOverlayControl` | 1 file | Small | Low |
| R2.7 | Implement `IFlyoutSource` on equaliser flyout section (was in ControlsBoxControl) | 1 file | Small | Low |
| R2.8 | Inline `OnOverlayDismissed` handlers into the interface pattern, remove from all controls | 4 files | Small | Low |

**Interface design:**

```csharp
// Controls/Flyout/IFlyoutSource.cs
public interface IFlyoutSource
{
    string FlyoutKey { get; }               // e.g. "volume", "chapters"
    Control Anchor { get; }                 // the button that triggers the flyout
    Control BuildContent();                 // builds the flyout overlay content
    bool CanOpen { get; }                   // optional guard (e.g. chapters count > 0)
}
```

```csharp
// Controls/Flyout/FlyoutManagerExtensions.cs
public static class FlyoutManagerExtensions
{
    public static void ShowFlyoutFor(this FlyoutManager manager, IFlyoutSource source,
        FlyoutOverlayControl overlay, Action? onDismissed = null)
    {
        if (!source.CanOpen) return;
        manager.ShowFlyout(source.FlyoutKey, source.Anchor, source.BuildContent(), true,
            (a, c, p) =>
            {
                overlay.OnBackgroundDismissed -= onDismissed;
                overlay.OnBackgroundDismissed += onDismissed;
                overlay.ShowContent(a, c, p);
            });
    }
}
```

**Build verification:** `dotnet build` — 0 errors. No behavioural change — all existing flyouts continue to work identically.

---

### R3 — ControlsBox Decomposition

**Goal:** ControlsBoxControl becomes a pure orchestrator that composes child flyout components. No flyout logic lives inside it.

**Steps:**

| # | Step | Files Affected | Effort | Risk |
|---|------|---------------|--------|------|
| R3.1 | Extract `VideoTrackSelector` — new `.axaml` + `.axaml.cs` that owns `BtnVideoMenu`, builds video track flyout, implements `IFlyoutSource` | 2 new files + ControlsBoxControl modification | Medium | Medium |
| R3.2 | Extract equaliser wiring from `OpenEqualizerFlyout()` into a dedicated `AudioEqualizerFlyout` enhancement (it already implements a close pattern) | 1 file modified | Small | Low |
| R3.3 | Remove `_flyoutOverlay`, `_flyoutManager`, `OnOverlayDismissed` from ControlsBoxControl — child controls own these | 1 file | Small | Low |
| R3.4 | ControlsBoxControl XAML references `VideoTrackSelector`, `VolumeFlyoutControl`, `AudioTrackSelectorControl`, `ChaptersFlyoutControl` as composed children | 1 XAML file | Small | Low |

**ControlsBoxControl target state:**
- No `_flyoutOverlay` field
- No `_flyoutManager` field
- No `OpenEqualizerFlyout()`, `OnVideoMenuClick()`, `OnChaptersMenuClick()`, `OnVolumeMenuClick()`
- No overlay registration or dismiss events
- Pure composition: `<components:VolumeFlyout /> <components:VideoTrackSelector /> ...`

**Build verification:** `dotnet build` — 0 errors. Video/chapters/volume/equaliser flyouts still work.

---

### R4 — Service Layer Clarity

**Goal:** Every file lives in the logical namespace. UI helpers move out of `Application/Services/`. Manager vs Service delineation.

**Steps:**

| # | Step | Files Affected | Effort | Risk |
|---|------|---------------|--------|------|
| R4.1 | Move `Token.cs` → `UI/Constants/Token.cs`, update namespace to `Cine.Avalonia.Constants` | 1 file moved + 10+ files updated | Small | Low |
| R4.2 | Rename `Application/Managers/` → `Application/State/` (each manager is a state holder, not a manager) | 8 files moved, namespaces updated | Medium | Medium |
| R4.3 | Consolidate duplicate patterns in `AudioSettingsStore` / `SubtitleSettingsStore` / `PlaylistSettingsStore` → `SettingsStore<T>` generic base | 4 files | Medium | Medium |
| R4.4 | Move `PlaybackStateManager.cs` into `Application/State/` alongside other state holders | 1 file moved | Tiny | Low |

**Build verification:** `dotnet build` — 0 errors. Settings behaviour unchanged.

---

### R5 — SOLID Hardening

**Goal:** Apply SOLID principles systematically across the UI layer.

| Principle | Current Violation | Fix |
|-----------|------------------|-----|
| **SRP** | ControlsBoxControl orchestrates flyouts, holds ViewModel reference, manages visibility, routes keyboard events | Decomposed in R3 |
| **OCP** | Adding a new flyout source requires modifying FlyoutManager (to register), ControlsBoxControl (to compose), MainWindow (to wire). Ideally open for extension, closed for modification | After R2 + R3: new flyout source = new component implementing IFlyoutSource, composed in XAML only |
| **LSP** | Check: do all controls that accept `FlyoutManager` setter behave uniformly? If one null‑checks differently, LSP is violated | Audit all `FlyoutManager` assignments |
| **ISP** | Is `IFlyoutSource` minimal? If not, split finer. Current proposal: `FlyoutKey`, `Anchor`, `BuildContent()`, `CanOpen` — sufficient | Keep under review |
| **DIP** | Controls reference `FlyoutManager` (concrete singleton) directly. Should depend on `IFlyoutService` abstraction | Define `IFlyoutService` interface, inject via constructor or DI |

**Steps:**

| # | Step | Files Affected | Effort | Risk |
|---|------|---------------|--------|------|
| R5.1 | Define `IFlyoutService` interface (subset of FlyoutManager's public API) | New file + FlyoutManager implements it | Small | Low |
| R5.2 | Replace `FlyoutManager` property on all controls with `IFlyoutService` | 6+ controls | Medium | Low |
| R5.3 | Audit all LSP violations — ensure null guards are consistent across IFlyoutSource implementations | 5 files | Small | Low |
| R5.4 | Add XML doc comments on all public API surfaces (interfaces, extensions) | 5-6 files | Small | None |

**Build verification:** `dotnet build` — 0 errors.

---

## 4. Target File Tree

Below is the **final** directory tree after all R1–R5 phases are applied. Additions are marked `[+]`, moves/renames marked `[→]`, deletions marked `[x]`.

```
src/App/UI/
├── Components/                           ← ALL UI components, one per folder
│   ├── Audio/
│   │   ├── AudioEqualizerFlyout.axaml
│   │   ├── AudioEqualizerFlyout.axaml.cs
│   │   ├── AudioTrackSelector.axaml      [→ from AudioTrackSelectorControl.axaml]
│   │   └── AudioTrackSelector.axaml.cs   [→ from AudioTrackSelectorControl.axaml.cs]
│   ├── Chapters/
│   │   ├── ChaptersFlyout.axaml          [→ from Controls/ChaptersFlyoutControl.axaml]
│   │   └── ChaptersFlyout.axaml.cs       [→ from Controls/ChaptersFlyoutControl.axaml.cs]
│   ├── Flyout/
│   │   ├── FlyoutOverlay.axaml           [→ from Controls/FlyoutOverlayControl.axaml]
│   │   ├── FlyoutOverlay.axaml.cs        [→ from Controls/FlyoutOverlayControl.axaml.cs]
│   │   ├── IFlyoutSource.cs              [+ new]
│   │   └── FlyoutManagerExtensions.cs    [+ new]
│   ├── Indicators/
│   │   ├── DragDropOverlay.axaml(.cs)    [→ from DragDropOverlayControl]
│   │   ├── NowPlayingInfo.axaml(.cs)     [→ from NowPlayingInfoControl]
│   │   ├── OsdNotification.axaml(.cs)    [→ from OsdNotificationControl]
│   │   ├── PauseOverlay.axaml(.cs)       [→ from PauseOverlayControl]
│   │   ├── ReplayOverlay.axaml(.cs)      [→ from ReplayOverlayControl]
│   │   └── SpinnerOverlay.axaml(.cs)     [→ from SpinnerOverlayControl]
│   ├── SeekBar/
│   │   ├── SeekBar.axaml                 [→ from SeekBarControl.axaml]
│   │   └── SeekBar.axaml.cs              [→ from SeekBarControl.axaml.cs]
│   ├── Shell/
│   │   ├── ControlsBox.axaml(.cs)        [→ from ControlsBoxControl]
│   │   ├── HeaderBar.axaml(.cs)          [→ from HeaderBarControl]
│   │   └── FullscreenHeader.axaml(.cs)   [→ from FullscreenHeaderControl]
│   ├── Start/
│   │   └── StartPage.axaml(.cs)
│   ├── Subtitle/
│   │   ├── SubtitleOverlay.axaml(.cs)    [→ from SubtitleOverlayControl]
│   │   └── SubtitleTrackSelector.axaml(.cs)  [+ extracted from SubtitleOverlayControl]
│   ├── TrackSelection/
│   │   └── TrackFlyoutBuilder.cs         [→ from Builders/]
│   ├── Video/
│   │   ├── VideoTrackSelector.axaml      [+ new: extracted from ControlsBoxControl]
│   │   └── VideoTrackSelector.axaml.cs   [+ new]
│   └── Volume/
│       ├── VolumeFlyout.axaml(.cs)       [→ from VolumeFlyoutControl]
│       └── VolumeIconState.cs            [+ extracted from RefreshVolumeIcon]
│
├── Constants/
│   ├── AppColors.cs
│   ├── Colors.json
│   ├── Token.cs                          [→ from Application/Services/Token.cs]
│   └── UiConstants.cs
│
├── Dialogs/                              [→ from Screens/Dialogs/]
│   ├── AboutDialog.axaml(.cs)
│   ├── CommandPaletteDialog.axaml(.cs)
│   ├── FirstLaunchDialog.axaml(.cs)
│   ├── GoToTimeDialog.axaml(.cs)
│   ├── KeyboardShortcutsDialog.axaml(.cs)
│   ├── PipWindow.axaml(.cs)
│   ├── PlaylistDialog.axaml(.cs)
│   ├── PreferencesDialog.axaml(.cs)
│   └── SubtitleSettingsDialog.axaml(.cs)
│
├── Resources/
│   ├── App.axaml
│   ├── Colors.axaml
│   ├── Elevation.axaml
│   ├── Icons.axaml
│   ├── Motion.axaml
│   ├── Radius.axaml
│   ├── Sizes.axaml
│   ├── Spacing.axaml
│   └── Typography.axaml
│
├── Shell/
│   ├── MainWindow.axaml                  [→ from Views/]
│   ├── MainWindow.axaml.cs               [→ from Views/]
│   ├── MainWindow.Core.cs
│   ├── MainWindow.Initialization.cs
│   ├── MainWindow.Input.cs
│   ├── MainWindow.MediaEvents.cs
│   ├── MainWindow.Pip.cs
│   ├── MainWindow.Startup.cs
│   ├── MainWindow.State.cs
│   ├── MainWindow.WindowControls.cs
│   └── MainWindow.Wiring.cs
```

**Deleted after R1:**
- `UI/Controls/` (all controls migrated to `Components/`)
- `UI/Screens/` (Dialogs → `Dialogs/`, Shell → `Components/Shell/` + `Shell/`, Start → `Components/Start/`)
- `UI/Builders/` (all builders moved to `Components/Shell/` or `Components/TrackSelection/`)
- `UI/Views/` (MainWindow moved to `Shell/`)

**Namespace convention for `Components/`:**
All components use **flat namespace** `Cine.Avalonia.Components` regardless of subfolder. This is required because `MainWindow.axaml` and other shell XAML files import a single XML namespace:
```xml
xmlns:components="using:Cine.Avalonia.Components"
```
Subfolders are purely for file organization (component cohesion / SRP). Each `.axaml` still needs its own `x:Class` (e.g. `Cine.Avalonia.Components.StartPage`), but all share the same namespace.

---

## 5. Verification & Rollout

### Per‑Phase Checkpoints

| Phase | Check | Expected |
|-------|-------|----------|
| R1 | `dotnet build` | 0 errors |
| R1 | `dotnet test` (if tests exist) | All pass |
| R2 | `dotnet build` | 0 errors |
| R2 | Manual: click every flyout button | Opens correctly |
| R3 | `dotnet build` | 0 errors |
| R3 | Manual: volume, chapters, video menu, equaliser | All open and close correctly |
| R4 | `dotnet build` | 0 errors |
| R4 | Manual: settings persist/load | Works |
| R5 | `dotnet build` | 0 errors |
| Full | Run app, full E2E smoke test | No regression |

### Rollout Strategy

```
Week 1: R1 (file moves, renames — cheap, safe)
Week 2: R2 (flyout abstractions — moderate, high value)
Week 3: R3 (ControlsBox decomposition — highest value, moderate risk)
Week 4: R4 + R5 (service clarity + SOLID hardening — polish)
```

Each phase is **independently mergeable**. If R3 hits unexpected complexity, R1+R2+R4 can ship independently.

### Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| File move breaks XAML `x:Class` / namespace | Use `dotnet build` after each move. Commit per‑file, not per‑batch. |
| Renamed controls break bindings | All controls are UserControls — only `x:Class` and namespace need updating. No string‑based bindings reference control names. |
| UseWindowsForms=true causes `Control` ambiguity | The `.csproj` has `<UseWindowsForms>true</UseWindowsForms>`. All renamed controls that used `Control` base class need explicit `using Control = Avalonia.Controls.Control` alias to avoid collision with `System.Windows.Forms.Control`. Add this alias in every `Components/` code-behind that references `Control`. |
| IFlyoutSource abstraction doesn't fit all 5 flyout patterns | Keep `OnVolumeMenuClick` fallback for edge cases; migrate incrementally. |
| SettingsStore<T> breaks existing settings serialization | Add a migration path (read old format, write new format). Keep both for one release. |
| XAML `components:` namespace doesn't exist yet | MainWindow.axaml already has `xmlns:components="using:Cine.Avalonia.Components"` which maps to the new namespace tree. No change needed — it will resolve once Components/ folders and namespaces exist. |

---

## Appendix A: Naming Conventions

| Current | New | Reason |
|---------|-----|--------|
| `AudioTrackSelectorControl` | `AudioTrackSelector` | "Control" is redundant in a Controls namespace |
| `ChaptersFlyoutControl` | `ChaptersFlyout` | Same |
| `VolumeFlyoutControl` | `VolumeFlyout` | Same |
| `SeekBarControl` | `SeekBar` | Same |
| `FlyoutOverlayControl` | `FlyoutOverlay` | Same |
| `ControlsBoxControl` | `ControlsBox` | Same |
| `HeaderBarControl` | `HeaderBar` | Same |
| `FullscreenHeaderControl` | `FullscreenHeader` | Same |
| `SubtitleOverlayControl` | `SubtitleOverlay` | Same |
| `DragDropOverlayControl` | `DragDropOverlay` | Same |
| `NowPlayingInfoControl` | `NowPlayingInfo` | Same |
| `OsdNotificationControl` | `OsdNotification` | Same |
| `PauseOverlayControl` | `PauseOverlay` | Same |
| `ReplayOverlayControl` | `ReplayOverlay` | Same |
| `SpinnerOverlayControl` | `SpinnerOverlay` | Same |
| `FlyoutManager` | → implements `IFlyoutService` | DIP: depend on abstraction |

## Appendix B: IFlyoutService Interface

```csharp
// UI/Components/Flyout/IFlyoutService.cs
public interface IFlyoutService
{
    string? CurrentOpenKey { get; }
    bool HasActiveFlyouts { get; }
    void ShowFlyout(string key, Control anchor, Control content, bool placeAbove,
        Action<Control, Control, bool> showContent);
    void HideFlyout(string key, Action? hideContent);
    void DismissOthers(string key);
    void MarkClosed(string key);
    void Register(string key, Action? closeAction = null);
}
```

## Appendix C: SettingsStore<T> Generic

```csharp
// Application/State/SettingsStore.cs (new)
public class SettingsStore<T> where T : class, new()
{
    private readonly string _sectionName;
    private readonly IConfigService _config;

    public T Current { get; private set; }

    public void Save() => _config.SetSection(_sectionName, Current);
    public void Reset() { Current = new T(); Save(); }
}
```

Eliminates the 3× duplicated save/load/reset pattern in `AudioSettingsStore`, `SubtitleSettingsStore`, `PlaylistSettingsStore`.

---

> **Document version:** 1.0
> **Created:** 2026-07-02
> **Related:** [phase4-bug-audit.md](phase4-bug-audit.md) · [phase4-architectural-flaws.md](phase4-architectural-flaws.md)
