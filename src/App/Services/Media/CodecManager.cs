using System;
using System.Collections.Generic;
using System.Linq;
using Simba.Core;
using Simba.Media.Codecs;
using Simba.Media.Interfaces;

namespace Simba.Avalonia.Services;

/// <summary>
/// Orchestrates codec provider selection and session creation.
/// Probes all registered <see cref="ICodecProvider"/> instances at startup,
/// selects the best available one, and creates <see cref="IDecodingSession"/>s
/// wrapping player instances.
/// </summary>
public sealed class CodecManager
{
    private readonly IEnumerable<ICodecProvider> _providers;

    /// <summary>The provider selected at startup as the best available.</summary>
    public ICodecProvider ActiveProvider { get; private set; }

    /// <summary>
    /// All registered providers (useful for showing codec info in preferences).
    /// </summary>
    public IReadOnlyList<ICodecProvider> AllProviders { get; }

    public CodecManager(IEnumerable<ICodecProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        AllProviders = providers.ToList().AsReadOnly();
        ActiveProvider = SelectBestProvider();
    }

    /// <summary>
    /// Create a decoding session wrapping the given player.
    /// The player should already be configured by <see cref="ICodecProvider.Configure"/>.
    /// </summary>
    public IDecodingSession CreateSession(IMediaPlayer player)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));

        var supportsHwDec = ActiveProvider.GetCapabilities()
            .Any(c => c.SupportsHardwareDecoding);

        return new DecodingSession(
            player,
            ActiveProvider.Name,
            supportsHwDec,
            $"{ActiveProvider.Name} backend");
    }

    /// <summary>
    /// Select the best available provider by preferring:
    /// 1. MpvCodecProvider (most capable, always available)
    /// 2. MFCodecProvider (Windows Media Foundation, if available)
    /// 3. SoftwareFallbackCodecProvider (last resort)
    /// </summary>
    private ICodecProvider SelectBestProvider()
    {
        // Order by capability count descending, then availability
        var ranked = _providers
            .Where(p => p.IsAvailable)
            .OrderByDescending(p => p.GetCapabilities().Count)
            .ToList();

        if (ranked.Count == 0)
        {
            var ex = new InvalidOperationException(
                "No codec provider is available on this system.");
            Log.ForContext<CodecManager>().Error(ex, "No codec providers available");
            throw ex;
        }

        var selected = ranked[0];
        Log.ForContext<CodecManager>()
            .Info("Selected codec provider: {Provider} ({CapCount} codecs)",
                selected.Name, selected.GetCapabilities().Count);
        return selected;
    }
}
