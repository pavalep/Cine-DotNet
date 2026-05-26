# Phase 2: Media Playback Engine — Implementation Guide

## Overview
Port the Python mpv-based video playback system to **native Windows Media Foundation**

## Reference Analysis

### Python `window.py` Media Components (Lines 1186-1345)

#### Video Rendering (Lines 1186-1245)
```python
# Setup render context
self.mpv.rendering_set(63)  # OpenGL rendering
self.mpv.gl_init()
self.mpv.update()
# Connect render events to GLArea
```

**C# Equivalent (To Implement):**
```csharp
// Use Windows Media Foundation D3D11 Renderer
IMFVideoPresenter videoPresenter = MFHelper.CreateD3D11Renderer(panelHandle);
// Connect to IMFDXGIDeviceManager for GPU interop
```

#### Event Callbacks (Lines 1265-1345)
Python uses `@mpv.event_callback` decorator for automatic event handling:

```python
@mpv.event_callback("end-file")
def on_end_file(event):
    GLib.idle_add(self.spinner.set_visible, False)

@mpv.event_callback("file-loaded")
def on_files_loaded(event):
    GLib.idle_add(self.spinner.set_visible, True)

@mpv.property_observer("time-pos")
def on_time_change(_name, value):
    GLib.idle_add(_update_progress, float(value or 0))

@mpv.property_observer("duration")
def on_duration_change(_name, value):
    GLib.idle_add(_update_duration, float(value or 0))
```

**C# Equivalent (To Implement):**
```csharp
// Using event subscriptions
_mediaPlayer.EndFile += (s, e) => ShowSpinner(false);
_mediaPlayer.FileLoaded += (s, e) => ShowSpinner(true);
_mediaPlayer.PositionChanged += (s, e) => UpdateProgressBar(e.Position);
_mediaPlayer.DurationChanged += (s, e) => UpdateDurationLabel(e.Duration);
```

---

## Implementation Plan

### Step 1: Implement Basic Event System (Lines 1-150)

**File:** `Cine.Media\Interfaces\IMediaPlayer.cs`

**Current Interface:**
```csharp
namespace Cine.Media.Interfaces;

public interface IMediaPlayer : IDisposable
{
    void Open(string path, string mode);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(double volume);
    
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    // ... more properties
}
```

**Add Event Definitions:**
```csharp
// In IMediaPlayer.cs
public event EventHandler<MediaEventArgs> StartFile;
public event EventHandler<MediaEventArgs> FileLoaded;
public event EventHandler<MediaEventArgs> EndFile;
public event EventHandler<PositionChangedEventArgs> PositionChanged;
public event EventHandler<DurationChangedEventArgs> DurationChanged;
public event EventHandler<PlaybackStateEventArgs> PlaybackStateChanged;
// ...
```

**Implement Event Classes:**
```csharp
// File: Cine.Media\Events\MediaEventArgs.cs
public class MediaEventArgs : EventArgs
{
    public string FilePath { get; }
    public string ErrorMessage { get; }
    
    public MediaEventArgs(string filePath, string error = null)
    {
        FilePath = filePath;
        ErrorMessage = error;
    }
}

// File: Cine.Media\Events\PositionChangedEventArgs.cs
public class PositionChangedEventArgs : EventArgs
{
    public TimeSpan Position { get; }
    
    public PositionChangedEventArgs(TimeSpan position)
    {
        Position = position;
    }
}
```

### Step 2: Create Media Foundation Helper (Lines 151-350)

**File:** `Cine.Media\Implementations\MFHelper.cs`

**Simple COM Wrappers (No full interop yet):**
```csharp
namespace Cine.Media.Implementations;

internal static class MFHelper
{
    // Simple wrapper for creating source reader
    public static IntPtr CreateSourceReader(string path)
    {
        try
        {
            // Future: Use DirectShow/DirectX for compatibility
            // Initial implementation: use System.Windows.Media
            return IntPtr.Zero;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create source reader: {ex.Message}", ex);
        }
    }
    
    // Simple wrapper for video rendering
    public static IntPtr CreateVideoRenderer(IntPtr hwnd)
    {
        // Future: Use D3D11 rendering
        return IntPtr.Zero;
    }
}
```

**Note:** Start with **Windows Presentation Foundation (WPF)** for rendering, then migrate to Media Foundation D3D11 later:
- WPF provides easy `MediaElement` control
- Less complex than pure MF interop
- Can be replaced with native MF renderer later without changing `MediaFoundationPlayer.cs`

