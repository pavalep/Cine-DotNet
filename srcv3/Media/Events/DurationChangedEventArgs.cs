namespace Cine.Media.Events;

/// <summary>
/// Event args for duration changes - matches Python's @mpv.property_observer("duration")
/// </summary>
public class DurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Total duration of media
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Creates DurationChangedEventArgs with duration value
    /// </summary>
    /// <param name="duration">The duration value in TimeSpan</param>
    public DurationChangedEventArgs(TimeSpan duration)
    {
        Duration = duration;
    }
}
