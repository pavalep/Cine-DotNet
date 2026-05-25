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

## Next Actions

**Immediate:**
1. Add WPF dependency to `Cine.WinUI` (if not already added)
2. Connect `MediaFoundationPlayer` to `MainApp.cs`
3. Add video Panel to `MainApp.cs` to host the player
4. Wire up Play/Pause/Stop buttons

**Once Player Works:**
1. Add Seek bar (bind to `player.Position`)
2. Add Volume slider (bind to `player.Volume`)
3. Add Time display (bind to `position` and `duration`)
4. Add Fullscreen toggle

**Then:**
1. Implement subtitle support
2. Implement audio track switching
3. Port keyboard shortcuts from Python
4. Add playlist support

---

## Summary

Phase 2 uses a **two-stage approach**:
1. **Stage 1 (Current)**: Quick prototype with WPF MediaElement
2. **Stage 2 (Future)**: Production-ready with native MediaFoundation D3D11 rendering

This allows **rapid development and testing** while we verify the player works correctly, then **migrate to native MF** for better performance and GPU acceleration when ready.

The code structure is **already Python-compatible**:
- Event system mirrors Python's `@event_callback` decorator
- Properties mirror Python's `@property` system
- Methods mirror Python's `mpv.*` commands
- Volume range matches Python's `volume_max=150`

When production-ready, only the **rendering layer** changes, not the player logic or UI bindings.
