# Preferences — Premium Media Player Design

## Philosophy

A premium media player's preferences dialog is **discoverable, organized, and persistent**. Unlike VLC's overwhelming tree of 200+ options, we follow the **macOS / modern Windows** approach:

- **Tabbed or sectioned** layout (General, Audio, Subtitles, Keyboard, Advanced)
- **Immediate apply** — no "Apply" or "OK" button
- **Searchable** — find any setting quickly
- **Minimal but extensible** — only show settings users actually change
- **Per-user profile** — settings stored in `%LOCALAPPDATA%\Cine\preferences.json`

---

## Current State

### What Exists ✅

| Feature | Status | File |
|---|---|---|
| `PreferencesDialog.axaml` | ✅ Done | `PreferencesDialog.axaml` |
| **General section**: Audio Normalization toggle | ✅ Done | `PreferencesDialog.axaml` |
| **Rendering section**: Hardware Acceleration toggle | ✅ Done | `PreferencesDialog.axaml` |
| **Keyboard Shortcuts section**: reference table (static) | ✅ Done | `PreferencesDialog.axaml` |
| **"View All Shortcuts"** button → KeyboardShortcutsDialog | ✅ Done | `PreferencesDialog.axaml.cs` |
| Esc to close | ✅ Done | `PreferencesDialog.axaml.cs` |

### What's Missing ❌

| Feature | Priority | Reason |
|---|---|---|
| **Subtitle section** — preferred language, external sub dirs, default style | High | No subtitle config |
| **Audio section** — default volume, normalization default, EQ preset default | Medium | No audio config |
| **Playback section** — auto-play, resume from last position | Medium | No playback config |
| **Save/load** preferences from JSON | High | All settings lost on restart |
| **Tabbed navigation** (General / Audio / Subtitles / Keyboard / Advanced) | Medium | Single scroll page |
| **Search preferences** | Low | Setting count is small |
| **Theme / Appearance** section | Low | No theme support yet |
| **Advanced section** — cache size, log level, mpv config path | Low | Power-user settings |

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                      PreferencesDialog.axaml                         │
│                                                                      │
│  ┌──────────┬──────────┬──────────────┬──────────┬──────────────┐   │
│  │  General  │  Audio   │  Subtitles   │ Keyboard │  Advanced    │   │
│  │          │          │              │          │              │   │
│  │ HW Accel │ Default  │ Pref. Lang   │ Ref.     │ Cache        │   │
│  │ Auto-play│ Volume   │ Sub Dirs     │ Table    │ Logging      │   │
│  │ Resume   │ EQ       │ Default      │ Full     │ mpv config   │   │
│  │          │ Preset   │ Style        │ Dialog   │              │   │
│  └──────────┴──────────┴──────────────┴──────────┴──────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
         │
         ▼
   PreferencesStore (JSON persistence)
