using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Layout = Avalonia.Layout;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Controls;

public partial class HeaderBarControl : AvaloniaUserControl
{
    public event EventHandler? PipToggled;

    private MainViewModel? _viewModel;
    private int _activeFlyouts;

    public HeaderBarControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    public void SetTitle(string title)
    {
        TitleText.Text = title;
    }

    public void SetBarVisibility(bool visible)
    {
        HeaderBar.IsVisible = visible;
        HeaderBar.Opacity = visible ? 1 : 0;
    }

    public void ShowOpenMenu()
    {
        BtnOpenMenu.IsVisible = true;
    }

    public void HideOpenMenu()
    {
        BtnOpenMenu.IsVisible = false;
    }

    public void SetPipChecked(bool isChecked)
    {
        BtnPip.IsChecked = isChecked;
    }

    public void ShowFullscreenClose()
    {
        BtnFullscreenClose.IsVisible = true;
    }

    public void HideFullscreenClose()
    {
        BtnFullscreenClose.IsVisible = false;
    }

    public void ShowPrimaryMenu()
    {
        BtnPrimaryMenu.IsVisible = true;
    }

    public void HidePrimaryMenu()
    {
        BtnPrimaryMenu.IsVisible = false;
    }

    public void ShowWindowControls()
    {
        WindowControlsPanel.IsVisible = true;
    }

    public void HideWindowControls()
    {
        WindowControlsPanel.IsVisible = false;
    }

    public void UpdateMaximizeIcon(bool isMaximized)
    {
        if (isMaximized)
        {
            MaximizeRestoreIconPath.Kind = Material.Icons.MaterialIconKind.WindowRestore;
            ToolTip.SetTip(BtnMaximizeRestore, "Restore");
        }
        else
        {
            MaximizeRestoreIconPath.Kind = Material.Icons.MaterialIconKind.WindowMaximize;
            ToolTip.SetTip(BtnMaximizeRestore, "Maximize");
        }
    }

    public void SetPipVisibility(bool visible)
    {
        BtnPip.IsVisible = visible;
    }

    public void TrackFlyoutOpened(object? sender, EventArgs e)
    {
        _activeFlyouts++;

        // P5.4: When Open menu flyout opens, add recent files dynamically
        if (sender is Flyout flyout)
        {
            UpdateOpenMenuRecentFiles(flyout);
        }
    }

    private void UpdateOpenMenuRecentFiles(Flyout flyout)
    {
        try
        {
            if (flyout.Content is not Border border) return;
            if (border.Child is not StackPanel stack) return;
            if (_viewModel == null || !_viewModel.HasRecentFiles) return;

            var app = global::Avalonia.Application.Current;

            // Remove old recent files section
            var sepIndex = -1;
            for (int i = stack.Children.Count - 1; i >= 0; i--)
            {
                if (stack.Children[i] is TextBlock tb && tb.Text == "Recent Files")
                {
                    sepIndex = i;
                    break;
                }
            }
            if (sepIndex >= 0)
            {
                while (stack.Children.Count > sepIndex)
                    stack.Children.RemoveAt(stack.Children.Count - 1);
            }

            if (_viewModel.RecentFiles.Count == 0) return;

            // Add separator and recent files section
            stack.Children.Add(new Separator
            {
                Background = (IBrush?)app?.FindResource("PopoverBorder"),
                Margin = new Thickness(4, 2)
            });

            var header = new TextBlock
            {
                Text = "Recent Files",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = (IBrush?)app?.FindResource("OsdForeground"),
                Opacity = 0.5,
                Margin = new Thickness(12, 5, 0, 2)
            };
            stack.Children.Add(header);

            foreach (var file in _viewModel.RecentFiles.Take(10))
            {
                if (!System.IO.File.Exists(file)) continue;

                var fileName = System.IO.Path.GetFileName(file);
                var recentBtn = new global::Avalonia.Controls.Button
                {
                    Classes = { "flyout-item" },
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions
                        {
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Star)
                        },
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "📄",
                                FontSize = 12,
                                VerticalAlignment = Layout.VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = fileName,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                FontSize = 12,
                                Foreground = (IBrush?)app?.FindResource("OsdForeground"),
                                Margin = new Thickness(10, 0, 0, 0),
                                VerticalAlignment = Layout.VerticalAlignment.Center
                            }
                        }
                    },
                    HorizontalContentAlignment = Layout.HorizontalAlignment.Stretch,
                    Tag = file
                };
                Grid.SetColumn((TextBlock)((Grid)recentBtn.Content).Children[1], 1);

                recentBtn.Click += (_, _) =>
                {
                    if (recentBtn.Tag is string path && _viewModel != null)
                        _viewModel.OpenFile(path);
                    flyout.Hide();
                };
                stack.Children.Add(recentBtn);
            }
        }
        catch { }
    }

    public void TrackFlyoutClosed(object? sender, EventArgs e)
    {
        _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
    }

    public bool HasActiveFlyouts => _activeFlyouts > 0;

    public void CloseOpenFlyouts()
    {
        if (BtnOpenMenu?.Flyout is Flyout of)
            of.Hide();
        if (BtnPrimaryMenu?.Flyout is Flyout pf)
            pf.Hide();
    }

    // --- Window-level operations ---

    private Window? GetParentWindow() => TopLevel.GetTopLevel(this) as Window;

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w != null) w.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w == null) return;
        w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        GetParentWindow()?.Close();
    }

    private void OnFullscreenCloseClick(object? sender, RoutedEventArgs e)
    {
        GetParentWindow()?.Close();
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var w = GetParentWindow();
            if (w != null) w.BeginMoveDrag(e);
        }
    }

    // --- PIP ---

    private void OnTogglePip(object? sender, RoutedEventArgs e)
    {
        PipToggled?.Invoke(this, EventArgs.Empty);
    }

    // --- Primary menu handlers ---

    private void OnPlayPause(object? sender, RoutedEventArgs e) => _viewModel?.PlayPause();
    private void OnStop(object? sender, RoutedEventArgs e) => _viewModel?.Stop();
    private void OnSeekBackward(object? sender, RoutedEventArgs e) => _viewModel?.SeekBackward();
    private void OnSeekForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekForward();
    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();
    private void OnToggleAlwaysOnTop(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w != null)
        {
            w.Topmost = !w.Topmost;
        }
    }
    private void OnToggleLoopFile(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopFile();
    private void OnToggleLoopPlaylist(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopPlaylist();
    private void OnToggleShuffle(object? sender, RoutedEventArgs e) => _viewModel?.ToggleShuffle();
    private void OnShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w != null)
        {
            var dlg = new ShortcutsDialog { DataContext = _viewModel };
            dlg.Show(w);
        }
    }
    private void OnPreferencesClick(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w != null)
        {
            var dlg = new PreferencesDialog { DataContext = _viewModel };
            dlg.Show(w);
        }
    }
    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w != null)
        {
            var dlg = new AboutDialog { DataContext = _viewModel };
            dlg.Show(w);
        }
    }
}

