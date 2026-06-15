# Domain Managers — Architecture & Implementation Plan

## Motivation

The `MainViewModel` has become a **god class** (~1400 lines) handling:
- Play/Pause/State (moved to `PlaybackStateManager`)
- Volume, Mute, Equalizer, Audio tracks, Dialogue Boost, Audio Normalization
- Video filters (Contrast, Brightness, Gamma, Saturation, Hue, Zoom, Aspect Ratio)
- Rotation & Flip (RotateLeft, FlipHorizontal, etc.)
- Subtitles (tracks, delay, position, font size)
- Session save/load
- Track menu building
- File operations

Separating into focused domain managers improves:
- **Testability** — each manager can be unit-tested with a mock player
- **Maintainability** — each file is <300 lines with a single responsibility
- **Discoverability** — all audio code is in `AudioManager`, not scattered
- **Binding clarity** — XAML can bind to `AudioManager.Volume` directly

## Architecture

```
IMediaPlayer
    │
    ├── PlaybackStateManager   (done)  → State, Position, Duration
    ├── AudioManager           (new)   → Volume, Mute, Equalizer, Audio tracks,
    │                                       Dialogue Boost, Audio Normalization,
    │                                       Audio Delay
    ├── VideoManager           (new)   → Contrast, Brightness, Gamma, Saturation,
    │                                       Hue, Zoom, Aspect Ratio, Rotation, Flip,
    │                                       Video Presets
    └── SubtitleManager        (new)   → Subtitle tracks, Delay, Position, Font Size
```

Each manager:
- Takes `IMediaPlayer` in constructor
- Implements `INotifyPropertyChanged`
- Exposes typed events for non-trivial changes
- Owns all `_player.Command()` calls for its domain
- Is `IDisposable` (unsubscribes from player events)

`MainViewModel` delegates to these managers instead of duplicating logic.

---

## Phase 1: AudioManager

**File:** `src/App/Application/Helpers/AudioManager.cs`

### Move from MainViewModel

| Property/Method | Source Line(s) | Target |
|----------------|---------------|--------|
| `_volumeValue`, `Volume`, `VolumeValue`, `VolumeMax`, `VolumeText` | 58, 719–761 | AudioManager |
| `_isMuted`, `IsMuted`, `ToggleMute()` | 63, 930–945 | AudioManager |
| `_equalizerBands`, `EqualizerBands` | 517–529 | AudioManager |
| `EqualizerFrequencies` | 518 | AudioManager |
| `EqualizerPresetName`, `ApplyEqualizerPreset()` | 528–548 | AudioManager |
| `SetEqualizerBand()` | 534–539 | AudioManager |
| `ApplyEqualizer()` | 551–571 | AudioManager |
| `_isAudioNormalizationEnabled`, `IsAudioNormalizationEnabled`, `ToggleAudioNormalization()` | 580–584, 574–577 | AudioManager |
| `_isDialogueBoostEnabled`, `IsDialogueBoostEnabled` | 769–788 | AudioManager |
| `AudioDelay`, `AudioDelayValue`, `ResetAudioDelay()` | 822–826, 878 | AudioManager |
| `AudioTracks` collection, `OnSelectAudio`, audio track selection | 87–88, 263–278 | AudioManager |
| `_currentAudioTrackId`, `_pendingAudioTrackId` | 79, 84 | AudioManager |

### Events

| Event | Description |
|-------|-------------|
| `VolumeChanged` | Fires when volume or mute changes |
| `EqualizerChanged` | Fires when equalizer bands change |
| `AudioTracksChanged` | Fires when audio track list changes |

### Steps

1. Create `AudioManager` class with all above properties and methods
2. Wire equalizer presets (`GetPreset()` → move from MainViewModel)
3. Wire `_player.VolumeChanged` to sync volume state
4. Wire `_player.TrackListChanged` to sync audio tracks
5. Expose `ResetAllAudio()` for master reset

---

## Phase 2: VideoManager

**File:** `src/App/Application/Helpers/VideoManager.cs`

### Move from MainViewModel

| Property/Method | Source Line(s) | Target |
|----------------|---------------|--------|
| `ContrastValue`, `ResetContrast()` | 788–792, 873 | VideoManager |
| `BrightnessValue`, `ResetBrightness()` | 794–798, 874 | VideoManager |
| `GammaValue`, `ResetGamma()` | 800–804, 875 | VideoManager |
| `SaturationValue`, `ResetSaturation()` | 806–810, 876 | VideoManager |
| `HueValue`, `ResetHue()` | 812–816, 877 | VideoManager |
| `ZoomValue`, `ResetZoom()` | 832–836, 882 | VideoManager |
| `AspectRatioValue`, `ResetAspectRatio()`, `SetAspectRatio()` | 854–859, 881 | VideoManager |
| `RotateLeft()`, `RotateRight()`, `ResetRotation()` | 861–863 | VideoManager |
| `FlipHorizontal()`, `FlipVertical()`, `ResetFlip()` | 864–866 | VideoManager |
| `VideoTracks` collection, `OnSelectVideo` | 89, 280–307 | VideoManager |
| `_hasMultipleVideoTracks` | 75 | VideoManager |

