using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Cine.Avalonia.Helpers;
using Cine.Avalonia.Models;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.Components;

/// <summary>
/// Overlay control showing "Now Playing" media information.
/// Displays resolution, codec, frame rate, audio info, and duration.
/// Toggled via Ctrl+J.
/// </summary>
public partial class NowPlayingInfo : UserControl
{
    private IMediaPlayer? _player;

    /// <summary>Sets the player reference to read metadata from.</summary>
    public void SetPlayer(IMediaPlayer? player) => _player = player;

    /// <summary>
    /// Refresh all info fields from the current player state.
    /// Call this when a new file is opened or when the panel is shown.
    /// </summary>
    public void Refresh()
    {
        if (_player == null) return;

        try
        {
            // File name
            var path = _player.CurrentPath;
            FileNameText.Text = !string.IsNullOrWhiteSpace(path)
                ? System.IO.Path.GetFileName(path)
                : "No media";

            // Duration
            var dur = _player.Duration;
            DurationText.Text = dur.TotalSeconds > 0
                ? $"{(int)dur.TotalHours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}"
                : "—";

            // Video tracks
            var videoTracks = _player.VideoSources;
            if (videoTracks.Length > 0)
            {
                var vt = videoTracks[0]; // primary video track
                ResolutionText.Text = $"{vt.Width}×{vt.Height}";
                FrameRateText.Text = vt.Fps > 0 ? $"{vt.Fps:F2} fps" : "—";
                VideoCodecText.Text = !string.IsNullOrWhiteSpace(vt.Codec)
                    ? vt.Codec.ToUpperInvariant()
                    : "—";
            }
            else
            {
                ResolutionText.Text = "—";
                FrameRateText.Text = "—";
                VideoCodecText.Text = "—";
            }

            // Audio tracks
            var audioTracks = _player.AudioSources;
            if (audioTracks.Length > 0)
            {
                var at = audioTracks.FirstOrDefault(t => t.IsSelected)
                         ?? audioTracks[0];
                var lang = TrackDisplayHelper.GetLanguageName(at.Language);
                var ch = at.Channels > 0 ? $" {at.Channels}ch" : "";
                var codec = !string.IsNullOrWhiteSpace(at.Codec)
                    ? $" {at.Codec.ToUpperInvariant()}"
                    : "";
                AudioText.Text = $"{lang}{codec}{ch}";
            }
            else
            {
                AudioText.Text = "—";
            }
        }
        catch
        {
            // Silently handle if player state is not yet ready
        }
    }
}
