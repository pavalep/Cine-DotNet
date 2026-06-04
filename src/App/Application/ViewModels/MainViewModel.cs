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
using Cine.Avalonia.Helpers;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Cine.Media.Events;
using System.Text.Json;

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
    public ObservableCollection<string> RecentFiles { get; } = new();

    // --- Commands ---
    public ICommand OpenFilesCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand AddSubtitleCommand { get; }
    public ICommand AddAudioCommand { get; }
    public ICommand OpenRecentCommand { get; }

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
        AddSubtitleCommand = new RelayCommand(async _ => await OnAddSubtitle());
        AddAudioCommand = new RelayCommand(async _ => await OnAddAudio());
        OpenRecentCommand = new RelayCommand(path =>
        {
            if (path is string p) OpenRecentFile(p);
        });

        // Build initial empty track menus with placeholder entries
        BuildEmptyTrackMenus();

        // Load recent files
        LoadRecentFiles();
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
        if (!string.IsNullOrWhiteSpace(path))
            _player.AddAudio(path);
    }

    // --- Playback commands ---
    public void PlayPause()
    {
        State = _player.State;

        if (IsPlaying)
        {
            _player.Pause();
            State = PlaybackState.Paused;
        }
        else
        {
            _player.Play();
            State = PlaybackState.Playing;
        }
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
    public void SeekForward()
    {
        _player.Seek(Position + TimeSpan.FromSeconds(5));
        NotifyPipSync?.Invoke();
    }

    public void SeekBackward()
    {
        _player.Seek(Position - TimeSpan.FromSeconds(5));
        NotifyPipSync?.Invoke();
    }

    public void SeekLargeForward()
    {
        _player.Seek(Position + TimeSpan.FromSeconds(60));
        NotifyPipSync?.Invoke();
    }

    public void SeekLargeBackward()
    {
        _player.Seek(Position - TimeSpan.FromSeconds(60));
        NotifyPipSync?.Invoke();
    }
    public void IncreaseVolume() => VolumeValue = Math.Min(VolumeMax, VolumeValue + 5);
    public void DecreaseVolume() => VolumeValue = Math.Max(0, VolumeValue - 5);
    public void ToggleMute() => IsMuted = !IsMuted;
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
    public void SetSpeed(double speed) => SpeedValue = speed;
    public void Screenshot() => _player.TakeScreenshot(GetScreenshotPath());

    // === PIP Decode Resolution (P10.2) ===

    private string _pipResolution = "Auto";
    public string PipResolution
    {
        get => _pipResolution;
        set { _pipResolution = value; OnPropertyChanged(); }
    }

    public static readonly string[] PipResolutionOptions = { "Auto", "480p", "720p", "1080p", "Source" };

    public void SetPipResolution(string resolution)
    {
        PipResolution = resolution;
        OnPropertyChanged(nameof(PipResolution));
    }

    // === Audio Equalizer (P9.1) ===

    public static readonly double[] EqualizerFrequencies = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    private double[] _equalizerBands = new double[10];
    public double[] EqualizerBands
    {
        get => _equalizerBands;
        set { _equalizerBands = value; OnPropertyChanged(); }
    }

    private string _equalizerPresetName = "Flat";
    public string EqualizerPresetName
    {
        get => _equalizerPresetName;
        set { _equalizerPresetName = value; OnPropertyChanged(); }
    }

    public void SetEqualizerBand(int bandIndex, double gain)
    {
        if (bandIndex < 0 || bandIndex >= 10) return;
        _equalizerBands[bandIndex] = Math.Clamp(gain, -20, 20);
        ApplyEqualizer();
    }

    public void ApplyEqualizerPreset(string presetName)
    {
        var preset = GetPreset(presetName);
        for (int i = 0; i < 10 && i < preset.Length; i++)
            _equalizerBands[i] = preset[i];
        EqualizerPresetName = presetName;
        OnPropertyChanged(nameof(EqualizerBands));
        ApplyEqualizer();
    }

    private void ApplyEqualizer()
    {
        try
        {
            var bands = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                if (Math.Abs(_equalizerBands[i]) > 0.5)
                    bands.Add($"equalizer=f={EqualizerFrequencies[i]}:t=q:width=1:g={_equalizerBands[i]:F1}");
            }

            if (bands.Count > 0)
                _player.Command("set_property", "af", string.Join(",", bands));
            else
                _player.Command("set_property", "af", "");
        }
        catch { /* player not ready */ }
    }

    // === Audio Normalization (P9.2) ===

    private bool _isAudioNormalizationEnabled;
    public bool IsAudioNormalizationEnabled
    {
        get => _isAudioNormalizationEnabled;
        set { _isAudioNormalizationEnabled = value; OnPropertyChanged(); }
    }

    public void ToggleAudioNormalization()
    {
        IsAudioNormalizationEnabled = !IsAudioNormalizationEnabled;
        try
        {
            if (_isAudioNormalizationEnabled)
                _player.Command("set_property", "af", "drc");
            else
                _player.Command("set_property", "af", "");
        }
        catch { /* player not ready */ }
    }

    private static double[] GetPreset(string name) => name switch
    {
        "Classical" => new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -4.0, -4.0, -4.0, -6.0 },
        "Rock" => new[] { 4.0, 3.0, 2.0, 1.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 },
        "Pop" => new[] { -1.0, 0.0, 2.0, 3.0, 4.0, 3.0, 2.0, 0.0, -1.0, -1.0 },
        "Jazz" => new[] { 3.0, 2.0, 1.0, 2.0, 3.0, 3.0, 2.0, 1.0, 1.0, 2.0 },
        "Bass Boost" => new[] { 6.0, 5.0, 4.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
        _ => new double[10] // Flat
    };

    // === Session resume ===
    public Action<string, TimeSpan>? SessionResumeRequested { get; set; }

    private static string SessionPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "session.json");

    public void SaveSession()
    {
        try
        {
            var dir = Path.GetDirectoryName(SessionPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var session = new
            {
                FilePath = _filePath,
                Position = _player.Position.Ticks,
                Playlist = Playlist.ToList(),
                // P5.2: Window bounds saved by MainWindow before close
            };
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(session));
        }
        catch { }
    }

    public void LoadSession()
    {
        try
        {
            if (!File.Exists(SessionPath)) return;
            var json = File.ReadAllText(SessionPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("FilePath", out var pathEl) && pathEl.GetString() is string path
                && File.Exists(path)
                && root.TryGetProperty("Position", out var posEl))
            {
                var pos = TimeSpan.FromTicks(posEl.GetInt64());
                SessionResumeRequested?.Invoke(path, pos);
            }
            if (root.TryGetProperty("Playlist", out var plEl))
            {
                foreach (var item in plEl.EnumerateArray())
                {
                    var p = item.GetString();
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        Playlist.Add(p);
                }
                if (Playlist.Count > 0)
                    OnPropertyChanged(nameof(HasMultiplePlaylistItems));
            }
        }
        catch { }
    }

    public void ClearSession()
    {
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); }
        catch { }
    }

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
            if (Math.Abs(_volumeValue - clamped) < 0.001)
                return;

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
        get => _player.SubtitleDelay;
        set { _player.SubtitleDelay = value; OnPropertyChanged(); }
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
        NotifyPipSync?.Invoke();
    }

    // Optional callback for PIP sync
    public Action? NotifyPipSync { get; set; }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value)
                return;

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

    public bool HasPlaylistItems => PlaylistItems.Count > 0;

    public bool HasChapters => Chapters.Count > 0;

    public bool HasMultipleVideoTracks
    {
        get => _hasMultipleVideoTracks;
        set { _hasMultipleVideoTracks = value; OnPropertyChanged(); }
    }

    // --- Recent files ---
    private static string RecentFilesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "recent.json");

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > 10)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        SaveRecentFiles();
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    private void SaveRecentFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(RecentFilesPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(RecentFilesPath, JsonSerializer.Serialize(RecentFiles.ToList()));
        }
        catch { }
    }

    public void LoadRecentFiles()
    {
        try
        {
            if (!File.Exists(RecentFilesPath)) return;
            var json = File.ReadAllText(RecentFilesPath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null)
            {
                RecentFiles.Clear();
                foreach (var f in list.Where(File.Exists))
                    RecentFiles.Add(f);
                OnPropertyChanged(nameof(HasRecentFiles));
            }
        }
        catch { }
    }

    public void OpenRecentFile(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            OpenFile(path);
    }

    // --- Drag & drop support ---
    public async void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        AddRecentFile(path);
        FilePath = path;
        // Ensure UI binding propagation before loading media
        await Dispatcher.UIThread.OnUiThreadAsync(() => { }, DispatcherPriority.Render);
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
        Dispatcher.UIThread.OnUiThread(() =>
        {
            if (IsSeeking) return;

            _isUpdatingPositionFromPlayer = true;
            try
            {
                State = _player.State;
                PositionText = FormatTime(e.Position);
                DurationText = FormatTime(e.Duration);
                SeekValue = e.NormalizedPosition;
            }
            finally
            {
                _isUpdatingPositionFromPlayer = false;
            }
        });
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            State = e.State;
        });
    }

    private void OnVolumeChanged(object? sender, VolumeChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            var playerVolume = Math.Clamp(_player.Volume, 0, VolumeMax);
            if (Math.Abs(_volumeValue - playerVolume) >= 0.001)
            {
                _volumeValue = playerVolume;
                OnPropertyChanged(nameof(VolumeValue));
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumeText));
            }

            var playerMuted = _player.IsMuted;
            if (_isMuted != playerMuted)
            {
                _isMuted = playerMuted;
                OnPropertyChanged(nameof(IsMuted));
            }
        });
    }

    /// <summary>
    /// Rebuilds typed track menu items from player track list events.
    /// Matches Python's _update_track_menus() behavior: preserves "Add..." and "None"
    /// pseudo-entries at the top, followed by actual tracks with language/state info.
    /// </summary>
    private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
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
                    var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                    var item = new TrackMenuItem(
                        FormatTrack("Sub", track),
                        TrackType.Subtitle,
                        trackId,
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
                    var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                    var item = new TrackMenuItem(
                        FormatTrack("Audio", track),
                        TrackType.Audio,
                        trackId,
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
            if (e.VideoTracks != null && e.VideoTracks.Any())
            {
                int idx = 0;
                foreach (var track in e.VideoTracks)
                {
                    var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                    var item = new TrackMenuItem(
                        FormatTrack("Video", track),
                        TrackType.Video,
                        trackId,
                        OnSelectVideo,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    VideoTracks.Add(item);
                    idx++;
                }
            }
            else
            {
                VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
            }
            HasMultipleVideoTracks = e.VideoTracks?.Count() > 1;
        });
    }

    private void OnPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
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
        Dispatcher.UIThread.OnUiThread(SyncLoopFlags);
    }

    internal void RefreshState()
    {
        _state = _player.State;
        _volumeValue = Math.Clamp(_player.Volume, 0, VolumeMax);
        _isMuted = _player.IsMuted;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(VolumeMax));
        OnPropertyChanged(nameof(VolumeValue));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumeText));
        OnPropertyChanged(nameof(IsMuted));

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
        OnPropertyChanged(nameof(HasChapters));

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
