# Cine Avalonia — UI Alignment Plan

**Reference:** Python GTK4 UI (`code_for_reference/`)  
**Target Design:** `.kombai/canvas/cine-alignment.canvas`  
**Spec:** `cine-alignment.canvas` nodes: *Cine – Playback State (Aligned)* · *Cine – Start Page / Idle State (Aligned)*

---

## Current State Assessment

### ✅ What Already Works

| Feature | File(s) |
|---------|---------|
| Video playback (D3D11 renderer) | `D3D11VideoHost.cs`, `MainWindow.axaml.cs` |
| Play / Pause (button + keyboard Space/K/P) | `MainViewModel.cs` |
| Seek bar (custom click-to-seek) | `MainWindow.axaml`, `MainWindow.axaml.cs` |
| Chapter markers on seek bar | `MainViewModel.cs` |
| Chapter preview popover on hover | `MainWindow.axaml.cs` |
| Volume slider flyout | `MainWindow.axaml` |
| Previous / Next navigation | `MainViewModel.cs` |
| Subtitle track menu (dynamic) | `MainViewModel.cs`, `MainWindow.axaml.cs` |
| Audio track menu (dynamic) | `MainViewModel.cs`, `MainWindow.axaml.cs` |
| Video track menu (conditional) | `MainViewModel.cs` |
| Shuffle toggle | `MainViewModel.cs` |
| Loop File toggle | `MainViewModel.cs` |
| Playlist dialog | `PlaylistDialog.axaml` |
| Fullscreen toggle | `MainViewModel.cs` |
| Drag and drop files | `MainWindow.axaml.cs` |
| Auto-hide controls (3 s timer) | `MainWindow.axaml.cs` |
| Start page (idle overlay) | `StartPage.axaml` |
| OSD notification badge | `MainWindow.axaml` |
| About dialog | `MainWindow.axaml.cs` |
| Keyboard shortcuts (30+ bindings) | `MainWindow.axaml.cs` |
| Contrast / Brightness / Gamma / Saturation / Hue via keyboard | `MainWindow.axaml.cs` |
| Responsive breakpoint at 495 px | `MainWindow.axaml.cs` |

---

### ❌ Gaps — Grouped by Priority Phase

#### Phase 1 — UI Layout Alignment *(target: this session)*

> Controls layout must match `cine-alignment.canvas` design exactly.

| # | Gap | Current State | Target State | File(s) |
|---|-----|--------------|-------------|---------|
| 1.1 | Controls row order | Seek bar is **above** buttons | Buttons **above** seek bar (matches Python WrapBox → seek Box) | `MainWindow.axaml` |
| 1.2 | Rewind / Forward buttons | Present (BtnRewind, BtnForward) | **Remove** — not in Python reference | `MainWindow.axaml`, `.cs` |
| 1.3 | Loop Playlist button | Handler `OnToggleLoopPlaylist` exists but **no button in AXAML** | Add `BtnLoopPlaylist` ToggleButton between Shuffle and LoopFile | `MainWindow.axaml`, `.cs` |
| 1.4 | Time labels placement | Elapsed/Total in **controls row** | Elapsed/Total in **seek row** (right of seek bar) | `MainWindow.axaml` |
| 1.5 | Expandable spacer | No spacer between audio/video group and shuffle group | Invisible `*`-width spacer column (matching Python `Separator hexpand`) | `MainWindow.axaml` |
| 1.6 | Open button hidden | `BtnOpenMenu` is `IsVisible="False" Width="0" Height="0"` | Pill button "Open ▾" with `MenuFlyout` (visible when playing) | `MainWindow.axaml` |
| 1.7 | Volume flyout layout | Vertical StackPanel (mute toggle above slider) | **Horizontal** Box (mute toggle + slider side by side) | `MainWindow.axaml` |
| 1.8 | Mute toggle icon | Shows text `"M"`, `IsChecked` unbound | Shows `VolumeMuteIcon` path, `IsChecked="{Binding IsMuted}"` | `MainWindow.axaml` |
| 1.9 | IsMuted setter broken | Setter does not call `_player.Mute()` | Add `_player.Mute(value)` to setter | `MainViewModel.cs` |
| 1.10 | Volume icon stale | Volume button icon never changes | Add `RefreshVolumeIcon()` called on volume/mute change | `MainWindow.axaml.cs` |

