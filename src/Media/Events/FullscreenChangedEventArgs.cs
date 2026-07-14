namespace Simba.Media.Events;

/// <summary>
/// Event args for fullscreen state changes - matches Python's @mpv.event for fullscreen property changes
/// </summary>
public class FullscreenChangedEventArgs : EventArgs
{
    /// <summary>
    /// Whether player is now in fullscreen mode
    /// </summary>
    public bool IsFullscreen { get; }

    /// <summary>
    /// Previous fullscreen state
    /// </summary>
    public bool PreviousIsFullscreen { get; }

    /// <summary>
    /// Creates FullscreenChangedEventArgs with fullscreen state
    /// </summary>
    /// <param name="isFullscreen">The new fullscreen state</param>
    /// <param name="previousIsFullscreen">The previous fullscreen state</param>
    public FullscreenChangedEventArgs(bool isFullscreen, bool previousIsFullscreen)
    {
        IsFullscreen = isFullscreen;
        PreviousIsFullscreen = previousIsFullscreen;
    }
}
