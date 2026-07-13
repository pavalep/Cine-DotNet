using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Cine.Avalonia.Utilities;

/// <summary>Returns the negated boolean value.</summary>
public sealed class NegateBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? false : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? false : true;
}
