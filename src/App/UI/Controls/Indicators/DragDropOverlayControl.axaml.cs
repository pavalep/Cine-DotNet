using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;

namespace Cine.Avalonia.Controls;

public partial class DragDropOverlayControl : AvaloniaUserControl
{
    public DragDropOverlayControl()
    {
        InitializeComponent();
    }

    public async Task Show()
    {
        DragDropOverlay.IsVisible = true;
        await FadeTo(1, 150);
    }

    public async Task Hide()
    {
        await FadeTo(0, 150);
        DragDropOverlay.IsVisible = false;
    }

    public bool IsShowing => DragDropOverlay.IsVisible;

    private async Task FadeTo(double targetOpacity, double durationMs)
    {
        var startOpacity = DragDropOverlay.Opacity;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            var eased = 1 - Math.Cos(progress * Math.PI / 2);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
                DragDropOverlay.Opacity = startOpacity + (targetOpacity - startOpacity) * eased);
            await Task.Delay(16);
        }
        await Dispatcher.UIThread.OnUiThreadAsync(() => DragDropOverlay.Opacity = targetOpacity);
    }
}

