using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using AvaloniaLayout = Avalonia.Layout;
using Cine.Avalonia.Helpers;

namespace Cine.Avalonia.Controls;

public partial class OsdNotificationControl : AvaloniaUserControl
{
    private CancellationTokenSource? _osdCts;

    public event EventHandler? NotificationClicked;

    public bool IsControlsBoxVisible { get; set; } = true;

    public OsdNotificationControl()
    {
        InitializeComponent();
    }

    public async void Show(string text, double durationMs = 2000)
    {
        // Hide icon for text-only notifications
        OsdIcon.IsVisible = false;
        _osdCts?.Cancel();
        _osdCts = new CancellationTokenSource();
        await ShowInternal(text, durationMs, _osdCts.Token);
    }

    // P6.1: Icon indicator overload
    public async void ShowWithIcon(MaterialIconKind iconKind, string text, double durationMs = 2000)
    {
        OsdIcon.IsVisible = true;
        OsdIcon.Kind = iconKind;
        _osdCts?.Cancel();
        _osdCts = new CancellationTokenSource();
        await ShowInternal(text, durationMs, _osdCts.Token);
    }

    private async Task ShowInternal(string text, double durationMs, CancellationToken ct)
    {
        if (IsControlsBoxVisible)
        {
            OsdNotificationBorder.VerticalAlignment = AvaloniaLayout.VerticalAlignment.Bottom;
            OsdNotificationBorder.Margin = new Thickness(0, 0, 0, 110);
        }
        else
        {
            OsdNotificationBorder.VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center;
            OsdNotificationBorder.Margin = new Thickness(0);
        }

        OsdNotificationText.Text = text;
        OsdNotificationBorder.IsVisible = true;
        OsdNotificationBorder.Opacity = 0;

        try
        {
            await FadeTo(1, 200, ct);
            if (ct.IsCancellationRequested) return;

            await Task.Delay((int)durationMs, ct);
            if (ct.IsCancellationRequested) return;

            await FadeTo(0, 300, ct);
            if (!ct.IsCancellationRequested)
                OsdNotificationBorder.IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    public void Hide()
    {
        _osdCts?.Cancel();
        OsdNotificationBorder.IsVisible = false;
    }

    private async Task FadeTo(double targetOpacity, double durationMs, CancellationToken ct)
    {
        var startOpacity = OsdNotificationBorder.Opacity;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            if (ct.IsCancellationRequested) return;
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            var eased = progress < 0.5
                ? 1 - Math.Cos(progress * Math.PI / 2)
                : Math.Sin(progress * Math.PI / 2);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
                OsdNotificationBorder.Opacity = startOpacity + (targetOpacity - startOpacity) * eased);
            await Task.Delay(16);
        }
        if (!ct.IsCancellationRequested)
            await Dispatcher.UIThread.OnUiThreadAsync(() => OsdNotificationBorder.Opacity = targetOpacity);
    }

    private void OnOsdNotificationClick(object? sender, PointerPressedEventArgs e)
    {
        NotificationClicked?.Invoke(this, EventArgs.Empty);
    }
}

