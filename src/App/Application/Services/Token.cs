using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Cine.Avalonia.Services;

/// <summary>
/// Resolves resource tokens from code-behind.
/// Provides typed accessors so builders never hardcode raw pixel/color values.
/// </summary>
public static class Token
{
    private static object? Get(string resourceKey)
    {
        return AvaloniaApp.Current?.FindResource(resourceKey);
    }

    /// <summary>Resolve a Double resource by key. Falls back to 0.</summary>
    public static double Size(string resourceKey)
    {
        var result = Get(resourceKey);
        if (result is double d) return d;
        return 0;
    }

    /// <summary>Resolve a Thickness resource by key. Falls back to default.</summary>
    public static Thickness GetThickness(string resourceKey)
    {
        var result = Get(resourceKey);
        if (result is Thickness t) return t;
        return new Thickness(0);
    }

    /// <summary>Resolve a CornerRadius resource by key. Falls back to default.</summary>
    public static CornerRadius GetRadius(string resourceKey)
    {
        var result = Get(resourceKey);
        if (result is CornerRadius c) return c;
        return new CornerRadius(0);
    }

    /// <summary>Resolve an IBrush resource by key. Falls back to null.</summary>
    public static IBrush? Brush(string resourceKey)
    {
        var result = Get(resourceKey);
        return result as IBrush;
    }
}
