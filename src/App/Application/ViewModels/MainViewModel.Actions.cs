using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using System.Text.Json;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Playback actions, file operations, and event handlers.
/// </summary>
public partial class MainViewModel
{
    // ─────────────────────────────────────────────────────
    //  File Operations
    // ─────────────────────────────────────────────────────

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
        try
        {
            var path = await RequestSubtitleFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _player?.AddSubtitle(path);
                global::Cine.Core.Log.ForContext<MainViewModel>().Info("Subtitle added: {Path}", Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "AddSubtitle failed");
        }
    }

    private async Task OnAddAudio()
    {
        if (RequestAudioFileAsync == null) return;
        try
        {
            var path = await RequestAudioFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _player?.AddAudio(path);
                global::Cine.Core.Log.ForContext<MainViewModel>().Info("Audio track added: {Path}", Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "AddAudio failed");
        }
    }

    /// <summary>
    /// Load an external subtitle file directly (bypasses file dialog).
    /// </summary>
    public void LoadExternalSubtitle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _player == null) return;
        try
        {
            _player.AddSubtitle(filePath);
            global::Cine.Core.Log.ForContext<MainViewModel>().Info("External subtitle loaded: {Path}", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "LoadExternalSubtitle failed");
            OnError?.Invoke(this, $"Failed to load subtitle: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Load an external audio file directly (bypasses file dialog).
    /// </summary>
    public void LoadExternalAudio(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _player == null) return;
        try
        {
            _player.AddAudio(filePath);
            global::Cine.Core.Log.ForContext<MainViewModel>().Info("External audio loaded: {Path}", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "LoadExternalAudio failed");
            OnError?.Invoke(this, $"Failed to load audio track: {ex.Message}");
            throw;
        }
    }

    public async void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        AddRecentFile(path);
        FilePath = path;
        _currentSubtitleTrackId = -1;
        _currentAudioTrackId = -1;
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
        Playlist.Clear();
        PlaylistItems.Clear();
        foreach (var path in paths)
            Playlist.Add(path);
        OpenFile(paths[0]);
    }

    // ─────────────────────────────────────────────────────
    //  Playback Commands
    // ─────────────────────────────────────────────────────

    public void PlayPause()
    {
        Log($"PlayPause called. _player.IsPlaying={_player.IsPlaying} _state={_state}");
        if (_player.IsPlaying)
            _player.Pause();
        else
            _player.Play();
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
    public void IncreaseVolume() => VolumeValue = Math.Min(VolumeMax, VolumeValue + 5);
    public void DecreaseVolume() => VolumeValue = Math.Max(0, VolumeValue - 5);
    public void ToggleMute() => IsMuted = !IsMuted;

    public void ToggleFullscreen()
    {
        _player.SetFullscreen(!_player.IsFullscreen);
        IsFullscreen = _player.IsFullscreen;
    }

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

    // ─────────────────────────────────────────────────────
    //  PIP Decode Resolution
    // ─────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────
    //  Audio Equalizer
    // ─────────────────────────────────────────────────────

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
            var filters = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                if (Math.Abs(_equalizerBands[i]) > 0.5)
                    filters.Add($"equalizer=f={EqualizerFrequencies[i]}:t=q:width=1:g={_equalizerBands[i]:F1}");
            }
            if (_isAudioNormalizationEnabled)
                filters.Add("drc");

            var afValue = filters.Count > 0 ? string.Join(",", filters) : "";
            _player.Command("set_property", "af", afValue);
        }
        catch { /* player not ready */ }
    }

    public void ToggleAudioNormalization()
    {
        IsAudioNormalizationEnabled = !IsAudioNormalizationEnabled;
        ApplyEqualizer();
    }

    private bool _isAudioNormalizationEnabled;

    public bool IsAudioNormalizationEnabled
    {
        get => _isAudioNormalizationEnabled;
        set { _isAudioNormalizationEnabled = value; OnPropertyChanged(); }
    }

    /// <summary>Renderer mode: Auto (D3D11 hardware), Software (software only).</summary>
    public enum RendererType { Auto, Software }

    private RendererType _rendererMode;

    public RendererType RendererMode
    {
        get => _rendererMode;
        set
        {
            if (_rendererMode == value) return;
            _rendererMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHardwareAccelerationEnabled));
        }
    }

    public bool IsHardwareAccelerationEnabled
    {
        get => _rendererMode == RendererType.Auto;
        set => RendererMode = value ? RendererType.Auto : RendererType.Software;
    }

    private static double[] GetPreset(string name) => name switch
    {
        "Classical" => new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -4.0, -4.0, -4.0, -6.0 },
        "Rock" => new[] { 4.0, 3.0, 2.0, 1.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 },
        "Pop" => new[] { -1.0, 0.0, 2.0, 3.0, 4.0, 3.0, 2.0, 0.0, -1.0, -1.0 },
        "Jazz" => new[] { 3.0, 2.0, 1.0, 2.0, 3.0, 3.0, 2.0, 1.0, 1.0, 2.0 },
        "Bass Boost" => new[] { 6.0, 5.0, 4.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
        _ => new double[10]
    };

    // ─────────────────────────────────────────────────────
    //  Session Resume
    // ─────────────────────────────────────────────────────

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
                SubtitleTrackId = _currentSubtitleTrackId,
                AudioTrackId = _currentAudioTrackId,
                SubtitleDelay = _player.SubtitleDelay,
                AudioDelay = _player.AudioDelay,
                RendererMode = _rendererMode.ToString()
            };
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(session));
        }
        catch (Exception ex) { global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "SaveSession failed"); }
    }

    public void LoadSession()
    {
        try
        {
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
            if (root.TryGetProperty("SubtitleTrackId", out var subIdEl))
                _pendingSubtitleTrackId = subIdEl.GetInt32();
            if (root.TryGetProperty("AudioTrackId", out var audIdEl))
                _pendingAudioTrackId = audIdEl.GetInt32();
            if (root.TryGetProperty("SubtitleDelay", out var subDelayEl))
                _player.SubtitleDelay = (float)subDelayEl.GetDouble();
            if (root.TryGetProperty("AudioDelay", out var audDelayEl))
                _player.AudioDelay = (float)audDelayEl.GetDouble();
            if (root.TryGetProperty("RendererMode", out var rmEl) && Enum.TryParse<RendererType>(rmEl.GetString(), out var rm))
                RendererMode = rm;
        }
        catch { /* best-effort */ }
    }

    public void ClearSession()
    {
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); }
        catch { /* best-effort */ }
    }

    // ─────────────────────────────────────────────────────
    //  Recent Files
    // ─────────────────────────────────────────────────────

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
        catch { /* best-effort */ }
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

    // ─────────────────────────────────────────────────────
    //  Event Handlers
    // ─────────────────────────────────────────────────────

    private TimeSpan _lastPosTextTime = TimeSpan.Zero;
    private double _lastSeekValue;

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            if (IsSeeking) return;

            _isUpdatingPositionFromPlayer = true;
            try
            {
                State = _player.State;

                if (Math.Abs((e.Position - _lastPosTextTime).TotalSeconds) >= 0.1)
                {
                    _lastPosTextTime = e.Position;
                    PositionText = FormatTime(e.Position);
                    DurationText = FormatTime(e.Duration);
                }

                if (Math.Abs(e.NormalizedPosition - _lastSeekValue) >= 0.001)
                {
                    _lastSeekValue = e.NormalizedPosition;
                    SeekValue = e.NormalizedPosition;
                }
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
            Log($"VM.OnPlaybackStateChanged: oldState={_state} newState={e.State}");
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
}
