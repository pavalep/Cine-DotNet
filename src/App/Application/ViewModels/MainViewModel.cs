using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Cine.Media.Events;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the main player window. Wraps IMediaPlayer for MVVM binding.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private static string GetLogPath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cine_startup.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "cine_startup.log");
        }
    }

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(GetLogPath(), $"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] {msg}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private readonly IMediaPlayer _player;
    // --- Bindable state ---
    private PlaybackState _state = PlaybackState.Stopped;
    private string _positionText = string.Empty;
    private string _durationText = string.Empty;
    private double _volumeValue = 50;
    private double _speedValue;
    private double _seekValue;
    private bool _isSeeking;
    private bool _isMuted;
    private string _filePath = string.Empty;
    private string _chapterTitle = string.Empty;
    private bool _isShuffleEnabled;
    private bool _isLoopFileEnabled;
    private bool _isLoopPlaylistEnabled;
    private bool _isSubtitleEnabled = true;
    private bool _isAudioEnabled = true;
    private bool _hasMultiplePlaylistItems;
    private bool _hasMultipleVideoTracks;

    // --- Typed track collections (replaces plain string lists) ---
    public ObservableCollection<TrackMenuItem> SubtitleTracks { get; } = new();
    public ObservableCollection<TrackMenuItem> AudioTracks { get; } = new();
    public ObservableCollection<TrackMenuItem> VideoTracks { get; } = new();

    // --- Other collections ---
    public ObservableCollection<ChapterInfo> Chapters { get; } = new();
    public ObservableCollection<string> Playlist { get; } = new();
    public ObservableCollection<PlaylistItemViewModel> PlaylistItems { get; } = new();
    public ObservableCollection<double> ChapterMarkers { get; } = new();

    // --- Commands ---
    public ICommand OpenFilesCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand AddSubtitleCommand { get; }
    public ICommand AddAudioCommand { get; }

    // File dialog callbacks (set by MainWindow code-behind)
    public Func<Task<string[]?>>? RequestOpenFilesAsync { get; set; }
    public Func<Task<string?>>? RequestOpenFolderAsync { get; set; }
    public Func<Task<string[]?>>? RequestAddFilesAsync { get; set; }
    public Func<Task<string?>>? RequestSubtitleFileAsync { get; set; }
    public Func<Task<string?>>? RequestAudioFileAsync { get; set; }

    public string Title => !string.IsNullOrEmpty(_filePath)
        ? TruncateFilename(Path.GetFileName(_filePath))
        : "Cine";

    private static string TruncateFilename(string name, int maxLen = 48)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLen)
            return name;
        var ext = Path.GetExtension(name);
        var nameOnly = Path.GetFileNameWithoutExtension(name);
        var avail = maxLen - ext.Length - 3;
        return nameOnly[..Math.Max(0, avail)] + "..." + ext;
    }

    public MainViewModel(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _player.Volume = _volumeValue;

        // Wire player events
        _player.Opened += OnMediaOpened;
        _player.TrackListChanged += OnTrackListChanged;
        _player.PlaylistChanged += OnPlaylistChanged;
        _player.LoopChangedEvent += OnLoopChanged;
        _player.PositionChanged += OnPositionChanged;

        // Initialize commands
        OpenFilesCommand = new RelayCommand(async _ => await OnOpenFiles());
        OpenFolderCommand = new RelayCommand(async _ => await OnOpenFolder());
        AddFilesCommand = new RelayCommand(async _ => await OnAddFiles());
        AddSubtitleCommand = new RelayCommand(async _ => await OnAddSubtitle());
        AddAudioCommand = new RelayCommand(async _ => await OnAddAudio());

        // Build initial empty track menus with placeholder entries
        BuildEmptyTrackMenus();
    }

    /// <summary>Initializes track menus with "Add..." and "None" pseudo-entries.</summary>
    private void BuildEmptyTrackMenus()
    {
        SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));

        AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
        AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));

        VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
    }

    // ---- Track selection handlers ----

    private void OnSelectSubtitle(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Subtitle Track…")
        {
            _ = OnAddSubtitle();
            return;
        }

        if (item.DisplayName == "None")
        {
            // Just select a negative track index to turn off subtitles in mpv
            _player.SelectSubtitleTrack(-1);
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectSubtitleTrack(item.TrackIndex);
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

    private void OnSelectAudio(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Audio Track…")
        {
            _ = OnAddAudio();
            return;
        }

        if (item.DisplayName == "None")
        {
            // Fallback/No audio
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectAudioTrack(item.TrackIndex);
            foreach (var t in AudioTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

    private void OnSelectVideo(TrackMenuItem item)
    {
        if (item.TrackIndex >= 0)
        {
            _player.SelectAudioTrack(item.TrackIndex); // Uses existing track indexer under hood
            foreach (var t in VideoTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

    private async Task OnOpenFiles()
    {
        if (RequestOpenFilesAsync == null) return;
        var paths = await RequestOpenFilesAsync();
        if (paths != null && paths.Length > 0)
            OpenFiles(paths);
    }

    private async Task OnOpenFolder()
    {
        if (RequestOpenFolderAsync == null) return;
        var path = await RequestOpenFolderAsync();
        if (!string.IsNullOrEmpty(path))
            OpenFile(path);
    }

    private async Task OnAddFiles()
    {
        if (RequestAddFilesAsync == null) return;
        var paths = await RequestAddFilesAsync();
        if (paths != null)
            foreach (var p in paths)
                Playlist.Add(p);
    }

    private async Task OnAddSubtitle()
    {
        if (RequestSubtitleFileAsync == null) return;
        var path = await RequestSubtitleFileAsync();
        if (!string.IsNullOrWhiteSpace(path))
            _player.AddSubtitle(path);
    }

    private async Task OnAddAudio()
    {
        if (RequestAudioFileAsync == null) return;
        var path = await RequestAudioFileAsync();
        // TODO: wire audio track loading to player when supported
    }

    // --- Playback commands ---
    public void PlayPause()
    {
        if (_player.IsPlaying)
            _player.Pause();
        else
            _player.Play();
        State = _player.State;
    }

    public void Stop() => _player.Stop();
    public int PlaylistPosition
    {
        get => _player.PlaylistPosition;
        set
        {
            _player.PlaylistPosition = value;
            OnPropertyChanged();
            foreach (var item in PlaylistItems) item.NotifyPlayingChanged();
        }
    }

    public void PlayPlaylistItem(int index)
    {
        PlaylistPosition = index;
    }
    public void RemovePlaylistItem(int index)
    {
        if (index < 0 || index >= PlaylistItems.Count) return;
        PlaylistItems.RemoveAt(index);
        Playlist.RemoveAt(index);
        for (int i = index; i < PlaylistItems.Count; i++)
            PlaylistItems[i].NotifyPlayingChanged();
        HasMultiplePlaylistItems = PlaylistItems.Count > 1;
    }
    public void SeekForward() => _player.Seek(Position + TimeSpan.FromSeconds(5));
    public void SeekBackward() => _player.Seek(Position - TimeSpan.FromSeconds(5));
    public void SeekLargeForward() => _player.Seek(Position + TimeSpan.FromSeconds(60));
    public void SeekLargeBackward() => _player.Seek(Position - TimeSpan.FromSeconds(60));
    public void IncreaseVolume() => VolumeValue = Math.Min(150, VolumeValue + 10);
    public void DecreaseVolume() => VolumeValue = Math.Max(0, VolumeValue - 10);
    public void ToggleMute() => IsMuted = !_player.IsMuted;
    public void ToggleFullscreen() => _player.SetFullscreen(!_player.IsFullscreen);
    public void NextChapter() => _player.NextChapter();
    public void PreviousChapter() => _player.PreviousChapter();
    public void NextItem() => _player.NextPlaylistItem();
    public void PreviousItem() => _player.PreviousPlaylistItem();
    public void ToggleLoopFile()
    {
        _player.ToggleLoopFile();
        SyncLoopFlags();
    }
    public void ToggleLoopPlaylist()
    {
        _player.ToggleLoopPlaylist();
        SyncLoopFlags();
    }
    public void ToggleShuffle()
    {
        _player.IsShuffled = !_player.IsShuffled;
        IsShuffleEnabled = _player.IsShuffled;
        RefreshPlaylistState();
    }
    public void ResetSpeed() => SpeedValue = 1.0;
    public void Screenshot() => _player.TakeScreenshot(GetScreenshotPath());

    // --- Properties for binding ---
    public PlaybackState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
        }
    }

    public bool IsPlaying => _state == PlaybackState.Playing;
    public bool IsPaused => _state == PlaybackState.Paused;

    public double Volume
    {
        get => _volumeValue;
        set => VolumeValue = value;
    }

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Seek(value);
    }

    public string PositionText
    {
        get => _positionText;
        set { _positionText = value; OnPropertyChanged(); }
    }

    public string DurationText
    {
        get => _durationText;
        set { _durationText = value; OnPropertyChanged(); }
    }

    public TimeSpan Duration => _player.Duration;

    public double VolumeValue
    {
        get => _volumeValue;
        set
        {
            _volumeValue = value;
            _player.Volume = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Volume));
        }
    }

    public double SpeedValue
    {
        get => _speedValue;
        set { _speedValue = value; _player.Speed = value; OnPropertyChanged(); }
    }

    public double ContrastValue
    {
        get => _player.Contrast;
        set { _player.Contrast = value; OnPropertyChanged(); }
    }

    public double BrightnessValue
    {
        get => _player.Brightness;
        set { _player.Brightness = value; OnPropertyChanged(); }
    }

    public double GammaValue
    {
        get => _player.Gamma;
        set { _player.Gamma = value; OnPropertyChanged(); }
    }

    public double SaturationValue
    {
        get => _player.Saturation;
        set { _player.Saturation = value; OnPropertyChanged(); }
    }

    public double HueValue
    {
        get => _player.Hue;
        set { _player.Hue = value; OnPropertyChanged(); }
    }

    public float SubtitleDelayValue
    {
        get => _player.SubtitleDelay;
        set { _player.SubtitleDelay = value; OnPropertyChanged(); }
    }

    public float AudioDelayValue
    {
        get => _player.AudioDelay;
        set { _player.AudioDelay = value; OnPropertyChanged(); }
    }

    // --- Zoom ---
    public double ZoomValue
    {
        get => _player.Zoom;
        set { _player.Zoom = value; OnPropertyChanged(); }
    }

    // --- Aspect Ratio ---
    public double AspectRatioValue
    {
        get => _player.AspectRatio;
        set { _player.AspectRatio = value; OnPropertyChanged(); }
    }

    // --- Rotation & Flip ---
    public void ResetAspectRatio() => AspectRatioValue = -1;
    public void SetAspectRatio(double ratio) => AspectRatioValue = ratio;
    public void RotateLeft() => _player.Command("set", "video-rotate", "90");
    public void RotateRight() => _player.Command("set", "video-rotate", "270");
    public void ResetRotation() => _player.Command("set", "video-rotate", "0");
    public void FlipHorizontal() => _player.Command("vf", "toggle", "hflip");
    public void FlipVertical() => _player.Command("vf", "toggle", "vflip");
    public void ResetFlip() => _player.Command("vf", "del", "@hflip", "@vflip");
    public void ResetZoom() => ZoomValue = 0;

    // --- Reset Commands ---
    public void ResetContrast() => ContrastValue = 0;
    public void ResetBrightness() => BrightnessValue = 0;
    public void ResetGamma() => GammaValue = 1;
    public void ResetSaturation() => SaturationValue = 1;
    public void ResetHue() => HueValue = 0;
    public void ResetSubtitleDelay() => SubtitleDelayValue = 0;
    public void ResetAudioDelay() => AudioDelayValue = 0;
    public void ResetAllOptions()
    {
        ResetContrast();
        ResetBrightness();
        ResetGamma();
        ResetSaturation();
        ResetHue();
        ResetSubtitleDelay();
        ResetAudioDelay();
        ResetSpeed();
        ResetZoom();
        ResetAspectRatio();
        ResetRotation();
        ResetFlip();
    }

    private bool _isUpdatingPositionFromPlayer;
    public double SeekValue
    {
        get => _seekValue;
        set
        {
            if (Math.Abs(_seekValue - value) > 0.001)
            {
                _seekValue = value;
                OnPropertyChanged(nameof(SeekValue));
                if (!_isUpdatingPositionFromPlayer && Duration.TotalSeconds > 0)
                    _player.Seek(TimeSpan.FromSeconds(value * Duration.TotalSeconds));
            }
        }
    }

    public bool IsSeeking
    {
        get => _isSeeking;
        set
        {
            if (_isSeeking != value)
            {
                _isSeeking = value;
                OnPropertyChanged(nameof(IsSeeking));
            }
        }
    }

    public void SeekTo(double normalizedValue)
    {
        if (Duration.TotalSeconds <= 0) return;
        
        var target = TimeSpan.FromSeconds(normalizedValue * Duration.TotalSeconds);
        
        _isUpdatingPositionFromPlayer = true;
        try
        {
            _seekValue = Math.Clamp(normalizedValue, 0.0, 1.0);
            OnPropertyChanged(nameof(SeekValue));
            PositionText = FormatTime(target);
        }
        finally
        {
            _isUpdatingPositionFromPlayer = false;
        }
        
        _player.Seek(target);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            _player.Mute(value);
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); }
    }

    public string ChapterTitle
    {
        get => _chapterTitle;
        set { _chapterTitle = value; OnPropertyChanged(); }
    }

    public bool IsShuffleEnabled
    {
        get => _isShuffleEnabled;
        set { _isShuffleEnabled = value; OnPropertyChanged(); }
    }

    public bool IsLoopFileEnabled
    {
        get => _isLoopFileEnabled;
        set { _isLoopFileEnabled = value; OnPropertyChanged(); }
    }

    public bool IsLoopPlaylistEnabled
    {
        get => _isLoopPlaylistEnabled;
        set { _isLoopPlaylistEnabled = value; OnPropertyChanged(); }
    }

    public bool IsSubtitleEnabled
    {
        get => _isSubtitleEnabled;
        set { _isSubtitleEnabled = value; OnPropertyChanged(); }
    }

    public bool IsAudioEnabled
    {
        get => _isAudioEnabled;
        set { _isAudioEnabled = value; OnPropertyChanged(); }
    }

    public bool HasMultiplePlaylistItems
    {
        get => _hasMultiplePlaylistItems;
        set { _hasMultiplePlaylistItems = value; OnPropertyChanged(); }
    }

    public bool HasMultipleVideoTracks
    {
        get => _hasMultipleVideoTracks;
        set { _hasMultipleVideoTracks = value; OnPropertyChanged(); }
    }

    // --- Drag & drop support ---
    public async void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        FilePath = path;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await System.Threading.Tasks.Task.Delay(50);
        try
        {
            _player.Open(path);
        }
        catch
        {
            Log($"Open failed for '{path}'.");
            FilePath = string.Empty;
        }
        finally
        {
            RefreshState();
        }
    }

    public void OpenFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;
        foreach (var path in paths)
            Playlist.Add(path);
        OpenFile(paths[0]);
    }

    // --- Internal helpers ---
    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsSeeking) return;

            _isUpdatingPositionFromPlayer = true;
            try
            {
                State = _player.State;
                PositionText = FormatTime(e.Position);
                DurationText = FormatTime(_player.Duration);
                SeekValue = Duration.TotalSeconds > 0
                    ? e.Position.TotalSeconds / Duration.TotalSeconds
                    : 0;
            }
            finally
            {
                _isUpdatingPositionFromPlayer = false;
            }
        });
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            State = e.IsPaused ? PlaybackState.Paused : PlaybackState.Playing;
        });
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        RefreshState();
    }

    /// <summary>
    /// Rebuilds typed track menu items from player track list events.
    /// Matches Python's _update_track_menus() behavior: preserves "Add..." and "None"
    /// pseudo-entries at the top, followed by actual tracks with language/state info.
    /// </summary>
    private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // --- Subtitle tracks ---
            SubtitleTracks.Clear();
            SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
            SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));
            if (e.SubtitleTracks != null)
            {
                int idx = 0;
                foreach (var track in e.SubtitleTracks)
                {
                    var item = new TrackMenuItem(
                        FormatTrack("Sub", track),
                        TrackType.Subtitle,
                        idx,
                        OnSelectSubtitle,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    SubtitleTracks.Add(item);
                    idx++;
                }
            }
            IsSubtitleEnabled = e.SubtitleTracks?.Any(t => t.IsEnabled) ?? true;

            // --- Audio tracks ---
            AudioTracks.Clear();
            AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
            AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));
            if (e.AudioTracks != null)
            {
                int idx = 0;
                foreach (var track in e.AudioTracks)
                {
                    var item = new TrackMenuItem(
                        FormatTrack("Audio", track),
                        TrackType.Audio,
                        idx,
                        OnSelectAudio,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    AudioTracks.Add(item);
                    idx++;
                }
            }
            IsAudioEnabled = e.AudioTracks?.Any(t => t.IsEnabled) ?? true;

            // --- Video tracks ---
            VideoTracks.Clear();
            VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
            if (e.VideoTracks != null)
            {
                int idx = 0;
                foreach (var track in e.VideoTracks)
                {
                    var item = new TrackMenuItem(
                        FormatTrack("Video", track),
                        TrackType.Video,
                        idx,
                        OnSelectVideo,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    VideoTracks.Add(item);
                    idx++;
                }
            }
            HasMultipleVideoTracks = e.VideoTracks?.Count() > 1;
        });
    }

    private void OnPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Playlist.Clear();
            PlaylistItems.Clear();
            int idx = 0;
            foreach (var item in e.PlaylistItems)
            {
                Playlist.Add(item);
                PlaylistItems.Add(new PlaylistItemViewModel(this, idx, item));
                idx++;
            }
            RefreshPlaylistState();
            HasMultiplePlaylistItems = Playlist.Count > 1;
            foreach (var item in PlaylistItems) item.NotifyPlayingChanged();
        });
    }

    private void OnLoopChanged(object? sender, LoopChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(SyncLoopFlags);
    }

    internal void RefreshState()
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(VolumeValue));

        // Ensure time labels show immediately on media open
        PositionText = FormatTime(_player.Position);
        DurationText = FormatTime(_player.Duration);

        Chapters.Clear();
        ChapterMarkers.Clear();
        foreach (var ch in _player.ChapterList)
        {
            Chapters.Add(ch);
            if (Duration.TotalSeconds > 0)
                ChapterMarkers.Add(ch.Time / Duration.TotalSeconds);
        }

        RefreshPlaylistState();
        SyncLoopFlags();
        IsShuffleEnabled = _player.IsShuffled;
    }

    private void RefreshPlaylistState()
    {
        Playlist.Clear();
        foreach (var item in _player.Playlist)
            Playlist.Add(item);
        HasMultiplePlaylistItems = Playlist.Count > 1;
    }

    private void SyncLoopFlags()
    {
        IsLoopFileEnabled = _player.LoopMode == LoopMode.File;
        IsLoopPlaylistEnabled = _player.LoopMode == LoopMode.Playlist;
    }

    /// <summary>Formats a subtitle/audio/video track for display in a menu flyout.</summary>
    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        var state = track.IsEnabled ? "on" : "off";
        return $"{prefix}: {lang} ({state})";
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero)
            return "-" + TimeSpan.FromTicks(-ts.Ticks).ToString("hh\\:mm\\:ss");
        return ts.ToString("hh\\:mm\\:ss");
    }

    private string GetScreenshotPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(dir, $"cine_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
