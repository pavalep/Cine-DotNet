namespace Simba.Media.Events;

using System.Collections.Generic;
using Simba.Media.Models;

/// <summary>
/// Event args for track list changes - matches Python's @mpv.property_observer for audio/video subtitle tracks
/// </summary>
public class TrackListChangedEventArgs : EventArgs
{
    /// <summary>
    /// Collection of available audio tracks
    /// </summary>
    public IEnumerable<SubtitleSource> AudioTracks { get; }

    /// <summary>
    /// Collection of available video tracks
    /// </summary>
    public IEnumerable<SubtitleSource> VideoTracks { get; }

    /// <summary>
    /// Collection of available subtitle tracks
    /// </summary>
    public IEnumerable<SubtitleSource> SubtitleTracks { get; }

    /// <summary>
    /// Creates TrackListChangedEventArgs with track collections
    /// </summary>
    /// <param name="audioTracks">Audio track list</param>
    /// <param name="videoTracks">Video track list</param>
    /// <param name="subtitleTracks">Subtitle track list</param>
    public TrackListChangedEventArgs(
        IEnumerable<SubtitleSource> audioTracks,
        IEnumerable<SubtitleSource> videoTracks,
        IEnumerable<SubtitleSource> subtitleTracks)
    {
        AudioTracks = audioTracks;
        VideoTracks = videoTracks;
        SubtitleTracks = subtitleTracks;
    }
}
