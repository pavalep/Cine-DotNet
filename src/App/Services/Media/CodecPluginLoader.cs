using System;
using System.Collections.Generic;
using Simba.Core;
using Simba.Media.Interfaces;

namespace Simba.Avalonia.Services;

/// <summary>
/// Placeholder for MEF-based external codec plugin loading.
/// </summary>
/// <remarks>
/// This is a future-phase stub. Actual MEF-based loading from a plugin directory
/// will be implemented when external codec plugins are developed (requires
/// System.ComponentModel.Composition NuGet package).
/// </remarks>
public sealed class CodecPluginLoader
{
    /// <summary>
    /// Load external codec plugins from the specified directory.
    /// Currently returns an empty list — stub for future implementation.
    /// </summary>
    public IEnumerable<ICodecProvider> LoadPlugins(string pluginDirectory)
    {
        Log.ForContext<CodecPluginLoader>()
            .Info("CodecPluginLoader: plugin directory {Dir} (stub — no plugins loaded)",
                pluginDirectory);
        return Array.Empty<ICodecProvider>();
    }
}
