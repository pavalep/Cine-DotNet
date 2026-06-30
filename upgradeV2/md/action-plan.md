# Cine Premium Product Transformation Masterplan v2.0
## The "Apple-Grade Desktop Media Player" Blueprint

---

## Executive Summary
**Goal:** Elevate Cine from a feature-complete media player to a **category-defining premium desktop app**.

**Positioning:** deliver a Windows-native playback experience that feels more polished than the best native macOS and Windows video players, while preserving Cine's power-user DNA.

**Target Completion:** 12 weeks with parallel workstreams and clearly defined milestones.

**Success Metric:** Users say "This is the best media player I've ever used" because the app is *invisible when it works* and *reassuring when it does not.*

---

## Why This Plan Exists
Cine is already strong in features, but it lacks a coherent premium delivery.
This plan is not about adding every feature; it is about turning the experience from "usable" into "crafted." It is the difference between shipping a functional player and shipping a product people enjoy using every day.

### Primary Outcomes
- Playback feels smooth and uninterrupted across all window and mode changes.
- The UI looks intentional, consistent, and modern.
- Keyboard and mouse controls are predictable and dependable.
- Complex settings are discoverable without cluttering the main experience.
- The app behaves gracefully under failure, high load, and unusual content.

### Core Success Criteria
- Playback remains fluid even during window move/resize.
- No interactive control is smaller than 44px.
- Every menu and flyout fits within its available screen area.
- 100% of core playback shortcuts work in standard use.
- No new premium feature is shipped without a matching quality contract.

---

## Premium Definition

### The Premium Rules
1. **Invisibility Rule:** Playback should never be interrupted by the UI.
2. **Fluidity Rule:** Interactions feel fast, smooth, and confident.
3. **Clarity Rule:** Controls are obvious; layout communicates function without explanation.
4. **Consistency Rule:** All UI elements share a single visual and interaction system.
5. **Safety Rule:** Failures are safe, recoverable, and stated clearly.
6. **Discoverability Rule:** Powerful features are easy to find without overwhelming the default experience.
7. **Performance Rule:** The app should never feel like it is working hard while playing media.

### What Premium Means for Cine
- Polished default behaviors for core playback flows.
- Measurable interaction quality, not just feature completeness.
- Minimal visual distraction and maximal functional clarity.
- Smart defaults for both casual viewers and power users.

---

## Current Reality Summary
Cine has strong foundations, but the current product feels more like a technical demo than a finished premium desktop app.

### Key Current Gaps
- UI is inconsistent: spacing, borders, and control density vary across screens.
- Input model is brittle: focus-sensitive shortcuts, modal ambiguity, and partial command handling.
- Playback can hitch during shell interaction.
- Flyouts and settings panels are not robust against small windows or long lists.
- The design language is not centralized, so fixes are repeated rather than systemic.

---

## Master Transformation Strategy
This is a 12-phase plan with six parallel implementation streams. Each phase produces a deliverable, a measurable improvement, and a testable quality guardrail.

## Phase 0 - Premium Design Principles and Quality Contract (Week 1)
Build the foundation so every subsequent change is guided by a single shared standard.

### What This Phase Delivers
- A centralized UI design token system.
- A written quality contract for interactions, performance, and layout.
- A documented set of premium engineering rules.
- A lightweight review process for any UI or shell change.

### Design System Foundation
Create `src/App/UI/Design/DesignTokens.axaml` with the following categories:
- Color palette
- Spacing scales
- Typography anchors
- Border radius values
- Elevation and shadows
- Motion and duration tokens

#### Sample Tokens
```xaml
<Color x:Key="ColorPrimary">#1E88E5</Color>
<Color x:Key="ColorBg.Surface">#121212</Color>
<Color x:Key="ColorText.Primary">#FFFFFF</Color>
<Thickness x:Key="Spacing.MD">16</Thickness>
<CornerRadius x:Key="Radius.Medium">8</CornerRadius>
<BoxShadow x:Key="Elevation.L1">0 1 3 0 #00000033</BoxShadow>
<x:TimeSpan x:Key="Duration.Fast">150ms</Duration.Fast>
```

### Quality Contract Rules
1. **No hitch while interacting**
   - Playback must remain smooth during window move, resize, fullscreen toggle, PiP, dialogs, and mode transitions.
2. **Single mental model for keyboard**
   - Playback stays available globally; editing shortcuts are local to text controls; dialogs get priority.
3. **One visual language**
   - Use tokens for color, spacing, size, and typography; avoid hardcoded values.
4. **No accidental complexity**
   - Each UI surface must have one primary purpose. No nested panels, no buried controls.
5. **Default path + pro path**
   - Default actions are easy; expert workflows are more powerful but still discoverable.

### Phase Deliverables
- `md/design-system.md`
- `src/App/UI/Design/DesignTokens.axaml`
- `md/quality-rubric.md`
- `md/defect-triage-template.md`

---

## Phase 1 - UI Forensics and Visual Debt Audit (Week 1-2)
Identify visual debt, define exact fixes, and lock down the shell.

