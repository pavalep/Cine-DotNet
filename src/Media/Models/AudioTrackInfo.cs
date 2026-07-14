namespace Simba.Media.Models;

/// <summary>
/// Represents an audio track from the media file.
/// </summary>
public class AudioTrackInfo
{
    /// <summary>Track ID (maps to mpv's id field).</summary>
    public int Id { get; set; }

    /// <summary>Language code (e.g. "en", "ja", "und").</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Display title (e.g. "English 5.1", "Japanese Stereo").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Codec name (e.g. "aac", "opus", "flac").</summary>
    public string Codec { get; set; } = string.Empty;

    /// <summary>Number of channels (2 = stereo, 6 = 5.1, etc.).</summary>
    public int Channels { get; set; }

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate { get; set; }

    /// <summary>Whether this track is currently selected/active.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Whether this is the default track.</summary>
    public bool IsDefault { get; set; }

    public override string ToString()
    {
        var label = !string.IsNullOrWhiteSpace(Language) ? Language : Title;
        if (string.IsNullOrWhiteSpace(label)) label = $"Track {Id}";
        if (Channels > 0) label += $" ({Channels}ch)";
        return label;
    }
}
