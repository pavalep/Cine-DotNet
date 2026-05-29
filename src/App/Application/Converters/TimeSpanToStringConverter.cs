using System;
using System.Globalization;
using Avalonia;
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
        if (value is double d)
        {
            double clamped = Math.Clamp(d, 0.0, 1.0);
            // If target is Thickness (for thumb margin), return the pixel offset
            if (targetType == typeof(Thickness))
            {
                return new Thickness(clamped * 100, 0, 0, 0);
            }
            // If target is Rect (for RectangleGeometry clip), return a proportional rect
            if (targetType == typeof(Rect))
            {
                return new Rect(0, 0, clamped, 1);
            }
            return $"{(int)(clamped * 100)}%";
        }
        return "0%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Converts a chapter position (0.0-1.0) to a margin for the seek bar overlay.</summary>
public class ChapterMarginConverter : IValueConverter
{
    public static ChapterMarginConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Expects a double value representing position (0.0 to 1.0)
        // Returns a Thickness with left margin as percentage of slider width
        if (value is double position)
        {
            var left = position * 100;
            return new Thickness(left, 0, 0, 0);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}