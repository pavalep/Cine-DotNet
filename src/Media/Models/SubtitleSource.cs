namespace Cine.Media.Models;

/// <summary>
/// Subtitle source information - matches Python's sub files and embedded subtitles
/// </summary>
public class SubtitleSource
{
    /// <summary>
    /// Subtitle file path or embedded track id
    /// </summary>
    public string PathOrId { get; set; } = string.Empty;

    /// <summary>
    /// Subtitle language code (e.g., "en", "es", "fr")
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Whether subtitle track is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// True if this track is marked as "forced" by mpv.
    /// Forced subtitles (e.g., foreign dialogue in an otherwise-dubbed track)
    /// should be auto-enabled even when subtitles are globally disabled.
    /// </summary>
    public bool IsForced { get; set; }

    /// <summary>
    /// Subtitle type (file, embedded, external)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Codec name from mpv (e.g., "subrip", "hdmv_pgs_subtitle", "dvd_subtitle", "ass").
    /// Empty string if unknown.
    /// </summary>
    public string Codec { get; set; } = string.Empty;

    /// <summary>
    /// True if this is a bitmap-based subtitle (PGS, VOBSUB, etc.) that cannot be styled.
    /// </summary>
    public bool IsBitmap =>
        !string.IsNullOrWhiteSpace(Codec) &&
        (Codec.Contains("pgs") || Codec.Contains("hdmv") || Codec.Contains("dvd_sub") || Codec.Contains("vobsub") || Codec.Contains("dvb"));

    /// <summary>
    /// Returns subtitle info as formatted string
    /// </summary>
    /// <returns>Formatted subtitle string</returns>
    public override string ToString()
    {
        return $"{Type}: {Language} ({(IsEnabled ? "enabled" : "disabled")})";
    }
}
