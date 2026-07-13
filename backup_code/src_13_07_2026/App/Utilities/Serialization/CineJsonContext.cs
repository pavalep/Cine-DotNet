using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cine.Avalonia.Storage;

namespace Cine.Avalonia.Serialization;

/// <summary>
/// Source-generated JSON serialization context for Cine types.
/// Reduces startup cost vs reflection for high-frequency serialization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(PipState))]
[JsonSerializable(typeof(PlaylistData))]
internal partial class CineJsonContext : JsonSerializerContext
{
}
