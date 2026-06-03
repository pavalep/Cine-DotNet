using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
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

