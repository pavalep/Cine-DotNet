using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class VolumePanel : UserControl
{
    public Slider VolumeSliderControl => VolumeSlider;

    public event EventHandler<RoutedEventArgs>? MuteClicked;
    public event EventHandler<RoutedEventArgs>? Volume25Clicked;
    public event EventHandler<RoutedEventArgs>? Volume50Clicked;
    public event EventHandler<RoutedEventArgs>? Volume100Clicked;

    public VolumePanel()
    {
        InitializeComponent();
    }

    private void OnToggleMute(object? sender, RoutedEventArgs e)
    {
        MuteClicked?.Invoke(sender, e);
    }

    private void OnPresetVolume25(object? sender, RoutedEventArgs e)
    {
        Volume25Clicked?.Invoke(sender, e);
    }

    private void OnPresetVolume50(object? sender, RoutedEventArgs e)
    {
        Volume50Clicked?.Invoke(sender, e);
    }

    private void OnPresetVolume100(object? sender, RoutedEventArgs e)
    {
        Volume100Clicked?.Invoke(sender, e);
    }
}
