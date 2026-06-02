# Cine V2 — Premium Desktop Media Player Roadmap

> **Status**: 20% complete. This document defines the path to 95% completion.
> **Framework**: C# .NET 8, Avalonia UI, libmpv (mpv-2.dll), D3D11 hardware rendering.

---

## Architecture Principles

1. **Single Source of Truth**: Each piece of state (position, duration, play-state) has ONE authoritative path through the system. Code-behind and ViewModel never set the same property independently — that's how V1 broke.
2. **UI Thread Only for UI**: Native mpv API calls (`GetDouble("duration")`, `GetFlag("pause")`) MUST NOT be called from the UI thread while the mpv event loop holds its internal mutex. Cache values on the event loop thread, post them to UI.
3. **Animation as First Class**: Every state change has a 120–200ms transition. Buttons scale on hover, sliders ease, OSD fades. No instant jumps.
4. **Every UI Element is Closable**: Escape key dismisses the active flyout, popover, dialog, or exits fullscreen — in that priority order, always.
5. **Icons are Precise**: 24×24 viewBox, Viewbox-wrapped, centered in 34px circular buttons with self-documenting geometry.

---

## Phase 0 — Foundation Fixes (Critical — Do First)

### 0.1 Fix Seek Bar (Current: Broken)
**Current behavior**: Time labels update but progress bar stays at 0. Caused by `_seekUpdateTimer` reading `_cachedDuration` which is set by another `Dispatcher.UIThread.Post` on the ViewModel side — the two posts race and the seek timer often fires before the duration is cached.

**Fix**: Abandon the dual-post architecture. Extend `PositionChangedEventArgs` to carry both `Position` and a **pre-computed normalized seek value** (`Position.TotalSeconds / Duration.TotalSeconds`). The mpv event loop already has both values — compute the ratio ON the background thread (no race possible), then post a single event that both the ViewModel and MainWindow consume reliably.

```csharp
// In PositionChangedEventArgs (Media/Events/PositionChangedEventArgs.cs)
public class PositionChangedEventArgs : EventArgs
{
    public TimeSpan Position { get; }
    public TimeSpan Duration { get; }
    public double NormalizedPosition { get; } // pre-computed on bg thread
    public PositionChangedEventArgs(TimeSpan pos, TimeSpan dur)
    {
        Position = pos;
        Duration = dur;
        NormalizedPosition = dur.TotalSeconds > 0 ? pos.TotalSeconds / dur.TotalSeconds : 0;
    }
}
```

```csharp
// In MpvPlayer.EventLoop — send duration alongside position
var pos = GetDouble("time-pos");
var dur = GetDouble("duration"); 
if (pos >= 0 && dur > 0)
    PositionChanged?.Invoke(this, new PositionChangedEventArgs(
        TimeSpan.FromSeconds(pos), TimeSpan.FromSeconds(dur)));
```

Then MainWindow's `OnSeekUpdateTick` reads `_lastPosition` and `_lastDuration` (both set atomically from event args on the background thread), computing the seek bar fill trivially — no native calls on UI thread.

### 0.2 Fix Mute Toggle Visibility
**Issue**: `VolumeMuteCrossPath.IsVisible` is set to `False` in XAML but never toggled. The mute cross is always invisible.

**Fix**: In `RefreshVolumeIcon()`, set `VolumeMuteCrossPath.IsVisible = _viewModel.IsMuted`.

### 0.3 Fix Play/Pause Icon Update on Media Open
**Issue**: `UpdatePlayPauseIcon()` only fires on PropertyChanged for `IsPlaying`/`IsPaused`, but not when a new file is first opened and the player auto-plays.

**Fix**: Call `UpdatePlayPauseIcon()` inside `OnMediaOpened` (after `RefreshState()` in the ViewModel posts to UI). Also call `UpdateFullscreenIcon()` there.

### 0.4 Remove Duplicate Icon Setting
**Issue**: `OnPositionChanged` in MainWindow sets `_lastPosition` but `OnSeekUpdateTick` also reads `_viewModel.Duration`. This creates an implicit dependency.

**Fix**: Per 0.1, both position and duration arrive in the EventArgs. Save both:
```csharp
private TimeSpan _lastPosition;
private TimeSpan _lastDuration; // NEW — arrives pre-computed from event args
```

### 0.5 Fix Primary Menu Flyout — Remove Redundant Items
**Issue**: Play AND Pause shown as separate menu items, Previous/Next also already available as transport buttons.

