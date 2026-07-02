using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Models;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using Control = Avalonia.Controls.Control;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Components;

/// <summary>
/// Video track selector that renders a video-track button and shows a track
/// selection popover on click. Uses the FlyoutOverlay for reliable overlay
/// behaviour and implements <see cref="IFlyoutSource"/> for standardised
/// flyout lifecycle management.
/// </summary>
public partial class VideoTrackSelector : AvaloniaUserControl, IFlyoutSource
{
    private MainViewModel? _viewModel;
    private IFlyoutService? _flyoutManager;
    private FlyoutOverlay? _overlay;

    string IFlyoutSource.FlyoutKey => "video-menu";
    Control IFlyoutSource.Anchor => BtnVideoMenu;
    bool IFlyoutSource.CanOpen => _viewModel?.VideoTracks?.Any(t => !t.IsPseudoEntry) == true;
    Border IFlyoutSource.BuildContent() => BuildTrackMenuContent(_viewModel?.VideoTracks);

    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            value?.Register("video-menu", () => _overlay?.HideContent());
        }
    }

    public VideoTrackSelector()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>
    /// Updates the button state based on available video tracks.
    /// Called by the parent control (ControlsBoxControl) during responsive layout.
    /// </summary>
    public void UpdateState(bool hasMultipleVideoTracks)
    {
        if (hasMultipleVideoTracks)
        {
            BtnVideoMenu.IsEnabled = true;
            BtnVideoMenu.Opacity = 1;
            ToolTip.SetTip(BtnVideoMenu, "Switch video track");
        }
        else
        {
            BtnVideoMenu.IsEnabled = false;
            BtnVideoMenu.Opacity = 0.4;
            ToolTip.SetTip(BtnVideoMenu, "Single video track — no switching available");
        }
    }

    private void OnVideoMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _overlay ??= MainWindow.GetOverlay(this);
        if (_overlay == null) return;

        _flyoutManager?.ShowFlyoutFor(this, _overlay);
    }

    private Border BuildTrackMenuContent(ObservableCollection<TrackMenuItem>? tracks)
    {
        var stackPanel = new global::Avalonia.Controls.StackPanel();

        if (tracks == null || tracks.Count == 0)
        {
            var text = new TextBlock
            {
                Text = "No tracks available",
                FontSize = Token.Size("font-size-body1"),
                Foreground = AppColors.TextTertiary,
                Padding = new Thickness(12, 8)
            };
            stackPanel.Children.Add(text);
        }
        else
        {
            // Safety: if no real tracks, show fallback message
            var hasRealTracks = tracks.Any(t => !t.IsPseudoEntry);
            if (!hasRealTracks)
            {
                var text = new TextBlock
                {
                    Text = "No tracks available",
                    FontSize = Token.Size("font-size-body1"),
                    Foreground = AppColors.TextTertiary,
                    Padding = new Thickness(12, 8)
                };
                stackPanel.Children.Add(text);
            }

            foreach (var track in tracks)
            {
                var dot = new Border
                {
                    Width = 6, Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = track.IsSelected && !track.IsPseudoEntry
                        ? AppColors.Accent
                        : AppColors.IconDim,
                    VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var text = new TextBlock
                {
                    Text = track.DisplayName,
                    FontWeight = track.IsSelected ? FontWeight.SemiBold : FontWeight.Normal,
                    FontSize = Token.Size("font-size-body1"),
                    Foreground = AppColors.TextPrimary
                };

                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star)
                    }
                };
                grid.Children.Add(dot);
                grid.Children.Add(text);
                Grid.SetColumn(text, 1);

                var button = new Button
                {
                    Content = grid,
                    Background = AppColors.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 8),
                    HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                    Cursor = new Cursor(StandardCursorType.Arrow),
                    Opacity = track.DisplayOpacity
                };

                button.Click += (_, _) =>
                {
                    if (track.SelectCommand.CanExecute(track))
                        track.SelectCommand.Execute(track);
                    _flyoutManager?.CloseAll();
                };

                button.PointerEntered += (_, _) =>
                    button.Background = AppColors.HoverSubtle;
                button.PointerExited += (_, _) =>
                    button.Background = AppColors.Transparent;

                stackPanel.Children.Add(button);
            }
        }

        var scroll = new ScrollViewer
        {
            MaxHeight = 320,
            Content = stackPanel
        };

        var border = new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 180,
            Child = scroll
        };

        return border;
    }
}
