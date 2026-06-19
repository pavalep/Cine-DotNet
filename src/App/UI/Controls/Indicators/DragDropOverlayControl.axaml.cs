using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;

namespace Cine.Avalonia.Controls;

public partial class DragDropOverlayControl : AvaloniaUserControl
{
    private bool _isShowing;
    private CancellationTokenSource? _fadeCts;

    public DragDropOverlayControl()
    {
        InitializeComponent();
    }

    public async Task Show()
    {
        if (_isShowing) return;

        CancelFade();
        _isShowing = true;
        DragDropOverlay.IsVisible = true;
        await FadeTo(1, 150);
    }

    public async Task Hide()
    {
        if (!_isShowing) return;

        CancelFade();
        await FadeTo(0, 150);
        DragDropOverlay.IsVisible = false;
        _isShowing = false;
    }

    public bool IsShowing => _isShowing;

    private void CancelFade()
    {
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = new CancellationTokenSource();
    }

    private async Task FadeTo(double targetOpacity, double durationMs)
    {
        var ct = _fadeCts!.Token;
        var startOpacity = DragDropOverlay.Opacity;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            while (sw.Elapsed.TotalMilliseconds < durationMs)
            {
                ct.ThrowIfCancellationRequested();
                var progress = System.Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
                var eased = 1 - System.Math.Cos(progress * System.Math.PI / 2);
                await Dispatcher.UIThread.OnUiThreadAsync(() =>
                    DragDropOverlay.Opacity = startOpacity + (targetOpacity - startOpacity) * eased);
                await Task.Delay(16, ct);
            }
            await Dispatcher.UIThread.OnUiThreadAsync(() => DragDropOverlay.Opacity = targetOpacity);
        }
        catch (OperationCanceledException)
        {
            // Fade was cancelled — don't apply final opacity
        }
    }
}