**Fix**: Consolidate to:
- **PLAYBACK**: Play/Pause (single item, label toggles) | Stop | Seek ±10s | Seek ±60s
- **VIEW**: Fullscreen | PIP
- **LOOP**: Loop File | Loop Playlist | Shuffle
- **TOOLS**: Screenshot | Subtitles | Audio Tracks | Shortcuts | Preferences | About

---

## Phase 1 — Seek Bar Rewrite

### 1.1 New Seek Bar Component
Create `SeekBarControl.axaml` / `.axaml.cs` — a self-contained user control owned by MainWindow that:
- Receives `Duration` and `Position` via bound properties
- Has its own `DispatcherTimer` at 30fps (33ms) for smooth updates
- Contains the track, fill, thumb, and chapter markers
- Handles click-to-seek, drag-to-seek, wheel-to-seek±5s all internally
- Renders the seek preview thumbnail on hover (frame stepping via mpv screenshot)

### 1.2 Seek Bar States
```
┌─────────────────────────────────────────────────────────┐
│ ═══════════════════●──────────────────────────────────    │  idle (4px track, 16px thumb)
│ ═══════════════════════●──────────────────────────────    │  hover (6px track, 16px thumb, accent glow)
│ ═══════════════════════════════●──────────────────────    │  drag  (6px track, 16px thumb, blue #4aa3ff)
│ ═══●══●══════════════●═════════════════●═══════════●═══    │  chapter markers (2px×8px white at 30% opacity)
└─────────────────────────────────────────────────────────┘
```

### 1.3 Seek Thumb Animation
Avalonia `Transitions` on the thumb's `Margin` property:
```xml
<Border.Transitions>
    <Transitions>
        <ThicknessTransition Property="Margin" Duration="0:0:0.08" Easing="SineEaseOut" />
    </Transitions>
</Border.Transitions>
```

### 1.4 Hover Preview
On hover-over-seek-bar, capture a frame screenshot at the hover position and show it in a tooltip-style popup above the cursor. This requires:
- `mpv screenshot-to-file` at the seek position (or use `screenshot-raw` API)
- Display in a 240px × 135px Border with rounded corners and drop shadow
- Debounce to 200ms to avoid excessive screenshot captures

---

## Phase 2 — PIP Window Rewrite

### 2.1 Resizable PIP
**Current**: `WindowDecorations="BorderOnly"` prevents resize.
**Fix**: Change to `WindowDecorations="System"` or use `ResizeMode="CanResize"` with custom resize handles. Add `MinWidth="240" MinHeight="160"` to maintain readable aspect.

### 2.2 PIP Video Actually Plays
**Current**: A second mpv instance opens but the video surface may not render correctly because `InitializeRenderer(hwnd)` is called on a background thread before the D3D11 host is ready.

**Fix**: 
1. In `PipWindow.OnOpened`, wait for `ChildWindowCreated` event
2. Only then call `InitializeRenderer` on the native hwnd
3. Force a repaint by calling `Command("show-text", "", "0")` after first frame
4. Sync position from main player once, then let PIP run independently with its own timer

### 2.3 PIP Position Sync
**Current**: Timer at 250ms which is jittery.
**Fix**: Use mpv's `observe_property("time-pos")` on both instances. When main player's time-pos changes by >250ms, seek the PIP player. This avoids polling and gives frame-accurate sync.

### 2.4 PIP Exit Animation
When closing PIP, animate the PIP window shrinking back to the PIP button position (scale 1→0.3, opacity 1→0, 200ms) using `Window.Position` + `Window.Width/Height` interpolation on a `DispatcherTimer`.

---

## Phase 3 — Menu System Redesign

### 3.1 Primary Menu (Header ··· button)
| Section | Items |
|---------|-------|
| PLAYBACK | ▶⏸ Play/Pause (toggles label) · ⏹ Stop · ⏩ +10s · ⏪ -10s · ⏩ +60s · ⏪ -60s |
| VIEW | ⛶ Fullscreen (F) · 🖼 Picture-in-Picture (I) |
| LOOP | 🔂 Loop File (L) · 🔁 Loop Playlist (Ctrl+R) · 🔀 Shuffle (Ctrl+S) |
| TOOLS | 📸 Screenshot (S) · 💬 Subtitles (V) · 🔊 Audio Tracks (B) · ⌨ Shortcuts |
| SYSTEM | ⚙ Preferences · ℹ About Cine · 🚪 Exit |

