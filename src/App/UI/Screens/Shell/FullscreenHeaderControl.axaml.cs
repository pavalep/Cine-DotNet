using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.ViewModels;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using Cine.Avalonia.Views.Dialogs;

namespace Cine.Avalonia.Controls;

public partial class FullscreenHeaderControl : UserControl
{
    public event EventHandler? ExitFullscreenRequested;

    private MainViewModel? _viewModel;
    private int _activeFlyouts;

    public FullscreenHeaderControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    public bool HasActiveFlyouts => _activeFlyouts > 0;

    public void Show()
    {
        FullscreenHeader.IsVisible = true;
        FullscreenHeader.Opacity = 1;
    }

    public void Hide()
    {
        FullscreenHeader.IsVisible = false;
    }

    // --- Menu handlers ---

    private void OnExitFullscreen(object? sender, RoutedEventArgs e)
    {
        ExitFullscreenRequested?.Invoke(this, EventArgs.Empty);
    }

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
        if (w != null) new ShortcutsDialog { DataContext = _viewModel }.Show(w);
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

