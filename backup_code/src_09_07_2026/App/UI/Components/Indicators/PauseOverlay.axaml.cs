using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;

namespace Cine.Avalonia.Components;

public partial class PauseOverlay : AvaloniaUserControl
{
    private CancellationTokenSource? _fadeCts;

    public PauseOverlay()
    {
        InitializeComponent();
    }

    public async Task Show(double fadeDurationMs = 150)
    {
        _fadeCts?.Cancel();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;

        PauseIndicator.IsVisible = true;
        PauseIndicator.Opacity = 0;
        await FadeTo(1, fadeDurationMs, ct);
    }

    public async void Hide()
    {
        _fadeCts?.Cancel();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;
        try
        {
            await FadeTo(0, 200, ct);
        }
        catch (OperationCanceledException) { /* expected when Show() is called mid-fade */ }
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
