# Audio Manager — Premium Media Player Design

## Philosophy

Audio is a **first-class citizen** alongside subtitles. Users expect:

- Volume that "just works" with keyboard (↑/↓) and slider
- Mute toggle that persists visually
- **Equalizer** with presets (Classical, Rock, Pop, Jazz, Bass Boost)
- **Audio Normalization** (DRC) for consistent volume across content
- **Dialogue Boost** for clearer speech in movies
- **Audio Delay** sync correction (−10s to +10s)
- **Per-file audio track selection** that persists across sessions
- **Session override** — manual track choice beats auto-detect
- Instant feedback — every change applies to mpv instantly

---

## Architecture Overview

```
┌───────────────────────────────────────────────────────────────────┐
│                         AudioManager                              │
│                                                                   │
│  ┌──────────────────┐  ┌────────────────────┐  ┌───────────────┐ │
│  │   Volume / Mute   │  │  Equalizer / DRC  │  │  Audio Tracks │ │
│  │                  │  │                   │  │               │ │
│  │ - Volume (0-200) │  │ - 10 EQ bands     │  │ - Track list  │ │
│  │ - IsMuted        │  │ - 6 presets       │  │ - Selection   │ │
│  │ - VolumeMax      │  │ - Normalization   │  │ - Session     │ │
│  │ - VolumeText     │  │ - Dialogue Boost  │  │   override    │ │
│  └──────────────────┘  │ - Audio Delay     │  └───────────────┘ │
│                        └────────────────────┘                    │
└───────────────────────────────────────────────────────────────────┘
         │                          │
         ▼                          ▼
   IMediaPlayer              AudioSettingsStore
   (Volume, Mute,            (JSON persistence:
   AudioDelay,               per-file + global
   TrackListChanged,         defaults)
   VolumeChanged)
```

---

## IMediaPlayer Interface (consumed)

| Member | Type | Description |
|---|---|---|
| `Volume` | `double` (get/set) | Current volume 0–200 |
| `VolumeMax` | `double` (get) | Maximum volume (typically 200) |
| `IsMuted` | `bool` (get) | Current mute state |
| `Mute(bool)` | method | Set mute state |
| `AudioDelay` | `float` (get/set) | Audio delay in seconds |
| `SelectAudioTrack(int)` | method | Select audio track by ID |
| `AddAudio(string)` | method | Load external audio file |
| `AudioSources` | `IEnumerable<AudioTrackInfo>` | Available audio tracks |
| `Command(string, string, string)` | method | Set audio filters |
| `VolumeChanged` | event | Fires when volume/mute changes |
| `TrackListChanged` | event | Fires when track list changes |

---

## Property Change Flow

```
User adjusts slider / presses ↑/↓
        │
        ▼
AudioManager.VolumeValue = 55
        │
        ├──> _player.Volume = 55           (immediate mpv apply)
        ├──> Fire PropertyChanged(nameof(VolumeValue))
        ├──> Fire PropertyChanged(nameof(Volume))
        ├──> Fire PropertyChanged(nameof(VolumeText))
        ├──> Fire VolumeChanged event
        │       │
        │       ▼
        │   UI binds → slider updates, OSD shows "Volume: 55%"
        │
        └──> Start/restart 2s debounce save timer
```

```
Equalizer preset selected: "Rock"
        │
        ▼
AudioManager.ApplyEqualizerPreset("Rock")
        │
        ├──> Set 10 band gains
        ├──> ApplyEqualizer() → _player.Command("set_property", "af", "...")
        ├──> Fire PropertyChanged(nameof(EqualizerBands))
        ├──> Fire PropertyChanged(nameof(EqualizerPresetName))
        └──> MarkDirty() → debounced save
```

---

## Observable Properties (single source of truth)

### Volume / Mute

| Property | Type | Range | Default | Description |
|---|---|---|---|---|
| `VolumeValue` | `double` | 0–200 | 50 | Current volume |
| `Volume` | `double` | 0–200 | 50 | Alias for binding compatibility |
| `VolumeMax` | `double` | — | 200 | Max volume (from player) |
| `VolumeText` | `string` | — | "50%" | Formatted for display |
| `IsMuted` | `bool` | true/false | false | Mute state |

### Equalizer

| Property | Type | Range | Default | Description |
|---|---|---|---|---|
| `EqualizerBands` | `double[10]` | −20..+20 | all 0 | 10-band EQ (31–16000 Hz) |
| `EqualizerPresetName` | `string` | — | "Flat" | Active preset name |
| `IsAudioNormalizationEnabled` | `bool` | true/false | false | DRC normalization |
| `IsDialogueBoostEnabled` | `bool` | true/false | false | Dialogue compression |

### Audio Delay

| Property | Type | Range | Default | Description |
|---|---|---|---|---|
| `AudioDelay` | `float` | −10..+10 | 0 | Audio sync delay in seconds |
| `ResetAudioDelay()` | method | — | — | Reset delay to 0 |

### Audio Tracks

| Property | Type | Description |
|---|---|---|
| `AudioTracks` | `ObservableCollection<TrackMenuItem>` | Track menu items |
| `IsAudioEnabled` | `bool` | True if any audio track is selected |

---

## Equalizer Presets

