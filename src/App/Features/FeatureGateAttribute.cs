using System;

namespace Cine.Avalonia.Features;

/// <summary>
/// Declarative feature gating for UI components.
/// Apply to controls, flyouts, or menu items to hide/disable them
/// when the specified feature is not enabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public sealed class FeatureGateAttribute : Attribute
{
    /// <summary>The feature key that gates this component.</summary>
    public string FeatureKey { get; }

    /// <summary>Optional — if true, the component is hidden; if false, it's disabled (but visible).</summary>
    public bool HideWhenDisabled { get; set; } = true;

    public FeatureGateAttribute(string featureKey)
    {
        FeatureKey = featureKey ?? throw new ArgumentNullException(nameof(featureKey));
    }
}
