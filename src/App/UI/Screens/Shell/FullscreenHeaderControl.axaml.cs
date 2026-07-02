using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.Builders;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia.Controls;

public partial class FullscreenHeaderControl : UserControl
{
    public event EventHandler? PipToggled;

    private MainViewModel? _viewModel;
    private PrimaryMenuBuilder? _fullscreenMenuBuilder;
    private FlyoutManager? _flyoutManager;
    private FlyoutOverlayControl? _overlay;

    public FullscreenHeaderControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // Also capture DataContext if already set before handler was attached
        if (DataContext is MainViewModel vm) _viewModel = vm;

        _fullscreenMenuBuilder = BuildFullscreenMenu();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>Builds the fullscreen menu using shared PrimaryMenuBuilder.</summary>
    private PrimaryMenuBuilder BuildFullscreenMenu()
    {
        var builder = new PrimaryMenuBuilder();
        builder
            .AddSection("PLAYBACK")
            .AddItem("Play", "Play / Pause", "Space", () => _viewModel?.PlayPause())
            .AddItem("Stop", "Stop", "Ctrl+S", () => _viewModel?.Stop())
            .AddItem("SkipPrevious", "Seek -10s", "Left", () => _viewModel?.SeekBackward())
            .AddItem("SkipNext", "Seek +10s", "Right", () => _viewModel?.SeekForward())
            .AddSeparator()
            .AddSection("TOOLS")
            .AddItem("ClockOutline", "Go to Time\u2026", "Ctrl+G", () =>
            {
                var w = TopLevel.GetTopLevel(this) as Window;
                if (w != null && _viewModel != null) new GoToTimeDialog { DataContext = _viewModel }.Show(w);
            })
            .AddItem("Keyboard", "Keyboard Shortcuts", null, () =>
            {
                var w = TopLevel.GetTopLevel(this) as Window;
                if (w != null) new KeyboardShortcutsDialog().Show(w);
            })
            .AddItem("Cog", "Preferences", null, () =>
            {
                var w = TopLevel.GetTopLevel(this) as Window;
                if (w != null) new PreferencesDialog { DataContext = _viewModel }.Show(w);
            })
            .AddItem("Information", "About Cine", null, () =>
            {
                var w = TopLevel.GetTopLevel(this) as Window;
                if (w != null) new AboutDialog { DataContext = _viewModel }.Show(w);
            })
            .AddSeparator()
            .AddSection("VIEW")
            .AddItem("Fullscreen", "Exit Fullscreen", "F", () =>
            {
                _viewModel?.ToggleFullscreen();
            })
            .AddItem("PictureInPictureBottomRight", "Picture in Picture", "Ctrl+Shift+P", () => PipToggled?.Invoke(this, EventArgs.Empty))
            .AddSeparator()
            .AddItem("PinOutline", "Always on Top", null, () =>
            {
                var w = TopLevel.GetTopLevel(this) as Window;
                if (w != null) w.Topmost = !w.Topmost;
            });
        return builder;
    }

    public bool HasActiveFlyouts => _flyoutManager?.HasActiveFlyouts == true;

    private MenuFlyout? _btnFlyout;

    public FlyoutManager? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            if (value != null)
            {
                // Obtain overlay reference for mutual exclusion with overlay-based flyouts
                _overlay ??= MainWindow.GetOverlay(this);
                value.Register("fullscreen-menu", () => { _btnFlyout?.Hide(); _overlay?.HideContent(); });
                // Pass to child track selector controls for mutual exclusion
                if (FullscreenSubOverlay != null) FullscreenSubOverlay.FlyoutManager = value;
                if (FullscreenAudioOverlay != null) FullscreenAudioOverlay.FlyoutManager = value;
            }
        }
    }

    // P12: Expose inner FullscreenHeader Border for overlay hover tracking
    public global::Avalonia.Controls.Border FullscreenHeaderElement => FullscreenHeader;

    private void OnFullscreenMenuClick(object? sender, RoutedEventArgs e)
    {
        _fullscreenMenuBuilder?.SyncCheckStates();
        _flyoutManager?.DismissOthers("fullscreen-menu");
        _btnFlyout = _fullscreenMenuBuilder!.Build();
        _btnFlyout.Placement = PlacementMode.Bottom;
        _btnFlyout.Opened += (_, _) => { _flyoutManager?.DismissOthers("fullscreen-menu"); };
        _btnFlyout.Closed += (_, _) => { _flyoutManager?.MarkClosed("fullscreen-menu"); _btnFlyout = null; };
        try
        {
            _btnFlyout.ShowAt(BtnFullscreenMenu);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<FullscreenHeaderControl>().Error(ex, "OnFullscreenMenuClick ShowAt failed (BtnFullscreenMenu)");
        }
    }

    public void Show()
    {
        IsVisible = true;
        FullscreenHeader.IsVisible = true;
        FullscreenHeader.Opacity = 1;
    }

    public void Hide()
    {
        IsVisible = false;
        FullscreenHeader.IsVisible = false;
    }

    // --- Menu handlers ---

    private void OnPlayPause(object? sender, RoutedEventArgs e) => _viewModel?.PlayPause();
    private void OnStop(object? sender, RoutedEventArgs e) => _viewModel?.Stop();
    private void OnSeekBackward(object? sender, RoutedEventArgs e) => _viewModel?.SeekBackward();
    private void OnSeekForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekForward();

    private void OnToggleAlwaysOnTop(object? sender, RoutedEventArgs e)
    {
        var w = TopLevel.GetTopLevel(this) as Window;
        if (w != null) w.Topmost = !w.Topmost;
    }

    private void OnShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var w = TopLevel.GetTopLevel(this) as Window;
        if (w != null) new KeyboardShortcutsDialog().Show(w);
    }

    private void OnPreferencesClick(object? sender, RoutedEventArgs e)
    {
        var w = TopLevel.GetTopLevel(this) as Window;
        if (w != null) new PreferencesDialog { DataContext = _viewModel }.Show(w);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var w = TopLevel.GetTopLevel(this) as Window;
        if (w != null) new AboutDialog { DataContext = _viewModel }.Show(w);
    }
}
