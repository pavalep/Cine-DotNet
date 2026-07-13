namespace Cine.Avalonia.Features;

/// <summary>
/// Compile-time-safe feature key constants.
/// Convention: categories use dot-notation (e.g. <c>"codecs.hdr10"</c>).
/// </summary>
public static class FeatureKeys
{
    // ── Playback Features ──
    public const string Playback4K = "playback.4k";
    public const string Playback8K = "playback.8k";
    public const string PlaybackHdr = "playback.hdr";

    // ── Codec Features ──
    public const string CodecHdr10 = "codecs.hdr10";
    public const string CodecDolbyVision = "codecs.dolbyVision";
    public const string CodecDts = "codecs.dts";
    public const string CodecTrueHD = "codecs.truehd";

    // ── Audio Features ──
    public const string AudioEqualizer = "audio.equalizer";
    public const string AudioDeviceExclusive = "audio.deviceExclusive";

    // ── Video Features ──
    public const string VideoShaders = "video.shaders";
    public const string VideoFiltersAdvanced = "video.filters.advanced";

    // ── Subtitle Features ──
    public const string SubtitlesAdvancedStyling = "subtitles.advancedStyling";

    // ── Playlist Features ──
    public const string PlaylistSaveLoad = "playlist.saveLoad";

    // ── UI Features ──
    public const string UiPipMode = "ui.pipMode";
    public const string UiCrashReporting = "ui.crashReporting";
    public const string UiTrialWatermark = "ui.trialWatermark";
}
