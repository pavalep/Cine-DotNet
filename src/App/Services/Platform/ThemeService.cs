using System;

namespace Simba.Avalonia.Services;

/// <summary>
/// Manages application theme (dark/light/system) and high-contrast detection.
/// Registers as singleton in DI. Theme choice is persisted in settings store.
/// </summary>
public class ThemeService
{
    public enum AppTheme { Dark, Light, System }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public bool IsHighContrast { get; private set; }

    public event Action<AppTheme>? ThemeChanged;

    /// <summary>
    /// Switch to the given theme and notify all subscribers.
    /// </summary>
    public void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme) return;
        CurrentTheme = theme;
        ThemeChanged?.Invoke(theme);
    }

    /// <summary>
    /// Toggle between Dark and Light themes.
    /// </summary>
    public void Toggle() =>
        SetTheme(CurrentTheme is AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    /// <summary>
    /// Detect Windows high-contrast mode at startup.
    /// On macOS/Linux, this is a no-op.
    /// </summary>
    public void DetectHighContrast()
    {
        // High-contrast detection requires platform-specific P/Invoke.
        // Placeholder for future implementation.
        // On Windows: SystemParametersInfo(SPI_GETHIGHCONTRAST, ...)
        IsHighContrast = false;
    }
}
