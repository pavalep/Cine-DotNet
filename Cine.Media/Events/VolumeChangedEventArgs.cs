namespace Cine.Media.Events;

/// <summary>
/// Event args for volume changes - matches Python's @mpv.property_observer("volume")
/// </summary>
public class VolumeChangedEventArgs : EventArgs
{
    /// <summary>
    /// New volume level (0-150 as per Python's volume_max)
    /// </summary>
    public double Volume { get; }

    /// <summary>
    /// Whether mute state changed
    /// </summary>
    public bool IsMuted { get; }

    /// <summary>
    /// Creates VolumeChangedEventArgs with volume value
    /// </summary>
    /// <param name="volume">The volume level</param>
    public VolumeChangedEventArgs(double volume)
    {
        Volume = volume;
        IsMuted = false;
    }

    /// <summary>
    /// Creates VolumeChangedEventArgs with mute state
    /// </summary>
    /// <param name="isMuted">The mute state</param>
    public VolumeChangedEventArgs(bool isMuted)
    {
        Volume = 0;
        IsMuted = isMuted;
    }
}
