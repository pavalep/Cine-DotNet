namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Renderer mode switching: Auto (D3D11 hardware) vs Software.
/// </summary>
public partial class MainViewModel
{
    public enum RendererType { Auto, Software }

    private RendererType _rendererMode;

    public RendererType RendererMode
    {
        get => _rendererMode;
        set
        {
            if (_rendererMode == value) return;
            _rendererMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHardwareAccelerationEnabled));
        }
    }

    public bool IsHardwareAccelerationEnabled
    {
        get => _rendererMode == RendererType.Auto;
        set => RendererMode = value ? RendererType.Auto : RendererType.Software;
    }
}
