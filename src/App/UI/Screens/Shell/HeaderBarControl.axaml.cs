using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.Builders;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Cine.Core;
using Layout = Avalonia.Layout;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Controls;

public partial class HeaderBarControl : AvaloniaUserControl
{
    public event EventHandler? PipToggled;

    private MainViewModel? _viewModel;
    private FlyoutManager? _flyoutManager;
    private readonly List<global::Avalonia.Controls.Primitives.FlyoutBase> _trackedFlyouts = new();
    private PrimaryMenuBuilder? _primaryMenuBuilder;

    public HeaderBarControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Build primary menu from shared builder
        _primaryMenuBuilder = BuildPrimaryMenu();
        BtnPrimaryMenu.Flyout = _primaryMenuBuilder.Build();

        // Sync menu checkmarks when the primary menu opens
        BtnPrimaryMenu.Flyout.Opened += (_, _) =>
        {
            TrackFlyoutOpened(null, EventArgs.Empty);
            _primaryMenuBuilder?.SyncCheckStates();
        };
        BtnPrimaryMenu.Flyout.Closed += TrackFlyoutClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>Builds the shared primary menu using PrimaryMenuBuilder.</summary>
    private PrimaryMenuBuilder BuildPrimaryMenu()
    {
        var builder = new PrimaryMenuBuilder();
        builder
            .AddSection("PLAYBACK")
            .AddItem("Play", "Play / Pause", "Space", () => _viewModel?.PlayPause())
            .AddItem("Stop", "Stop", "Ctrl+S", () => _viewModel?.Stop())
            .AddItem("SkipPrevious", "Seek -10s", "Left", () => _viewModel?.SeekBackward())
            .AddItem("SkipNext", "Seek +10s", "Right", () => _viewModel?.SeekForward())
            .AddSeparator()
            .AddSection("VIEW")
            .AddToggleItem("Fullscreen", "Fullscreen", "F",
                () => _viewModel?.ToggleFullscreen(),
                () => _viewModel?.IsFullscreen ?? false)
            .AddItem("PictureInPictureBottomRight", "Picture in Picture", "Ctrl+Shift+P", () => PipToggled?.Invoke(this, EventArgs.Empty))
            .AddItem("PinOutline", "Always on Top", null, () =>
            {
                var w = GetParentWindow();
                if (w != null) w.Topmost = !w.Topmost;
            })
            .AddSeparator()
            .AddSection("LOOP")
            .AddToggleItem("RepeatOnce", "Loop File", "L",
                () => _viewModel?.ToggleLoopFile(),
                () => _viewModel?.IsLoopFileEnabled ?? false)
            .AddToggleItem("Repeat", "Loop Playlist", "Ctrl+I",
                () => _viewModel?.ToggleLoopPlaylist(),
                () => _viewModel?.IsLoopPlaylistEnabled ?? false)
            .AddToggleItem("ShuffleVariant", "Shuffle", "H",
                () => _viewModel?.ToggleShuffle(),
                () => _viewModel?.IsShuffleEnabled ?? false)
            .AddSeparator()
            .AddSection("TOOLS")
            .AddItem("ClockOutline", "Go to Time…", "Ctrl+G", () =>
            {
                var w = GetParentWindow();
                if (w != null && _viewModel != null) new GoToTimeDialog { DataContext = _viewModel }.Show(w);
            })
            .AddItem("Keyboard", "Keyboard Shortcuts", null, () =>
            {
                var w = GetParentWindow();
                if (w != null) new KeyboardShortcutsDialog().Show(w);
            })
            .AddItem("Cog", "Preferences", null, () =>
            {
                var w = GetParentWindow();
                if (w != null) new PreferencesDialog { DataContext = _viewModel }.Show(w);
            })
            .AddItem("Information", "About Cine", null, () =>
            {
                var w = GetParentWindow();
                if (w != null) new AboutDialog { DataContext = _viewModel }.Show(w);
            });
        return builder;
    }

    // P12: Expose inner HeaderBar Border for overlay hover tracking
    public global::Avalonia.Controls.Border HeaderBarElement => HeaderBar;

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

    /// <summary>
    /// Flyout ecosystem manager. Registers the Open and Primary menus for mutual exclusion.
    /// </summary>
    public FlyoutManager? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            value?.Register("open-menu", () => BtnOpenMenu.Flyout?.Hide());
            value?.Register("primary-menu", () => BtnPrimaryMenu.Flyout?.Hide());

