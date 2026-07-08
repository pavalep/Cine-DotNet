using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Media.Codecs;

/// <summary>
/// Codec provider backed by Windows Media Foundation (MediaFoundationPlayer).
/// Supports H.264 and basic HEVC via system codecs with D3D11VA acceleration.
/// </summary>
public sealed class MFCodecProvider : ICodecProvider
{
    private static readonly IReadOnlyList<CodecCapability> Capabilities = new List<CodecCapability>
    {
        // Video codecs — limited by system MF codec availability
        new() { Codec = "h264", Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "hevc", Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 4320 },
        new() { Codec = "mpeg2video", Type = CodecType.Video, SupportsHardwareDecoding = true, MaxResolution = 2160 },
        // Audio codecs
        new() { Codec = "aac",  Type = CodecType.Audio },
        new() { Codec = "mp3",  Type = CodecType.Audio },
        new() { Codec = "ac3",  Type = CodecType.Audio },
        new() { Codec = "eac3", Type = CodecType.Audio },
        new() { Codec = "flac", Type = CodecType.Audio },
        // Subtitle — MF does not expose embedded subtitle parsing
    };

    public string Name => "MediaFoundation";

    /// <summary>
    /// MediaFoundation is available on Windows 8+.
    /// We check via the OS version (all Windows 10/11 builds support MF).
    /// </summary>
    public bool IsAvailable => OperatingSystem.IsWindowsVersionAtLeast(6, 2);

    public IReadOnlyList<CodecCapability> GetCapabilities() => Capabilities;

    /// <summary>
    /// Enable hardware decoding via D3D11VA for the MediaFoundation player.
    /// </summary>
    public void Configure(IMediaPlayer player)
    {
        if (player is Implementations.MediaFoundationPlayer mf)
        {
            mf.HardwareDecoding = HwdecMode.Direct3D11VA;
        }
    }
}
