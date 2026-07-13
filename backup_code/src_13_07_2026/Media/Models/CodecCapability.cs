namespace Cine.Media.Models;

/// <summary>Describes a single codec capability of a provider.</summary>
public record CodecCapability
{
    /// <summary>Codec name (e.g. "h264", "hevc", "vp9", "av1", "aac", "flac", "dts", "subrip").</summary>
    public required string Codec { get; init; }

    /// <summary>Whether this is video, audio, or subtitle.</summary>
    public CodecType Type { get; init; }

    /// <summary>Whether hardware-accelerated decoding is supported for this codec.</summary>
    public bool SupportsHardwareDecoding { get; init; }

    /// <summary>Maximum vertical resolution supported (e.g. 4320 for 8K). 0 = unknown/unlimited.</summary>
    public int MaxResolution { get; init; }
}