            // Wire Opened/Closed to dismiss others and track state
            if (BtnOpenMenu.Flyout != null)
            {
                BtnOpenMenu.Flyout.Opened += (_, _) => value?.DismissOthers("open-menu");
                BtnOpenMenu.Flyout.Closed += (_, _) => value?.MarkClosed("open-menu");
            }
            if (BtnPrimaryMenu.Flyout != null)
            {
                BtnPrimaryMenu.Flyout.Opened += (_, _) => value?.DismissOthers("primary-menu");
                BtnPrimaryMenu.Flyout.Closed += (_, _) => value?.MarkClosed("primary-menu");
            }
        }
    }

    /// Force-close the Open menu Flyout. Required by Avalonia #18969:
    /// StorageProvider native dialogs freeze Windows if a Flyout is still open.
    /// Must be called BEFORE any StorageProvider dialog (OpenFilePicker etc.).
    /// </summary>
    public void CloseFlyout()
    {
        BtnOpenMenu.Flyout?.Hide();
    }

    /// <summary>
    /// Reopens the Open menu Flyout (call after dialog completes).
    /// Part of the close → dialog → reopen cycle for Avalonia #18969.
    /// </summary>
    public void ReopenFlyout()
    {
        BtnOpenMenu.Flyout?.ShowAt(BtnOpenMenu);
    }

    public void TrackFlyoutOpened(object? sender, EventArgs e)
    {
        if (sender is Flyout flyout)
        {
            if (!_trackedFlyouts.Contains(flyout))
                _trackedFlyouts.Add(flyout);

            // P5.4: When Open menu flyout opens, add recent files dynamically
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
                Margin = new Thickness(8, 4)
            });

            var header = new TextBlock
            {
                Text = "Recent Files",
                FontSize = Token.Size("font-size-caption"),
                FontWeight = FontWeight.SemiBold,
                Foreground = (IBrush?)app?.FindResource("OsdForeground"),
                Opacity = 0.5,
                Margin = new Thickness(12, 4, 0, 4)
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
                                FontSize = Token.Size("font-size-body2"),
                                VerticalAlignment = Layout.VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = fileName,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                FontSize = Token.Size("font-size-body2"),
                                Foreground = (IBrush?)app?.FindResource("OsdForeground"),
                                Margin = new Thickness(12, 0, 0, 0),
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
                        _ = _viewModel.OpenFile(path);
                    flyout.Hide();
                };
                stack.Children.Add(recentBtn);
            }
        }
        catch
        {
            Log.ForContext<HeaderBarControl>().Debug("Failed to load recent files list");
        }
    }

    public void TrackFlyoutClosed(object? sender, EventArgs e)
    {
        // No counter needed — rely on IsOpen check
    }

    public bool HasActiveFlyouts => _trackedFlyouts.Any(f => f.IsOpen);

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
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            var w = GetParentWindow();
            if (w == null) return;

            // Double-click to maximize/restore
            if (e.ClickCount >= 2)
            {
                w.WindowState = w.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            w.BeginMoveDrag(e);
        }
    }

    // --- Responsive layout ---

    /// <summary>
    /// Adjusts header visibility based on window width.
    /// On narrow widths: hide secondary controls to prevent overlap.
    /// </summary>
    public void UpdateResponsiveLayout(double width)
    {
        // Narrow (< 600px): hide PIP button, reduce title max-width
        // Very narrow (< 400px): hide window controls
        bool isNarrow = width < UiConstants.BreakpointCompact;
        bool isVeryNarrow = width < UiConstants.BreakpointTiny;

        if (isNarrow)
        {
            SetVis(BtnPip, false);
            TitleText.MaxWidth = 150;
        }
        else
        {
            TitleText.MaxWidth = 300;
        }

        if (isVeryNarrow)
        {
            TitleText.MaxWidth = 80;
        }
    }

    private static void SetVis(global::Avalonia.Controls.Control? c, bool v) { if (c != null) c.IsVisible = v; }

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
    private void OnToggleShuffle(object? sender, RoutedEventArgs e) => _viewModel?.ToggleShuffle();
    private void OnShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w != null) new KeyboardShortcutsDialog().Show(w);
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

