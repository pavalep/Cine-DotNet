namespace Cine.Media.Events;

using System.Collections.Generic;

/// <summary>
/// Event args for playlist changes - matches Python's @mpv.property_observer("playlist")
/// </summary>
public class PlaylistChangedEventArgs : EventArgs
{
    /// <summary>
    /// Collection of playlist items (file paths)
    /// </summary>
    public IEnumerable<string> PlaylistItems { get; }

    /// <summary>
    /// Current position in playlist (0-based index)
    /// </summary>
    public int CurrentPosition { get; }

    /// <summary>
    /// Creates PlaylistChangedEventArgs with playlist collection and current position
    /// </summary>
    /// <param name="playlistItems">The list of playlist items</param>
    /// <param name="currentPosition">Current position in playlist</param>
    public PlaylistChangedEventArgs(IEnumerable<string> playlistItems, int currentPosition)
    {
        PlaylistItems = playlistItems;
        CurrentPosition = currentPosition;
    }
}
