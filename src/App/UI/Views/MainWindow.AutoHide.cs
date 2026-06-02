using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void InitializeAutoHide()
    {
        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AutoHideDelaySeconds)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        PointerMoved += OnWindowPointerMoved;
        SetUiControlsVisibility(true);
        _autoHideTimer?.Start();
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        _autoHideTimer?.Stop();
        
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        var isInteractiveOverlayActive = _activeFlyouts > 0 || (DropIndicatorOverlay?.IsVisible ?? false);
        if (!_isMouseOverControls && hasMedia && !isInteractiveOverlayActive)
            HideUiControls();
    }

    private void OnWindowPointerMoved(object? sender, global::Avalonia.Input.PointerEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;

        _isMouseOverControls =
            (HeaderBar != null && IsPositionOverElement(pos, HeaderBar)) ||
            (FullscreenHeader != null && IsPositionOverElement(pos, FullscreenHeader)) ||
            (ControlsBox != null && IsPositionOverElement(pos, ControlsBox));

        if (Math.Abs(pos.X - _lastMousePosition.X) > 1 ||
            Math.Abs(pos.Y - _lastMousePosition.Y) > 1)
        {
            _lastMousePosition = pos;
            if (!_uiVisible)
            {
                bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;
                if (pos.Y >= Math.Max(0, Bounds.Height - 90) ||
                    (isFullscreen && pos.Y <= 50))
                    ShowUiControls();
                return;
            }
            else
            {
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            }
        }
    }

    private bool IsPositionOverElement(global::Avalonia.Point pos, Visual element)
    {
        try
        {
            var elementOffset = element.TranslatePoint(new global::Avalonia.Point(0, 0), this);
            if (elementOffset.HasValue)
            {
                var elementRect = new global::Avalonia.Rect(elementOffset.Value, new global::Avalonia.Size(element.Bounds.Width, element.Bounds.Height));
                return elementRect.Contains(pos);
            }
        }
        catch { }
        return false;
    }

    private async void ShowUiControls()
    {
        if (_uiVisible) return;
        if (HeaderBar == null && ControlsBox == null && FullscreenHeader == null) return;
        _uiVisible = true;
        _autoHideTimer?.Stop();
        bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;
        if (HeaderBar != null && !isFullscreen)
        {
            HeaderBar.IsVisible = true;
            await FadeVisual(HeaderBar, 0, 1, 350, true);
        }
        if (FullscreenHeader != null && isFullscreen)
        {
            FullscreenHeader.IsVisible = true;
            await FadeVisual(FullscreenHeader, 0, 1, 350, true);
        }
        if (ControlsBox != null && !string.IsNullOrEmpty(_viewModel?.FilePath))
        {
            ControlsBox.IsVisible = true;
            await FadeVisual(ControlsBox, 0, 1, 350, true);
        }
        _autoHideTimer?.Start();
    }

    private async void HideUiControls()
    {
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (!_uiVisible || !hasMedia) return;
        if (_activeFlyouts > 0 || (DropIndicatorOverlay?.IsVisible ?? false)) return;
        
        _uiVisible = false;
        _autoHideTimer?.Stop();
        if (HeaderBar != null)
            await FadeVisual(HeaderBar, 1, 0, 300, false);
        if (FullscreenHeader != null)
            await FadeVisual(FullscreenHeader, 1, 0, 300, false);
        if (ControlsBox != null)
            await FadeVisual(ControlsBox, 1, 0, 300, false);
        if (!_uiVisible)
        {
            if (HeaderBar != null) HeaderBar.IsVisible = false;
            if (FullscreenHeader != null) FullscreenHeader.IsVisible = false;
            if (ControlsBox != null) ControlsBox.IsVisible = false;
        }
    }

    private void StartLoadingSpinner()
    {
        if (LoadingSpinner == null) return;
        LoadingSpinner.IsVisible = true;
        LoadingSpinner.Opacity = 0;
        _ = FadeVisual(LoadingSpinner, 0, 0.7, 200, true);
        if (_spinnerTimer == null)
        {
            _spinnerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (s, a) =>
            {
                _spinnerAngle = (_spinnerAngle + 8) % 360;
                if (LoadingSpinner != null)
                    LoadingSpinner.RenderTransform = new global::Avalonia.Media.RotateTransform(_spinnerAngle);
            });
        }
        _spinnerTimer.Start();
    }

    private void StopLoadingSpinner()
    {
        _spinnerTimer?.Stop();
        if (LoadingSpinner != null)
        {
            LoadingSpinner.IsVisible = false;
            LoadingSpinner.RenderTransform = null;
            LoadingSpinner.Opacity = 0;
        }
        _spinnerAngle = 0;
    }

    private CancellationTokenSource? _osdCts;

    private async void ShowOsdNotification(string text, double durationMs = 2000)
    {
        if (OsdNotificationBorder == null || OsdNotificationText == null) return;

        _osdCts?.Cancel();
        _osdCts = new CancellationTokenSource();
        var ct = _osdCts.Token;

        if (ControlsBox?.IsVisible == true)
        {
            OsdNotificationBorder.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Bottom;
            OsdNotificationBorder.Margin = new global::Avalonia.Thickness(0, 0, 0, 110);
        }
        else
        {
            OsdNotificationBorder.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
            OsdNotificationBorder.Margin = new global::Avalonia.Thickness(0);
        }

        OsdNotificationText.Text = text;
        OsdNotificationBorder.IsVisible = true;
        OsdNotificationBorder.Opacity = 0;

        try
        {
            await FadeVisual(OsdNotificationBorder, 0, 1, 200, true);
            if (ct.IsCancellationRequested) return;

            await Task.Delay((int)durationMs, ct);
            if (ct.IsCancellationRequested) return;

            await FadeVisual(OsdNotificationBorder, 1, 0, 300, false);
            if (!ct.IsCancellationRequested)
                OsdNotificationBorder.IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new global::Avalonia.Controls.Window
        {
            Title = "Cine — Error",
            SizeToContent = global::Avalonia.Controls.SizeToContent.WidthAndHeight,
            WindowStartupLocation = global::Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xFF, 0x1E, 0x1E, 0x2E)),
            Padding = new global::Avalonia.Thickness(24),
            MinWidth = 320,
            MaxWidth = 480
        };

        var stack = new global::Avalonia.Controls.StackPanel { Spacing = 16 };
        stack.Children.Add(new global::Avalonia.Controls.TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            Foreground = global::Avalonia.Media.Brushes.White,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        });
        stack.Children.Add(new global::Avalonia.Controls.TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        });
        var okBtn = new global::Avalonia.Controls.Button
        {
            Content = "OK",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            MinWidth = 100,
            Padding = new global::Avalonia.Thickness(16, 8),
            FontSize = 14,
            Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow)
        };
        okBtn.Click += (_, _) => dialog.Close();
        stack.Children.Add(okBtn);

        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private async Task FadeVisual(global::Avalonia.Visual visual, double from, double to, double durationMs, bool easeOut)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Dispatcher.UIThread.InvokeAsync(() => visual.Opacity = from);
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            double eased = easeOut
                ? 1 - Math.Cos(progress * Math.PI / 2)
                : Math.Sin(progress * Math.PI / 2);
            var opacity = from + (to - from) * eased;
            await Dispatcher.UIThread.InvokeAsync(() => visual.Opacity = opacity);
            await Task.Delay(16);
        }
        await Dispatcher.UIThread.InvokeAsync(() => visual.Opacity = to);
    }

    private void SetUiControlsVisibility(bool visible)
    {
        _uiVisible = visible;
        bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;
        if (HeaderBar != null && !isFullscreen)
        {
            HeaderBar.IsVisible = visible;
            HeaderBar.Opacity = visible ? 1 : 0;
        }
        if (FullscreenHeader != null && isFullscreen)
        {
            FullscreenHeader.IsVisible = visible;
            FullscreenHeader.Opacity = visible ? 1 : 0;
        }
        if (ControlsBox != null)
        {
            ControlsBox.IsVisible = visible && !string.IsNullOrEmpty(_viewModel?.FilePath);
            ControlsBox.Opacity = visible ? 1 : 0;
        }
    }

    private void ToggleUiControls()
    {
        if (_uiVisible) HideUiControls(); else ShowUiControls();
    }

    private void InitializeFlyoutTracking()
    {
        TrackFlyout(BtnOpenMenu);
        TrackFlyout(BtnPrimaryMenu);
        TrackFlyout(BtnVolumeMenu);
        TrackFlyout(BtnSubtitlesMenu);
        TrackFlyout(BtnAudioMenu);
        TrackFlyout(BtnVideoMenu);
        TrackFlyout(BtnOptionsMenu);
        TrackFlyout(BtnFullscreenMenu);
    }

    private void TrackFlyout(global::Avalonia.Controls.Control? control)
    {
        if (control is null) return;
        if (control is global::Avalonia.Controls.Button b && b.Flyout != null)
        {
            b.Flyout.Opened += (_, _) => _activeFlyouts++;
            b.Flyout.Closed += (_, _) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
        }
    }

    // Session save timer
    private DispatcherTimer? _sessionSaveTimer;

    private void InitializeSessionSave()
    {
        _sessionSaveTimer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, (s, a) =>
        {
            _viewModel?.SaveSession();
        });
        _sessionSaveTimer.Start();
    }
}
