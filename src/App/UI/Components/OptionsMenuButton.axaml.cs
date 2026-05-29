using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia.Components;

public partial class OptionsMenuButton : global::Avalonia.Controls.UserControl
{
    public OptionsMenuButton()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnResetAllClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ResetAllOptions();
    }

    private void OnResetContrastClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetContrast();
    private void OnResetBrightnessClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetBrightness();
    private void OnResetGammaClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetGamma();
    private void OnResetSaturationClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetSaturation();
    private void OnResetHueClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetHue();
    private void OnResetSubtitleDelayClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetSubtitleDelay();
    private void OnResetAudioDelayClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetAudioDelay();
    private void OnResetSpeedClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetSpeed();
    private void OnScreenshotClick(object? sender, RoutedEventArgs e) => ViewModel?.Screenshot();

    // --- Aspect Ratio ---
    private void OnAspectResetClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetAspectRatio();

    private void OnAspectRatioChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item && item.Tag is string tag)
        {
            if (double.TryParse(tag, out var ratio))
                ViewModel?.SetAspectRatio(ratio);
        }
    }

    // --- Rotate ---
    private void OnRotateResetClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetRotation();
    private void OnRotateLeftClick(object? sender, RoutedEventArgs e) => ViewModel?.RotateLeft();
    private void OnRotateRightClick(object? sender, RoutedEventArgs e) => ViewModel?.RotateRight();

    // --- Flip ---
    private void OnFlipResetClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetFlip();
    private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e) => ViewModel?.FlipHorizontal();
    private void OnFlipVerticalClick(object? sender, RoutedEventArgs e) => ViewModel?.FlipVertical();

    // --- Zoom ---
    private void OnZoomResetClick(object? sender, RoutedEventArgs e) => ViewModel?.ResetZoom();
}