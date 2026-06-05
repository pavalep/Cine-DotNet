using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;

namespace Cine.Avalonia.Controls;

public partial class PauseOverlayControl : AvaloniaUserControl
{
    private CancellationTokenSource? _fadeCts;

    public PauseOverlayControl()
    {
        InitializeComponent();
    }

    public async void Show(double fadeDurationMs = 150)
    {
        _fadeCts?.Cancel();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;

        PauseIndicator.IsVisible = true;
        PauseIndicator.Opacity = 0;
        await FadeTo(1, fadeDurationMs, ct);
    }

    public void Hide()
    {
        _fadeCts?.Cancel();
        PauseIndicator.IsVisible = false;
        PauseIndicator.Opacity = 0;
    }

    private async Task FadeTo(double targetOpacity, double durationMs, CancellationToken ct)
    {
        var startOpacity = PauseIndicator.Opacity;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            if (ct.IsCancellationRequested) return;
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            var eased = Math.Sin(progress * Math.PI / 2);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
                PauseIndicator.Opacity = startOpacity + (targetOpacity - startOpacity) * eased);
            await Task.Delay(16);
        }
        if (!ct.IsCancellationRequested)
            await Dispatcher.UIThread.OnUiThreadAsync(() => PauseIndicator.Opacity = targetOpacity);
    }
}