| Preset | 31Hz | 62Hz | 125Hz | 250Hz | 500Hz | 1kHz | 2kHz | 4kHz | 8kHz | 16kHz |
|---|---|---|---|---|---|---|---|---|---|---|
| **Flat** | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **Classical** | 0 | 0 | 0 | 0 | 0 | 0 | −4 | −4 | −4 | −6 |
| **Rock** | +4 | +3 | +2 | +1 | 0 | 0 | +1 | +2 | +3 | +4 |
| **Pop** | −1 | 0 | +2 | +3 | +4 | +3 | +2 | 0 | −1 | −1 |
| **Jazz** | +3 | +2 | +1 | +2 | +3 | +3 | +2 | +1 | +1 | +2 |
| **Bass Boost** | +6 | +5 | +4 | +2 | 0 | 0 | 0 | 0 | 0 | 0 |

Implementation via mpv `lavfi` equalizer filter chain:

```
af=equalizer=f=31:t=q:width=1:g=4,equalizer=f=62:t=q:width=1:g=3,...
```

---

## Persistence Strategy

| Scope | File | Content |
|---|---|---|
| Global defaults | `defaults.json` | `Volume`, `IsMuted`, `EqualizerPresetName`, `IsAudioNormalizationEnabled` |
| Per-file | `{hash}.json` | `SelectedTrackId`, `AudioDelay` |

**Debounced save:** 2 seconds after last change (same as SubtitleManager).
**Force-save:** On file close and app exit.

---

## Setting Priority Hierarchy

```
Session override (user manual track pick)
    ↓
Per-file saved settings
    ↓
Global defaults (from defaults.json / preferences)
    ↓
Player default (50% volume, unmuted, flat EQ)
```

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `↑` | Volume +5% |
| `↓` | Volume −5% |
| `M` | Toggle mute |
| `0–9` | Set volume to 0–90% (10% increments) |
| `Ctrl+Shift+E` | Open equalizer |

---

## Reset Behavior

`AudioManager.ResetAllAudio()` restores:
- Volume → 50%
- Mute → false
- Audio Delay → 0s
- Equalizer → Flat (all bands 0)
- Dialogue Boost → off
- Audio Normalization → off

Per-file settings are NOT deleted (only in-memory state is reset).

---

## Implementation Phases

### Phase 1 — Core AudioManager ✅ (DONE)

| Step | What | File |
|---|---|---|
| 1 | Create `AudioManager` with Volume/Mute | `AudioManager.cs` |
| 2 | Wire `_player.VolumeChanged` for sync | `AudioManager.cs` |
| 3 | Add Equalizer with presets + `ApplyEqualizer()` | `AudioManager.cs` |
| 4 | Add Dialogue Boost + Audio Normalization | `AudioManager.cs` |
| 5 | Add Audio Delay | `AudioManager.cs` |
| 6 | Add Audio Tracks (track list, selection, session restore) | `AudioManager.cs` |
| 7 | Wire `_player.TrackListChanged` for track refresh | `AudioManager.cs` |

### Phase 2 — Persistence (pending)

| Step | What | File |
|---|---|---|
| 8 | `AudioSettingsStore` — JSON per-file + global defaults | `AudioSettingsStore.cs` |
| 9 | Debounced auto-save (2s) + session override | `AudioManager.cs` |
| 10 | Load-on-open: volume, EQ preset, track selection | `AudioManager.cs` |
| 11 | Force-save on file close + app exit | `AudioManager.cs` |
| 12 | "Reset to Default" clears per-file + session override | `MainViewModel.cs` |

### Phase 3 — Premium UI (pending)

| Step | What | File |
|---|---|---|
| 13 | Equalizer flyout (`AudioEqualizerFlyout.axaml`) with sliders + presets | `AudioEqualizerFlyout.axaml` + `.cs` |
| 14 | OSD feedback: volume bar, mute icon, EQ preset name | `MainWindow.Core.cs` |
| 15 | Keyboard shortcuts (↑/↓/M, number keys) | `MainWindow.Input.cs` |
| 16 | Preferences: Normalization + Dialogue Boost toggles | `PreferencesDialog.axaml` |

---

## Edge Cases

| Scenario | Behavior |
|---|---|
| **Volume > 100%** | Clamp to `VolumeMax`. OSD shows 100%+ values. mpv supports up to 200%. |
| **Track list changes mid-playback** | AudioManager rebuilds track list, preserves current selection if still valid |
| **No audio tracks** | Flyout shows "No audio tracks available" |
| **Equalizer filter fails** | `ApplyEqualizer()` catches exception silently — no crash |
| **Audio delay extreme values** | Clamp to −10..+10 range in setter |
| **Mute + volume change** | Changing volume while muted does NOT unmute (mpv behavior). Only `IsMuted = false` unmutes. |
| **Per-file JSON corrupted** | Catch → log → delete → fall back to defaults |

---

## References

- [mpv manual — audio properties](https://mpv.io/manual/stable/#options-volume)
- [mpv manual — audio filters](https://mpv.io/manual/stable/#audio-filters)
- [Equalizer APO — Preset reference](https://sourceforge.net/p/equalizerapo/wiki/)
- [domain-managers-plan.md](./domain-managers-plan.md) — Original domain manager architecture