---

#### Phase 2 — Options Menu Alignment

| # | Gap | Current State | Target State | File(s) |
|---|-----|--------------|-------------|---------|
| 2.1 | Sliders instead of spinners | Options popover uses `Slider` controls | Replace all with `NumericUpDown` (matches Python SpinButton) | `OptionsMenuButton.axaml` |
| 2.2 | Zoom control missing | Row has label but no control | Add `NumericUpDown` bound to `ZoomValue` | `OptionsMenuButton.axaml`, `MainViewModel.cs` |
| 2.3 | Aspect Ratio missing | Not present | Add `ComboBox` with 9 ratio presets (`-1`, `16:9`, `4:3`, `1:1`, `16:10`, `2.21:1`, `2.35:1`, `2.39:1`, `5:4`) | `OptionsMenuButton.axaml`, `.cs`, `MainViewModel.cs` |
| 2.4 | Rotate missing | Not present | Add Rotate Left / Right buttons (via `_player.Command("set","video-rotate",...)`) | `OptionsMenuButton.axaml`, `.cs` |
| 2.5 | Flip missing | Not present | Add Flip Horizontal / Vertical buttons (via `_player.Command("vf","toggle",...)`) | `OptionsMenuButton.axaml`, `.cs` |
| 2.6 | NumericUpDown dark styling | N/A | Style `NumericUpDown` for dark popover theme | `App.axaml` |

---

#### Phase 3 — Dialogs (Preferences + Shortcuts)

| # | Gap | Current State | Target State | File(s) |
|---|-----|--------------|-------------|---------|
| 3.1 | Preferences dialog | `OnPreferencesClick` is empty placeholder | Full Adw.Dialog equivalent with: subtitle font/color/scale/languages, HW decoding, normalize volume, save position | New `Preferences.axaml` + `.cs` |
| 3.2 | Keyboard shortcuts dialog | `OnShortcutsClick` is empty placeholder | Dialog listing all keyboard bindings in groups | New `ShortcutsDialog.axaml` + `.cs` |
| 3.3 | New Window | `OnNewWindowClick` opens a bare `new MainWindow()` | Should pass player instance / settings | `MainWindow.axaml.cs` |

---

#### Phase 4 — IMediaPlayer Backend Extensions

| # | Gap | Current State | Target State | File(s) |
|---|-----|--------------|-------------|---------|
| 4.1 | VideoRotation missing | No property in `IMediaPlayer` | Add `int VideoRotation { get; set; }` and update all implementations | `IMediaPlayer.cs`, Media project |
| 4.2 | Flip H/V missing | No property in `IMediaPlayer` | Add `bool VideoFlipHorizontal/Vertical { get; set; }` | `IMediaPlayer.cs`, Media project |
| 4.3 | `OnAddAudio` not implemented | `TODO` comment, no actual wiring | Wire audio file loading to player | `MainViewModel.cs` |
| 4.4 | Play/pause icon via ViewModel | `OnPlaybackStateChanged` updates icon directly | Should use `PlaybackState` binding in AXAML | `MainWindow.axaml`, `.cs` |

---

#### Phase 5 — Polish & Parity

