using System;
using System.Collections.Generic;
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

/// <summary>Converts SeekValue (0.0-1.0) × parent width to pixel width for the seek fill bar.</summary>
public class SeekWidthConverter : IMultiValueConverter
{
    public static SeekWidthConverter Instance { get; } = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is double seekValue && values[1] is double parentWidth && parentWidth > 0)
            return Math.Clamp(seekValue * parentWidth, 0, parentWidth);
        return 0d;
    }
}

/// <summary>Converts SeekValue (0.0-1.0) × parent width to a Thickness margin for the seek thumb.</summary>
public class SeekThumbMarginConverter : IMultiValueConverter
{
    public static SeekThumbMarginConverter Instance { get; } = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is double seekValue && values[1] is double parentWidth && parentWidth > 0)
        {
            var x = seekValue * parentWidth - 8;
            return new Thickness(x, 0, 0, 0);
        }
        return new Thickness(0);
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
            return $"{(int)(clamped * 100)}%";
        }
        return "0%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Converts a chapter position (0.0-1.0) to a Canvas.Left offset.</summary>
public class ChapterMarginConverter : IValueConverter
{
    public static ChapterMarginConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double position)
            return position * 100.0;
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