### Step 3: Implement Core Player Methods (Lines 351-550)

**File:** `Cine.Media\Implementations\MediaFoundationPlayer.cs`

**Complete Implementation:**
```csharp
namespace Cine.Media.Implementations;

public class MediaFoundationPlayer : IMediaPlayer
{
    private System.Windows.Media.MediaElement _mediaElement;
    
    public MediaFoundationPlayer()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        _mediaElement = new System.Windows.Media.MediaElement();
        
        // Connect events (equivalent to Python observer system)
        _mediaElement.MediaOpened += OnMediaOpened;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.PlayingStateChanged += OnPlayingStateChanged;
        _mediaElement.CurrentStateChanged += OnCurrentStateChanged;
        
        // Enable GPU acceleration
        _mediaElement.IsVideoEnabled = true;
        _mediaElement.BufferTime = TimeSpan.FromSeconds(0.1);
    }
    
    public void Open(string path, string mode = "replace")
    {
        if (string.IsNullOrEmpty(path))
            return;
            
        // Stop current playback
        Stop();
        
        // Fire event
        StartFile?.Invoke(this, new MediaEventArgs(path));
        
        try
        {
            // Load video
            _mediaElement.Source = new Uri(path);
            _mediaElement.LoadedBehavior = MediaState.Manual;
            _mediaElement.UnloadedBehavior = MediaState.Clear;
            
            // Seek to 0 before playing
            _mediaElement.Position = TimeSpan.Zero;
            
            // Play immediately if mode is "replace"
            if (mode == "replace" || mode == "append")
            {
                _mediaElement.Play();
                _currentState = PlaybackState.Playing;
            }
            
            // Trigger loaded event
            FileLoaded?.Invoke(this, new MediaEventArgs(path));
        }
        catch (Exception ex)
        {
            EndFile?.Invoke(this, new MediaEventArgs(path, ex.Message));
            throw;
        }
    }
    
    public void Play()
    {
        if (_mediaElement?.Source != null)
        {
            _mediaElement.Play();
            _currentState = PlaybackState.Playing;
            PlaybackResumed?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public void Pause()
    {
        if (_mediaElement?.Source != null)
        {
            _mediaElement.Pause();
            _currentState = PlaybackState.Paused;
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public void Stop()
    {
        if (_mediaElement != null)
        {
            _mediaElement.Stop();
            _position = TimeSpan.Zero;
            _duration = TimeSpan.Zero;
            _currentState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
            EndFile?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public void Seek(TimeSpan position)
    {
        if (_mediaElement?.Source != null)
        {
            _mediaElement.Position = position;
            Position = position;
        }
    }
    
    public void SetVolume(double volume)
    {
        Volume = volume;
    }
    
    public void SetSubtitle(string path)
    {
        // TODO: Add subtitle handling
        // Future: Use Windows.Media.SpeechRecognition for VTT
    }
    
    // Position tracking timer (equivalent to Python @property_observer)
    private System.Windows.Threading.DispatcherTimer _positionTimer;
    
    private void StartPositionTracking()
    {
        _positionTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += (s, e) =>
        {
            Position = _mediaElement?.Position ?? TimeSpan.Zero;
        };
        _positionTimer.Start();
    }
    
    private void StopPositionTracking()
    {
        _positionTimer?.Stop();
    }
    
    // Event handlers (equivalent to Python observers)
    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        _duration = _mediaElement.NaturalDuration.TimeSpan;
        DurationChanged?.Invoke(this, new DurationChangedEventArgs(_duration));
    }
    
    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        EndFile?.Invoke(this, new MediaEventArgs(_path, e.ErrorException.Message));
    }
    
    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        EndFile?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnPlayingStateChanged(object sender, PlayingStateEventArgs e)
    {
        if (e.PlayingState == PlayingState.Playing)
        {
            StartPositionTracking();
        }
        else
        {
            StopPositionTracking();
        }
    }
    
    private void OnCurrentStateChanged(object sender, EventArgs e)
    {
        if (_mediaElement?.CurrentState == MediaState.Stopped && _currentState != PlaybackState.Stopped)
        {
            Stop();
        }
    }
    
    // Properties (matching Python mpv properties)
    public PlaybackState State => _currentState;
    public TimeSpan Position 
    { 
        get => _position;
        private set 
        { 
            _position = value;
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(value));
        }
    }
    public TimeSpan Duration 
    { 
        get => _duration;
        private set 
        { 
            _duration = value;
            DurationChanged?.Invoke(this, new DurationChangedEventArgs(value));
        }
    }
    public double Volume 
    { 
        get => _volume * 100.0; // Return 0-150
        set 
        { 
            _volume = Math.Clamp(value / 100.0, 0.0, 1.5);
            _mediaElement?.Volume = (float)_volume;
        }
    }
}
```

