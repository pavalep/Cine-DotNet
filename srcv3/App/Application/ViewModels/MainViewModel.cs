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
using Cine.Avalonia.State;
using Cine.Avalonia.Models;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Services;
using Cine.Avalonia.Utilities;
using Cine.Core;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Cine.Media.Events;
using System.Text.Json;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the main player window. Wraps IMediaPlayer for MVVM binding.
/// </summary>
public partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private readonly ISessionService _session;
    private readonly IPlaylistService _playlistCoordinator;
    private readonly IMediaFileService _mediaFile;
    private readonly IFileDialogService? _fileDialog;
    private bool _disposed;
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
    private bool _isAudioEnabled = true;
    private bool _isFullscreen;
    private bool _hasMultiplePlaylistItems;


    // Track persistence
    private int _currentAudioTrackId = -1;

    // Pending track restore values loaded from session data
    private int? _pendingAudioTrackId;

    // ── Typed track collections ──
    // Subtitle tracks are owned by SubtitleManager; we delegate for UI bindings.
    public ObservableCollection<TrackMenuItem> SubtitleTracks => Subtitles?.SubtitleTracks ?? _emptySubtitleTracks;
    private static readonly ObservableCollection<TrackMenuItem> _emptySubtitleTracks = new();
    public ObservableCollection<TrackMenuItem> AudioTracks => Audio?.AudioTracks ?? _emptyAudioTracks;
    private static readonly ObservableCollection<TrackMenuItem> _emptyAudioTracks = new();
    public ObservableCollection<TrackMenuItem> VideoTracks => Video?.VideoTracks ?? _emptyVideoTracks;
    private static readonly ObservableCollection<TrackMenuItem> _emptyVideoTracks = new();

    // --- Other collections ---
    public ObservableCollection<ChapterInfo> Chapters { get; } = new();
    public ObservableCollection<string> Playlist { get; } = new();
    public ObservableCollection<PlaylistItemViewModel> PlaylistItems { get; } = new();
    public ObservableCollection<double> ChapterMarkers { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();

    // --- Commands ---
    public ICommand OpenFilesCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand AddAudioCommand { get; }
    public ICommand OpenRecentCommand { get; }

    /// <summary>File dialog service for requesting file selections.</summary>
    public IFileDialogService? FileDialog => _fileDialog;

    /// <summary>Fired when an error occurs during async operations.</summary>
    public event EventHandler<string>? OnError;

    // ── Domain Managers ──
    public IAudioManager Audio { get; }
    public VideoManager Video { get; }
    public ISubtitleManager Subtitles { get; } = null!;

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

    public MainViewModel(IMediaPlayer player,
        ISessionService session,
        IPlaylistService playlistCoordinator,
        IAudioManager? audioManager = null,
        VideoManager? videoManager = null,
        ISubtitleManager? subtitleManager = null,
        IRendererService? rendererService = null,
        IMediaFileService? mediaFileService = null,
        IFileDialogService? fileDialogService = null)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _playlistCoordinator = playlistCoordinator ?? throw new ArgumentNullException(nameof(playlistCoordinator));
        _mediaFile = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
        _fileDialog = fileDialogService;
        Audio = audioManager ?? new AudioManager(player);
        Video = videoManager ?? new VideoManager(player);
        Subtitles = subtitleManager ?? new SubtitleManager(player);
        Renderer = rendererService ?? throw new ArgumentNullException(nameof(rendererService));

        // Wire file-dialog delegates for AudioManager and SubtitleManager
        if (_fileDialog != null)
        {
            Audio.RequestAudioFileAsync = () => _fileDialog.OpenAudioAsync();
            Subtitles!.RequestSubtitleFileAsync = () => _fileDialog.OpenSubtitleAsync();
        }

        // Wire player events
        _player.Opened += OnPlayerOpened;
        _player.TrackListChanged += OnTrackListChanged;
        _player.PlaylistChanged += OnPlaylistChanged;
        _player.LoopChangedEvent += OnLoopChanged;
        _player.PositionChanged += OnPositionChanged;
        _player.PlaybackStateChangedEvent += OnPlaybackStateChanged;
        _player.VolumeChanged += OnVolumeChanged;

        // Initialize commands
        OpenFilesCommand = new RelayCommand(async _ => await OnOpenFiles());
        OpenFolderCommand = new RelayCommand(async _ => await OnOpenFolder());
        AddFilesCommand = new RelayCommand(async _ => await OnAddFiles());
        AddAudioCommand = new RelayCommand(async _ => await OnAddAudio());
        OpenRecentCommand = new RelayCommand(path =>
        {
            if (path is string p) OpenRecentFile(p);
        });

        BuildEmptyTrackMenus();
        LoadRecentFiles();
        LoadPlaylist(); // Restore playlist from previous session
    }

    // ─────────────────────────────────────────────────────
    //  Observable Properties
    // ─────────────────────────────────────────────────────

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

    public double VolumeMax => _player.VolumeMax;
    public string VolumeText => $"{VolumeValue:F0}%";

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
        get => Audio?.Volume ?? _volumeValue;
        set
        {
            var clamped = Math.Clamp(value, 0, VolumeMax);
            if (Math.Abs(_volumeValue - clamped) < 0.001) return;
            _volumeValue = clamped;
            if (Audio != null) Audio.VolumeValue = clamped;
            else _player.Volume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumeText));
        }
    }

    public double SpeedValue
    {
        get => _speedValue;
        set { _speedValue = value; _player.Speed = value; OnPropertyChanged(); }
    }

    private bool _isDialogueBoostEnabled;
    /// <summary>Proxies to AudioManager. Keeps local field for PropertyChanged notification.</summary>
    public bool IsDialogueBoostEnabled
    {
        get => Audio?.IsDialogueBoostEnabled ?? _isDialogueBoostEnabled;
        set
        {
            if (_isDialogueBoostEnabled == value) return;
            _isDialogueBoostEnabled = value;
            if (Audio != null) Audio.IsDialogueBoostEnabled = value;
            OnPropertyChanged();
        }
    }

    public float AudioDelayValue
    {
        get => Audio?.AudioDelay ?? _player.AudioDelay;
        set
        {
            if (Audio != null) Audio.AudioDelay = value;
            else _player.AudioDelay = value;
            OnPropertyChanged();
        }
    }

    public void ResetAudioDelay() => AudioDelayValue = 0;
    public void ResetAllOptions()
    {
        ResetContrast();
        ResetBrightness();
        ResetGamma();
        ResetSaturation();
        ResetHue();
        ResetAudioDelay();
        ResetSpeed();
        ResetZoom();
        ResetAspectRatio();
        ResetCrop();
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

    public bool IsMuted
    {
        get => Audio?.IsMuted ?? _isMuted;
        set
        {
            if (_isMuted == value && Audio?.IsMuted == value) return;
            _isMuted = value;
            if (Audio != null) Audio.IsMuted = value;
            else _player.Mute(value);
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
        get => _playlistCoordinator.IsShuffleEnabled;
        set { _playlistCoordinator.IsShuffleEnabled = value; OnPropertyChanged(); }
    }

    public bool IsLoopFileEnabled
    {
        get => _playlistCoordinator.IsLoopFileEnabled;
        set { _playlistCoordinator.IsLoopFileEnabled = value; OnPropertyChanged(); }
    }

    public bool IsLoopPlaylistEnabled
    {
        get => _playlistCoordinator.IsLoopPlaylistEnabled;
        set { _playlistCoordinator.IsLoopPlaylistEnabled = value; OnPropertyChanged(); }
    }

    public bool IsFullscreen
    {
        get => _isFullscreen;
        set { _isFullscreen = value; OnPropertyChanged(); }
    }

    public bool IsSubtitleEnabled => Subtitles?.IsSubtitleEnabled ?? false;

    // Backwards-compatible subtitle delay property used by some tests/UI
    public float SubtitleDelayValue
    {
        get => Subtitles?.SubtitleDelay ?? 0f;
        set
        {
            if (Subtitles != null) Subtitles.SubtitleDelay = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Helper to select a subtitle track by id (legacy name).</summary>
    public void SelectTrackById(int id) => Subtitles?.SelectSubtitleTrackById(id);

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

    public bool HasPlaylistItems => PlaylistItems.Count > 0;
    public bool HasChapters => Chapters.Count > 0;


    // ─────────────────────────────────────────────────────
    //  INotifyPropertyChanged
    // ─────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ─────────────────────────────────────────────────────
    //  IDisposable
    // ─────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Force-save subtitle settings before disposing
        Subtitles?.OnFileClosing();
        Subtitles?.Dispose();

        // Force-save audio settings before disposing
        Audio.OnFileClosing();
        Audio.Dispose();

        // Force-save playlist before disposing
        SavePlaylist();

        _player.Opened -= OnPlayerOpened;
        _player.TrackListChanged -= OnTrackListChanged;
        _player.PlaylistChanged -= OnPlaylistChanged;
        _player.LoopChangedEvent -= OnLoopChanged;
        _player.PositionChanged -= OnPositionChanged;
        _player.PlaybackStateChangedEvent -= OnPlaybackStateChanged;
        _player.VolumeChanged -= OnVolumeChanged;

        if (_player is IDisposable disposable)
            disposable.Dispose();

        global::Cine.Core.Log.ForContext<MainViewModel>().Info("MainViewModel disposed");
    }
}
