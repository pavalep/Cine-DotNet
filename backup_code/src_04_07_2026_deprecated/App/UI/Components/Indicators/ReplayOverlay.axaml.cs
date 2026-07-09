using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia.Components;

public partial class ReplayOverlay : AvaloniaUserControl
{
    public event EventHandler? ReplayRequested;

    public ReplayOverlay()
    {
        InitializeComponent();
    }

    public void Show() => ReplayOverlayBorder.IsVisible = true;
    public void Hide() => ReplayOverlayBorder.IsVisible = false;

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        ReplayOverlayBorder.IsVisible = false;
        ReplayRequested?.Invoke(this, EventArgs.Empty);
    }
}
