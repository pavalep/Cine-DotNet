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
    /// vo=libmpv is REQUIRED — this tells mpv to use the libmpv video output
    /// that works with mpv_render_context_create/mpv_render_context_render.
    /// Without it the render update callback will never fire.
    /// gpu-context and gpu-api are NOT set because we create our OWN ANGLE/GL
    /// context externally and pass it to mpv via get_proc_address.
    /// No wid — we don't need a child HWND.
    /// </summary>
    public static Dictionary<string, string> GetRenderApiOptions()
    {
        return new Dictionary<string, string>
        {
            // ── Core ──
            ["terminal"] = "no",
            ["msg-level"] = "all=warn",
            ["keep-open"] = "yes",
            ["keep-open-pause"] = "no",
            ["osc"] = "no",
            ["vo"] = "libmpv",         // REQUIRED: Enables libmpv VO for render API

            // ── Audio ──
            ["ao"] = "wasapi",
            ["volume-max"] = "150",
            ["gapless-audio"] = "yes",

            // ── Color / levels ──
            // video-output-levels = full prevents mpv from treating the FBO as
            // limited-range (16-235 TV levels) which would crush blacks and dim
            // whites on a PC monitor expecting full-range (0-255).
            ["video-output-levels"] = "full",

            // gamma slightly above neutral — standard sRGB curve is 2.2, which
            // some find slightly dark on uncalibrated displays. Adding a touch
            // of gamma (10) lifts midtones without clipping highlights.
            ["gamma"] = "10",

            // ── Hardware decoding (disabled until ANGLE interop verified) ──
            ["hwdec"] = "no",

            // ── Subtitle styling ──
            // sub-ass-override=force ensures SRT subtitles use mpv styling rather
            // than the internal libass defaults which can be too small/dim to see.
            ["sub-font-size"] = "24",
            ["sub-color"] = "#FFFFFF",
            ["sub-border-size"] = "2",
            ["sub-shadow-offset"] = "1",
            ["sub-ass-override"] = "force"
        };
    }

    /// <summary>
    /// Premium low-latency tuning options for the OpenGL render API path.
    /// These options reduce latency and improve frame pacing:
    /// - cache=no: Disable read-ahead cache (not needed for local files; reduces seek latency).
    /// - opengl-early-flush=yes: Flush OpenGL commands early to reduce frame latency.
    /// - hwdec=auto: Enable hardware decoding (auto-selects the best available decoder).
    /// - audio-buffer=0.1: Reduce audio buffer to 100ms for lower A/V sync latency.
    /// - display-resample=linear: Linear resampling for smooth display rate matching.
    /// - video-sync=display-resample: Sync video to display refresh with resampling.
    /// - video-sync-max-audio-change=0.1: Limit audio pitch correction during sync.
    ///
    /// These are applied ON TOP OF GetRenderApiOptions() by the caller.
    /// </summary>
    public static Dictionary<string, string> GetPremiumTuningOptions()
    {
        return new()
        {
            // ── Audio: moderate buffer for stability ──
            ["audio-buffer"] = "0.2",

            // ── Cache: small cache for smoother network streams ──
            ["cache"] = "yes",
            ["cache-default"] = "1500",
            ["cache-secs"] = "10",

            // ── Display sync: match video to display refresh ──
            ["video-sync"] = "display-resample",
            ["video-sync-max-audio-change"] = "0.2",

            // ── Hardware decoding ──
            ["hwdec"] = "auto-safe"
        };
    }


}