### Step 4: Add Subtitle Support (Lines 551-700)

**File:** `Cine.Media\Implementations\MFHelper.cs`

```csharp
// Subtitle rendering support
public class SubtitleRenderer
{
    private List<string> _subtitlePaths;
    
    public void AddSubtitle(string path)
    {
        // Load VTT/SRT subtitle
        _subtitlePaths.Add(path);
    }
    
    public void SetSubtitleVisible(bool visible)
    {
        // Show/hide all subtitles
    }
    
    public void SetSubtitleDelay(float seconds)
    {
        // Sync subtitle timing
    }
    
    // Future: Implement subtitle rendering
    // - SRT (SubRip Text)
    // - VTT (WebVTT)
    // - SSA/ASS (Advanced SubStation Alpha)
}
```

### Step 5: Add Audio Track Support (Lines 701-800)

**File:** `Cine.Media\Implementations\MediaFoundationPlayer.cs`

```csharp
// Audio track switching
private int _currentAudioTrack = 0;

public void SelectAudioTrack(int trackIndex)
{
    _currentAudioTrack = trackIndex;
    // TODO: Change audio track if multiple exist
    // MediaElement doesn't directly support track selection
    // Future: Use MediaFoundation or FFmpeg.WindowsBinding library
}

// Audio delay adjustment
// Equivalent to Python's mpv.audio_delay property
private float _audioDelay = 0f;

public void SetAudioDelay(float seconds)
{
    _audioDelay += seconds;
    // TODO: Apply delay to audio stream
}
```

---

## Implementation Progress Tracking

### Completed:
- ✅ Event system defined (IMediaPlayer interface)
- ✅ EventArgs classes created (MediaEventArgs, etc.)
- ✅ MediaFoundationPlayer.cs stub ready

### In Progress (Phase 2 Step 1-3):
- 🔄 Implement WPF-based player (quick prototyping)
- 🔄 Connect event handlers to WPF MediaElement
- 🔄 Add position tracking timer

### Pending:
- ⏳ Add WPF UI integration
- ⏳ Implement subtitle support
- ⏳ Implement audio track support
- ⏳ Add MediaFoundation D3D11 rendering (production)

---

## Testing Strategy

### Manual Tests
1. **Load video file**: Verify `FileLoaded` event fires
2. **Play video**: Verify video displays in window
3. **Pause/Resume**: Verify play/pause toggle
4. **Seek**: Verify seek bar updates
5. **Volume**: Verify volume control (0-150%)
6. **End of video**: Verify `EndFile` event fires

