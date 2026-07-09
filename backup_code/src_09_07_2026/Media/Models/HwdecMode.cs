namespace Cine.Media.Models;

/// <summary>
/// Hardware decoding mode - matches Python's hwdec automatic, direct3d11va options
/// </summary>
public enum HwdecMode
{
    /// <summary>
    /// Automatic hardware decoding - matches Python's hwdc=auto (default)
    /// </summary>
    Automatic,

    /// <summary>
    /// Force Direct3D11VA hardware decoding - matches Python's hwdec=d3d11va
    /// </summary>
    Direct3D11VA
}
