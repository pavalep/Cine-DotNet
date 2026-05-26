using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Cine.Avalonia.Converters;

/// <summary>Converts a TimeSpan to a human-readable string like "01:23:45" or "-00:05".</summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public static TimeSpanToStringConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan ts) return "--:--:--";
        if (ts < TimeSpan.Zero) return $"-{TimeSpan.FromTicks(-ts.Ticks):hh\\:mm\\:ss}";
        return ts.ToString("hh\\:mm\\:ss");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Converts a double (0.0-1.0) to a percentage string like "75%".</summary>
public class PercentConverter : IValueConverter
{
    public static PercentConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is double d ? $"{(int)(d * 100)}%" : "0%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}