namespace Cine.Media.Events;

/// <summary>
/// Event args for subtitle property changes observed from mpv.
/// </summary>
public class SubtitlePropertyChangedEventArgs : EventArgs
{
    /// <summary>The mpv property name that changed (e.g. "sid", "sub-visibility", "sub-pos", "sub-scale", "sub-delay").</summary>
    public string PropertyName { get; }

    /// <summary>The new value as an object. Cast based on PropertyName.</summary>
    public object Value { get; }

    public SubtitlePropertyChangedEventArgs(string propertyName, object value)
    {
        PropertyName = propertyName;
        Value = value;
    }
}
