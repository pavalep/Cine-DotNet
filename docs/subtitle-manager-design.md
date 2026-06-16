# Subtitle Manager — Premium Media Player Design

## Philosophy

A premium media player treats subtitle settings as **first-class per-track preferences**, not global settings. Users expect:

- Subtitles that "just work" (auto-select preferred language)
- Settings that **remember** per-file (position, font size, delay, enabled)
- Instant feedback — change font size, see it immediately
- **Manual selection overrides auto-detect** — once user picks a track, that choice sticks
- Persistence across sessions (close app, reopen, subtitles are exactly as you left them)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           SubtitleManager                               │
│                                                                         │
│  ┌────────────────┐  ┌──────────────────────┐  ┌────────────────────┐  │
│  │   Track State   │  │   Display Settings   │  │  Persistence Layer │  │
│  │                │  │                      │  │                    │  │
│  │ - sid          │  │ - sub-visibility     │  │ - Save/Load per    │  │
│  │ - tracks[]     │  │ - sub-pos (0-100)    │  │   file overrides   │  │
│  │ - selected     │  │ - sub-scale          │  │ - Global defaults  │  │
│  │ - currentLang  │  │ - sub-delay          │  │ - Auto-detect prefs│  │
│  │ - hasPGS       │  │ - sub-font           │  │                    │  │
│  │                │  │ - sub-font-size      │  │                    │  │
│  └────────────────┘  │ - sub-border-size    │  └────────────────────┘  │
│                      │ - sub-color          │                          │
│                      │ - sub-shadow-offset  │                          │
│                      └──────────────────────┘                          │
└─────────────────────────────────────────────────────────────────────────┘
         │                           │
         ▼                           ▼
   MpvPlayer (observed        SettingsStore
   properties: sid,           (JSON files)
   sub-visibility,
   sub-pos, sub-scale,
   sub-delay)
```

---

## Mpv Properties Observed

| Property | Type | Range | Default | Description |
|---|---|---|---|---|
| `sid` | `int` | -1..N | -1 | Selected subtitle track (-1 = none) |
| `sub-visibility` | `flag` | true/false | true | Show/hide subtitles |
| `sub-pos` | `int` | 0-100 | 100 | Vertical position (100 = bottom of screen) |
| `sub-scale` | `double` | 0.1-10.0 | 1.0 | Font size relative to default |
| `sub-delay` | `double` | -60..60 | 0.0 | Delay in seconds (negative = earlier) |
| `sub-font` | `string` | — | "Arial" | Font family name |
| `sub-font-size` | `int` | 1-100 | 32 | Absolute font size (takes precedence if set) |
| `sub-border-size` | `float` | 0-10 | 2.0 | Outline/border width |
| `sub-color` | `string` | hex/rgb | "#FFFFFF" | Text color |
| `sub-shadow-offset` | `float` | 0-10 | 1.0 | Shadow distance |

All observed via `mpv_observe_property` (same as `pause`) — single source of truth.

---

## Setting Priority Hierarchy (Industry Standard)

From most to least specific:

```
┌───────────────────────────────────────────┐
│    1. Session override (user pick)        │  ← Never overridden by auto-detect
├───────────────────────────────────────────┤
│    2. Per-file saved settings             │  ← Saved from previous session
├───────────────────────────────────────────┤
│    3. Global defaults                     │  ← From preferences / defaults.json
├───────────────────────────────────────────┤
│    4. mpv built-in defaults               │  ← Last resort
└───────────────────────────────────────────┘
```

**Key rule (from Moonfin player):** Once a user manually selects a track or adjusts a setting, the `_sessionOverride` flag is set. Auto-detect is **disabled** for the remainder of the session (or until user explicitly resets). This prevents the player from fighting the user.

---

## Property Change Flow

```
mpv internal state change
        │
        ▼
MpvPlayer.HandlePropertyChange("sub-scale")
        │
        ▼