| # | Gap | Current State | Target State | File(s) |
|---|-----|--------------|-------------|---------|
| 5.1 | SubtitleIcon wrong paths | Uses hardcoded basic path string | Use `{StaticResource SubtitlesIcon}` / `SubtitlesOffIcon` from `Icons.axaml` | `MainWindow.axaml.cs` |
| 5.2 | AudioIcon wrong paths | Same issue | Use `{StaticResource AudioIcon}` / `AudioOffIcon` | `MainWindow.axaml.cs` |
| 5.3 | PlaylistDialog styling | Missing playing-item highlight, no drop indicator polish | Add `playing-item-playlist` style, refine drop indicator | `PlaylistDialog.axaml` |
| 5.4 | OSD notification no fade-out | Fade-in animation only | Add fade-out after 2 s delay | `App.axaml`, `MainWindow.axaml.cs` |
| 5.5 | PIP button no-op | `BtnPip` does nothing | Implement (requires separate HWND window + video surface sharing) | `MainWindow.axaml.cs` |
| 5.6 | Pause indicator not shown on play | Shown only on pause | Should flash briefly on both play and pause (matching Python) | `MainWindow.axaml.cs` |
| 5.7 | Title doesn't update | `TitleText.Text` only updated on file open | Should truncate filename, match Python `media-title` observer | `MainWindow.axaml.cs` |

---

## Controls Button Order (aligned)

```
Python reference (window.blp):            Avalonia target (after Phase 1):

[prev] [play/pause] [next]                [prev] [play/pause] [next]
[volume]                                  [volume]
[subtitles] [audio] [video?]              [subtitles] [audio] [video?]
 ─── spacer (hexpand) ───                  ─── * spacer ───────────────
[shuffle?] [loop_playlist?]               [shuffle?] [loop_playlist?]  ← NEW
[loop_file] [playlist]                    [loop_file] [playlist]
[options] [fullscreen]                    [options] [fullscreen]
                                          ↑ NO rewind/forward (removed)
```

## Seek Row Layout (aligned)

```
Python reference:
┌──────────────────── seek scale ──────────────────┐ elapsed │ sep │ total │

Avalonia target (after Phase 1):
┌──────────────────── SeekArea (Grid col *) ───────┐ Position │ │ Duration │ 
                                                      TimeLabel  │   TimeLabel
                                                       margin -7px  margin 20px end
```

---

## File Change Map

| File | Phase | Change Type |
|------|-------|-------------|
| `MainWindow.axaml` | 1 | Major restructure of ControlsBox + header Open button |
| `MainWindow.axaml.cs` | 1 | Remove Rewind/Forward, add LoopPlaylist, RefreshVolumeIcon |
| `MainViewModel.cs` | 1 | Fix IsMuted, add ZoomValue + AspectRatioValue |
| `OptionsMenuButton.axaml` | 2 | Full rewrite: NumericUpDown, ComboBox, rotate, flip |
| `OptionsMenuButton.axaml.cs` | 2 | Add aspect/rotate/flip/zoom handlers |
| `App.axaml` | 1+2 | open-header-btn style, NumericUpDown dark theme |
| `Preferences.axaml` (new) | 3 | Full preferences dialog |
| `ShortcutsDialog.axaml` (new) | 3 | Shortcuts listing |
| `IMediaPlayer.cs` | 4 | Add Rotation, Flip properties |
| `PlaylistDialog.axaml` | 5 | Playing item highlight, drop indicator |

---

## Design Token Reference

| Token | Value | Usage |
|-------|-------|-------|
| Window background | `#0C0C0E` | Main window |
| OSD foreground | `white` | All icons and labels |
| Header gradient | `rgba(0,0,0,0.14)→0.08→0` | Top overlay |
| Controls gradient | `rgba(0,0,0,0.20)→0.10→0` | Bottom overlay |
| Button hover | `rgba(255,255,255,0.17)` | All circular buttons |
| Button active | `rgba(255,255,255,0.25)` | Pressed state |
| Toggle checked bg | `white` | Loop/shuffle checked |
| Toggle checked fg | `black` | Icons when checked |
| Popover bg | `rgba(25,25,27,0.95)` = `#F219191B` | Flyout backgrounds |
| Popover border | `#202021` | Flyout border |
| Progress trough | `rgba(255,255,255,0.225)` | Seek/volume track |
| Progress fill + thumb | `white` | Seek/volume fill |
| Time separator | `#ddd` at `0.4` opacity | Between elapsed/total |
| Open btn bg | `rgba(255,255,255,0.12)` | Header Open button |
| Drop indicator border | `#FF4AA3FF` | Dashed drag overlay |
