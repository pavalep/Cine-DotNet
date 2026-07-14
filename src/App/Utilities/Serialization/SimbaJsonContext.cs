using System.Collections.Generic;
using System.Text.Json.Serialization;
using Simba.Avalonia.Models;
using Simba.Avalonia.Storage;

namespace Simba.Avalonia.Serialization;

/// <summary>
/// Source-generated JSON serialization context for Simba types.
/// Reduces startup cost vs reflection for high-frequency serialization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<RecentFileEntry>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(PipState))]
[JsonSerializable(typeof(PlaylistData))]
internal partial class SimbaJsonContext : JsonSerializerContext
{
}
