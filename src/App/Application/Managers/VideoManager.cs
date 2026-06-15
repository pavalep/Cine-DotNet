using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Cine.Avalonia.Models;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Centralized manager for all video-related state: Contrast, Brightness,
/// Gamma, Saturation, Hue, Zoom, Aspect Ratio, Rotation, and Flip.
///
/// Each setter immediately applies the value to the player via IMediaPlayer.
/// </summary>
public sealed class VideoManager : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private bool _disposed;

    // ── Video Filters ──

    // ── Video Tracks ──
    private int _currentVideoTrackId = -1;

    public VideoManager(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        BuildEmptyTrackMenus();
    }

    // ── Observable Properties ──

    #region Video Filters

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

    #endregion

    #region Zoom & Aspect

    public double ZoomValue
    {
        get => _player.Zoom;
        set { _player.Zoom = value; OnPropertyChanged(); }
    }

    public double AspectRatioValue
    {
        get => _player.AspectRatio;
        set { _player.AspectRatio = value; OnPropertyChanged(); }
    }

    public void ResetZoom() => ZoomValue = 0;
    public void ResetAspectRatio() => AspectRatioValue = -1;
    public void SetAspectRatio(double ratio) => AspectRatioValue = ratio;

    #endregion

    #region Rotation & Flip

    public void RotateLeft() => _player.Command("set", "video-rotate", "90");
    public void RotateRight() => _player.Command("set", "video-rotate", "270");
    public void ResetRotation() => _player.Command("set", "video-rotate", "0");
    public void FlipHorizontal() => _player.Command("vf", "toggle", "hflip");
    public void FlipVertical() => _player.Command("vf", "toggle", "vflip");
    public void ResetFlip() => _player.Command("vf", "del", "@hflip", "@vflip");

    #endregion

    #region Video Tracks

    public ObservableCollection<TrackMenuItem> VideoTracks { get; } = new();

    /// <summary>True if the current media has multiple video tracks.</summary>
    public bool HasMultipleVideoTracks => VideoTracks.Count(t => !t.IsPseudoEntry) > 1;

    private void BuildEmptyTrackMenus()
    {
        VideoTracks.Clear();
        VideoTracks.Add(new TrackMenuItem("Add Video Track…", TrackType.Video, -1, OnSelectVideo));
        VideoTracks.Add(new TrackMenuItem("None", TrackType.Video, -2, OnSelectVideo));
        VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
    }

    private void OnSelectVideo(TrackMenuItem item)
    {
        if (item.IsPseudoEntry) return;

        if (item.TrackIndex >= 0)
        {
            _player.SelectVideoTrack(item.TrackIndex);
            _currentVideoTrackId = item.TrackIndex;
            foreach (var t in VideoTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
        else
        {
            _player.SelectVideoTrack(-1);
            _currentVideoTrackId = -1;
            foreach (var t in VideoTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

    /// <summary>
    /// Refresh video tracks from a track list update.
    /// Called by the owner when track list changes.
    /// </summary>
    public void RefreshVideoTracks(IEnumerable<SubtitleSource> videoSources)
    {
        VideoTracks.Clear();
        VideoTracks.Add(new TrackMenuItem("Add Video Track…", TrackType.Video, -1, OnSelectVideo));
        VideoTracks.Add(new TrackMenuItem("None", TrackType.Video, -2, OnSelectVideo));

        if (videoSources != null && videoSources.Any())
        {
            int idx = 0;
            foreach (var track in videoSources)
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

        OnPropertyChanged(nameof(HasMultipleVideoTracks));
    }

    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        var state = track.IsEnabled ? "on" : "off";
        return $"{prefix}: {lang} ({state})";
    }

    #endregion

    #region Reset

    public void ResetAllVideo()
    {
        ContrastValue = 0;
        BrightnessValue = 0;
        GammaValue = 1;
        SaturationValue = 1;
        HueValue = 0;
        ResetZoom();
        ResetAspectRatio();
        ResetRotation();
        ResetFlip();
    }

    #endregion

    // ── INotifyPropertyChanged ──

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ── Cleanup ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