### Future: Video Presets

```csharp
// Future — not implementing yet
public enum VideoPreset { Normal, Warm, Cool, Vivid, Cinema }
public VideoPreset ActivePreset { get; set; }
public void ApplyPreset(VideoPreset preset); // Sets contrast/brightness/gamma/saturation/hue
```

### Steps

1. Create `VideoManager` class with all video filter properties
2. Each setter calls `_player.Property = value` + `OnPropertyChanged()`
3. Expose `ResetAllVideo()` for master reset
4. Wire rotation/flip methods

---

## Phase 3: SubtitleManager

**File:** `src/App/Application/Helpers/SubtitleManager.cs`

### Move from MainViewModel

| Property/Method | Source Line(s) | Target |
|----------------|---------------|--------|
| `SubtitleTracks` collection | 87 | SubtitleManager |
| `_currentSubtitleTrackId`, `_pendingSubtitleTrackId` | 78, 83 | SubtitleManager |
| `OnSelectSubtitle()` | 232–257 | SubtitleManager |
| `_isSubtitleEnabled`, `IsSubtitleEnabled` | 70, 1002 | SubtitleManager |
| `SubtitleDelayValue`, `ResetSubtitleDelay()` | 818–820, 879 | SubtitleManager |
| `SubtitlePosition`, `SubtitleFontSize` | 826–834 | SubtitleManager |
| `LoadExternalSubtitle()` | 375–386 | SubtitleManager |
| `AddSubtitleCommand`, `OnAddSubtitle()` | 148, 335–349 | SubtitleManager |
| `RequestSubtitleFileAsync` | 111 | SubtitleManager |

### Steps

1. Create `SubtitleManager` class
2. Wire subtitle track selection and persistence
3. Wire file dialog callback for "Add Subtitle Track…"

---

## Phase 4: Wire into MainWindow and MainViewModel

### MainWindow.Core.cs

```csharp
// After creating _stateManager:
_audioManager = new AudioManager(player);
_videoManager = new VideoManager(player);
_subtitleManager = new SubtitleManager(player);
```

### MainViewModel.cs

```csharp
// Constructor takes managers instead of raw player
public MainViewModel(
    IMediaPlayer player,
    AudioManager audioManager,
    VideoManager videoManager,
    SubtitleManager subtitleManager)
{
    Audio = audioManager;
    Video = videoManager;
    Subtitles = subtitleManager;
}

// Public properties for XAML binding
public AudioManager Audio { get; }
public VideoManager Video { get; }
public SubtitleManager Subtitles { get; }
```

### XAML bindings

```xml
<!-- Before: -->
<materialIcons:MaterialIcon Kind="{Binding IsMuted, Converter=...}" />
<Slider Value="{Binding VolumeValue}" />

<!-- After: -->
<materialIcons:MaterialIcon Kind="{Binding Audio.IsMuted, Converter=...}" />
<Slider Value="{Binding Audio.VolumeValue}" />
```

---

## Phase 5: Clean Up

### Remove from MainViewModel

After migration, remove these fields and methods:
- All volume/mute fields and properties ✓ (→ AudioManager)
- All equalizer fields and methods ✓ (→ AudioManager)
- All dialogue boost / normalization ✓ (→ AudioManager)
- All contrast/brightness/gamma/saturation/hue ✓ (→ VideoManager)
- All zoom/aspect/rotate/flip ✓ (→ VideoManager)
- All subtitle-related fields and methods ✓ (→ SubtitleManager)
- `OnVolumeChanged()` handler ✓ (→ AudioManager)
- `GetPreset()` ✓ (→ AudioManager)
- `ResetAllOptions()` ✓ (→ delegates to managers)

### Remove from MainWindow

- PropertyWatcher entries for volume, mute (→ AudioManager handles internally)
- Direct player event subscriptions that are now in managers

---

## Migration Strategy

Each phase is self-contained and independently testable:

1. **Create** AudioManager.cs (no breaking changes — add alongside existing)
2. **Create** VideoManager.cs (no breaking changes)
3. **Create** SubtitleManager.cs (no breaking changes)
4. **Wire** in MainWindow — pass to ViewModel
5. **Migrate** XAML bindings one control at a time
6. **Remove** old code from MainViewModel (after verifying no bindings reference it)
7. **Build** verify

## File Summary

| File | Lines (est.) | Domain |
|------|-------------|--------|
| `PlaybackStateManager.cs` | ~180 | Play/Pause/Position (done) |
| `AudioManager.cs` | ~250 | Volume, Equalizer, Audio tracks |
| `VideoManager.cs` | ~150 | Video filters, Zoom, Aspect, Flip |
| `SubtitleManager.cs` | ~200 | Subtitle tracks, Delay, Font |
