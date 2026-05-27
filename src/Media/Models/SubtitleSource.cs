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
    /// Whether subtitle is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Subtitle type (file, embedded, external)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Returns subtitle info as formatted string
    /// </summary>
    /// <returns>Formatted subtitle string</returns>
    public override string ToString()
    {
        return $"{Type}: {Language} ({(IsEnabled ? "enabled" : "disabled")})";
    }
}
