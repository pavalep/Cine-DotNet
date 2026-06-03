using Avalonia.Controls;
using Avalonia.Interactivity;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia.Components;

public partial class OptionsMenuButton : global::Avalonia.Controls.UserControl
{
    public OptionsMenuButton()
    {
        InitializeComponent();
        if (BtnOptionsMenu?.Flyout is Flyout flyout)
            flyout.Opened += OnOptionsFlyoutOpened;
    }

    private void OnOptionsFlyoutOpened(object? sender, EventArgs e)
    {
        SyncAspectRatioSelection();
    }

    private void SyncAspectRatioSelection()
    {
        if (ViewModel == null || AspectRatioCombo == null) return;
        var current = ViewModel.AspectRatioValue;
        for (int i = 0; i < AspectRatioCombo.Items.Count; i++)
        {
            if (AspectRatioCombo.Items[i] is ComboBoxItem item &&
                item.Tag is string tag && double.TryParse(tag, out var ratio) &&
                Math.Abs(ratio - current) < 0.001)
            {
                AspectRatioCombo.SelectedIndex = i;
                return;
            }
        }
        AspectRatioCombo.SelectedIndex = 0;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    // --- Tab-level resets ---
    private void OnResetVideoClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ResetContrast();
        ViewModel?.ResetBrightness();
        ViewModel?.ResetGamma();
        ViewModel?.ResetSaturation();
        ViewModel?.ResetHue();
        ViewModel?.ResetRotation();
        ViewModel?.ResetFlip();
        ViewModel?.ResetZoom();
        ViewModel?.ResetAspectRatio();
    }

    private void OnResetAudioClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ResetAudioDelay();
        ViewModel?.ResetSpeed();
    }

    private void OnResetSubtitleClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ResetSubtitleDelay();
        if (ViewModel != null) ViewModel.SubtitleFontSize = 24;
    }

    // --- Aspect Ratio ---
    private void OnAspectRatioChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item && item.Tag is string tag)
        {
            if (double.TryParse(tag, out var ratio))
                ViewModel?.SetAspectRatio(ratio);
        }
    }

    // --- Rotate ---
    private void OnRotateLeftClick(object? sender, RoutedEventArgs e) => ViewModel?.RotateLeft();
    private void OnRotateRightClick(object? sender, RoutedEventArgs e) => ViewModel?.RotateRight();
    private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e) => ViewModel?.FlipHorizontal();
    private void OnFlipVerticalClick(object? sender, RoutedEventArgs e) => ViewModel?.FlipVertical();

    // --- Speed Pills ---
    private void OnSpeedPillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Button btn && btn.Tag is string tag && double.TryParse(tag, out var speed))
            ViewModel?.SetSpeed(speed);
    }

    public void CloseFlyout()
    {
        if (BtnOptionsMenu?.Flyout is Flyout fly && fly.IsOpen)
            fly.Hide();
    }
}
