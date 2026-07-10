using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cine.Avalonia.Core;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Cine.Avalonia.Views.Shell;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia.Views.Components;

public partial class FullscreenHeader : UserControl
{
    public IEventBus? EventBus { get; set; }
    public event EventHandler? PipToggled;
    public event EventHandler? SubtitlePanelRequested;
    public event EventHandler? AudioTrackPanelRequested;

    private MainViewModel? _viewModel;

    public FullscreenHeader()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // Also capture DataContext if already set before handler was attached
        if (DataContext is MainViewModel vm) _viewModel = vm;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    // P12: Expose inner FullscreenHeader Border for overlay hover tracking
    public global::Avalonia.Controls.Border FullscreenHeaderElement => FullscreenHeaderBorder;

    private void OnFullscreenMenuClick(object? sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();
        flyout.Items.Add(new MenuItem { Header = "PLAYBACK", IsEnabled = false });

        var playItem = new MenuItem { Header = "Play / Pause" };
        playItem.Click += (_, _) => _viewModel?.PlayPause();
        flyout.Items.Add(playItem);

        var stopItem = new MenuItem { Header = "Stop" };
        stopItem.Click += (_, _) => _viewModel?.Stop();
        flyout.Items.Add(stopItem);

        var seekBackItem = new MenuItem { Header = "Seek -10s" };
        seekBackItem.Click += (_, _) => _viewModel?.SeekBackward();
        flyout.Items.Add(seekBackItem);

        var seekFwdItem = new MenuItem { Header = "Seek +10s" };
        seekFwdItem.Click += (_, _) => _viewModel?.SeekForward();
        flyout.Items.Add(new MenuItem { Header = "-" });

        flyout.Items.Add(new MenuItem { Header = "TOOLS", IsEnabled = false });

        var shortcutsItem = new MenuItem { Header = "Keyboard Shortcuts" };
        shortcutsItem.Click += (_, _) =>
        {
            var w = TopLevel.GetTopLevel(this) as Window;
            if (w != null) new KeyboardShortcutsDialog().Show(w);
        };
        flyout.Items.Add(shortcutsItem);

        var prefsItem = new MenuItem { Header = "Preferences" };
        prefsItem.Click += (_, _) =>
        {
            var w = TopLevel.GetTopLevel(this) as Window;
            if (w != null) new PreferencesWindow().Show(w);
        };
        flyout.Items.Add(prefsItem);

        var aboutItem = new MenuItem { Header = "About Cine" };
        aboutItem.Click += (_, _) =>
        {
            var w = TopLevel.GetTopLevel(this) as Window;
            if (w != null) new PreferencesWindow().Show(w);
        };
        flyout.Items.Add(new MenuItem { Header = "-" });

        flyout.Items.Add(new MenuItem { Header = "VIEW", IsEnabled = false });

        var fsItem = new MenuItem { Header = "Exit Fullscreen" };
        fsItem.Click += (_, _) => _viewModel?.ToggleFullscreen();
        flyout.Items.Add(fsItem);

        var pipItem = new MenuItem { Header = "Picture in Picture" };
        pipItem.Click += (_, _) =>
        {
            EventBus?.Publish(new PipToggleEvent());
            PipToggled?.Invoke(this, EventArgs.Empty);
        };
        flyout.Items.Add(pipItem);

        flyout.Items.Add(new MenuItem { Header = "-" });

        var ontopItem = new MenuItem { Header = "Always on Top" };
        ontopItem.Click += (_, _) =>
        {
            var w = TopLevel.GetTopLevel(this) as Window;
            if (w != null) w.Topmost = !w.Topmost;
        };
        flyout.Items.Add(ontopItem);

        flyout.Placement = PlacementMode.Bottom;
        try
        {
            flyout.ShowAt(BtnFullscreenMenu);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<FullscreenHeader>().Error(ex, "OnFullscreenMenuClick ShowAt failed (BtnFullscreenMenu)");
        }
    }

    private void OnFsSubtitleClick(object? sender, RoutedEventArgs e)
        => SubtitlePanelRequested?.Invoke(this, EventArgs.Empty);

    private void OnFsAudioClick(object? sender, RoutedEventArgs e)
        => AudioTrackPanelRequested?.Invoke(this, EventArgs.Empty);

    public void Show()
    {
        IsVisible = true;
        FullscreenHeaderBorder.IsVisible = true;
        FullscreenHeaderBorder.Opacity = 1;
    }

    public void Hide()
    {
        IsVisible = false;
        FullscreenHeaderBorder.IsVisible = false;
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
        if (w != null) new PreferencesWindow().Show(w);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var w = TopLevel.GetTopLevel(this) as Window;
        if (w != null) new PreferencesWindow().Show(w);
    }
}