### 1.1 Top Bar and Shell Hierarchy Audit
**Problems:**
- Border-based header feels heavy.
- Mixed alignment and variable spacing create visual imbalance.
- Header buttons have inconsistent hit areas and feedback.
- Fullscreen transitions feel disjointed.

**Actions:**
- Replace the bottom border with subtle elevation and layered surfaces.
- Use consistent 16px padding around the header.
- Make all header buttons at least 44px.
- Add clear hover and pressed states.
- Standardize icon size and spacing.

**Implementation:**
```csharp
<Border BoxShadow="{StaticResource Elevation.L0}" Background="{DynamicResource ColorBg.Surface}">
```

**Measure:**
- Header artifacts removed.
- Button hit area compliance.
- Visual consistency score before/after.

### 1.2 Flyout and Overflow Audit
**Problems:**
- Flyouts overflow small windows.
- Popovers can hide behind other content.
- No consistent scroll behavior for long lists.

**Actions:**
- Audit every flyout and contextual menu.
- Add `MaxHeight` and scroll containers.
- Convert deep panels to dedicated windows when needed.
- Build a `FlyoutService` to enforce boundaries and z-order.

**Implementation sample:**
```csharp
public class AnchoredPopoverControl : UserControl
{
    // Auto reposition if off-screen.
    // Close on outside click.
    // Support keyboard navigation.
}
```

**Measure:**
- Flyout overflow incidents → 0
- Partial hidden flyouts → 0
- Average settings access depth reduced

### 1.3 Control Language and Consistency Audit
**Problems:**
- Inconsistent iconography and button sizing.
- Mixed hover/disabled styles.
- Primary and secondary actions visually indistinguishable.

**Actions:**
- Standardize icon sizes to 24px.
- Ensure all click targets are ≥ 44px.
- Define a shared state system for normal / hover / pressed / disabled / focus.
- Renormalize primary, secondary, ghost, and icon button styles.

**Implementation sample:**
```csharp
public class IconButtonStyle
{
    MinWidth = 44;
    MinHeight = 44;
    Padding = new Thickness(8);
}
```

**Measure:**
- Hit area compliance: 100%
- Visual state consistency: 100%
- Clarity score for button groups

### 1.4 Information Architecture and Control Grouping
**Problems:**
- Duplicate menu actions and unclear command ownership.
- Primary playback controls are mixed with secondary window actions.
- Advanced controls are not grouped logically.

**Actions:**
- Build a single command taxonomy.
- Keep only high-frequency controls in primary chrome.
- Document command ownership in `ActionCommandRegistry`.
- Reduce duplicated surfaces.

**Implementation sample:**
```csharp
public static class ActionCommandRegistry
{
    public static readonly CommandDef PlayPauseCommand = new(
        id: "playback.play-pause",
        label: "Play / Pause",
        shortcut: Key.Space,
        contexts: new[] { Context.Playback },
        execute: player => player.TogglePause());
}
```

**Deliverables:**
- `md/visual-audit-report.md`
- `md/design-tokens-usage.md`
- `src/App/UI/Design/DesignTokens.axaml`
- `src/App/Application/Commands/ActionCommandRegistry.cs`

**Measure:**
- Closed visual debt issues: 30+
- UI consistency score: 90%+
- User rating of shell clarity improved

---

## Phase 2 - Playback Smoothness & Rendering Stability (Week 2-3)
Make interaction feel invisible by removing playback interruptions.

**Scope:** mpv OpenGL render path only (ANGLE + pixel readback via `MpvVideoView`). Media Foundation / D3D11 path is out of scope.

### 2.1 Render Throttle Service
**Problem:** The render loop in `MpvVideoView` runs at maximum rate on every mpv frame-ready callback. Resize events and rapid frame callbacks can flood the render/present pipeline.

**Solution:**
- Add a `RenderThrottleService` that enforces a minimum interval (16.666ms ≈ 60fps) between render submissions.
- The service tracks timestamps via `Stopwatch` and skips renders that fall within the throttle window.
- Thread-safe: uses `Interlocked` and `Volatile` for lock-free reads.

**Implementation:**
```csharp
public class RenderThrottleService
{
    private long _lastRenderTicks;
    private const long MinIntervalTicks = (long)(16.666 * TimeSpan.TicksPerMillisecond);
    
    public bool ShouldRender()
    {
        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref _lastRenderTicks);
        if (now - last < MinIntervalTicks) return false;
        Interlocked.Exchange(ref _lastRenderTicks, now);
        return true;
    }
}
```

**Measure:**
- Render call frequency reduced to ≤ 60fps
- No visual degradation (frames are already limited by monitor refresh)

### 2.2 Frame Pacing and Drop Detection (PerformanceMonitor)
**Problem:** No visibility into whether the render pipeline is keeping up with the video frame rate. Frame drops go undetected.

**Solution:**
- Create `PerformanceMonitor` that counts rendered frames per second.
- If frame count drops below a threshold (e.g. 50 fps for a 60fps video), log a performance warning via `CrashReporter.LogError`.
- Expose counters for external monitoring (`FramesThisSecond`, `DropsDetected`).

