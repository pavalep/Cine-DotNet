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
using Cine.Avalonia.Managers;
using Cine.Avalonia.Models;
using Cine.Avalonia.Extensions;
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

    [Conditional("DEBUG")]
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
    private bool _isShuffleEnabled;
    private bool _isLoopFileEnabled;
    private bool _isLoopPlaylistEnabled;
    private bool _isAudioEnabled = true;
    private bool _isFullscreen;
    private bool _hasMultiplePlaylistItems;
    private bool _hasMultipleVideoTracks;

    // Track persistence
    private int _currentAudioTrackId = -1;

    // Pending track restore values loaded from session data
    private int? _pendingAudioTrackId;

    // Playlist persistence
    private readonly Managers.PlaylistSettingsStore _playlistStore = new();

    // ── Typed track collections ──
    // Subtitle tracks are owned by SubtitleManager; we delegate for UI bindings.
    public ObservableCollection<TrackMenuItem> SubtitleTracks => Subtitles?.SubtitleTracks ?? _emptySubtitleTracks;
    private static readonly ObservableCollection<TrackMenuItem> _emptySubtitleTracks = new();
    public ObservableCollection<TrackMenuItem> AudioTracks { get; } = new();
    public ObservableCollection<TrackMenuItem> VideoTracks { get; } = new();

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

    // File dialog callbacks (set by MainWindow code-behind)
    public Func<Task<string[]?>>? RequestOpenFilesAsync { get; set; }
    public Func<Task<string?>>? RequestOpenFolderAsync { get; set; }
    public Func<Task<string[]?>>? RequestAddFilesAsync { get; set; }
    public Func<Task<string?>>? RequestSubtitleFileAsync { get; set; }
    public Func<Task<string?>>? RequestAudioFileAsync { get; set; }

    /// <summary>Fired when an error occurs during async operations.</summary>
    public event EventHandler<string>? OnError;

    // ── Domain Managers ──
    public AudioManager Audio { get; }
    public VideoManager Video { get; }
    public SubtitleManager Subtitles { get; } = null!;

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
        AudioManager? audioManager = null,
        VideoManager? videoManager = null,
        SubtitleManager? subtitleManager = null)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        Audio = audioManager ?? new AudioManager(player);
        Video = videoManager ?? new VideoManager(player);
        Subtitles = subtitleManager ?? new SubtitleManager(player);

#pragma warning disable CS8603 // Nullable flow analysis — Audio/Subtitles are assigned in ctor
        Audio.RequestAudioFileAsync = () => RequestAudioFileAsync?.Invoke();
        Subtitles!.RequestSubtitleFileAsync = () => RequestSubtitleFileAsync?.Invoke();
#pragma warning restore CS8603

        _player.Volume = _volumeValue;

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
        get => _volumeValue;
        set
        {
            var clamped = Math.Clamp(value, 0, VolumeMax);
            if (Math.Abs(_volumeValue - clamped) < 0.001) return;
            _volumeValue = clamped;
            _player.Volume = clamped;
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
    public bool IsDialogueBoostEnabled
    {
        get => _isDialogueBoostEnabled;
        set
        {
            if (_isDialogueBoostEnabled == value) return;
            _isDialogueBoostEnabled = value;
            if (value)
                _player.Command("af", "set", "lavfi=[acompressor=threshold=-20dB:ratio=4:makeup=8dB]");
            else
                _player.Command("af", "del", "lavfi=[acompressor=threshold=-20dB:ratio=4:makeup=8dB]");
            OnPropertyChanged();
        }
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
        get => Subtitles?.SubtitleDelay ?? _player.SubtitleDelay;
        set
        {
            if (Subtitles != null) Subtitles.SubtitleDelay = value;
            else _player.SubtitleDelay = value;
            OnPropertyChanged();
        }
    }

    private double _subtitleFontSize = 24;
    public double SubtitleFontSize
    {
        get => _subtitleFontSize;
        set
        {
            _subtitleFontSize = value;
            _player.SetSubtitleFontSize(value);
            OnPropertyChanged();
        }
    }

    public float AudioDelayValue
    {
        get => _player.AudioDelay;
        set { _player.AudioDelay = value; OnPropertyChanged(); }
    }

    public double ZoomValue
    {
        get => _player.Zoom;
        set { _player.Zoom = value; OnPropertyChanged(); }
    }

    public double AspectRatioValue
    {
        get => _player.AspectRatio;
        set
        {
            _player.AspectRatio = value;
            OnPropertyChanged();
            UpdateCropFilter();
        }
    }

    // --- Rotation & Flip ---
    public void ResetAspectRatio() => AspectRatioValue = -1;
    public void SetAspectRatio(double ratio) => AspectRatioValue = ratio;

    // ── Crop (removes black bars, VLC-style) ──
    private const string CropFilterLabel = "@crop";
    private double _cropValue = -1;

    public double CropValue
    {
        get => _cropValue;
        set { _cropValue = value; OnPropertyChanged(); }
    }

    public void SetCrop(double aspectRatio)
    {
        CropValue = aspectRatio;
        UpdateCropFilter();
    }

    public void ResetCrop()
    {
        CropValue = -1;
        UpdateCropFilter();
    }

    public void UpdateCropFilter()
    {
        if (_cropValue <= 0)
        {
            _player.Command("vf", "remove", CropFilterLabel);
        }
        else
        {
            // First remove to prevent duplicates/errors
            _player.Command("vf", "remove", CropFilterLabel);

            double R = _cropValue;
            double A = AspectRatioValue; // -1 or positive override

            if (A > 0)
            {
                // Aspect ratio is overridden
                string filter;
                if (A > R)
                {
                    double ratio = R / A;
                    string ratioStr = ratio.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    filter = $"{CropFilterLabel}:crop=w=iw*{ratioStr}:h=ih";
                }
                else
                {
                    double ratio = A / R;
                    string ratioStr = ratio.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    filter = $"{CropFilterLabel}:crop=w=iw:h=ih*{ratioStr}";
                }
                _player.Command("vf", "add", filter);
            }
            else
            {
                // Aspect ratio is original
                string rStr = R.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                string filter = $"{CropFilterLabel}:crop=w=if(gt(iw/ih\\,{rStr})\\,ih*{rStr}\\,iw):h=if(gt(iw/ih\\,{rStr})\\,ih\\,iw/{rStr})";
                _player.Command("vf", "add", filter);
            }
        }
    }

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
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
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

    public bool IsFullscreen
    {
        get => _isFullscreen;
        set { _isFullscreen = value; OnPropertyChanged(); }
    }

    public bool IsSubtitleEnabled => Subtitles?.IsSubtitleEnabled ?? false;

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

    public bool HasMultipleVideoTracks
    {
        get => _hasMultipleVideoTracks;
        set { _hasMultipleVideoTracks = value; OnPropertyChanged(); }
    }

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
