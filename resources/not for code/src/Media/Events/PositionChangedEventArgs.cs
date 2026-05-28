namespace Cine.Media.Events;

/// <summary>
/// Event args for position changes - matches Python's @mpv.property_observer("time-pos")
/// </summary>
public class PositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Current position in playback
    /// </summary>
    public TimeSpan Position { get; }

    /// <summary>
    /// Creates PositionChangedEventArgs with current position value
    /// </summary>
    /// <param name="position">The position value in TimeSpan</param>
    public PositionChangedEventArgs(TimeSpan position)
    {
        Position = position;
    }
}
