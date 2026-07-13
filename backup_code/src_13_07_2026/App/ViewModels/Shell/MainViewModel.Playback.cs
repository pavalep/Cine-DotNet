using Cine.Core;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Playback commands: play/pause/stop, seek, volume, fullscreen, speed, screenshot.
/// Bound to header-bar / seek-bar / keyboard shortcuts.
/// </summary>
public partial class MainViewModel
{
    // ─────────────────────────────────────────────────────
    //  Play / Pause / Stop
    // ─────────────────────────────────────────────────────

    public void PlayPause()
    {
        Log.ForContext<MainViewModel>().Debug("PlayPause called. IsPlaying={IsPlaying} State={State}", _player.IsPlaying, _state);
        if (_player.IsPlaying)
            _player.Pause();
        else
            _player.Play();
    }

    public void Stop() => _player.Stop();

    // ─────────────────────────────────────────────────────
    //  Seek
    // ─────────────────────────────────────────────────────

    public void SeekForward() => _player.Seek(Position + TimeSpan.FromSeconds(5));
    public void SeekBackward() => _player.Seek(Position - TimeSpan.FromSeconds(5));
    public void SeekLargeForward() => _player.Seek(Position + TimeSpan.FromSeconds(60));
    public void SeekLargeBackward() => _player.Seek(Position - TimeSpan.FromSeconds(60));

    // ─────────────────────────────────────────────────────
    //  Volume
    // ─────────────────────────────────────────────────────

    public void IncreaseVolume() => VolumeValue = Math.Min(VolumeMax, VolumeValue + 5);
    public void DecreaseVolume() => VolumeValue = Math.Max(0, VolumeValue - 5);
    public void ToggleMute() => IsMuted = !IsMuted;

    // ─────────────────────────────────────────────────────
    //  Fullscreen
    // ─────────────────────────────────────────────────────

    public void ToggleFullscreen()
    {
        _player.SetFullscreen(!_player.IsFullscreen);
        IsFullscreen = _player.IsFullscreen;
    }

    // ─────────────────────────────────────────────────────
    //  Chapter / Item Navigation
    // ─────────────────────────────────────────────────────

    public void NextChapter() => _player.NextChapter();
    public void PreviousChapter() => _player.PreviousChapter();
    public void NextItem() => _player.NextPlaylistItem();
    public void PreviousItem() => _player.PreviousPlaylistItem();

    // ─────────────────────────────────────────────────────
    //  Loop / Shuffle
    // ─────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────
    //  Speed / Screenshot
    // ─────────────────────────────────────────────────────

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
    //  Audio Settings (proxied to AudioManager)
    // ─────────────────────────────────────────────────────

    public void ToggleAudioNormalization()
    {
        IsAudioNormalizationEnabled = !IsAudioNormalizationEnabled;
    }

    private bool _isAudioNormalizationEnabled;

    /// <summary>Proxies to AudioManager. Keeps local field for PropertyChanged notification.</summary>
    public bool IsAudioNormalizationEnabled
    {
        get => Audio?.IsAudioNormalizationEnabled ?? _isAudioNormalizationEnabled;
        set
        {
            _isAudioNormalizationEnabled = value;
            if (Audio != null) Audio.IsAudioNormalizationEnabled = value;
            OnPropertyChanged();
        }
    }
}
