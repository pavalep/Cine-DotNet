namespace Simba.Media.Events;

using Simba.Media.Models;

/// <summary>
/// Event args for playback state changes - matches Python's @mpv.event("pause"), "unpause", "stop"
/// </summary>
public class PlaybackStateEventArgs : EventArgs
{
    /// <summary>
    /// New playback state
    /// </summary>
    public PlaybackState NewState { get; set; }

    /// <summary>
    /// Previous playback state
    /// </summary>
    public PlaybackState PreviousState { get; set; }

    /// <summary>
    /// Creates PlaybackStateEventArgs with new and previous states
    /// </summary>
    /// <param name="newState">The new state</param>
    /// <param name="previousState">The previous state</param>
    public PlaybackStateEventArgs(PlaybackState newState, PlaybackState previousState)
    {
        NewState = newState;
        PreviousState = previousState;
    }
}