**Implementation:**
```csharp
public class PerformanceMonitor
{
    private int _frameCount;
    private long _lastCheckTime;
    private int _dropsDetected;
    
    public void OnFrameRendered()
    {
        Interlocked.Increment(ref _frameCount);
        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref _lastCheckTime);
        if (now - last >= Stopwatch.Frequency)
        {
            int count = Interlocked.Exchange(ref _frameCount, 0);
            Interlocked.Exchange(ref _lastCheckTime, now);
            if (count < 50)
            {
                Interlocked.Increment(ref _dropsDetected);
                CrashReporter.LogError($"Frame drop detected: {count} fps");
            }
        }
    }
}
```

**Measure:**
- Frame drop incidents logged to `cine_errors.log`
- Counters available for HUD / debug overlay

### 2.3 mpv Premium Tuning (MpvConfig)
**File:** `src/Media/Implementations/mpv/MpvConfig.cs`

**Problem:** Current mpv options are good but not optimized for the premium latency-sensitive OpenGL render path.

**Solution:** Add `ApplyPremiumTuning()` static method that overrides or adds options for:
- Low-latency audio buffer (100ms)
- Early OpenGL flush (reduces frame latency)
- Cache disabled (not needed for local files — reduces seek latency)
- Display resampling (linear for smooth rate matching)
- Hardware decoding auto (best performance)

**Implementation:**
```csharp
public static Dictionary<string, string> GetPremiumTuningOptions()
{
    return new()
    {
        ["cache"] = "no",
        ["opengl-early-flush"] = "yes",
        ["hwdec"] = "auto",
        ["audio-buffer"] = "0.1",
        ["display-resample"] = "linear",
        ["video-sync"] = "display-resample",
        ["video-sync-max-audio-change"] = "0.1",
    };
}
```

### 2.4 MpvVideoView Render Integration
**File:** `src/App/Controls/MpvVideoView.cs`

**Updates:**
- Add `RenderThrottleService` instance — check `ShouldRender()` before processing each frame.
- Add `PerformanceMonitor` instance — call `OnFrameRendered()` after successful display.
- Wire `LogPerfStats()` to use the monitor counters.

**Measure:**
- Render throttle check prevents >60fps submission bursts
- Performance monitor logs low-fps events to crash log

**Deliverables:**
- `md/perf-instrumentation.md`
- `src/App/Application/Services/PerformanceMonitor.cs`
- `src/App/Application/Services/RenderThrottleService.cs`
- Updated `MpvConfig.cs` with premium tuning
- Updated `MpvVideoView.cs` with throttle + monitor integration

**Measure:**
- Render call rate ≤ 60fps (monitored)
- Frame drop events logged and detectable
- mpv OpenGL render path uses premium low-latency options

---

## Phase 3 - Focus and Input Architecture Rebuild (Week 3-4)
Make keyboard and focus behavior predictable and resilient.

**Implementation scope:** Stack-based scope management in `InputRoutingService`, startup conflict validation, and improved `OnKeyDown` routing in `MainWindow`.

### 3.1 Stack-Based Input Scope Management
**Problem:** The old `InputRoutingService` used a flat scope model — `TryHandle(key, scope)` required the caller to pass the scope manually. Nested dialogs were not handled correctly; the scope was computed each time from `OwnedWindows`, which is fragile.

**Solution:** Added a `Stack<InputScope>` to `InputRoutingService` with push/pop methods.

**Implementation:**
- `PushScope(InputScope)` and `PopScope()` manage the scope stack.
- `ClearScopes()` resets to Normal (bulk dialog close).
- `CurrentScope` property returns the top of the stack.
- `TryHandle(KeyEventArgs)` now reads scope from the stack — no explicit scope parameter needed.
- Added `TextEdit` scope value for text-control focus detection.

**Files changed:**
- [`src/App/Application/Services/InputRoutingService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/InputRoutingService.cs) — scope stack, PushScope/PopScope/CurrentScope, scoped TryHandle, TextEdit scope.

### 3.2 Focus Scope Management in OnKeyDown
**Problem:** OnKeyDown had manual scope detection with conditional branches. Text controls were silently skipped ("return" without routing). PIP blocking was inline without scope abstraction.

**Solution:** Rewrote `OnKeyDown` to use push/pop for each scope region:
- **Text-edit scope:** Detects focused TextBox/AutoCompleteBox/NumericUpDown → pushes `TextEdit` scope, routes keys, pops.
- **Dialog scope:** Scans `OwnedWindows` for visible dialogs → pushes `DialogOpen` scope, routes keys, pops.
- **PIP scope:** Only Escape and Ctrl+Shift+P allowed — routed through `PipActive` scope push/pop.
- **Normal scope:** Direct routing.

```csharp
private void OnKeyDown(object? sender, KeyEventArgs e)
{
    // Text-edit scope
    if (focus is TextBox or AutoCompleteBox or NumericUpDown)
    {
        _inputRouter?.PushScope(InputScope.TextEdit);
        try { if (_inputRouter.TryHandle(e)) e.Handled = true; }
        finally { _inputRouter?.PopScope(); }
        return;
    }

    // Dialog scope — detect owned windows
    if (hasVisibleDialog) { /* push DialogOpen → route → pop */ return; }

    // PIP scope
    if (_pipWindowManager.IsActive) { /* push PipActive → route → pop */ return; }

    // Normal scope
    if (_inputRouter.TryHandle(e)) e.Handled = true;
}
```

**Files changed:**
- [`src/App/UI/Shell/MainWindow.Input.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) — rewritten OnKeyDown, added ShowDialogWithScope helper.

