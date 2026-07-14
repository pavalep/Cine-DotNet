using Simba.Media.Interfaces;
using Simba.Media.Models;

namespace Simba.Media.Codecs;

/// <summary>
/// Codec provider backed by libmpv (MpvPlayer).
/// Supports H.264, H.265/HEVC, VP9, and AV1 with hardware acceleration
/// via mpv's auto-safe hwdec strategy.
/// </summary>
public sealed class MpvCodecProvider : ICodecProvider
{
    private static readonly IReadOnlyList<CodecCapability> Capabilities = new List<CodecCapability>
    {
        // Video codecs
        new() { Codec = "h264", Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "hevc", Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "vp9",  Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "av1",  Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "mpeg2video", Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "vc1",  Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        // Audio codecs
        new() { Codec = "aac",    Type = CodecType.Audio },
        new() { Codec = "mp3",    Type = CodecType.Audio },
        new() { Codec = "opus",   Type = CodecType.Audio },
        new() { Codec = "flac",   Type = CodecType.Audio },
        new() { Codec = "vorbis", Type = CodecType.Audio },
        new() { Codec = "dts",    Type = CodecType.Audio },
        new() { Codec = "truehd", Type = CodecType.Audio },
        new() { Codec = "eac3",   Type = CodecType.Audio },
        new() { Codec = "ac3",    Type = CodecType.Audio },
        // Subtitle codecs
        new() { Codec = "subrip",              Type = CodecType.Subtitle },
        new() { Codec = "ass",                 Type = CodecType.Subtitle },
        new() { Codec = "hdmv_pgs_subtitle",  Type = CodecType.Subtitle },
        new() { Codec = "dvd_subtitle",        Type = CodecType.Subtitle },
        new() { Codec = "mov_text",            Type = CodecType.Subtitle },
        new() { Codec = "webvtt",             Type = CodecType.Subtitle },
    };

    public string Name => "MPV";

    /// <summary>MPV is always available (bundled with the application).</summary>
    public bool IsAvailable => true;

    public IReadOnlyList<CodecCapability> GetCapabilities() => Capabilities;

    /// <summary>
    /// Configure the player for hardware-accelerated decoding.
    /// Sets UseSoftwareRendering = false and HighQualityRendering = true.
    /// </summary>
    public void Configure(IMediaPlayer player)
    {
        if (player is Implementations.MpvPlayer mpv)
        {
            mpv.UseSoftwareRendering = false;
            mpv.HighQualityRendering = true;
        }
    }
}
