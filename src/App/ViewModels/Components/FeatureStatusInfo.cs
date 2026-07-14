namespace Simba.Avalonia.ViewModels;

/// <summary>
/// Represents a single feature's status for display in the Preferences dialog.
/// </summary>
public sealed record FeatureStatusInfo
{
    /// <summary>Human-readable feature name (e.g. "Audio Equalizer").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Whether the feature is currently enabled under the active license tier.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Short explanation (e.g. "Requires Full tier", "Enabled by default").</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>XAML-friendly icon or label for enabled/disabled state.</summary>
    public string StatusIcon => IsEnabled ? "\u2713" : "\u2717";

    /// <summary>Colour-friendly status label for display.</summary>
    public string StatusLabel => IsEnabled ? "Enabled" : "Disabled";
}
