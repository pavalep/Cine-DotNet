using System.Collections.Generic;

namespace Simba.Avalonia.Features;

/// <summary>
/// Loads feature definitions from the embedded JSON resource
/// and provides runtime override storage (user preferences).
/// </summary>
public interface IFeatureStore
{
    /// <summary>All defined features, keyed by <see cref="FeatureDefinition.Key"/>.</summary>
    IReadOnlyDictionary<string, FeatureDefinition> AllDefinitions { get; }

    /// <summary>Reload definitions from embedded JSON (e.g. after update).</summary>
    void Reload();

    /// <summary>Get a user override for the given feature key, or null if no override.</summary>
    bool? GetOverride(string key);

    /// <summary>Set a user override for the given feature key.</summary>
    void SetOverride(string key, bool enabled);

    /// <summary>Remove the user override, reverting to default.</summary>
    void ClearOverride(string key);
}
