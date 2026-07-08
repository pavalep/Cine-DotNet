using Cine.Media.Models;

namespace Cine.Media.Interfaces;

/// <summary>
/// Describes a codec provider's capabilities and can configure a player.
/// Providers are probed at startup; the best available one is selected.
/// </summary>
public interface ICodecProvider
{
    /// <summary>Human-readable provider name (e.g. "MPV", "MediaFoundation").</summary>
    string Name { get; }

    /// <summary>Whether this provider is available on the current system.</summary>
    bool IsAvailable { get; }

    /// <summary>List of codecs this provider supports.</summary>
    IReadOnlyList<CodecCapability> GetCapabilities();

    /// <summary>
    /// Apply provider-specific configuration to a player instance.
    /// Called after the player is created, before media is opened.
    /// </summary>
    void Configure(IMediaPlayer player);
}