SubtitleManager.OnSubScaleChanged(double newValue)
        │
        ├──> Update _fontScale field
        ├──> Fire PropertyChanged(nameof(FontScale))
        │       │
        │       ▼
        │   UI binds → slider updates, OSD shows value
        │
        └──> _dirtySettings.Add("sub-scale")
             _debounceSaveTimer starts (2s)
```

When user adjusts from **UI** (slider, keyboard, menu):

```
User drags font-size slider
        │
        ▼
SubtitleManager.FontScale = 1.2
        │
        ├──> Set session override flag
        ├──> _player.SetProperty("sub-scale", 1.2)  ← Immediate
        ├──> Fire PropertyChanged(nameof(FontScale))
        └──> Save debounced (2s)
```

---

## External Subtitle File Discovery (Industry Standard)

### Naming Convention

From GOM Player, VLC, and ViewRA — the standard pattern:

```
Movie.mkv                          ← Video file
Movie.srt                          ← Subtitle (language-agnostic)
Movie.en.srt                       ← English subtitles
Movie.en.forced.srt                ← Forced (foreign dialogue only)
Movie.en.sdh.srt                   ← SDH (captions for deaf/HoH)
Movie.en.hi.srt                    ← Hearing impaired (same as SDH)
Movie.en.cc.srt                    ← Closed captions
```

### Search Order

```
OnMediaOpened
  │
  ├── 1. same directory as media
  ├── 2. ./subs/
  ├── 3. ./subtitles/
  ├── 4. ./.subtitles/
  ├── 5. Custom paths from preferences
  │
  └── Match priority:
        a. Exact filename match (Movie.en.srt)
        b. Fuzzy match (Movie*.srt)
        c. Language priority match
           ["eng", "jpn", "und"] → prefer eng
```

---

## Track Selection Logic

### Auto-Select (first play / no session override)

```
1. If per-file has savedTrackId → select it
2. Else find first track matching preferredLanguages[]
   e.g. if languages = ["eng", "jpn"]
   → select first track tagged "eng"
   → if none, try "jpn"
   → if none, try "und" (undetermined)
3. Else if external sub found for preferred language → load it
4. Else → sid = -1 (no subtitles)
```

### Manual Override

```csharp
public void SelectTrack(int trackId)
{
    _sessionOverride = true;        // ← Stops auto-detect from overriding
    _player.SelectSubtitleTrack(trackId);
    // Save queued
}
```

### Reset

```csharp
public void ResetToDefault()
{
    _sessionOverride = false;       // ← Re-enable auto-detect
    DeletePerFileSettings();
    LoadGlobalDefaults();
    AutoSelectTrack();               // ← Re-run auto-select logic
}
```

---

## Persistence

### Storage Format

```
%LOCALAPPDATA%\Cine\subtitles\
  ├── defaults.json              ← Global defaults
  ├── md5_of_media_path.json    ← Per-file overrides
  └── ...
```

### `defaults.json`

Includes styling preferences (from mpv.conf best practices):

```json
{
  "version": 2,
  "autoEnabled": true,
  "preferredLanguages": ["eng", "jpn", "und"],
  "fallbackToExternal": true,
  "externalSubDirectories": ["./subs", "./subtitles"],
  "style": {
    "fontScale": 1.0,
    "fontName": "Arial Unicode MS",
    "fontSize": 48,
    "position": 100,
    "delay": 0.0,
    "color": "#d1d1d1",
    "borderSize": 1.5,
    "borderColor": "#000000",
    "shadowOffset": 1.0,
    "shadowColor": "#00000040",
    "blur": 0.2,
    "bold": true
  }
}
```

### Per-file Override

```json
{
  "version": 2,
  "mediaPath": "C:\\Movies\\Film.mkv",
  "mediaHash": "abc123...",
  "selectedTrackId": 2,
  "subtitleVisible": true,
  "styleOverrides": {
    "fontScale": 1.2,
    "position": 92,
    "delay": -0.5
  },
  "updatedAt": "2026-06-16T14:30:00Z"
}
```

### Load Strategy

```
OnMediaOpened
  │
  ├──> 1. Load global defaults (apply to mpv)
  │
  ├──> 2. Load per-file override if exists
  │       └──> Apply non-null styleOverrides to mpv
  │
  ├──> 3. Auto-select track (unless session override active)
  │       ├── savedTrackId → select
  │       ├── preferredLanguage match → select
  │       ├── external sub found → load & select
  │       └── none → sid = -1
  │
  └──> 4. Clear session override only if new file ≠ previous file
