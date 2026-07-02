using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using AvaloniaLayout = Avalonia.Layout;
using Brushes = Avalonia.Media.Brushes;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Volume flyout button and overlay content. Handles volume slider, presets,
/// and mute icon state. Exposes BtnVolumeMenu for external focus targeting.
/// </summary>
public partial class VolumeFlyoutControl : AvaloniaUserControl
{
    private MainViewModel? _viewModel;
    private FlyoutManager? _flyoutManager;
    private FlyoutOverlayControl? _overlay;

    public VolumeFlyoutControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>The inner button, exposed for external focus (e.g. MainWindow keyboard routing).</summary>
    public Button BtnVolumeMenu => BtnVolume;

    public FlyoutManager? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            if (value != null)
                value.Register("volume", () => _overlay?.HideContent());
        }
    }

    /// <summary>Updates the volume icon to reflect current mute/volume state.</summary>
    public void RefreshVolumeIcon()
    {
        if (_viewModel == null) return;
        var vol = _viewModel.VolumeValue;
        var muted = _viewModel.IsMuted && vol <= 0;
        if (muted)
        {
            VolumeIcon.Opacity = 0;
            VolumeMuteIcon.Opacity = 1;
        }
        else
        {
            VolumeIcon.Opacity = 1;
            VolumeMuteIcon.Opacity = 0;
        }
    }

    private void OnVolumeOverlayDismissed()
    {
        _flyoutManager?.MarkClosed("volume");
    }

    private void OnVolumeMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        // Ensure overlay is cached and event is subscribed
        if (_overlay == null)
        {
            _overlay = MainWindow.GetOverlay(this);
            if (_overlay != null)
                _overlay.OnBackgroundDismissed += OnVolumeOverlayDismissed;
        }

        if (_overlay == null) return;

        _flyoutManager?.DismissOthers("volume");

        var content = BuildVolumeContent();

        _overlay.ShowContent(BtnVolume, content, placeAbove: true);
    }

    private void OnVolumeButtonScroll(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.Delta.Y > 0)
            _viewModel.IncreaseVolume();
        else if (e.Delta.Y < 0)
            _viewModel.DecreaseVolume();
        e.Handled = true;
    }

    /// <summary>Builds the volume controls overlay content.</summary>
    private Border BuildVolumeContent()
    {
        var stack = new StackPanel
        {
            Width = 200,
            HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            Spacing = 12
        };

        // Volume label row
        var labelRow = new StackPanel
        {
            Orientation = AvaloniaLayout.Orientation.Horizontal,
            Margin = new Thickness(12, 0),
            Spacing = 12
        };

        labelRow.Children.Add(new TextBlock
        {
            Text = "Volume",
            Classes = { "md3-subtitle1" },
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        });

        var volumePercentLabel = new TextBlock
        {
            Name = "VolumePercentLabel",
            Text = _viewModel?.VolumeText ?? "100%",
            Classes = { "md3-body2" },
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };
        labelRow.Children.Add(volumePercentLabel);
        stack.Children.Add(labelRow);

        // Slider
        var slider = new Slider
        {
            Classes = { "compact" },
            Minimum = 0,
            Maximum = _viewModel?.VolumeMax ?? 150,
            Value = _viewModel?.VolumeValue ?? 100,
            Height = 36,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
            HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Stretch
        };
        slider.ValueChanged += (_, args) =>
        {
            if (_viewModel != null)
                _viewModel.VolumeValue = args.NewValue;
            volumePercentLabel.Text = $"{args.NewValue:F0}%";
        };
        stack.Children.Add(slider);

        // Presets row
        var presetsRow = new StackPanel
        {
            Orientation = AvaloniaLayout.Orientation.Horizontal,
            HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            Spacing = 8
        };

        foreach (var value in new[] { 25, 50, 75, 100 })
        {
            var btn = new Button
            {
                Content = $"{value}%",
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                Foreground = global::Avalonia.Application.Current?.FindResource("TextPrimary") as IBrush,
                Padding = new Thickness(12, 9),
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                Classes = { "flyout-item" },
                Cursor = new Cursor(StandardCursorType.Arrow)
            };
            btn.Click += (_, __) =>
            {
                if (_viewModel != null)
                    _viewModel.VolumeValue = value;
            };
            presetsRow.Children.Add(btn);
        }
        stack.Children.Add(presetsRow);

        // Wrap in Border with flyout styling
        var border = new Border
        {
            Background = global::Avalonia.Application.Current?.FindResource("PopoverBackground") as IBrush,
            BorderBrush = global::Avalonia.Application.Current?.FindResource("PopoverBorder") as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(0),
            Child = stack
        };

        return border;
    }
}
