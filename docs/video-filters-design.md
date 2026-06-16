# Video Filters — Premium Media Player Design

## Philosophy

A premium media player provides **fine-grained video control** that applies instantly. Users expect:

- **Real-time adjustment** — change contrast, see it immediately
- **Per-file memory** — settings persist per video file
- **Non-destructive** — original file is never modified
- **Industry-standard shortcuts** — matching VLC/mpv conventions
- **Video presets** — quick switching between Normal, Warm, Cool, Vivid, Cinema
- **Aspect ratio control** — maintain or override original aspect ratio
- **Rotation & flip** — for improperly oriented videos
- **Zoom** — digital zoom without quality loss

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                         VideoManager                                 │
│                                                                      │
│  ┌────────────────────┐  ┌──────────────────┐  ┌────────────────┐   │
│  │   Color Filters     │  │  Zoom & Aspect   │  │  Transform     │   │
│  │                    │  │                  │  │               │   │
│  │ - Contrast         │  │ - Zoom (digital) │  │ - RotateLeft   │   │
│  │ - Brightness       │  │ - Aspect Ratio   │  │ - RotateRight  │   │
│  │ - Gamma            │  │ - ResetZoom      │  │ - ResetRotation│   │
│  │ - Saturation       │  │ - ResetAspect    │  │ - FlipH        │   │
│  │ - Hue              │  │                  │  │ - FlipV        │   │
│  │                    │  │                  │  │ - ResetFlip    │   │
│  └────────────────────┘  └──────────────────┘  └────────────────┘   │
│                                                                      │
│  ┌────────────────────┐  ┌──────────────────────────────────────┐   │
│  │   Crop System       │  │   Video Presets                     │   │
│  │                    │  │                                      │   │
│  │ - CropLeft/Top     │  │ - Normal (all defaults)              │   │
│  │ - CropRight/Bottom │  │ - Warm (+warmth via hue/saturation)  │   │
│  │ - ApplyCrop()      │  │ - Cool (+cool via hue)               │   │
│  │ - ResetCrop()      │  │ - Vivid (high contrast + sat)        │   │
│  │ - UpdateCropFilter │  │ - Cinema (cinematic gamma)           │   │
│  └────────────────────┘  └──────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
         │
         ▼
   IMediaPlayer
   (Contrast, Brightness, Gamma, Saturation,
    Hue, Zoom, AspectRatio, Command())
