namespace Simba.Avalonia.Services;

/// <summary>Renderer mode: Auto (D3D11 hardware) vs Software.</summary>
public enum RendererType { Auto, Software }

/// <summary>
/// Manages renderer mode switching for the media player.
/// </summary>
public interface IRendererService
{
    /// <summary>Current renderer mode.</summary>
    RendererType RendererMode { get; set; }

    /// <summary>True when hardware acceleration (Auto) is enabled.</summary>
    bool IsHardwareAccelerationEnabled { get; set; }
}
