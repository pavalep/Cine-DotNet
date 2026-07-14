using Simba.Media.Interfaces;
using Simba.Media.Models;

namespace Simba.Media.Codecs;

/// <summary>
/// Software-only codec provider that forces CPU-based decoding.
/// Used as a last-resort fallback when no hardware-accelerated provider is available.
/// Wraps MpvPlayer with UseSoftwareRendering = true.
/// </summary>
public sealed class SoftwareFallbackCodecProvider : ICodecProvider
{
    private static readonly IReadOnlyList<CodecCapability> Capabilities = new List<CodecCapability>
    {
        // Video — software only, no hwdec
        new() { Codec = "h264", Type = CodecType.Video, SupportsHardwareDecoding = false },
        new() { Codec = "hevc", Type = CodecType.Video, SupportsHardwareDecoding = false },
        new() { Codec = "vp9",  Type = CodecType.Video, SupportsHardwareDecoding = false },
        new() { Codec = "av1",  Type = CodecType.Video, SupportsHardwareDecoding = false },
        new() { Codec = "mpeg2video", Type = CodecType.Video, SupportsHardwareDecoding = false },
        new() { Codec = "vc1",  Type = CodecType.Video, SupportsHardwareDecoding = false },
        // Audio
        new() { Codec = "aac",    Type = CodecType.Audio },
        new() { Codec = "mp3",    Type = CodecType.Audio },
        new() { Codec = "opus",   Type = CodecType.Audio },
        new() { Codec = "flac",   Type = CodecType.Audio },
        new() { Codec = "dts",    Type = CodecType.Audio },
        new() { Codec = "truehd", Type = CodecType.Audio },
        // Subtitles
        new() { Codec = "subrip",              Type = CodecType.Subtitle },
        new() { Codec = "ass",                 Type = CodecType.Subtitle },
        new() { Codec = "hdmv_pgs_subtitle",  Type = CodecType.Subtitle },
        new() { Codec = "dvd_subtitle",        Type = CodecType.Subtitle },
    };

    public string Name => "Software Fallback";

    /// <summary>Always available as a last resort.</summary>
    public bool IsAvailable => true;

    public IReadOnlyList<CodecCapability> GetCapabilities() => Capabilities;

    /// <summary>
    /// Force software rendering to disable hardware acceleration.
    /// Also uses lower-quality rendering to reduce CPU load.
    /// </summary>
    public void Configure(IMediaPlayer player)
    {
        if (player is Implementations.MpvPlayer mpv)
        {
            mpv.UseSoftwareRendering = true;
            mpv.HighQualityRendering = false;
        }
    }
}
