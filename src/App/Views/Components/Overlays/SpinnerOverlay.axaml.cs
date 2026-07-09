using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;

namespace Cine.Avalonia.Views.Components;

public partial class SpinnerOverlay : AvaloniaUserControl
{
    private DispatcherTimer? _spinnerTimer;
    private double _spinnerAngle;

    public SpinnerOverlay()
    {
        InitializeComponent();
    }

    public async Task Start()
    {
        SpinnerTrack.IsVisible = true;
        LoadingSpinner.IsVisible = true;
        LoadingSpinner.Opacity = 0;
        await FadeTo(0.9, 250);

        if (_spinnerTimer == null)
        {
            _spinnerTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(16),
                DispatcherPriority.Background,
                (s, a) =>
                {
                    _spinnerAngle = (_spinnerAngle + 8) % 360;
                    LoadingSpinner.RenderTransform = new RotateTransform(_spinnerAngle);
                });
        }
        _spinnerTimer.Start();
    }

    public void Stop()
    {
        _spinnerTimer?.Stop();
        SpinnerTrack.IsVisible = false;
        LoadingSpinner.IsVisible = false;
        LoadingSpinner.RenderTransform = null;
        LoadingSpinner.Opacity = 0;
        _spinnerAngle = 0;
    }

    private async Task FadeTo(double targetOpacity, double durationMs)
    {
        var startOpacity = LoadingSpinner.Opacity;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            var eased = 1 - Math.Cos(progress * Math.PI / 2);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
                LoadingSpinner.Opacity = startOpacity + (targetOpacity - startOpacity) * eased);
            await Task.Delay(16);
        }
        await Dispatcher.UIThread.OnUiThreadAsync(() => LoadingSpinner.Opacity = targetOpacity);
    }
}
