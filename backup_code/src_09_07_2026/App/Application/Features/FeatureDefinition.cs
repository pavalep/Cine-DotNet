using System.Collections.Generic;

namespace Cine.Avalonia.Features;

/// <summary>
/// Describes a single feature toggle — its key, type, default state, and dependencies.
/// Serialized from <c>feature-definitions.json</c>.
/// </summary>
public sealed record FeatureDefinition
{
    /// <summary>Unique identifier for the feature (e.g. <c>"codecs.hdr10"</c>).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable name shown in Preferences.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Short description explaining what this feature provides.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How this feature is toggled.</summary>
    public FeatureToggleType ToggleType { get; init; } = FeatureToggleType.Boolean;

    /// <summary>Whether the feature is enabled by default.</summary>
    public bool DefaultEnabled { get; init; }

    /// <summary>Required license tier (only for <see cref="FeatureToggleType.LicenseTierGate"/>).</summary>
    public LicensingTier RequiredTier { get; init; } = LicensingTier.Free;

    /// <summary>Feature keys that must be enabled for this feature to work.</summary>
    public List<string> DependsOn { get; init; } = new();
}