Each item is a styled `Button` inside a `Flyout` (not `MenuItem` which doesn't work in regular Flyouts), with:
- Icon: 14×14 Viewbox + Path on left
- Label: 13px Inter Medium on center
- Shortcut: 11px Inter at 30% opacity on right
- Hover: `rgba(255,255,255,0.08)` background with 4px border-radius

### 3.2 Options Menu (⚙ Settings button)
| Section | Controls |
|---------|----------|
| ASPECT RATIO | Dropdown: Original / 16:9 / 4:3 / 1:1 / 16:10 / 2.35:1 / 2.39:1 / 21:9 |
| ROTATE / FLIP | Row of pill buttons: ↺L · R↻ · ↺ · ⇔H · V⇕ |
| VIDEO | Sliders: Zoom (0–400%), Contrast (−100–100), Brightness (−100–100), Gamma (−100–100), Saturation (−100–100), Hue (−100–100) |
| AUDIO | Delay slider (−5s–5s), Volume boost (0–200%) |
| SUBTITLE | Delay slider (−5s–5s) |
| SPEED | Row of pill buttons: 0.5× · 0.75× · **1.0×** (highlighted) · 1.25× · 1.5× · 2.0× |

### 3.3 Volume Popover
- Trigger: Click volume button
- Design: 48px × 160px floating panel above button
- Content: Mute toggle icon (top) + vertical slider (center) + percentage label (bottom)
- Animate: Scale 0.8→1.0 + opacity 0→1, 150ms, EaseOutBack
- Dismiss: Click outside, Escape, or 3s idle timeout

### 3.4 Keyboard Shortcuts Dialog
A modal dialog showing all shortcuts in a clean two-column grid, matching the canvas design. Accessible via `?` key or menu.

---

## Phase 4 — Animation Suite

### 4.1 Button Hover (circular-menu / circular-transport)
```xml
<Style Selector="Button.circular-menu:pointerover">
    <Setter Property="Background" Value="rgba(255,255,255,0.17)" />
    <Setter Property="RenderTransform" Value="scale(1.05)" />
</Style>
```
Use `Transitions` on `RenderTransform`:
```xml
<Button.Transitions>
    <Transitions>
        <TransformTransition Property="RenderTransform" Duration="0:0:0.12" />
    </Transitions>
</Button.Transitions>
```

### 4.2 OSD Fade (Volume, Play/Pause indicator, Seek)
```xml
<Style Selector="Border.osd-notification">
    <Setter Property="Opacity" Value="0" />
    <Setter Property="Transitions">
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="0:0:0.2" />
        </Transitions>
    </Setter>
</Style>
```
Trigger: Set `Opacity=1`, then `Task.Delay(2000).ContinueWith(... Opacity=0)`.

### 4.3 Start Page Fade
When media loads: StartPage fades out (opacity 1→0, 300ms) while VideoHost fades in (opacity 0→1, 500ms, with a 100ms delay). Use two `DoubleTransition` triggers.

### 4.4 Loading Spinner
Current has a CSS spinner. Replace with an Avalonia `RotateTransform` animation:
```xml
<Border.Transitions>
    <Transitions>
        <TransformTransition Property="RenderTransform" Duration="0:0:0.8" />
    </Transitions>
</Border.Transitions>
<Border.RenderTransform>
    <RotateTransform Angle="0" />
</Border.RenderTransform>
```
On spinner visible: continuously increment `RotateTransform.Angle` by 360° every 800ms via `DispatcherTimer`.

### 4.5 Auto-Hide Controls Fade
When controls auto-hide (3s idle): `ControlsBox.Opacity` transitions 1→0 over 400ms, starting 2.5s after cursor leaves the window. On cursor re-enter: instantly back to 1.

---

## Phase 5 — Polish & QA

### 5.1 Title Bar
- **Window title**: Center-aligned, shows "Cine" when idle, "Cine — {filename}" when playing
- **Click-through**: Title bar area above the video is draggable (window move)
- **Context menu on title**: Right-click → regular window context menu with Move/Size/Minimize/Maximize/Close

### 5.2 Drag & Drop Indicator
When files are dragged over the window:
- Darkened overlay with dashed #4aa3ff border
- Centered play icon + "Drop to Play" text
- Animate: scale(0.97)→scale(1.0), opacity 0→1, 150ms

### 5.3 Subtitle Font Style
Add to Options Menu: Subtitle Font Family, Size, Color, Border Style (Outline/Shadow/Opaque Box), and Alignment.

### 5.4 Always-On-Top
Add toggle button in Primary Menu → VIEW section: "Always on Top". Uses `Topmost = true` on the Window.

### 5.5 Last Session Resume
Save the last played file path and position to `%LocalAppData%/Cine/session.json`. On app launch, offer "Resume {filename} from {timestamp}?" as an OSD notification with a "Resume" button.

### 5.6 Audio Visualizer (Optional)
Add a small audio frequency spectrum visualizer (30px height) between the seek bar and transport buttons. Uses mpv's `audio-levels` or `fft` output via `mpv_observe_property("audio-levels", MPV_FORMAT_NODE)`. This is a nice-to-have premium touch.

---

## Implementation Order (Priority)

| Order | Phase | Item | Impact | Effort |
|-------|-------|------|--------|--------|
| 1 | 0.1 | Fix seek bar — send Duration in EventArgs | Critical | Small |
| 2 | 0.2 | Fix mute cross visibility | Critical | Small |
| 3 | 0.3 | Fix play/pause icon on media open | Critical | Small |
| 4 | 0.4 | Remove duplicate icon setting in code-behind | Critical | Small |
| 5 | 0.5 | Simplify primary menu — remove redundant items | High | Small |
| 6 | 1.1–1.3 | New seek bar component with animations | High | Medium |
| 7 | 2.1–2.3 | PIP resizable + working video + smooth sync | High | Medium |
| 8 | 3.1 | Primary menu redesign (styled flyout) | High | Medium |
| 9 | 3.2 | Options menu v2 layout | High | Medium |
| 10 | 3.3 | Volume popover with animation | Medium | Small |
| 11 | 4.1–4.5 | Animation suite (hover, fade, spinner, auto-hide) | Medium | Medium |
| 12 | 5.1–5.2 | Title bar, drag-drop indicator | Medium | Small |
| 13 | 5.3–5.5 | Subtitle styling, always-on-top, session resume | Low | Medium |
| 14 | 5.6 | Audio visualizer | Nice-to-have | Large |

---

## File Structure — Target for V2

```
src/App/UI/
├── Views/
│   ├── MainWindow.axaml          (cleaner, ~500 lines)
│   ├── MainWindow.axaml.cs       (only UI logic, ~800 lines)
│   ├── PipWindow.axaml           (resizable, full controls)
│   ├── PipWindow.axaml.cs        (robust init, ~200 lines)
│   ├── PlaylistDialog.axaml      (search, clear, show-current)
│   └── PlaylistDialog.axaml.cs
├── Components/
│   ├── SeekBarControl.axaml      (NEW — standalone seek bar)
│   ├── SeekBarControl.axaml.cs
│   ├── SeekPreviewPopup.axaml    (NEW — frame preview on hover)
│   ├── SeekPreviewPopup.axaml.cs
│   ├── VolumePopover.axaml       (NEW — volume slider popover)
│   ├── VolumePopover.axaml.cs
│   ├── OptionsMenuButton.axaml   (rewritten)
│   ├── OptionsMenuButton.axaml.cs
│   ├── StartPage.axaml           (polished)
│   └── StartPage.axaml.cs
├── Resources/
│   ├── Icons.axaml               (all ~30 icon geometries)
│   ├── Colors.axaml              (full dark theme palette)
│   ├── Typography.axaml          (Inter + JetBrains Mono)
│   ├── Styles.axaml              (Button, Slider, Toggle, Flyout styles)
│   └── Animations.axaml          (NEW — shared Keyframe/Transition defs)
└── Controls/
    └── D3D11VideoHost.cs         (hardware rendering — no changes)
```

---

## State Machine — Player Lifecycle

```
IDLE ──[Open File]──▶ LOADING ──[FILE_LOADED]──▶ PLAYING
                          │                         │
                          └──[Error]──▶ ERROR       ├──[Pause]──▶ PAUSED
                                                    ├──[End]───▶ STOPPED
                                                    └──[Stop]──▶ STOPPED

All transitions should:
1. Update PlayPauseIconPath.Data (Play ↔ Pause)
2. Update state-dependent UI visibility (spinner, controls, OSD)
3. Animate the transition with 120ms easing
```

---

## Acceptance Criteria

- [ ] Open a video — seek bar fills smoothly, time labels update, no flicker
- [ ] Click anywhere on seek bar — seeks instantly, thumb follows mouse during drag
- [ ] PIP opens with working video, can be resized, seek works independently
- [ ] All flyouts close on Escape (volume → primary menu → options → fullscreen exit)
- [ ] Buttons have 120ms scale animation on hover
- [ ] OSD notifications (volume, play/pause) fade in/out with 200ms transition
- [ ] Start page fades out when media loads
- [ ] Drag-and-drop overlay shows with animation
- [ ] Primary menu has no duplicate/redundant items
- [ ] Volume popover shows mute button + slider + percentage
- [ ] Double-tap video surface toggles play/pause within 300ms
