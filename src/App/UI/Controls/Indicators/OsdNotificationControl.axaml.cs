using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using AvaloniaLayout = Avalonia.Layout;
using Cine.Avalonia.Extensions;

namespace Cine.Avalonia.Controls;

public partial class OsdNotificationControl : AvaloniaUserControl
{
    private CancellationTokenSource? _osdCts;
    private readonly Queue<OsdMessage> _queue = new();
    private bool _isShowing;

    private record OsdMessage(string Text, MaterialIconKind? Icon, double DurationMs);

    public event EventHandler? NotificationClicked;

    public bool IsControlsBoxVisible { get; set; } = true;

    /// <summary>
    /// Offset from the bottom of the window when controls box is visible.
    /// Updated by MainWindow when controls layout changes.
    /// </summary>
    public double ControlsBoxHeight { get; set; } = 110;

    public OsdNotificationControl()
    {
        InitializeComponent();
    }

    public void Show(string text, double durationMs = 2000)
    {
        Enqueue(new OsdMessage(text, null, durationMs));
    }

    public void ShowWithIcon(MaterialIconKind iconKind, string text, double durationMs = 2000)
    {
        Enqueue(new OsdMessage(text, iconKind, durationMs));
    }

    private void Enqueue(OsdMessage msg)
    {
        _queue.Enqueue(msg);
        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isShowing) return;
        _isShowing = true;

        while (_queue.Count > 0)
        {
            var msg = _queue.Dequeue();
            await ShowInternal(msg);
        }

        _isShowing = false;
    }

    private async Task ShowInternal(OsdMessage msg)
    {
        _osdCts?.Cancel();
        _osdCts = new CancellationTokenSource();
        var ct = _osdCts.Token;

        if (msg.Icon.HasValue)
        {
            OsdIcon.Opacity = 1;
            OsdIcon.Kind = msg.Icon.Value;
        }
        else
        {
            OsdIcon.Opacity = 0;
        }

        if (IsControlsBoxVisible)
        {
            OsdNotificationBorder.VerticalAlignment = AvaloniaLayout.VerticalAlignment.Bottom;
            OsdNotificationBorder.Margin = new Thickness(0, 0, 0, ControlsBoxHeight);
        }
        else
        {
            OsdNotificationBorder.VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center;
            OsdNotificationBorder.Margin = new Thickness(0);
        }

        OsdNotificationText.Text = msg.Text;
        OsdNotificationBorder.IsVisible = true;
        OsdNotificationBorder.Opacity = 0;
        // Start slightly below for slide-up effect
        if (OsdNotificationBorder.RenderTransform is TranslateTransform tt)
            tt.Y = 20;

        try
        {
            // Fade in + slide up
            await FadeTo(1, 150, ct, slideUp: true);
            if (ct.IsCancellationRequested) return;

            await Task.Delay((int)msg.DurationMs, ct);
            if (ct.IsCancellationRequested) return;

            // Fade out + slide down
            await FadeTo(0, 200, ct, slideUp: false);
            if (!ct.IsCancellationRequested)
                OsdNotificationBorder.IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    public void Hide()
    {
        _osdCts?.Cancel();
        _queue.Clear();
        OsdNotificationBorder.IsVisible = false;
    }

    private async Task FadeTo(double targetOpacity, double durationMs, CancellationToken ct, bool slideUp = false)
    {
        var startOpacity = OsdNotificationBorder.Opacity;
        var startY = OsdNotificationBorder.RenderTransform is TranslateTransform stt ? stt.Y : 0;
        var targetY = slideUp ? 0 : 20;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            if (ct.IsCancellationRequested) return;
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            var eased = progress < 0.5
                ? 1 - Math.Cos(progress * Math.PI / 2)
                : Math.Sin(progress * Math.PI / 2);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                OsdNotificationBorder.Opacity = startOpacity + (targetOpacity - startOpacity) * eased;
                if (OsdNotificationBorder.RenderTransform is TranslateTransform ftt)
                    ftt.Y = startY + (targetY - startY) * eased;
            });
            await Task.Delay(16);
        }
        if (!ct.IsCancellationRequested)
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                OsdNotificationBorder.Opacity = targetOpacity;
                if (OsdNotificationBorder.RenderTransform is TranslateTransform ftt)
                    ftt.Y = targetY;
            });
    }

    private void OnOsdNotificationClick(object? sender, PointerPressedEventArgs e)
    {
        NotificationClicked?.Invoke(this, EventArgs.Empty);
    }
}