```

### Auto-Save (Debounced)

```
User adjusts slider / changes track
        │
        ▼
SubtitleManager marks setting as dirty
        │
        ▼
2-second debounce timer starts (resets on each change)
        │
        ▼
Timer fires → serialize current state to per-file JSON
```

---

## Complete Lifecycle Walkthrough

### App First Launch
```
App starts
  │
  ├──> Check if defaults.json exists
  │     ├── YES → load and apply to mpv
  │     └── NO  → create defaults.json with built-in defaults, apply
  │
  └──> SubtitleManager ready, waiting for media
```

### File Opens
```
OnMediaOpened
  │
  ├──> 1. Compute media hash (MD5 of full path)
  ├──> 2. Load global defaults → apply to mpv (if not already loaded)
  ├──> 3. Load per-file override if exists
  │       └──> Apply non-null styleOverrides to mpv
  ├──> 4. Load external subtitles from nearby directories (if enabled)
  ├──> 5. Auto-select track (unless _sessionOverride from previous file)
  │       ├── savedTrackId → select it
  │       ├── preferredLanguage match → select
  │       ├── external sub found for language → load & select
  │       ├── forced/foreign sub found → auto-enable even if subs disabled
  │       └── none → sid = -1 (no subtitles)
  └──> 6. Clear _sessionOverride only if new file ≠ previous file
```

### During Playback — User Interaction
```
User changes track / adjusts style
  │
  ├──> Set _sessionOverride = true (prevents auto-detect override)
  ├──> Apply immediately to mpv via SetProperty()
  ├──> Update OSD feedback overlay
  ├──> Fire PropertyChanged → UI sliders update
  └──> Start/restart 2s debounce save timer
```

### File Closes / New File Opens
```
OnMediaClosing (before new file loads)
  │
  ├──> FORCE-SAVE pending dirty settings immediately (no debounce)
  │     └──> Flush _debounceTimer → serialize per-file JSON
  │
  ├──> If track was manually selected → keep _sessionOverride = true
  │     (so opening the same file restores the manual choice)
  │
  └──> Clear track list, reset OSD state
```

### App Closes
```
OnAppExit / OnDispose
  │
  ├──> FORCE-SAVE: flush any pending dirty settings
  ├──> Unsubscribe from all mpv property observations
  ├──> Clear track list
  └──> Dispose timer resources
```

### App Reopens (restored session)
```
App starts → restore last played file
  │
  ├──> Load defaults.json
  ├──> Open last media file
  ├──> Load per-file overrides → apply
  ├──> Auto-select track (no session override — fresh session)
  └──> Subtitles appear exactly as user left them
