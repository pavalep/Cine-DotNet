using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cine.Avalonia.Features;

/// <summary>
/// Loads <c>feature-definitions.json</c> from the embedded assembly resources
/// and maintains a concurrent dictionary of user overrides.
/// </summary>
public sealed class FeatureStore : IFeatureStore
{
    private readonly ConcurrentDictionary<string, bool?> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, FeatureDefinition> _definitions = new Dictionary<string, FeatureDefinition>();

    public IReadOnlyDictionary<string, FeatureDefinition> AllDefinitions => _definitions;

    public FeatureStore()
    {
        Reload();
    }

    public void Reload()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = "Cine.Avalonia.Features.feature-definitions.json";

        using var stream = asm.GetManifestResourceStream(resourceName)
                        ?? throw new InvalidOperationException(
                            $"Embedded resource '{resourceName}' not found. " +
                            "Ensure 'feature-definitions.json' is an EmbeddedResource in the .csproj.");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var container = JsonSerializer.Deserialize<FeatureContainer>(json, JsonOptions.Value);
        var dict = new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);

        if (container?.Features != null)
        {
            foreach (var f in container.Features)
            {
                dict[f.Key] = f;
            }
        }

        _definitions = dict;
    }

    public bool? GetOverride(string key) =>
        _overrides.TryGetValue(key, out var val) ? val : null;

    public void SetOverride(string key, bool enabled) =>
        _overrides[key] = enabled;

    public void ClearOverride(string key) =>
        _overrides.TryRemove(key, out _);

    // ── JSON model ──

    private sealed record FeatureContainer
    {
        public int Version { get; init; }
        public List<FeatureDefinition> Features { get; init; } = new();
    }

    private static readonly Lazy<JsonSerializerOptions> JsonOptions = new(() => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(null) },
    });
}
