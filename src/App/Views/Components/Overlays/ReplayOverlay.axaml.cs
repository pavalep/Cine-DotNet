using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cine.Avalonia.Core;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia.Views.Components;

public partial class ReplayOverlay : AvaloniaUserControl
{
    public IEventBus? EventBus { get; set; }
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
        EventBus?.Publish(new ReplayRequestedEvent());
        ReplayRequested?.Invoke(this, EventArgs.Empty);
    }
}