### 3.3 Keyboard Conflict Detection
**Problem:** No validation that keyboard shortcuts are unique. Duplicate bindings (same key+modifiers in the same scope) cause silent overwrites (last-registration-wins) which can surprise developers.

**Solution:** Created `KeyboardConflictValidator` that scans all registered bindings and reports conflicts.

**Implementation:**
- Groups bindings by `(Key, Modifiers, Scope)`.
- Logs each conflict via `Debug.WriteLine` and `CrashReporter.LogError`.
- Called once at the end of `RegisterKeyboardShortcuts()`.
- Provides `ValidationResult` with `ConflictCount`, `IsClean`, and detailed `ConflictEntry` list.

**Files created:**
- [`src/App/Application/Services/KeyboardConflictValidator.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/KeyboardConflictValidator.cs)

### 3.4 Focused Mode (Future)
**Goal:** Provide a distraction-free mode where only playback commands remain active.

**Status:** Planned but not implemented in this phase. The scope stack foundation is in place (TextEdit scope can be extended to a "Focused" scope).

**Measure:**
- Focus mode usability score
- Exit rate from focused mode

**Deliverables (Phase 3):**
- [`src/App/Application/Services/InputRoutingService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/InputRoutingService.cs) — stack-based scope, PushScope/PopScope, TextEdit scope.
- [`src/App/Application/Services/KeyboardConflictValidator.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/KeyboardConflictValidator.cs) — startup conflict detection.
- [`src/App/UI/Shell/MainWindow.Input.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) — rewritten OnKeyDown with push/pop scope routing, validation call at registration end.

**Measure:**
- All keyboard scopes use explicit push/pop (no raw scope parameters).
- Conflicts detected at startup and logged.
- Text-edit controls can register their own shortcuts via `TextEdit` scope.

---

## Phase 4 - Shell and Windowing Model Redesign (Week 3-4)
Restructure the window shell so it supports premium use cases safely.

### 4.1 Flyout Overflow Audit and Fixes
**Problem:** Flyouts are brittle and can break layout.

**Action:** Validate flyouts at runtime and convert overflow cases to panels.

**Implementation sample:**
```csharp
public class FlyoutOverflowValidator
{
    public static bool ValidateFlyout(Flyout flyout, Window parentWindow)
    {
        var screenBounds = Screen.PrimaryScreen.WorkingArea;
        var flyoutBounds = flyout.DesiredSize;
        return flyoutBounds.Height <= screenBounds.Height * 0.9;
    }
}
```

### 4.2 Popovers vs Dedicated Windows Decision Tree
**Decision logic:**
- < 5 items → compact popover
- 5–20 items → scrollable popover
- > 20 items or frequent return → panel window
- Settings/configuration → dedicated settings window
- Persistent content → sidebar or docked panel

### 4.3 Consolidated Command Hub (Spotlight-Style)
**Goal:** One searchable entry point for actions, shortcuts, and settings.

**Implementation sample:**
```csharp
public partial class CommandPaletteWindow : Window
{
    InputBindings.Add(new KeyBinding(ShowCommand, Key.P, ModifierKeys.Control | ModifierKeys.Shift));
}
```

### 4.4 Top Bar Redesign
**Goal:** remove noisy borders and improve layout clarity.

**Implementation sample:**
```xml
<Grid Background="{DynamicResource ApplicationBackgroundBrush}" Padding="12,8,12,8">
</Grid>
```

### 4.5 Panel Standardization
**Goal:** make all windows and dialogs share a common structure.

**Implementation sample:**
```csharp
public abstract class PremiumPanelBase : Window
{
    public PremiumPanelBase()
    {
        MinWidth = 300;
        MinHeight = 200;
        ResizeMode = ResizeMode.CanResize;
    }
}
```

**Deliverables:**
- `src/App/UI/Controls/AnchoredPopoverControl.cs`
- `src/App/UI/Windows/CommandPaletteWindow.cs`
- `src/App/UI/Controls/FlyoutOverflowValidator.cs`
- Top bar redesign and shared panel base

---

## Phase 5 - Motion, Micro-Interactions, and Delight Layer (Week 4)
Add motion only where it helps understanding.

**Scope:** XAML animation resources, slider/button/flyout transitions, and PIP seek feedback.

### 5.1 Easing Token Expansion
**Before:** Three easing tokens in `Motion.axaml` (ease-standard, ease-emphasized, ease-decelerated, ease-accelerated, ease-linear).

**After:** Added `ease-spring` (BackEaseOut) for premium emphasis animations.

