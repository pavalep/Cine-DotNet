using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Dialogs;
using Cine.Core;
using Layout = Avalonia.Layout;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Components;

public partial class HeaderBar : AvaloniaUserControl
{
    public event EventHandler? PipToggled;

    private MainViewModel? _viewModel;
    private IFlyoutService? _flyoutManager;
    private readonly List<global::Avalonia.Controls.Primitives.FlyoutBase> _trackedFlyouts = new();
    private PrimaryMenuBuilder? _primaryMenuBuilder;
    private MenuFlyout? _openMenuFlyout;

    public HeaderBar()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Build primary menu from shared builder
        _primaryMenuBuilder = BuildPrimaryMenu();
        BtnPrimaryMenu.Flyout = _primaryMenuBuilder.Build();

        // Sync menu checkmarks when the primary menu opens
        BtnPrimaryMenu.Flyout.Opened += (_, _) =>
        {
            TrackFlyoutOpened(BtnPrimaryMenu.Flyout, EventArgs.Empty);
            _primaryMenuBuilder?.SyncCheckStates();
        };
        BtnPrimaryMenu.Flyout.Closed += TrackFlyoutClosed;

        // Build open menu from shared builder (same pattern as primary menu)
        var openMenuBuilder = new OpenMenuBuilder();
        openMenuBuilder
            .AddItem("FileOutline", "Open File…", null, () =>
            {
                openMenuBuilder.Hide();
                _viewModel?.OpenFilesCommand.Execute(null);
            })
            .AddItem("FolderOutline", "Open Folder…", null, () =>
            {
                openMenuBuilder.Hide();
                _viewModel?.OpenFolderCommand.Execute(null);
            });
        _openMenuFlyout = openMenuBuilder.Build();
        BtnOpenMenu.Flyout = _openMenuFlyout;

        BtnOpenMenu.Flyout.Opened += (_, _) =>
        {
            TrackFlyoutOpened(BtnOpenMenu.Flyout, EventArgs.Empty);

            // Remove stale recent items (keep first 2: Open File, Open Folder)
            while (_openMenuFlyout.Items.Count > 2)
                _openMenuFlyout.Items.RemoveAt(_openMenuFlyout.Items.Count - 1);

            var recentFiles = _viewModel?.RecentFiles;
            if (recentFiles != null && recentFiles.Count > 0)
            {
                _openMenuFlyout.Items.Add(new Separator());

                var sectionHeader = new MenuItem { Header = "RECENT" };
                sectionHeader.Classes.Add("menu-section-header");
                _openMenuFlyout.Items.Add(sectionHeader);

                foreach (var file in recentFiles.Take(10))
                    AddRecentFileItem(_openMenuFlyout, file);
            }
        };
        BtnOpenMenu.Flyout.Closed += TrackFlyoutClosed;
    }


    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>
    /// Adds a recent file item to the Open menu flyout.
    /// </summary>
    private void AddRecentFileItem(MenuFlyout flyout, string filePath)
    {
        if (!System.IO.File.Exists(filePath)) return;

        var fileName = System.IO.Path.GetFileName(filePath);
        var item = new MenuItem
        {
            Header = new TextBlock
            {
                Text = fileName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 240
            }
        };
        item.Click += (_, _) =>
        {
            flyout.Hide();
            _viewModel?.OpenFile(filePath);
        };
        flyout.Items.Add(item);
    }

    /// <summary>
    /// Makes the "Open" button visible so the user can open files/folders.
    /// </summary>
    public void ShowOpenMenu()
    {
        BtnOpenMenu.IsVisible = true;
    }

    /// <summary>
    /// Builds the shared primary menu using PrimaryMenuBuilder.
    /// Consolidated to show only items NOT available via keyboard shortcuts
    /// or the right-click context menu (Phase 6).
    ///
    /// Removed (available via keyboard/context menu):
    ///   - Play / Pause, Stop, Seek ±10s (PLAYBACK section)
    ///   - Fullscreen toggle (VIEW section — available via F key + context menu)
    /// Kept (unique to this menu):
    ///   - Picture in Picture, Always on Top
    ///   - Loop File, Loop Playlist, Shuffle
    ///   - Go to Time, Keyboard Shortcuts, Preferences, About
    /// </summary>
    private PrimaryMenuBuilder BuildPrimaryMenu()
    {
        var builder = new PrimaryMenuBuilder();
        builder
            .AddSection("VIEW")
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
    public global::Avalonia.Controls.Border HeaderBarElement => HeaderBarBorder;

    public void SetTitle(string title)
    {
        TitleText.Text = title;
    }

    public void SetBarVisibility(bool visible)
    {
        HeaderBarBorder.IsVisible = visible;
        HeaderBarBorder.Opacity = visible ? 1 : 0;
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
    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            value?.Register("open-menu", () => BtnOpenMenu.Flyout?.Hide());
            value?.Register("primary-menu", () => BtnPrimaryMenu.Flyout?.Hide());

            // Wire Opened/Closed for mutual exclusion on both flyouts
            if (BtnPrimaryMenu.Flyout != null)
            {
                BtnPrimaryMenu.Flyout.Opened += (_, _) => value?.DismissOthers("primary-menu");
                BtnPrimaryMenu.Flyout.Closed += (_, _) => value?.MarkClosed("primary-menu");
            }

            if (BtnOpenMenu.Flyout != null)
            {
                BtnOpenMenu.Flyout.Opened += (_, _) => value?.DismissOthers("open-menu");
                BtnOpenMenu.Flyout.Closed += (_, _) => value?.MarkClosed("open-menu");
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
        try
        {
            BtnOpenMenu.Flyout?.ShowAt(BtnOpenMenu);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<HeaderBar>().Error(ex, "ReopenFlyout ShowAt failed (BtnOpenMenu)");
        }
    }

    public void TrackFlyoutOpened(object? sender, EventArgs e)
    {
        if (sender is Flyout flyout)
        {
            if (!_trackedFlyouts.Contains(flyout))
                _trackedFlyouts.Add(flyout);
        }
    }

    public void TrackFlyoutClosed(object? sender, EventArgs e)
    {
        // No counter needed — rely on IsOpen check
    }

    public bool HasActiveFlyouts => _flyoutManager?.HasActiveFlyouts == true;

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
