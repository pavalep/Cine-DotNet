namespace Simba.Media.Models;

/// <summary>Options that influence decoding behaviour for a session.</summary>
public record DecodingOptions
{
    /// <summary>Hardware decoding preference. Default: Automatic.</summary>
    public HwdecMode HardwareDecoding { get; init; } = HwdecMode.Automatic;

    /// <summary>Whether to prefer high-quality rendering (spline36, deband, etc.).</summary>
    public bool HighQuality { get; init; } = true;
}
