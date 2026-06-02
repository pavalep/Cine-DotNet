using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia.Controls;

public partial class ReplayOverlayControl : AvaloniaUserControl
{
    public event EventHandler? ReplayRequested;

    public ReplayOverlayControl()
    {
        InitializeComponent();
    }

    public void Show() => ReplayOverlay.IsVisible = true;
    public void Hide() => ReplayOverlay.IsVisible = false;

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        ReplayOverlay.IsVisible = false;
        ReplayRequested?.Invoke(this, EventArgs.Empty);
    }
}

