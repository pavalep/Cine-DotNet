namespace Cine.Avalonia.Features;

/// <summary>
/// Evaluates feature toggle state by combining the embedded definition,
/// the current license tier, any user overrides, and dependency resolution.
/// </summary>
public interface IFeatureService
{
    /// <summary>True if the feature is enabled given the current license tier and overrides.</summary>
    bool IsEnabled(string featureKey);

    /// <summary>The license tier currently in effect.</summary>
    LicensingTier CurrentTier { get; }

    /// <summary>Fired when the feature state changes (tier switch, override toggled).</summary>
    event Action<string, bool>? FeatureStateChanged;

    /// <summary>Invalidate cached evaluations so the next call recomputes.</summary>
    void InvalidateCache();
}