```

## Current State

### What Exists ✅

| Feature | Status | File |
|---|---|---|
| `VideoManager` class with all properties | ✅ Done | `VideoManager.cs` |
| Contrast / Brightness / Gamma / Saturation / Hue | ✅ Done | `VideoManager.cs` |
| Zoom / Aspect Ratio | ✅ Done | `VideoManager.cs` |
| Rotation (90°/270°/0°) | ✅ Done | `VideoManager.cs` |
| Flip (horizontal / vertical) | ✅ Done | `VideoManager.cs` |
| Video Tracks (`RefreshVideoTracks`, `OnSelectVideo`) | ✅ Done | `VideoManager.cs` |
| `HasMultipleVideoTracks` | ✅ Done | `VideoManager.cs` |
| `ResetAllVideo()` | ✅ Done | `VideoManager.cs` |

### What's Missing ❌

| Feature | Priority | Reason |
|---|---|---|
| **Video presets** (Normal, Warm, Cool, Vivid, Cinema) | Medium | No preset system |
| **Crop system** — interactive crop with preview | Medium | Complex feature |
| **Per-file persistence** of filter settings | Medium | All reset on restart |
| **OSD feedback** for filter changes | Low | No visual feedback |
| **Keyboard shortcuts** for filters | Low | No keybindings for filters |
| **GPU shader presets** (deband, sharpen, denoise) | Low | Advanced mpv features |
| **Side-by-side comparison** ("before/after" split) | Low | Debugging tool |

---

## Observable Properties (single source of truth)

### Color Filters

| Property | Type | Range | Default | mpv Property | Description |
|---|---|---|---|---|---|
| `ContrastValue` | double | −1.0..1.0 | 0 | `contrast` | Image contrast |
| `BrightnessValue` | double | −1.0..1.0 | 0 | `brightness` | Image brightness |
| `GammaValue` | double | 0.1..2.0 | 1 | `gamma` | Gamma correction |
| `SaturationValue` | double | 0.0..2.0 | 1 | `saturation` | Color saturation |
| `HueValue` | double | −180..180 | 0 | `hue` | Hue rotation (degrees) |

### Zoom & Aspect

| Property | Type | Range | Default | Description |
|---|---|---|---|---|
| `ZoomValue` | double | −1.0..1.0 | 0 | Digital zoom (0 = 100%) |
| `AspectRatioValue` | double | −1..3.0 | −1 | −1 = auto, 1.33, 1.78, 1.85, 2.35, 2.39 |

### Transform

| Method | Description |
|---|---|
| `RotateLeft()` | Rotate 90° counter-clockwise |
| `RotateRight()` | Rotate 90° clockwise |
| `ResetRotation()` | Reset to 0° |
| `FlipHorizontal()` | Toggle horizontal flip |
| `FlipVertical()` | Toggle vertical flip |
| `ResetFlip()` | Remove all flip filters |

---

## Video Presets

A preset sets all 5 color filters at once:

| Preset | Contrast | Brightness | Gamma | Saturation | Hue |
|---|---|---|---|---|---|
| **Normal** | 0 | 0 | 1.0 | 1.0 | 0 |
| **Warm** | 0 | 0 | 0.95 | 1.05 | +5 |
| **Cool** | 0 | 0 | 1.05 | 0.95 | −5 |
| **Vivid** | +0.1 | 0 | 1.0 | +1.2 | 0 |
| **Cinema** | −0.05 | −0.02 | 0.90 | 0.90 | 0 |

Implementation:

```csharp
public void ApplyPreset(VideoPreset preset)
{
    ActivePreset = preset;
    var p = GetPresetValues(preset);
    ContrastValue = p.Contrast;
    BrightnessValue = p.Brightness;
    GammaValue = p.Gamma;
    SaturationValue = p.Saturation;
    HueValue = p.Hue;
    OnPropertyChanged(nameof(ActivePreset));
}
```

---

## Crop System

### Current State

`MainViewModel` has a `UpdateCropFilter()` method that applies a `lavfi=crop` filter:

```csharp
public void UpdateCropFilter()
{
    var cropFilter = $"crop={_cropRight - _cropLeft}:{_cropBottom - _cropTop}:{_cropLeft}:{_cropTop}";
    _player.Command("set", "vf", cropFilter);
}
```

### Proposed Crop Properties

| Property | Type | Range | Default | Description |
|---|---|---|---|---|
| `CropLeft` | int | 0..width/2 | 0 | Left crop offset (px) |
| `CropTop` | int | 0..height/2 | 0 | Top crop offset (px) |
| `CropRight` | int | width/2..width | width | Right crop bound (px) |
| `CropBottom` | int | height/2..height | height | Bottom crop bound (px) |
| `IsCropActive` | bool | — | false | True when crop is applied |

### Crop UI (future)

```
┌──────────────────────────────────────┐
│           Crop Controls              │
│                                      │
│  ┌────────────────────────────────┐  │
│  │          Video Preview         │  │
│  │   ┌── draggable overlay ──┐    │  │
│  │   │                       │    │  │
│  │   │                       │    │  │
│  │   └───────────────────────┘    │  │
│  └────────────────────────────────┘  │
│                                      │
│  Left: [===] Top: [===]             │
│  Right: [===] Bottom: [===]         │
│  [ Apply ] [ Reset ]                │
└──────────────────────────────────────┘
```

---

## Persistence Strategy

| Scope | File | Content |
|---|---|---|
| Global defaults | `defaults.json` | Default filter values |
| Per-file | `{hash}.json` | All filter values |

Same debounced pattern as SubtitleManager and AudioManager.

---

## Keyboard Shortcuts

| Key | Action | Status |
|---|---|---|
| `Ctrl+C` | Cycle contrast preset | ❌ Missing |
| `Ctrl+B` | Cycle brightness | ❌ Missing |
| `Ctrl+G` | Cycle gamma | ❌ Missing |
| `Ctrl+0` | Reset all video to defaults | ❌ Missing |
| `Ctrl+1` | Aspect ratio 4:3 | ❌ Missing |
| `Ctrl+2` | Aspect ratio 16:9 | ❌ Missing |
| `Ctrl+3` | Aspect ratio 21:9 | ❌ Missing |
| `Ctrl+4` | Auto aspect ratio | ❌ Missing |
| `[` / `]` | Zoom in/out | ❌ Missing |
| `Ctrl+[` / `Ctrl+]` | Rotate left/right | ❌ Missing |

---

## Implementation Phases

### Phase 1 — Core VideoManager ✅ (DONE)

| Step | What | File |
|---|---|---|
| 1 | Create `VideoManager` with all color filter properties | `VideoManager.cs` |
| 2 | Wire Zoom + Aspect Ratio | `VideoManager.cs` |
| 3 | Wire Rotation + Flip | `VideoManager.cs` |
| 4 | Add Video Tracks (selection, refresh) | `VideoManager.cs` |
| 5 | `ResetAllVideo()` | `VideoManager.cs` |

### Phase 2 — Video Presets (future sprint)

| Step | What | File |
|---|---|---|
| 6 | `VideoPreset` enum + preset value table | `VideoManager.cs` |
| 7 | `ApplyPreset(VideoPreset)` method | `VideoManager.cs` |
| 8 | Preset flyout / dropdown UI | `VideoFilterFlyout.axaml` |
| 9 | OSD feedback on preset change | `MainWindow.Core.cs` |

### Phase 3 — Crop System (future sprint)

| Step | What | File |
|---|---|---|
| 10 | Move crop properties to `VideoManager` | `VideoManager.cs` |
| 11 | `ApplyCrop()` / `ResetCrop()` methods | `VideoManager.cs` |
| 12 | Wire to mpv `lavfi=crop` filter | `VideoManager.cs` |
| 13 | Interactive crop overlay control | `CropOverlayControl.axaml` |
| 14 | Aspect-ratio-aware crop bounds | `VideoManager.cs` |

### Phase 4 — Persistence (future sprint)

| Step | What | File |
|---|---|---|
| 15 | `VideoSettingsStore` — per-file + global defaults | `VideoSettingsStore.cs` |
| 16 | Debounced auto-save (2s) | `VideoManager.cs` |
| 17 | Load-on-open: restore per-file filter values | `VideoManager.cs` |

### Phase 5 — Premium UI (future sprint)

| Step | What | File |
|---|---|---|
| 18 | `VideoFilterFlyout.axaml` — all filter sliders + presets + reset | `VideoFilterFlyout.axaml` + `.cs` |
| 19 | Keyboard shortcuts for filters | `MainWindow.Input.cs` |
| 20 | OSD feedback for filter changes | `MainWindow.Core.cs` |
| 21 | Side-by-side before/after comparison | `VideoFilterFlyout.axaml.cs` |

---

## Edge Cases

| Scenario | Behavior |
|---|---|
| **Hardware acceleration active** | GPU shaders may not support all filters. `ApplyPreset` warns via OSD. |
| **Resolution too small for crop** | Crop bounds clamped to valid range |
| **Aspect ratio extreme values** | Clamped to 0.5..3.0 ratio |
| **Hue rotation wrap** | −180..180 range enforced; values outside wrap back |
| **Multiple flip toggles** | Each flip is a toggle; state tracks current orientation |
| **No video tracks** | `HasMultipleVideoTracks` = false; track selector hidden |

---

## References

- [mpv manual — video filters](https://mpv.io/manual/stable/#video-filters)
- [mpv manual — video properties](https://mpv.io/manual/stable/#options-contrast)
- [FFmpeg crop filter documentation](https://ffmpeg.org/ffmpeg-filters.html#crop)
- [VLC video effects](https://www.videolan.org/vlc/features.php)
