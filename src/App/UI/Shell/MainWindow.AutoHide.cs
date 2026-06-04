using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaLayout = Avalonia.Layout;
using Control = Avalonia.Controls.Control;
using PointerEventArgs = Avalonia.Input.PointerEventArgs;
using Button = Avalonia.Controls.Button;
using Cine.Avalonia.Helpers;
using Material.Icons;

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
        bool isInteractiveOverlayActive = _controlsBox.HasActiveFlyouts ||
            _fullscreenHeader.HasActiveFlyouts ||
            _headerBar.HasActiveFlyouts ||
            _dropIndicator.IsShowing;

        if (!_isMouseOverControls && hasMedia && !isInteractiveOverlayActive)
            HideUiControls();
    }

    private void OnVideoPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!_uiVisible)
            ShowUiControls();
    }

    private void OnVideoPointerExited(object? sender, PointerEventArgs e)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer?.Start();
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;

        if (!_uiVisible)
        {
            ShowUiControls();
            // Fall through to update hover state and restart timer
        }

        _isMouseOverControls =
            (_headerBar != null && IsPositionOverElement(pos, _headerBar)) ||
            (_fullscreenHeader != null && IsPositionOverElement(pos, _fullscreenHeader)) ||
            (_controlsBox != null && IsPositionOverElement(pos, _controlsBox));

        if (Math.Abs(pos.X - _lastMousePosition.X) > 1 ||
            Math.Abs(pos.Y - _lastMousePosition.Y) > 1)
        {
            _lastMousePosition = pos;
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        }
    }

    private bool IsPositionOverElement(global::Avalonia.Point pos, Visual element)
    {
        try
        {
            var elementOffset = element.TranslatePoint(new AvaloniaPoint(0, 0), this);
            if (elementOffset.HasValue)
            {
                var elementRect = new AvaloniaRect(elementOffset.Value, new AvaloniaSize(element.Bounds.Width, element.Bounds.Height));
                return elementRect.Contains(pos);
            }
        }
        catch { }
        return false;
    }

    private async void ShowUiControls()
    {
        if (_uiVisible) return;
        _uiVisible = true;
        _autoHideTimer?.Stop();

        bool isFullscreen = WindowState == WindowState.FullScreen;
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);

        if (!isFullscreen)
        {
            _headerBar.SetBarVisibility(true);
            await FadeVisual(_headerBar.HeaderBar, 0, 1, 350, true);
        }
        if (isFullscreen)
        {
            _fullscreenHeader.Show();
            await FadeVisual(_fullscreenHeader.FullscreenHeader, 0, 1, 350, true);
        }
        if (hasMedia)
        {
            _controlsBox.SetControlsVisibility(true);
            await FadeVisual(_controlsBox.ControlsBox, 0, 1, 350, true);
        }
        _autoHideTimer?.Start();
    }

    private async void HideUiControls()
    {
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (!_uiVisible || !hasMedia) return;

        bool isInteractiveOverlayActive = _controlsBox.HasActiveFlyouts ||
            _fullscreenHeader.HasActiveFlyouts ||
            _headerBar.HasActiveFlyouts ||
            _dropIndicator.IsShowing;

        if (isInteractiveOverlayActive) return;

        _uiVisible = false;
        _autoHideTimer?.Stop();

        // Fade out content then hide
        if (_headerBar.HeaderBar?.IsVisible == true)
        {
            await FadeVisual(_headerBar.HeaderBar, 1, 0, 300, false);
            _headerBar.SetBarVisibility(false);
        }
        if (_fullscreenHeader.FullscreenHeader?.IsVisible == true)
        {
            await FadeVisual(_fullscreenHeader.FullscreenHeader, 1, 0, 300, false);
            _fullscreenHeader.Hide();
        }
        if (_controlsBox.ControlsBox?.IsVisible == true)
        {
            await FadeVisual(_controlsBox.ControlsBox, 1, 0, 300, false);
            _controlsBox.SetControlsVisibility(false);
        }
    }

    /// <summary>
    /// Sets the initial visibility of all UI controls without animation.
    /// UserControls themselves remain visible to preserve layout space.
    /// Only the inner content elements are toggled.
    /// </summary>
    private void SetUiControlsVisibility(bool visible)
    {
        _uiVisible = visible;
        bool isFullscreen = WindowState == WindowState.FullScreen;
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);

        _headerBar.SetBarVisibility(visible && !isFullscreen);
        if (isFullscreen)
        {
            if (visible) _fullscreenHeader.Show(); else _fullscreenHeader.Hide();
        }
        _controlsBox.SetControlsVisibility(visible && hasMedia);
    }

    private void ToggleUiControls()
    {
        if (_uiVisible) HideUiControls(); else ShowUiControls();
    }

    private CancellationTokenSource? _fadeCts;

    private async Task FadeVisual(Visual visual, double from, double to, double durationMs, bool easeOut)
    {
        if (visual == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Dispatcher.UIThread.OnUiThreadAsync(() => visual.Opacity = from);
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            double eased = easeOut
                ? 1 - Math.Cos(progress * Math.PI / 2)
                : Math.Sin(progress * Math.PI / 2);
            var opacity = from + (to - from) * eased;
            await Dispatcher.UIThread.OnUiThreadAsync(() => visual.Opacity = opacity);
            await Task.Delay(16);
        }
        await Dispatcher.UIThread.OnUiThreadAsync(() => visual.Opacity = to);
    }

    private async void ShowOsdNotification(string text, double durationMs = 2000)
    {
        _osdNotification.IsControlsBoxVisible = _controlsBox?.ControlsBox?.IsVisible == true;
        _osdNotification.Show(text, durationMs);
    }

    // P6.1: Icon indicator overload
    private async void ShowOsdNotification(MaterialIconKind icon, string text, double durationMs = 2000)
    {
        _osdNotification.IsControlsBoxVisible = _controlsBox?.ControlsBox?.IsVisible == true;
        _osdNotification.ShowWithIcon(icon, text, durationMs);
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = "Cine — Error",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(AvaloniaColor.FromArgb(0xFF, 0x1E, 0x1E, 0x2E)),
            Padding = new Thickness(24),
            MinWidth = 320,
            MaxWidth = 480
        };

        var stack = new global::Avalonia.Controls.StackPanel { Spacing = 16 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            Foreground = AvaloniaBrushes.White,
            TextWrapping = AvaloniaTextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(AvaloniaColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            TextWrapping = AvaloniaTextWrapping.Wrap
        });
        var okBtn = new Button
        {
            Content = "OK",
            HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            MinWidth = 100,
            Padding = new Thickness(16, 8),
            FontSize = 14,
            Cursor = new AvaloniaCursor(StandardCursorType.Arrow)
        };
        okBtn.Click += (_, _) => dialog.Close();
        stack.Children.Add(okBtn);

        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }
}
