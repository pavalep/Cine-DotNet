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
using Cine.Avalonia.Core;
using Cine.Avalonia.Extensions;

namespace Cine.Avalonia.Views.Components;

public partial class OsdNotification : AvaloniaUserControl
{
    public IEventBus? EventBus { get; set; }

    private CancellationTokenSource? _osdCts;
    private readonly Queue<OsdMessage> _queue = new();
    private bool _isShowing;
    private OsdMessage? _activeMessage;
    private DateTime _dismissTime = DateTime.MinValue;

    private record OsdMessage(string Text, MaterialIconKind? Icon, double DurationMs, double? Progress = null, string? Category = null);

    /// <summary>Provides the OSD message category when clicked.</summary>
    public class OsdClickedEventArgs : EventArgs
    {
        public string Category { get; }
        public OsdClickedEventArgs(string category) => Category = category;
    }

    public event EventHandler<OsdClickedEventArgs>? NotificationClicked;

    public bool IsControlsBoxVisible { get; set; } = true;

    /// <summary>
    /// Offset from the bottom of the window when controls box is visible.
    /// Updated by MainWindow when controls layout changes.
    /// </summary>
    public double ControlsBoxHeight { get; set; } = 110;

    public OsdNotification()
    {
        InitializeComponent();
    }

    public void Show(string text, double durationMs = 2000)
    {
        var category = Categorize(text);
        Enqueue(new OsdMessage(text, null, durationMs, null, category));
    }

    public void ShowWithIcon(MaterialIconKind iconKind, string text, double durationMs = 2000)
    {
        var category = Categorize(text);
        Enqueue(new OsdMessage(text, iconKind, durationMs, null, category));
    }

    /// <summary>Show OSD with a progress bar for a value (0-100).</summary>
    public void ShowWithProgress(MaterialIconKind iconKind, string text, double value, double durationMs = 1500)
    {
        var category = Categorize(text);
        Enqueue(new OsdMessage(text, iconKind, durationMs, value, category));
    }

    private static string Categorize(string text)
    {
        if (text.Contains("Volume", StringComparison.OrdinalIgnoreCase) || text.Equals("Muted", StringComparison.OrdinalIgnoreCase))
            return "volume";
        if (text.Contains("Speed", StringComparison.OrdinalIgnoreCase))
            return "speed";
        if (text.Contains("Subtitle", StringComparison.OrdinalIgnoreCase) || text.Contains("Track", StringComparison.OrdinalIgnoreCase))
            return "subtitle";
        if (text.Contains("Audio", StringComparison.OrdinalIgnoreCase))
            return "audio";
        if (text.Contains("Error", StringComparison.OrdinalIgnoreCase))
            return "error";
        return "default";
    }

    private void Enqueue(OsdMessage msg)
    {
        // If the same category is already showing, update in-place and extend duration.
        // Do NOT cancel/restart ShowInternal — that causes a visible flicker as the
        // OSD fades out and back in. Just extending _dismissTime is sufficient.
        if (_isShowing && _activeMessage != null && _activeMessage.Category == msg.Category)
        {
            _activeMessage = msg;
            OsdNotificationText.Text = msg.Text;
            if (msg.Icon.HasValue)
            {
                OsdIcon.Opacity = 1;
                OsdIcon.Kind = msg.Icon.Value;
            }
            else
            {
                OsdIcon.Opacity = 0;
            }

            if (msg.Progress.HasValue)
            {
                OsdProgressBar.Value = Math.Clamp(msg.Progress.Value, 0, 100);
                OsdProgressBar.IsVisible = true;
            }
            else
            {
                OsdProgressBar.IsVisible = false;
            }

            // Extend display time — the running ShowInternal while-loop will see this
            _dismissTime = DateTime.UtcNow.AddMilliseconds(msg.DurationMs);
            return;
        }

        // Remove any pending messages of the same category from the queue
        var temp = _queue.ToArray();
        _queue.Clear();
        foreach (var item in temp)
        {
            if (item.Category != msg.Category)
                _queue.Enqueue(item);
        }

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

        _activeMessage = msg;
        _dismissTime = DateTime.UtcNow.AddMilliseconds(msg.DurationMs);

        if (msg.Icon.HasValue)
        {
            OsdIcon.Opacity = 1;
            OsdIcon.Kind = msg.Icon.Value;
        }
        else
        {
            OsdIcon.Opacity = 0;
        }

        if (msg.Progress.HasValue)
        {
            OsdProgressBar.Value = Math.Clamp(msg.Progress.Value, 0, 100);
            OsdProgressBar.IsVisible = true;
        }
        else
        {
            OsdProgressBar.IsVisible = false;
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

            // Wait until dismiss time (this can be extended dynamically by new volume events)
            while (DateTime.UtcNow < _dismissTime)
            {
                var remaining = (_dismissTime - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0) break;
                await Task.Delay(Math.Min(100, (int)remaining), ct);
            }
            if (ct.IsCancellationRequested) return;

            // Fade out + slide down
            await FadeTo(0, 200, ct, slideUp: false);
            if (!ct.IsCancellationRequested)
            {
                OsdProgressBar.IsVisible = false;
                OsdNotificationBorder.IsVisible = false;
                _activeMessage = null;
            }
        }
        catch (TaskCanceledException) { }
    }

    public void Hide()
    {
        _osdCts?.Cancel();
        _queue.Clear();
        OsdProgressBar.IsVisible = false;
        OsdNotificationBorder.IsVisible = false;
        _activeMessage = null;
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
        var category = _activeMessage?.Category ?? "default";
        EventBus?.Publish(new OsdClickedEvent(category));
        NotificationClicked?.Invoke(this, new OsdClickedEventArgs(category));
    }
}
