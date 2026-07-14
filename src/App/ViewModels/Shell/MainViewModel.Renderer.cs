using Simba.Avalonia.Services;

namespace Simba.Avalonia.ViewModels;

/// <summary>
/// Renderer mode switching: Auto (D3D11 hardware) vs Software.
/// Delegates state to <see cref="IRendererService"/> for testability.
/// </summary>
public partial class MainViewModel
{
    /// <summary>Renderer service — public for XAML binding access via wrapper properties.</summary>
    public IRendererService Renderer { get; }

    public RendererType RendererMode
    {
        get => Renderer.RendererMode;
        set
        {
            if (Renderer.RendererMode == value) return;
            Renderer.RendererMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHardwareAccelerationEnabled));
        }
    }

    public bool IsHardwareAccelerationEnabled
    {
        get => Renderer.IsHardwareAccelerationEnabled;
        set => RendererMode = value ? RendererType.Auto : RendererType.Software;
    }
}
