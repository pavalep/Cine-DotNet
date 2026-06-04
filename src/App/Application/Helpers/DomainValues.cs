using System;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Immutable value types for domain concepts.
/// P8.5: Replaces raw doubles/TimeSpans with typed, immutable values.
/// </summary>

public readonly record struct PlaybackSpeed(double Value)
{
    public static readonly PlaybackSpeed Normal = new(1.0);
    public static readonly PlaybackSpeed Min = new(0.25);
    public static readonly PlaybackSpeed Max = new(4.0);

    public PlaybackSpeed Clamp() => new(Math.Clamp(Value, Min.Value, Max.Value));
    public override string ToString() => $"{Value:F1}x";
}

public readonly record struct VolumeLevel(int Percent)
{
    public static readonly VolumeLevel Min = new(0);
    public static readonly VolumeLevel Max = new(100);
    public static readonly VolumeLevel Default = new(100);

    public bool IsMuted => Percent <= 0;
    public VolumeLevel Clamp() => new(Math.Clamp(Percent, Min.Percent, Max.Percent));
    public override string ToString() => $"{Percent}%";
}

public readonly record struct TimePosition(double TotalSeconds)
{
    public static readonly TimePosition Zero = new(0);

    public TimeSpan ToTimeSpan() => TimeSpan.FromSeconds(TotalSeconds);
    public double Normalized(double duration) => duration > 0 ? Math.Clamp(TotalSeconds / duration, 0, 1) : 0;

    public override string ToString()
    {
        var ts = ToTimeSpan();
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
    }
}
