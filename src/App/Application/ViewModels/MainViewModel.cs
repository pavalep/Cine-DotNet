using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    private readonly IMediaPlayer _player;
    private readonly DispatcherTimer _positionTimer;

    // --- Bindable state ---
    private PlaybackState _state;
    private string _positionText = string.Empty;
    private string _durationText = string.Empty;
    private double _volumeValue;
    private double _speedValue;
    private double _seekValue;
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

    // --- Collections ---
    public ObservableCollection<string> SubtitleTracks { get; } = new();
    public ObservableCollection<string> AudioTracks { get; } = new();
    public ObservableCollection<string> VideoTracks { get; } = new();
    public ObservableCollection<ChapterInfo> Chapters { get; } = new();
    public ObservableCollection<string> Playlist { get; } = new();
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

    public string Title => Path.GetFileName(_filePath) ?? "Cine";

    public MainViewModel(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));

        // Wire player events
        _player.Opened += OnMediaOpened;
        _player.TrackListChanged += OnTrackListChanged;
        _player.PlaylistChanged += OnPlaylistChanged;
        _player.LoopChangedEvent += OnLoopChanged;

        // Position polling timer (matching Python's property_observer pattern)
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTick;

        // Initialize commands
        OpenFilesCommand = new RelayCommand(async _ => await OnOpenFiles());
        OpenFolderCommand = new RelayCommand(async _ => await OnOpenFolder());
        AddFilesCommand = new RelayCommand(async _ => await OnAddFiles());
        AddSubtitleCommand = new RelayCommand(async _ => await OnAddSubtitle());
        AddAudioCommand = new RelayCommand(async _ => await OnAddAudio());
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
        // TODO: wire subtitle loading to player
    }

    private async Task OnAddAudio()
    {
        if (RequestAudioFileAsync == null) return;
        var path = await RequestAudioFileAsync();
        // TODO: wire audio track loading to player
    }

    // --- Playback commands ---
    public void PlayPause()
    {
        if (_state == PlaybackState.Playing)
            _player.Pause();
        else
            _player.Play();
    }

    public void Stop() => _player.Stop();
    public void SeekForward() => _player.Seek(Position + TimeSpan.FromSeconds(5));
    public void SeekBackward() => _player.Seek(Position - TimeSpan.FromSeconds(5));
    public void SeekLargeForward() => _player.Seek(Position + TimeSpan.FromSeconds(60));
    public void SeekLargeBackward() => _player.Seek(Position - TimeSpan.FromSeconds(60));
    public void IncreaseVolume() => _player.Volume = Math.Min(150, VolumeValue + 10);
    public void DecreaseVolume() => _player.Volume = Math.Max(0, VolumeValue - 10);
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
        set { _volumeValue = value; OnPropertyChanged(); }
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
        set { _volumeValue = value; _player.Volume = value; OnPropertyChanged(); }
    }

    public double SpeedValue
    {
        get => _speedValue;
        set { _speedValue = value; _player.Speed = value; OnPropertyChanged(); }
    }

    public double SeekValue
    {
        get => _seekValue;
        set
        {
            if (Math.Abs(_seekValue - value) > 0.001)
            {
                _seekValue = value;
                if (Duration.TotalSeconds > 0)
                    _player.Seek(TimeSpan.FromSeconds(value * Duration.TotalSeconds));
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; _player.IsMuted = value; OnPropertyChanged(); }
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
        _player.Open(path);
        FilePath = path;
        await System.Threading.Tasks.Task.Delay(50);
        RefreshState();
    }

    public void OpenFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;
        foreach (var path in paths)
            Playlist.Add(path);
        OpenFile(paths[0]);
    }

    // --- Internal helpers ---
    private void OnPositionTick(object? sender, EventArgs e)
    {
        PositionText = FormatTime(_player.Position);
        DurationText = FormatTime(_player.Duration);
        SeekValue = Duration.TotalSeconds > 0
            ? _player.Position.TotalSeconds / Duration.TotalSeconds
            : 0;
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        RefreshState();
    }

    private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SubtitleTracks.Clear();
            SubtitleTracks.Add("Add Subtitle Track");
            SubtitleTracks.Add("None");
            foreach (var track in e.SubtitleTracks)
                SubtitleTracks.Add(FormatTrack("Sub", track));
            IsSubtitleEnabled = e.SubtitleTracks.Any(t => t.IsEnabled);

            AudioTracks.Clear();
            AudioTracks.Add("Add Audio Track");
            AudioTracks.Add("None");
            foreach (var track in e.AudioTracks)
                AudioTracks.Add(FormatTrack("Audio", track));
            IsAudioEnabled = e.AudioTracks.Any(t => t.IsEnabled);

            VideoTracks.Clear();
            foreach (var track in e.VideoTracks)
                VideoTracks.Add(FormatTrack("Video", track));
            HasMultipleVideoTracks = e.VideoTracks.Count() > 1;
        });
    }

    private void OnPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Playlist.Clear();
            foreach (var item in e.PlaylistItems)
                Playlist.Add(item);
            RefreshPlaylistState();
            HasMultiplePlaylistItems = Playlist.Count > 1;
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