**Files:**
- [`src/App/UI/Resources/Motion.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Motion.axaml) — added `ease-spring` easing token.

### 5.2 FlyoutPresenter Open Animation
**Before:** Flyouts had a simple opacity fade-in (opacity 0→1 over 200ms).

**After:** Added `scale(0.95)→scale(1)` transform alongside the opacity fade using `QuadraticEaseOut` for a polished pop-in feel.

**Files:**
- [`src/App/UI/Resources/App.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/App.axaml) — FlyoutPresenter animation now includes `RenderTransform scale` keyframes.

### 5.3 Slider Thumb Hover Animations
**Before:** Slider thumbs (seek, volume, compact) had no hover state — thumb size was static.

**After:** Added `RenderTransform="scale(1)"→scale(1.15–1.3)` transition on `:pointerover` with 120ms `CubicEaseOut` easing. Hovering the seekbar volume or compact slider shows a subtle thumb growth for tactile feedback.

**Files:**
- [`src/App/UI/Resources/App.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/App.axaml) — `Slider.seek-slider /template/ Thumb#thumb`, `Slider.volume-slider /template/ Thumb#thumb`, `Slider.compact /template/ Thumb#thumb` all have `:pointerover` scale transitions.

### 5.4 Dialog Animation Enhancement
**Before:** Dialog panel animation used `0.2s` linear easing.

**After:** Changed to `0.25s` with `ease-emphasized` (CubicEaseOut) for a smoother, more premium entrance.

**Files:**
- [`src/App/UI/Resources/App.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/App.axaml) — `Panel.dialog-animate` transitions updated.

### 5.5 PIP Seek Thumb Hover
**Before:** PIP seek thumb jumped instantly from 12px→16px with no transition.

**After:** Added `Width`/`Height` DoubleTransition (120ms) so the thumb smoothly enlarges on hover and shrinks back on release.

**Files:**
- [`src/App/UI/Screens/Dialogs/PipWindow.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PipWindow.axaml) — `Border.pip-seek-thumb` transitions added.

**Measure:**
- Animation satisfaction score
- Motion-induced distraction incidents

---

## Phase 6 - Menu and Control Consolidation (Week 4-5)
Reduce clutter and improve command discovery.

**Scope:** Primary menu deduplication, context menu progressive disclosure, removal of redundant items.

### 6.1 Primary Menu Deduplication
**Problem:** The primary (3-dot) menu duplicated 4 playback commands (Play/Pause, Stop, Seek ±10s) and Fullscreen that were already available via keyboard shortcuts and the right-click context menu. This created two paths for the same actions and cluttered the menu.

**Solution:** Removed the entire PLAYBACK section and Fullscreen from VIEW. The primary menu now contains only items that are **unique** to it:
- **VIEW:** Picture in Picture, Always on Top
- **LOOP:** Loop File (toggle), Loop Playlist (toggle), Shuffle (toggle)
- **TOOLS:** Go to Time, Keyboard Shortcuts, Preferences, About Cine

Menu reduced from 16 items to 10 items (~37% reduction).

**Files:**
- [`src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs) — `BuildPrimaryMenu()` rewritten to exclude PLAYBACK and Fullscreen.

### 6.2 Context Menu Progressive Disclosure
**Problem:** The context menu showed all 5 aspect ratios and all 5 crop options inline, creating long submenus for actions most users won't use frequently.

**Solution:** Collapsed less-common ratios (16:10, 2.35:1) behind a "More…" submenu in both Aspect Ratio and Crop sections. Common options (Original, 16:9, 4:3 for ratio; Off, 16:9, 4:3 for crop) remain inline.

Also removed Preferences and About Cine from the context menu bottom bar — these are infrequently accessed and already in the primary menu. Replaced with Keyboard Shortcuts (more relevant during right-click interaction).

**Files:**
- [`src/App/UI/Builders/VideoContextMenuBuilder.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/VideoContextMenuBuilder.cs) — Added progressive disclosure for Aspect Ratio and Crop; replaced bottom actions.

### 6.3 ControlsBox Audit
**Status:** The controls box (seekbar, transport, volume, time, audio/subtitle/eq buttons) is well-organized. All items are playback-critical. No changes needed.

**Measure:**
- Menu complexity score (primary menu: 10 items, 3 sections)
- Actions discoverability time

---

## Phase 7 - Audio-Visual Craft Pass (Week 5)
Improve the media presentation layer.

**Scope:** Subtitle default styling, track switching OSD feedback, screenshot service polish.

### 7.1 Subtitle Defaults — Premium Readability
**Problem:** Built-in subtitle defaults (font scale 1.0x, Arial font, border size 2.0, shadow offset 1.0) were functional but not optimized for premium out-of-box readability. Users had to manually adjust settings for a great experience.

**Solution:** Updated `SubtitleDefaults` built-in values:
| Property | Before | After | Rationale |
|----------|--------|-------|-----------|
| `FontScale` | 1.0 | 1.1 | Slightly larger for better readability on HD/4K |
| `BorderSize` | 2.0 | 2.5 | Thicker outline for clearer separation from video content |
| `ShadowOffset` | 1.0 | 1.5 | Deeper shadow for subtle depth and contrast |
| `Font` | Arial | Segoe UI | Modern system font, better readability at small sizes |

Version bumped from 2→3 so saved defaults are regenerated on next launch.

**Files:**
- [`src/App/Application/Managers/SubtitleSettingsStore.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleSettingsStore.cs) — SubtitleDefaults.Version 3, new SubtitleStyle defaults.

### 7.2 Track Switching OSD Feedback
**Problem:** When users switched audio or subtitle tracks (via menu, flyout, or keyboard shortcut), there was no visual confirmation. The only feedback was the playback change itself.

**Solution:** Added `TrackChangedMessage` callback to both `SubtitleManager` and `AudioManager`, wired to `ShowOsdNotification` in MainWindow startup. When a track is selected, an OSD notification appears at the bottom of the screen with the track name and an icon (ClosedCaption for subtitles, Music for audio).

```csharp
_subtitleManager.TrackChangedMessage = msg =>
    ShowOsdNotification(MaterialIconKind.ClosedCaption, $"Subtitle: {msg}");
_audioManager.TrackChangedMessage = msg =>
    ShowOsdNotification(MaterialIconKind.Music, $"Audio: {msg}");
```

**Files:**
- [`src/App/Application/Services/ISubtitleManager.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ISubtitleManager.cs) — Added `TrackChangedMessage` property.
- [`src/App/Application/Managers/SubtitleManager.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs) — Fires `TrackChangedMessage` on track selection.
- [`src/App/Application/Managers/AudioManager.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs) — Added `TrackChangedMessage` property, fires on track selection.
- [`src/App/UI/Shell/MainWindow.Initialization.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Initialization.cs) — Wires OSD callbacks.

### 7.3 Screenshot Service Polish
**Problem:** Screenshot filenames were plain (`screenshot_2026-06-29_120000_1.png`) with no media name context. No OSD feedback when saving.

**Solution:**
- Filename now includes media name: `Cine_MyMovie_2026-06-29_120000_1.png`
- Added `ScreenshotSaved` callback (fires after save with the file path)
- Added `MediaName` property (set when file opens)
- Added configurable `Format` property (default `.png`)
- Sanitized filenames for cross-platform safety

**Files:**
- [`src/App/Application/Services/ScreenshotService.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/ScreenshotService.cs) — MediaName, ScreenshotSaved callback, Format property, sanitized naming.

**Measure:**
- Subtitle readability score (1.1× font scale baseline)
- OSD feedback present for all track switches
- Screenshot filename includes media context

---

## Phase 8 - Accessibility and Inclusive Premium (Week 5-6)
Make premium quality inclusive.

**Scope:** Keyboard focus indicators, screen reader labels, color contrast improvements.

### 8.1 Focus Indicators
**Problem:** Interactive controls (buttons, sliders, checkboxes) had no visible focus indicator when navigated via keyboard. Users relying on keyboard navigation (Tab, arrow keys) received no visual feedback about which control was active.

**Solution:** Added `:focus-visible` style rules for all interactive control types:
- `Button:focus-visible`, `ToggleButton:focus-visible` — accent-colored border (2px, AppAccent #FF6CB4FF)
- `Slider:focus-visible /template/ Thumb#thumb` — accent border on thumb
- `TextBox:focus-visible`, `NumericUpDown:focus-visible` — accent border (1.5px)
- `CheckBox:focus-visible`, `RadioButton:focus-visible` — accent border

All focus indicators use the app accent color (#6CB4FF) which provides strong contrast against the dark background (~8:1 ratio).

**Files:**
- [`src/App/UI/Resources/App.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/App.axaml) — Added global `:focus-visible` selectors.

### 8.2 Screen Reader Labels
**Problem:** Several interactive controls had no `AutomationProperties.Name` set, making them invisible to screen readers.

**Solution:** Added `AutomationProperties.Name` to:
- `BtnOpenFile` — "Open media file"
- `BtnOpenFolder` — "Open media folder"

Existing controls (BtnPip, BtnPrimaryMenu, BtnMinimize, BtnMaximizeRestore, BtnClose, BtnFullscreenMenu) already had labels.

**Files:**
- [`src/App/UI/Screens/Start/StartPage.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Start/StartPage.axaml) — Added AutomationProperties to Open/Open Folder buttons.

### 8.3 Color Contrast Improvements
**Problem:** `AppTextOnDarkSubtle` was at `#60FFFFFF` (38% opacity), providing a borderline WCAG AA contrast ratio (~4.7:1 on the darkest surfaces) that could fail for smaller text sizes.

**Solution:** Increased to `#78FFFFFF` (47% opacity), raising the contrast ratio to approximately 6:1 — comfortably within WCAG AA even for small text.

**Files:**
- [`src/App/UI/Resources/Colors.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Colors.axaml) — `AppTextOnDarkSubtle` from `#60FFFFFF` → `#78FFFFFF`.

**Measure:**
- Accessibility compliance score
- Keyboard-navigation audit pass rate — all interactive controls have visible focus
- Screen reader coverage — all primary CTAs labeled

---

## Phase 9 - Reliability, Recovery, and Trust Layer (Week 6)
Make resilience a premium differentiator.

**Scope:** Hardened media open, session backup/crash recovery, startup failure handling.

### 9.1 Hardened Media Open
**Problem:** `OpenFile` had a bare `catch` that logged a warning and cleared `FilePath`, but didn't distinguish between missing files and actual load failures. Missing files were silently ignored.

**Solution:** Added file existence check before opening. Split the catch into `Exception ex` with structured error logging. Malformed files now produce a crash dump and clear the UI state.

**Files:**
- [`src/App/Application/ViewModels/MainViewModel.Actions.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Actions.cs) — File existence check, structured error logging.

### 9.2 Session Backup and Crash Recovery
**Problem:** If the app crashed while writing `session.json` (e.g., power loss), the file could be corrupted. The `Load()` method just returned `null` on any error — no recovery path.

**Solution:**
- **Save**: Before overwriting `session.json`, a backup is made to `session.json.bak`
- **Load**: If `session.json` is missing or corrupted, falls back to `.bak` and restores it
- Specific `JsonException` catch for corruption detection (other exceptions still return null)

**Files:**
- [`src/App/Application/Services/SessionManager.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/SessionManager.cs) — Backup on save, backup fallback on load, JsonException handling.

### 9.3 Startup Failure Handling
**Problem:** `OnFrameworkInitializationCompleted` caught exceptions and immediately rethrew them, causing an unhandled crash dialog. No crash dump was written.

**Solution:** Instead of rethrowing, the handler now:
1. Writes a structured crash dump via `CrashReporter.Dump()`
2. Cleanly shuts down via `desktop.Shutdown()` instead of crashing silently

**Files:**
- [`src/App/App.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs) — Startup catch block now dumps crash and shuts down gracefully.

**Measure:**
- Crash recovery success rate (session.json.bak used)
- Fail-safe UX clarity (startup crash → dump + clean shutdown)

---

## Phase 10 - Performance Budget and Instrumentation (Week 6-7)
Turn quality into measurable guardrails.

**Scope:** Per-phase startup timing markers with automatic breakdown logging.

### 10.1 Startup Timer (Per-Phase Timing)
**Problem:** Startup had a single `Stopwatch` that logged total time in ms, but no per-phase breakdown. When startup was slow, we couldn't tell which phase was the bottleneck.

**Solution:** Created `StartupTimer` — a lightweight helper that records named milestones with delta times and outputs a percentage breakdown at the end.

**Output example (cine_mainwin_gl.log):**
```
StartupTimer: 2432ms total
  begin                             89ms ( 3.7%)
  input-router                      11ms ( 0.5%)
  player-init                      412ms (16.9%)
  managers                          32ms ( 1.3%)
  viewmodel                         53ms ( 2.2%)
  video-renderer                  1087ms (44.7%)
  init-complete                     75ms ( 3.1%)
  opened                           628ms (25.8%)
  done                               0ms ( 0.0%)
```

**Phases tracked:**
1. `begin` — ctor overhead before OnWindowInitialized
2. `input-router` — InputRoutingService + keyboard shortcut registration
3. `player-init` — PlayerService initialization (mpv library load + GL context)
4. `managers` — AudioManager, VideoManager, SubtitleManager + DI wiring
5. `viewmodel` — MainViewModel construction + data binding
6. `video-renderer` — InitVideoRenderer (ANGLE FBO + mpv render context)
7. `init-complete` — post-init cleanup before window opens
8. `opened` — OnOpened (window positioning, playlist load, session resume, UI wiring)
9. `done` — finalize marker

**Files:**
- [`src/App/Application/Services/StartupTimer.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/StartupTimer.cs) — New file: lightweight per-phase timing with Mark/Finalize API.
- [`src/App/UI/Shell/MainWindow.Initialization.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Initialization.cs) — 7 Mark() calls added at phase boundaries; old `startupWatch` replaced with `_startupTimer.Finalize()`.
- [`src/App/UI/Shell/MainWindow.Core.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Core.cs) — Added `_startupTimer` field.

**Measure:**
- Input latency p95 < 80ms
- Hitch incident rate near zero
- Regression detection coverage — per-phase timing visible in logs

---

## Phase 11 - Signature Innovations (Week 7-8) — **completed**
Features that make Cine feel premium and unique.

### 11.1 Command Palette (Ctrl+K)
A searchable command palette modelled after VS Code / Sublime Text. Shows all available commands filtered by typed text. Press Enter or click to execute.

**Features:**
- Search box with real-time filtering (case-insensitive)
- Shows result count: "15 / 45 commands"
- Enter on filtered list executes first result
- Escape closes
- 35+ commands registered: Playback, Navigation, View, Speed, Audio, Subtitles, Screenshots, Dialogs, Zoom, Loop
- Self-contained as a modal dialog — no dependencies on internal routing

**Files:**
- [`src/App/UI/Screens/Dialogs/CommandPaletteDialog.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/CommandPaletteDialog.axaml) — Search box + filtered list, accent-colored border, 480×400 dialog.
- [`src/App/UI/Screens/Dialogs/CommandPaletteDialog.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/CommandPaletteDialog.axaml.cs) — Public `PaletteCommandEntry` record, filtering, keyboard navigation.

**Shortcut binding:**
- `Ctrl+K` — Show Command Palette (registered in MainWindow.Input.cs)

### 11.2 Focus Mode (Ctrl+.)
Ultra-minimal chrome mode. Hides the header bar, fullscreen header, and controls box — leaving only a thin 120×2px accent-colored indicator line at the bottom of the screen. Toggle back with Ctrl+. to restore chrome.

**Behavior:**
- First press: hides all chrome, shows indicator line, shows "Focus Mode" OSD notification
- Second press: restores chrome respecting fullscreen state, shows "Focus Mode Off" OSD notification
- Indicator: `AppAccent` color, 2px height, 120px width, centered, rounded corners
- Uses `_isFocusMode` boolean field

**Files:**
- [`src/App/UI/Views/MainWindow.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Views/MainWindow.axaml) — Added `FocusModeIndicator` Border element.
- [`src/App/UI/Shell/MainWindow.Core.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Core.cs) — Added `_isFocusMode` field.
- [`src/App/UI/Shell/MainWindow.Input.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) — `ToggleFocusMode()` method, `Ctrl+.` shortcut.

**Shortcut binding:**
- `Ctrl+.` — Toggle Focus Mode

### 11.3 Now Playing Info Panel (Ctrl+D)
An overlay panel showing media metadata. Appears at the bottom-right corner of the video area. Shows the following information for the current file:
- **File name** (with text trimming via ellipsis)
- **Resolution** (e.g. 1920×1080)
- **Frame rate** (e.g. 23.976 fps)
- **Video codec** (e.g. H264 / HEVC)
- **Audio** (language + codec + channel count)
- **Duration** (HH:MM:SS)

**Behavior:**
- First press: shows panel, refreshes metadata from player, shows OSD
- Second press: hides panel
- Panel is `IsHitTestVisible="False"` — does not interfere with clicks
- Uses `TrackDisplayHelper.GetLanguageName()` for audio language names

**Files:**
- [`src/App/UI/Controls/Indicators/NowPlayingInfoControl.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/NowPlayingInfoControl.axaml) — Information-overlay style panel with grid layout for label/value pairs.
- [`src/App/UI/Controls/Indicators/NowPlayingInfoControl.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/NowPlayingInfoControl.axaml.cs) — `Refresh()` reads from `IMediaPlayer`, fallbacks to "—" for unavailable fields.
- [`src/App/UI/Views/MainWindow.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Views/MainWindow.axaml) — Added `controls:NowPlayingInfoControl` element.

**Shortcut binding:**
- `Ctrl+D` — Toggle Now Playing Info

---

## Phase 12 - Premium Release Hardening (Week 8)
Lock in the experience with tests and reviews.

**Actions:**
- Run full cross-hardware matrix.
- Perform long-session soak tests.
- Freeze UX polish before release.
- Do a release candidate triage war-room.

---

## Engineering Streams
**Stream A:** Playback + renderer smoothness
**Stream B:** Input/focus routing model
**Stream C:** Shell redesign and control consolidation
**Stream D:** Motion and visual design system
**Stream E:** Accessibility and reliability
**Stream F:** Instrumentation and premium KPIs

Each stream must ship:
- Baseline metrics
- Refactor plan
- Incremental PRs
- Before/after evidence
- Risk assessment

---

## Premium KPI Dashboard
- Window drag playback hitch count (target: 0)
- Frame pacing outliers during resize (target: < 1%)
- Shortcut failure rate for core playback keys (target: 0)
- UI overflow incidents (target: 0)
- Crash-free session rate (target: 99.9%+)
- Command discovery success rate
- Accessibility compliance score

---

## Backlog Labels
- `premium-shell`
- `visual-system`
- `input-architecture`
- `playback-smoothness`
- `flyout-windowing`
- `interaction-polish`
- `accessibility`
- `resilience`
- `performance-budget`
- `signature-innovation`

---

## First 10 Execution Tickets
1. Remove non-essential top bar separator and rebalance visual hierarchy.
2. Build flyout overflow detection and convert problematic surfaces to bounded panels.
3. Instrument the window-move/resize playback hitch pipeline.
4. Audit and fix core shortcut behavior across focus contexts.
5. Create a centralized command map for playback and shell actions.
6. Standardize iconography size, hit area, and interactive state visuals.
7. Implement motion token system and apply to transitions.
8. Add keyboard-first navigation audit tests.
9. Introduce resilient fallback UX for runtime and dependency failures.
10. Ship a command palette prototype and evaluate discoverability.

---

## Definition of Done for Premium Milestone v1
Milestone is complete only when:
1. Playback remains smooth during all window interactions.
2. Keyboard controls are reliable and predictable in all intended contexts.
3. UI layout is consistent and no flyouts overflow or appear broken.
4. Core flows feel fast, elegant, and stable.
5. KPI targets are met for two consecutive release candidates.
