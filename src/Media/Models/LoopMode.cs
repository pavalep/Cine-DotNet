namespace Simba.Media.Models;

/// <summary>
/// Loop mode for playback - matches Python's loop=file, loop=playlist, loop=no
/// </summary>
public enum LoopMode
{
    /// <summary>
    /// No looping - matches Python's loop=no (default)
    /// </summary>
    NoLoop,

    /// <summary>
    /// Loop current file - matches Python's loop=file
    /// </summary>
    File,

    /// <summary>
    /// Loop entire playlist - matches Python's loop=playlist
    /// </summary>
    Playlist
}