### Comparison to Python
- ✅ Volume range matches: 0-150 (Python's `volume_max=150`)
- ✅ Event callbacks match Python's `@property_observer`
- ✅ Position tracking every 100ms (same as Python)
- ⏳ Subtitle support (same as Python `sub_add`, `sub_delay`)
- ⏳ Audio track switching (same as Python `mpv.aid`)

---

## Future Enhancements (Post-Phase 2)

1. **Native MediaFoundation D3D11** (replace WPF MediaElement)
   - Better GPU acceleration
   - Hardware decoding (DXVA2/D3D11)
   - Lower CPU usage

2. **FFmpeg Windows Binding**
   - More codec support
   - Better subtitle handling (VAASSA)
   - Audio track switching

3. **Advanced Features**
   - Hardware decoder selection
   - Video filters (contrast, brightness, etc.)
   - Frame-accurate seeking
   - HDR support

---

## Code References

| Python Feature | C# Equivalent | Location |
|---------------|---------------|----------|
| `mpv.MPV()` | `MediaFoundationPlayer` | `MediaFoundationPlayer.cs` |
| `mpv.loadfile(path)` | `player.Open(path)` | `MediaFoundationPlayer.Open()` |
| `mpv.pause = True` | `player.Pause()` | `MediaFoundationPlayer.Pause()` |
| `mpv.stop()` | `player.Stop()` | `MediaFoundationPlayer.Stop()` |
| `mpv.time_pos` | `player.Position` | `MediaFoundationPlayer.Position` |
| `mpv.duration` | `player.Duration` | `MediaFoundationPlayer.Duration` |
| `mpv.volume` | `player.Volume` | `MediaFoundationPlayer.Volume` |
| `@mpv.event_callback("end-file")` | `player.EndFile += ...` | Event handlers |
| `@mpv.property_observer("time-pos")` | `player.PositionChanged += ...` | Position tracking |
| `mpv.sub_add("path")` | `player.AddSubtitle(path)` | Subtitle support |
| `mpv.sid` | `player.SelectSubtitleTrack()` | Subtitle selection |
| `mpv.aid` | `player.SelectAudioTrack()` | Audio track selection |
| `mpv.audio_delay` | `player.SetAudioDelay()` | Audio delay |
| `mpv.sub_delay` | `player.SetSubtitleDelay()` | Subtitle delay |
| `mpv.screenshot` | `player.TakeScreenshot()` | Screenshot capture |
| `mpv.fullscreen` | `player.SetFullscreen()` | Fullscreen toggle |

---

## Next Actions (Phase 2 — COMPLETE)

All Phase 2 items have been implemented:
- ✅ Full `MediaFoundationPlayer` with native MF, D3D11 + WASAPI — all events, properties, methods working
- ✅ Native rendering via `D3D11Renderer` — GPU hardware decoding, swap chain, present pipeline
- ✅ Video filters (brightness, contrast, gamma, saturation, hue) via GPU shader
- ✅ Chapter navigation (auto-generated at 60s intervals)
- ✅ Screenshot capture via staging texture + PNG save
- ✅ All keyboard shortcuts from Python ported
- ✅ Playback controls, seek bar, volume, speed, playlist, subtitles, audio tracks

## Phase 6: Avalonia UI Migration (NEW DIRECTION — IN PROGRESS)

**Why Avalonia:**
- Pixel-perfect rendering with snap-to-pixel layout
- Cross-platform: Windows + Linux (expand market)
- Hardware-accelerated Skia rendering backend
- Native HWND interop via `NativeControlHost` for D3D11 video rendering
- Fluent design language, modern XAML/C#, active open-source community (MIT)
- Intent to sell product — needs professional-grade UI

**Migration approach:**
1. Create `Cine.Avalonia.csproj` with `<Avalonia.Desktop>` target
2. Reuse existing `MediaFoundationPlayer`, `D3D11Renderer`, `IMediaPlayer` — zero changes to media layer
3. Wrap D3D11 HWND in Avalonia `NativeControlHost`
4. Rebuild all UI screens in XAML/C# with Fluent theme
5. Port all keyboard shortcuts to Avalonia `KeyGesture` + `CommandBinding`
6. Same layout/proportions as WinForms prototype (1088×612 base, resolution-aware scaling)

**Tasks:**
- [ ] Create `Cine.Avalonia` project: `Avalonia.Desktop` + `Microsoft.Extensions.DependencyInjection`
- [ ] Implement `NativeControlHost`-wrapped D3D11 video panel
- [ ] Rebuild MainWindow: video area, playlist sidebar (230px), transport bar, seek bar
- [ ] Custom title bar with acrylic blur, minimize/maximize/close buttons
- [ ] Toolbar with icon buttons (Play, Pause, Stop, Prev, Next, Mute, Fullscreen, Screenshot)
- [ ] StatusBar: elapsed/total time, volume, speed, current chapter
- [ ] Port keyboard shortcuts (Space, F, M, ←/→, ↑/↓, P, S, L, Ctrl+L, PgUp/PgDn, etc.)
- [ ] Playlist list with file icons + playing indicator + drag-and-drop
- [ ] Chapter list view with thumbnails (future)
- [ ] Settings/Preferences dialog from `preferences.py`
- [ ] Pixel-perfect: `UseLayoutRounding`, `SnapToDevicePixels`, resolution-aware scaling
- [ ] Build: 0 errors, 0 warnings

---

## Summary

Phase 2 uses a **three-stage approach**:
1. **Stage 1 (Completed)**: All media playback logic + prototype WinForms UI
2. **Stage 2 (Completed)**: Native MediaFoundation D3D11 rendering with GPU acceleration
3. **Stage 3 (In Progress)**: Avalonia UI migration for pixel-perfect, cross-platform, sellable product

The `MediaFoundationPlayer`/`D3D11Renderer`/`IMediaPlayer` architecture is **UI-agnostic** — only the presentation layer changes. All business logic, decoding, rendering, and event systems carry over unchanged.

The code structure is **already Python-compatible**:
- Event system mirrors Python's `@event_callback` decorator
- Properties mirror Python's `@property` system
- Methods mirror Python's `mpv.*` commands
- Volume range matches Python's `volume_max=150`
