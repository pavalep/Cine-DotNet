namespace Cine.Media.Models;

/// <summary>
/// Current playback state - matches Python mpv's pause/unpause/stop states
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// No media loaded or playback stopped - matches Python's stopped state
    /// </summary>
    Stopped,

    /// <summary>
    /// Media is currently playing - matches Python's playing state
    /// </summary>
    Playing,

    /// <summary>
    /// Playback is paused - matches Python's paused state
    /// </summary>
    Paused
}
