using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.ViewModels;
using Cine.Media.Models;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Control = Avalonia.Controls.Control;
using Cursor = Avalonia.Input.Cursor;
using StandardCursorType = Avalonia.Input.StandardCursorType;
using ScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility;

namespace Cine.Avalonia.Components;

/// <summary>
/// Chapters flyout button and overlay content. Shows a scrollable list of chapters
/// that the user can click to seek to.
/// </summary>
public partial class ChaptersFlyout : AvaloniaUserControl, IFlyoutSource
{
    private MainViewModel? _viewModel;
    private IFlyoutService? _flyoutManager;
    private FlyoutOverlay? _overlay;

    string IFlyoutSource.FlyoutKey => "chapters";
    Control IFlyoutSource.Anchor => BtnChapters;
    bool IFlyoutSource.CanOpen => _viewModel != null && _viewModel.Chapters.Count > 0;
    Border IFlyoutSource.BuildContent() => BuildChaptersContent(_viewModel!.Chapters);

    public ChaptersFlyout()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            if (value != null)
                value.Register("chapters", () => _overlay?.HideContent());
        }
    }

    private void OnChaptersMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.Chapters.Count == 0) return;
        _overlay ??= MainWindow.GetOverlay(this);
        if (_overlay == null) return;

        _flyoutManager?.ShowFlyoutFor(this, _overlay);
    }

    private Border BuildChaptersContent(ObservableCollection<ChapterInfo> chapters)
    {
        var stack = new StackPanel();

        foreach (var chapter in chapters)
        {
            var timeStr = TimeSpan.FromSeconds(chapter.Time).TotalHours >= 1
                ? TimeSpan.FromSeconds(chapter.Time).ToString(@"h\:mm\:ss")
                : TimeSpan.FromSeconds(chapter.Time).ToString(@"mm\:ss");

            var seekTime = chapter.Time;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            var left = new TextBlock
            {
                Text = chapter.Title,
                FontSize = Token.Size("font-size-body2"),
                Foreground = (IBrush?)global::Avalonia.Application.Current?.FindResource("OsdForeground"),
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                Padding = new Thickness(12, 8, 4, 8)
            };
            var right = new TextBlock
            {
                Text = timeStr,
                FontSize = Token.Size("font-size-caption"),
                Foreground = AppColors.TextOnDarkHint,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                Padding = new Thickness(4, 8, 12, 8)
            };
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);

            var btn = new Button
            {
                Content = grid,
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow)
            };
            btn.Click += (_, _) =>
            {
                _viewModel?.SeekTo(seekTime / _viewModel.Duration.TotalSeconds);
            };
            stack.Children.Add(btn);
        }

        var border = new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 220,
            Child = new ScrollViewer
            {
                Content = stack,
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        return border;
    }
}
