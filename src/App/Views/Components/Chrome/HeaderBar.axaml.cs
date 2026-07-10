using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Core;
using Cine.Avalonia.Views.Resources;
using Cine.Avalonia.Views.Shell;
using Layout = Avalonia.Layout;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Views.Components;

public partial class HeaderBar : AvaloniaUserControl
{
    public event EventHandler? PipToggled;

    // Events for PrimaryMenuPanel actions that need window-level handling
    public event EventHandler? PrimaryPipToggled;
    public event EventHandler? PrimaryAlwaysOnTopToggled;
    public event EventHandler? PrimaryShortcutsRequested;
    public event EventHandler? PrimaryPreferencesRequested;
    public event EventHandler? PrimaryAboutRequested;

    public IEventBus? EventBus { get; set; }

    private MainViewModel? _viewModel;

    public HeaderBar()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Wire header menu buttons to toggle their MainWindow-level panels
        BtnOpenMenu.Click += (_, _) => TogglePanel(PanelHost?.MainOpenMenuPanel);
        BtnPrimaryMenu.Click += (_, _) => TogglePanel(PanelHost?.MainPrimaryMenuPanel);
    }

    private MainWindow? PanelHost => TopLevel.GetTopLevel(this) as MainWindow;

    private void WireMenuPanelEvents()
    {
        if (_viewModel == null) return;

        var host = PanelHost;
        if (host == null) return;

        // Open Menu
        host.MainOpenMenuPanel.OpenFileClicked += (_, _) =>
            _viewModel?.OpenFilesCommand?.Execute(null);
        host.MainOpenMenuPanel.OpenFolderClicked += (_, _) =>
            _viewModel?.OpenFolderCommand?.Execute(null);

        // Primary Menu — ViewModel-bound actions
        host.MainPrimaryMenuPanel.LoopFileClicked += (_, _) =>
            _viewModel?.ToggleLoopFile();
        host.MainPrimaryMenuPanel.LoopPlaylistClicked += (_, _) =>
            _viewModel?.ToggleLoopPlaylist();
        host.MainPrimaryMenuPanel.ShuffleClicked += (_, _) =>
            _viewModel?.ToggleShuffle();

        // Primary Menu — window-level actions forwarded via events
        host.MainPrimaryMenuPanel.PipClicked += (_, _) => PrimaryPipToggled?.Invoke(this, EventArgs.Empty);
        host.MainPrimaryMenuPanel.AlwaysOnTopClicked += (_, _) => PrimaryAlwaysOnTopToggled?.Invoke(this, EventArgs.Empty);
        host.MainPrimaryMenuPanel.ShortcutsClicked += (_, _) => PrimaryShortcutsRequested?.Invoke(this, EventArgs.Empty);
        host.MainPrimaryMenuPanel.PreferencesClicked += (_, _) => PrimaryPreferencesRequested?.Invoke(this, EventArgs.Empty);
        host.MainPrimaryMenuPanel.AboutClicked += (_, _) => PrimaryAboutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TogglePanel(Control? panel)
    {
        var host = PanelHost;
        if (host == null || panel == null) return;

        if (panel.IsVisible)
        {
            // Same button -> close this panel
            panel.IsVisible = false;
            host.UpdatePanelDismissState();
        }
        else
        {
            // Different button -> hide sibling, show this one
            HideAllInlinePanels();
            panel.IsVisible = true;
            host.EnablePanelDismiss();
        }
    }

    public void HideAllInlinePanels()
    {
        var host = PanelHost;
        if (host == null) return;
        host.HideAllPanels();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        WireMenuPanelEvents();
    }

    /// <summary>
    /// Expose inner HeaderBar Border for overlay hover tracking
    /// </summary>
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

    public void ShowBackButton()
    {
        BtnBack.IsVisible = true;
    }

    public void HideBackButton()
    {
        BtnBack.IsVisible = false;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.NavigateHome();
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
            SetVis(BtnPip, true);
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
        EventBus?.Publish(new PipToggleEvent());
        PipToggled?.Invoke(this, EventArgs.Empty);
    }

}
