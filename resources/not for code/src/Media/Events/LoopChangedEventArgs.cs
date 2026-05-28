namespace Cine.Media.Events;

using Cine.Media.Models;

/// <summary>
/// Event args for loop mode changes - matches Python's @mpv.event for loop property changes
/// </summary>
public class LoopChangedEventArgs : EventArgs
{
    /// <summary>
    /// New loop mode
    /// </summary>
    public LoopMode NewLoopMode { get; }

    /// <summary>
    /// Previous loop mode
    /// </summary>
    public LoopMode PreviousLoopMode { get; }

    /// <summary>
    /// Creates LoopChangedEventArgs with old and new loop modes
    /// </summary>
    /// <param name="newLoopMode">The new loop mode</param>
    /// <param name="previousLoopMode">The previous loop mode</param>
    public LoopChangedEventArgs(LoopMode newLoopMode, LoopMode previousLoopMode)
    {
        NewLoopMode = newLoopMode;
        PreviousLoopMode = previousLoopMode;
    }
}
