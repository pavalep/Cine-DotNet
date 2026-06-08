using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;
using Cine.Avalonia.ViewModels;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using Cine.Avalonia.Views.Dialogs;

namespace Cine.Avalonia.Controls;

public partial class FullscreenHeaderControl : UserControl
{
    private MainViewModel? _viewModel;
    private int _activeFlyouts;
    private PrimaryMenuBuilder? _fullscreenMenuBuilder;

    public FullscreenHeaderControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Build menu from shared builder
        _fullscreenMenuBuilder = BuildFullscreenMenu();
        BtnFullscreenMenu.Flyout = _fullscreenMenuBuilder.Build();
        BtnFullscreenMenu.Flyout.Opened += (_, _) => _fullscreenMenuBuilder?.SyncCheckStates();
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
            .AddItem("Play", "Play/Pause", "Space", () => _viewModel?.PlayPause())
            .AddItem("Stop", "Stop", "Ctrl+S", () => _viewModel?.Stop())
            .AddItem("Rewind", "Seek -10s", "←", () => _viewModel?.SeekBackward())
            .AddItem("FastForward", "Seek +10s", "→", () => _viewModel?.SeekForward())
            .AddSeparator()
            .AddSection("TOOLS")
            .AddItem("ClockOutline", "Go to Time…", "Ctrl+G", () =>
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
            .AddItem("Pin", "Always on Top", null, () =>
            {
                var w = TopLevel.GetTopLevel(this) as Window;
                if (w != null) w.Topmost = !w.Topmost;
            });
        return builder;
    }

    public bool HasActiveFlyouts => _activeFlyouts > 0;

    // P12: Expose inner FullscreenHeader Border for overlay hover tracking
    public global::Avalonia.Controls.Border FullscreenHeaderElement => FullscreenHeader;

    public void TrackFlyoutOpened(object? sender, EventArgs e)
    {
        _activeFlyouts++;
    }

    public void TrackFlyoutClosed(object? sender, EventArgs e)
    {
        _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
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

