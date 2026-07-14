using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Simba.Avalonia.Core;
using Simba.Media.Interfaces;
using Simba.Media.Models;
using Simba.Avalonia.Helpers;
using Simba.Avalonia.Models;

namespace Simba.Avalonia.Managers;

/// <summary>
/// Centralized manager for all video-related state: Contrast, Brightness,
/// Gamma, Saturation, Hue, Zoom, Aspect Ratio, Rotation, and Flip.
///
/// Each setter immediately applies the value to the player via IMediaPlayer.
/// </summary>
public sealed class VideoManager : DomainManager<IMediaPlayer>, INotifyPropertyChanged
{
    // ── Video Tracks ──
    private int _currentVideoTrackId = -1;

    // ── Lazy constructed track menu — only created when first accessed ──
    private Lazy<ObservableCollection<TrackMenuItem>> _videoTracks = new(() =>
    {
        var col = new ObservableCollection<TrackMenuItem>
        {
            new("Add Video Track…", TrackType.Video, -1, _ => { }),
            new("None", TrackType.Video, -2, _ => { }),
            new("No video tracks", TrackType.Video, -1, _ => { }),
        };
        return col;
    });

    public VideoManager(IMediaPlayer player) : base(player)
    {
        // Note: track menus are lazily created on first access.
    }

    // ── Observable Properties ──

    #region Video Filters

    public double ContrastValue
    {
        get => Player.Contrast;
        set { Player.Contrast = value; OnPropertyChanged(); }
    }

    public double BrightnessValue
    {
        get => Player.Brightness;
        set { Player.Brightness = value; OnPropertyChanged(); }
    }

    public double GammaValue
    {
        get => Player.Gamma;
        set { Player.Gamma = value; OnPropertyChanged(); }
    }

    public double SaturationValue
    {
        get => Player.Saturation;
        set { Player.Saturation = value; OnPropertyChanged(); }
    }

    public double HueValue
    {
        get => Player.Hue;
        set { Player.Hue = value; OnPropertyChanged(); }
    }

    #endregion

    #region Zoom & Aspect

    public double ZoomValue
    {
        get => Player.Zoom;
        set { Player.Zoom = value; OnPropertyChanged(); }
    }

    public double AspectRatioValue
    {
        get => Player.AspectRatio;
        set { Player.AspectRatio = value; OnPropertyChanged(); }
    }

    public void ResetZoom() => ZoomValue = 0;
    public void ResetAspectRatio() => AspectRatioValue = -1;
    public void SetAspectRatio(double ratio) => AspectRatioValue = ratio;

    #endregion

    #region Rotation & Flip

    public void RotateLeft() => Player.Command("set", "video-rotate", "90");
    public void RotateRight() => Player.Command("set", "video-rotate", "270");
    public void ResetRotation() => Player.Command("set", "video-rotate", "0");
    public void FlipHorizontal() => Player.Command("vf", "toggle", "hflip");
    public void FlipVertical() => Player.Command("vf", "toggle", "vflip");
    public void ResetFlip() => Player.Command("vf", "del", "@hflip", "@vflip");

    #endregion

    #region Video Tracks

    public ObservableCollection<TrackMenuItem> VideoTracks => _videoTracks.Value;

    /// <summary>True if the current media has multiple video tracks.</summary>
    public bool HasMultipleVideoTracks => VideoTracks.Count(t => !t.IsPseudoEntry) > 1;

    internal void OnSelectVideo(TrackMenuItem item)
    {
        // "Add Video Track…" (TrackIndex = -1) has no backend implementation yet.
        // "None" (TrackIndex = -2) falls through to disable video output.
        if (item.IsPseudoEntry && item.TrackIndex == -1) return;

        if (item.TrackIndex >= 0)
        {
            Player.SelectVideoTrack(item.TrackIndex);
            _currentVideoTrackId = item.TrackIndex;
            foreach (var t in VideoTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
        else
        {
            Player.SelectVideoTrack(-1);
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
        return Simba.Avalonia.Helpers.TrackDisplayHelper.FormatTrack(TrackType.Video, track);
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

}
