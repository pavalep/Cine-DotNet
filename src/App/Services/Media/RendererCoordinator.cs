namespace Simba.Avalonia.Services;

/// <summary>
/// Coordinates renderer mode switching: Auto (D3D11 hardware) vs Software.
/// </summary>
public class RendererCoordinator : IRendererService
{
    private RendererType _rendererMode;

    /// <inheritdoc/>
    public RendererType RendererMode
    {
        get => _rendererMode;
        set => _rendererMode = value;
    }

    /// <inheritdoc/>
    public bool IsHardwareAccelerationEnabled
    {
        get => _rendererMode == RendererType.Auto;
        set => RendererMode = value ? RendererType.Auto : RendererType.Software;
    }
}
