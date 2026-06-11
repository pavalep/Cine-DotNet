using System;
using System.Collections.Generic;

namespace Cine.Media.Implementations;

/// <summary>
/// Shared mpv initialization options for high-quality (primary) and low-quality (PiP) profiles.
/// </summary>
public static class MpvConfig
{
    /// <summary>
    /// Options common to both primary and PiP player instances (base config).
    /// </summary>
    public static Dictionary<string, string> GetBaseOptions()
    {
        return new()
        {
            ["terminal"] = "no",
            ["msg-level"] = "all=warn",
            ["keep-open"] = "yes",
            ["keep-open-pause"] = "no",
            ["osc"] = "no",
            ["vo"] = "gpu",
            ["gpu-context"] = "d3d11",
            ["hwdec"] = "auto-safe",
            ["volume-max"] = "150"
        };
    }

    /// <summary>
    /// High-quality rendering options for the primary MainWindow player.
    /// </summary>
    public static Dictionary<string, string> GetQualityOptions()
    {
        return new()
        {
            ["scale"] = "spline36",
            ["cscale"] = "spline36",
            ["dscale"] = "mitchell",
            ["correct-downscaling"] = "yes",
            ["deband"] = "yes",
            ["deband-iterations"] = "1",
            ["dither-depth"] = "auto"
        };
    }

    /// <summary>
    /// Low-quality (cheap) rendering options for the PiP secondary player.
    /// </summary>
    public static Dictionary<string, string> GetLowQualityOptions()
    {
        return new()
        {
            ["scale"] = "bilinear",
            ["cscale"] = "bilinear",
            ["dscale"] = "bilinear",
            ["correct-downscaling"] = "no",
            ["deband"] = "no",
            ["deband-iterations"] = "0",
            ["dither-depth"] = "no"
        };
    }

    /// <summary>
    /// Combines base options with quality-specific options for full init.
    /// Includes vo/gpu-context/wid for the native HWND rendering path.
    /// </summary>
    public static Dictionary<string, string> GetFullOptions(bool highQuality, IntPtr hwnd)
    {
        var options = new Dictionary<string, string>(GetBaseOptions());
        foreach (var kv in (highQuality ? GetQualityOptions() : GetLowQualityOptions()))
            options[kv.Key] = kv.Value;

        options["wid"] = hwnd.ToInt64().ToString();
        return options;
    }

    /// <summary>
    /// Options for the OpenGL render API path.
    /// vo=null — no standalone VO, mpv_render_context_create handles output.
    /// </summary>
    public static Dictionary<string, string> GetRenderApiOptions()
    {
        return new Dictionary<string, string>
        {
            ["terminal"] = "no",
            ["msg-level"] = "all=info",
            ["keep-open"] = "yes",
            ["keep-open-pause"] = "no",
            ["osc"] = "no",
            ["hwdec"] = "no",
            ["vo"] = "null",  // Render API handles output
            ["volume-max"] = "150"
        };
    }


}
