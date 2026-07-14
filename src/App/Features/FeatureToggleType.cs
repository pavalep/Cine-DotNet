namespace Simba.Avalonia.Features;

/// <summary>
/// Defines how a feature toggle is evaluated at runtime.
/// </summary>
public enum FeatureToggleType
{
    /// <summary>Simple on/off toggle — independent of license tier.</summary>
    Boolean,

    /// <summary>Gated by license tier — user must be at or above the required tier.</summary>
    LicenseTierGate,

    /// <summary>Gradual roll-out percentage (future).</summary>
    GradualRollout,
}