```

---

## Edge Cases Covered

| Scenario | Behavior |
|---|---|
| **Per-file JSON corrupted** | Catch parse error → log warning, delete corrupted file, fall back to defaults |
| **defaults.json corrupted** | Catch parse error → log warning, regenerate with built-in defaults |
| **Media file moved/renamed** | Hash changes → old per-file overrides become orphaned (no match). User gets defaults. Old file can be manually deleted, or add orphan cleanup in settings |
| **External subtitle file deleted** | If selected track becomes invalid, mpv falls back to sid=-1. SubtitleManager detects `TrackListChanged` → removes from list. OSD shows warning |
| **Drag-drop new subtitle** | Immediately loads external file via mpv. Sets _sessionOverride. Does NOT change per-file saved settings until debounce fires |
| **Forced/foreign subtitles** | If a track is tagged as "forced" or "foreign-dialogue" → auto-enable even when subtitles are globally disabled. Plex/VLC standard behavior |
| **Both embedded + external for same language** | Prefer embedded (better metadata, no file path dependency). External only used if no embedded match |
| **mpv property not supported** | `SetProperty` fails gracefully → catch error, log, show OSD "Not available" |
| **Track list changes during playback** | User loads external sub → `TrackListChanged` fires → SubtitleManager rebuilds track list, preserves current selection if still valid |
| **Session override across app restarts** | Does NOT persist. Each fresh session starts with auto-detect. This prevents "stuck" settings if user forgets they manually selected something |
| **No subtitle tracks at all** | Flyout shows "No subtitles available". Style controls hidden. Icon shows disabled state |

From ViewRA ADR analysis — Blu-ray PGS subtitles are bitmap-based and can't be styled like text subtitles:

| Subtitle Type | Can Style? | Can Convert? | Notes |
|---|---|---|---|
| SRT | ✅ Yes | ✅ Yes | Text-based, fully styleable |
| ASS/SSA | ✅ Yes | ✅ Yes (with loss) | Advanced styling, position, animation |
| WebVTT | ✅ Yes | ✅ Yes | Web standard |
| PGS/HDMV | ❌ No | ❌ No (burn-in only) | Bitmap, from Blu-ray |
| VOBSUB | ❌ No | ❌ No (burn-in only) | Bitmap, from DVD |

**Design response:**
- `SubtitleManager` exposes `HasTextSubtitles` property — UI shows/hides style controls accordingly
- If only PGS subtitles available, style sliders are disabled with tooltip: "Not available for bitmap subtitles"

---

## Styling Architecture

### mpv Properties vs. UI Controls

| mpv Property | UI Control | Range | Step |
|---|---|---|---|
| `sub-scale` | Slider "Size" | 0.5 – 3.0 | 0.1 |
| `sub-pos` | Slider "Position" | 0 – 100 | 1 |
| `sub-delay` | Slider "Sync" | -10 – 10 | 0.1 |
| `sub-font` | ComboBox | System fonts | — |
| `sub-font-size` | Slider "Font Size (px)" | 16 – 96 | 1 |
| `sub-border-size` | Slider "Border" | 0 – 5 | 0.5 |
| `sub-color` | ColorPicker | — | — |
| `sub-shadow-offset` | Slider "Shadow" | 0 – 5 | 0.5 |

### OSD Feedback

Every keyboard or slider change shows a brief overlay (2s auto-dismiss):

```
┌──────────────────────────────┐
│  Font Size: 1.2×            │
│  ████████████░░░░░░░░░░░    │
└──────────────────────────────┘
```

```
┌──────────────────────────────┐
│  Subtitle Position: 92%     │
│  ██████████████████░░░░░░░░ │
└──────────────────────────────┘
```

```
┌──────────────────────────────┐
│  Subtitles: Off  (press V)  │
└──────────────────────────────┘
```

---

## Keyboard Shortcuts (mpv Industry Standard)

Based on mpv player's widely adopted keybindings:

| Shortcut | Action | Status |
|---|---|---|
| `V` | Toggle subtitle visibility | Phase 3 |
| `J` / `Shift+J` | Cycle subtitle tracks forward/back | Phase 3 |
| `Z` / `Shift+Z` | Adjust delay -/+ 0.1s | Phase 3 |
| `G` / `F` | Decrease/increase font size by 10% | Phase 3 |
| `R` / `Shift+R` | Move subtitles up/down (sub-pos) | Phase 3 |
| `Ctrl+Shift+Left` / `Right` | Snap subtitle to previous/next cue | Phase 3 |
| `Ctrl+0` | Reset all subtitle settings | Phase 3 |

VLC also uses `G`/`H` for delay — we support both `Z`/`G` (mpv) and `G`/`H` (VLC).

---

## Implementation Phases

### Phase 1 — Core Integration (current sprint)

| Step | What | File |
|---|---|---|
| 1 | Observe all subtitle properties in MpvPlayer: `sid`, `sub-visibility`, `sub-pos`, `sub-scale`, `sub-delay` | `MpvPlayer.cs` |
| 2 | Route property changes through `MpvPlayer` → `SubtitleManager` | `MpvPlayer.cs` |
| 3 | `SubtitleManager` exposes observable properties, single source of truth | `SubtitleManager.cs` |
| 4 | UI binds to `SubtitleManager` instead of `MainViewModel` | `ControlsBoxControl.axaml` |
| 5 | Register `SubtitleManager` to receive `TrackListChanged` events | `SubtitleManager.cs` |
| 6 | Register `SubtitleManager` to receive file-closing event → force-save pending settings | `SubtitleManager.cs` |
| 7 | Remove duplicate track list rebuild from `MainViewModel.Tracks.cs` | `MainViewModel.Tracks.cs` |

### Phase 2 — Persistence

| Step | What | File |
|---|---|---|
| 8 | `SubtitleSettingsStore` — JSON read/write with versioning, corruption recovery | `SubtitleSettingsStore.cs` |
| 9 | Debounced auto-save (2s timer) + session override flag | `SubtitleManager.cs` |
| 10 | Load-on-open: defaults → per-file → auto-select track | `SubtitleManager.cs` |
| 11 | Force-save on file close + force-save on app exit | `SubtitleManager.cs` |
| 12 | "Reset to Default" command clears per-file + session override | `MainViewModel.cs` |

### Phase 3 — Premium UX + Styling

| Step | What | File |
|---|---|---|
| 11 | Keyboard shortcuts (V, J, Z, G, F, R) | `MainWindow.Input.cs` |
| 12 | OSD progress bar feedback | `OsdOverlay.cs` / `SubtitleManager.cs` |
| 13 | **Style panel flyout** — font picker, size/position/delay sliders, color picker, border/shadow controls in a dedicated `SubtitleStyleFlyout.axaml` (separate file, clean XAML) | `SubtitleStyleFlyout.axaml` + `.cs` |
| 14 | Refactor `SubtitleOverlayControl` — its flyout is built in code-behind via `TrackFlyoutBuilder`; move to a proper `.axaml` flyout for maintainability | `SubtitleOverlayControl.axaml` |
| 15 | PGS detection → disable style controls + tooltip | `SubtitleManager.cs` |
| 16 | Auto-load external subtitles with language matching | `SubtitleManager.cs` |
| 17 | Preferred language config + external sub dirs in Preferences | `PreferencesDialog.axaml` |

---

## Key Design Rules

1. **Single source of truth** — All subtitle state lives in `SubtitleManager`. `MainViewModel` reads from it, never duplicates.
2. **Observed properties, not polling** — mpv pushes changes to us (via `HandlePropertyChange`). We never poll.
3. **Manual selection > auto-detect** — Once user picks a track, `_sessionOverride` flag prevents auto-detect from overriding. This is the #1 complaint in media players that users switch away from.
4. **Global defaults + per-file overrides** — Every setting has a default. Per-file overrides are optional and merge on top. Session overrides sit above both.
5. **Debounced persistence** — Settings save 2 seconds after the last change. Prevents disk thrashing during slider drag.
6. **Immediate feedback** — Every change applies to mpv instantly. No "Apply" or "OK" button.
7. **PGS-aware** — Bitmap subtitles can't be styled. UI adapts accordingly.
8. **External sub auto-load** — Follows industry naming conventions (`Movie.en.srt`, `Movie.en.forced.srt`).

---

## References

- [mpv manual — subtitle properties](https://mpv.io/manual/stable/#options-sub-visibility)
- [mpv manual — keyboard shortcuts](https://mpv.io/manual/stable/#keyboard-control) (`v`, `j`, `z`, `G`, `F`, `r`, `R`)
- [ViewRA ADR 030 — Multi-Language Audio & Subtitle Support](https://github.com/mantonx/viewra/blob/main/docs/decisions/030-multi-language-audio-subtitles.md)
- [Moonfin-Core PR #509 — Respect manual subtitle/audio selections](https://github.com/Moonfin-Client/Moonfin-Core/pull/509)
- [GOM Player — Subtitle naming conventions & auto-discovery](https://gom-player.app/guides.html)
- [Android ExoPlayer — Track selection with constraints & overrides](https://developer.android.com/media/media3/exoplayer/track-selection)
