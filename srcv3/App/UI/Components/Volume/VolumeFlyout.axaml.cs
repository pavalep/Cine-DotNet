using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Application = Avalonia.Application;
using Control = Avalonia.Controls.Control;

namespace Cine.Avalonia.Components;

/// <summary>
/// Volume flyout control that renders a volume button and shows a volume slider
/// popover on click. Uses the FlyoutOverlay for reliable overlay behavior.
/// </summary>
public partial class VolumeFlyout : AvaloniaUserControl, IFlyoutSource
{
    private MainViewModel? _viewModel;
    private IFlyoutService? _flyoutManager;
    private FlyoutOverlay? _overlay;
    private bool _isFlyoutOpen;
    private global::Avalonia.Controls.Slider? _volumeSlider;

    string IFlyoutSource.FlyoutKey => "volume";
    Control IFlyoutSource.Anchor => BtnVolume;
    bool IFlyoutSource.CanOpen => _viewModel != null;
    Border IFlyoutSource.BuildContent() => BuildVolumeContent();
    void IFlyoutSource.OnDismissed() => _isFlyoutOpen = false;

    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            value?.Register("volume", () => HideFlyout());
        }
    }

    public VolumeFlyout()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        UpdateVolumeIcon();
    }

    /// <summary>Refreshes the volume icon to reflect current mute/level state.</summary>
    public void RefreshIcon()
    {
        UpdateVolumeIcon();
    }

    /// <summary>Closes the volume flyout if it is currently open.</summary>
    public void HideFlyout()
    {
        _isFlyoutOpen = false;
        _overlay?.HideContent();
    }

    private void OnVolumeClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (_isFlyoutOpen)
        {
            HideFlyout();
            return;
        }

        _overlay ??= MainWindow.GetOverlay(this);
        if (_overlay == null) return;

        _isFlyoutOpen = true;
        _flyoutManager?.ShowFlyoutFor(this, _overlay);
    }

    private Border BuildVolumeContent()
    {
        if (_viewModel == null) return new Border();

        _volumeSlider = new global::Avalonia.Controls.Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = _viewModel.Volume,
            Width = 120,
            Height = 120,
            Orientation = global::Avalonia.Layout.Orientation.Vertical,
            IsDirectionReversed = true
        };

        _volumeSlider.ValueChanged += (_, _) =>
        {
            if (_viewModel != null)
                _viewModel.Volume = (int)_volumeSlider.Value;
        };

        var icon = new global::Material.Icons.Avalonia.MaterialIcon
        {
            Kind = GetVolumeIconKind((int)_viewModel.Volume, _viewModel.IsMuted),
            Width = 18,
            Height = 18,
            Foreground = (IBrush?)Application.Current?.FindResource("OsdForeground")
        };

        // Update icon when volume or mute state changes
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.Volume) or nameof(MainViewModel.IsMuted))
            {
                icon.Kind = GetVolumeIconKind((int)_viewModel.Volume, _viewModel.IsMuted);
            }
        };

        var stack = new global::Avalonia.Controls.StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            Children = { icon, _volumeSlider }
        };

        var border = new Border
        {
            Background = (IBrush?)Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8),
            Child = stack
        };

        return border;
    }

    private void UpdateVolumeIcon()
    {
        if (_viewModel == null) return;
        if (VolumeIcon == null) return;
        VolumeIcon.Kind = GetVolumeIconKind((int)_viewModel.Volume, _viewModel.IsMuted);
    }

    private static global::Material.Icons.MaterialIconKind GetVolumeIconKind(int volume, bool isMuted)
    {
        if (isMuted) return global::Material.Icons.MaterialIconKind.VolumeOff;
        if (volume == 0) return global::Material.Icons.MaterialIconKind.VolumeMute;
        if (volume <= 33) return global::Material.Icons.MaterialIconKind.VolumeLow;
        if (volume <= 66) return global::Material.Icons.MaterialIconKind.VolumeMedium;
        return global::Material.Icons.MaterialIconKind.VolumeHigh;
    }
}
