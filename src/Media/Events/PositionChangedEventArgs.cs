namespace Cine.Media.Events;

public class PositionChangedEventArgs : EventArgs
{
    public TimeSpan Position { get; }
    public TimeSpan Duration { get; }
    public double NormalizedPosition { get; }

    public PositionChangedEventArgs(TimeSpan position, TimeSpan duration)
    {
        Position = position;
        Duration = duration;
        NormalizedPosition = duration.TotalSeconds > 0
            ? position.TotalSeconds / duration.TotalSeconds
            : 0;
    }
}
