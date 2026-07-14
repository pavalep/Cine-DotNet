namespace Simba.Media.Models;

/// <summary>
/// Represents a video track from the media file.
/// </summary>
public class VideoTrackInfo
{
    /// <summary>Track ID (maps to mpv's id field).</summary>
    public int Id { get; set; }

    /// <summary>Display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Codec name (e.g. "h264", "hevc", "vp9").</summary>
    public string Codec { get; set; } = string.Empty;

    /// <summary>Video width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Video height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Frame rate (e.g. 24, 30, 60).</summary>
    public double Fps { get; set; }

    /// <summary>Whether this track is currently selected/active.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Whether this is the default track.</summary>
    public bool IsDefault { get; set; }

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(Title)) return Title;
        var resolution = Width > 0 && Height > 0 ? $" ({Width}x{Height})" : "";
        return $"Video Track {Id}{resolution}";
    }
}
