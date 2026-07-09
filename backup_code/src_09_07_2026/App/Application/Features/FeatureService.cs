using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Cine.Avalonia.Features;

/// <summary>
/// Cached feature evaluation service.
///
/// Evaluation order:
///   1. User override present? → return override value
///   2. Feature missing from definition? → return false (safe default)
///   3. LicenseTierGate and user tier &lt; required tier? → return false
///   4. Any dependency disabled? → return false
///   5. Return <see cref="FeatureDefinition.DefaultEnabled"/>.
/// </summary>
public sealed class FeatureService : IFeatureService, IDisposable
{
    private readonly IFeatureStore _store;
    private readonly ILicensingService _licensing;
    private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public LicensingTier CurrentTier => _licensing.CurrentTier;

    public event Action<string, bool>? FeatureStateChanged;

    public FeatureService(IFeatureStore store, ILicensingService licensing)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _licensing = licensing ?? throw new ArgumentNullException(nameof(licensing));

        _licensing.TierChanged += OnTierChanged;
    }

    public bool IsEnabled(string featureKey)
    {
        if (_disposed) return false;

        if (_cache.TryGetValue(featureKey, out var cached))
            return cached;

        var result = Evaluate(featureKey);

        // Only cache non-overridden features (overrides can change).
        if (_store.GetOverride(featureKey) is null)
            _cache[featureKey] = result;

        return result;
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _licensing.TierChanged -= OnTierChanged;
        _cache.Clear();
    }

    // ── Private ──

    private bool Evaluate(string featureKey)
    {
        // 1. User override?
        var userOverride = _store.GetOverride(featureKey);
        if (userOverride.HasValue)
            return userOverride.Value;

        // 2. Feature known?
        if (!_store.AllDefinitions.TryGetValue(featureKey, out var def))
            return false; // unknown → disabled

        // 3. License tier gate?
        if (def.ToggleType == FeatureToggleType.LicenseTierGate)
        {
            var tier = _licensing.CurrentTier;
            if (tier < def.RequiredTier)
                return false;
        }

        // 4. Dependencies?
        foreach (var dep in def.DependsOn)
        {
            if (!IsEnabled(dep))
                return false;
        }

        // 5. Default
        return def.DefaultEnabled;
    }

    private void OnTierChanged(LicensingTier _)
    {
        InvalidateCache();
    }
}