```

---

## Observable Properties

These live on `MainViewModel` or dedicated `PreferencesViewModel`:

| Property | Section | Type | Default | Description |
|---|---|---|---|---|
| `IsHardwareAccelerationEnabled` | General | bool | true | GPU video decoding |
| `IsAudioNormalizationEnabled` | General | bool | false | DRC normalization (shared with AudioManager) |
| `PreferredSubtitleLanguages` | Subtitles | string[] | ["eng","jpn","und"] | Language priority |
| `AutoLoadExternalSubtitles` | Subtitles | bool | true | Auto-scan for .srt/.ass |
| `ExternalSubtitleDirectories` | Subtitles | string[] | ["./subs","./subtitles"] | Subtitle search paths |
| `DefaultSubtitleFontScale` | Subtitles | double | 1.0 | Default font size |
| `DefaultSubtitlePosition` | Subtitles | int | 100 | Default position |
| `DefaultVolume` | Audio | double | 50 | Startup volume |
| `DefaultEqualizerPreset` | Audio | string | "Flat" | Startup EQ |
| `AutoPlayOnOpen` | Playback | bool | true | Automatically play on file open |
| `ResumePlayback` | Playback | bool | true | Resume from last position |
| `CacheSizeMB` | Advanced | int | 512 | mpv cache size |

---

## Persistence Strategy

| File | Path | Content |
|---|---|---|
| `preferences.json` | `%LOCALAPPDATA%\Cine\preferences.json` | All user preferences |

### Schema

```json
{
  "version": 2,
  "general": {
    "hardwareAcceleration": true,
    "autoPlayOnOpen": true,
    "resumePlayback": true
  },
  "audio": {
    "defaultVolume": 50,
    "defaultEqualizerPreset": "Flat",
    "audioNormalization": false,
    "dialogueBoost": false
  },
  "subtitles": {
    "preferredLanguages": ["eng", "jpn", "und"],
    "autoLoadExternal": true,
    "externalSubDirectories": ["./subs", "./subtitles"],
    "defaultFontScale": 1.0,
    "defaultPosition": 100
  },
  "advanced": {
    "cacheSizeMB": 512,
    "logLevel": "warn",
    "mpvConfigPath": ""
  }
}
```

---

## Keyboard Navigation

| Key | Action |
|---|---|
| `Escape` | Close dialog |
| `Ctrl+Tab` | Next tab |
| `Ctrl+Shift+Tab` | Previous tab |
| `Ctrl+F` | Focus search (when implemented) |

---

## Implementation Phases

### Phase 1 — Current State (✅ DONE)

| Step | What | File |
|---|---|---|
| 1 | Basic dialog layout with scrollable sections | `PreferencesDialog.axaml` |
| 2 | General: Audio Normalization + Hardware Acceleration toggles | `PreferencesDialog.axaml` |
| 3 | Keyboard Shortcuts reference table | `PreferencesDialog.axaml` |
| 4 | "View All Shortcuts" → `KeyboardShortcutsDialog` | `PreferencesDialog.axaml.cs` |
| 5 | Esc to close | `PreferencesDialog.axaml.cs` |

### Phase 2 — Persistence (next sprint)

| Step | What | File |
|---|---|---|
| 6 | `PreferencesStore` — JSON read/write with corruption recovery | `PreferencesStore.cs` |
| 7 | Load preferences on app start → apply to managers | `MainWindow.Core.cs` |
| 8 | Save preferences on change (immediate, no debounce needed — user closes dialog) | `PreferencesDialog.axaml.cs` |
| 9 | Wire `IsHardwareAccelerationEnabled` → mpv `hwdec` property | `MainViewModel.cs` |

### Phase 3 — Tabbed Layout (future sprint)

| Step | What | File |
|---|---|---|
| 10 | TabControl with 5 tabs: General, Audio, Subtitles, Keyboard, Advanced | `PreferencesDialog.axaml` |
| 11 | **Subtitles tab**: preferred language (reorderable list), external sub dirs, default style sliders | `PreferencesDialog.axaml` |
| 12 | **Audio tab**: default volume slider, EQ preset dropdown, normalization default | `PreferencesDialog.axaml` |
| 13 | **Playback tab**: auto-play toggle, resume toggle | `PreferencesDialog.axaml` |
| 14 | **Advanced tab**: cache size, log level, mpv config path | `PreferencesDialog.axaml` |
| 15 | Wire subtitle preferences → `SubtitleSettingsStore.defaults.json` | `MainViewModel.cs` |
| 16 | Wire audio preferences → `AudioManager` defaults | `MainViewModel.cs` |

### Phase 4 — Search + Theme (future sprint)

| Step | What | File |
|---|---|---|
| 17 | Search bar that filters visible settings across all tabs | `PreferencesDialog.axaml` |
| 18 | Theme/Appearance tab (accent color, dark/light mode) | `PreferencesDialog.axaml` |
| 19 | Preferences import/export | `PreferencesDialog.axaml.cs` |

---

## Edge Cases

| Scenario | Behavior |
|---|---|
| **Preferences JSON corrupted** | Catch → log → regenerate with defaults |
| **Missing mpv hwdec support** | Hardware Acceleration toggle visible but tooltip explains it may not work on all systems |
| **Subtitle directory doesn't exist** | Silently skipped during auto-scan (no error shown) |
| **Invalid preferred language code** | Stored as-is; if no match found, falls back to first available track |
| **Cache size set too low** | Minimum 64MB enforced in setter |
| **Multiple preference changes** | All saved at once when dialog closes (not debounced per-change) |

---

## References

- [mpv manual — options](https://mpv.io/manual/stable/#options)
- [VLC Preferences reference](https://www.videolan.org/vlc/features.php)
- [IINA (macOS) — Preferences design](https://iina.io/)
