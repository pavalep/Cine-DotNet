using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Services;
using Cine.Media.Events;
using App = global::Avalonia.Application;
using AvaloniaLayout = Avalonia.Layout;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void OnPlayerFullscreenChanged(object? sender, FullscreenChangedEventArgs e)
    {
        App.DebugReport("VT", "MainWindow.OnPlayerFullscreenChanged", "FullscreenChangedEvent.", new
        {
            isFullscreen = e.IsFullscreen,
            beforeWindowState = WindowState.ToString(),
            renderScaling = RenderScaling
        }, runId: "pre-fix");
        Dispatcher.UIThread.OnUiThread(() =>
        {
            WindowState = e.IsFullscreen ? WindowState.FullScreen : WindowState.Normal;
            RefreshFullscreenUi();
        });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Window.WindowStateProperty)
        {
            if (change.NewValue is WindowState state)
            {
                bool isFullscreen = state == WindowState.FullScreen;
                if (_playerService?.Player != null && _playerService.Player.IsFullscreen != isFullscreen)
                {
                    _playerService.Player.SetFullscreen(isFullscreen);
                }
                RefreshFullscreenUi();
            }
        }
    }

    private void RefreshFullscreenUi()
    {
        if (_controlsBox == null) return;

        bool isFullscreen = WindowState == WindowState.FullScreen;
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        _controlsBox.UpdateFullscreenIcon(isFullscreen);

        if (isFullscreen)
        {
            ExtendClientAreaToDecorationsHint = false;
            _headerBar.IsVisible = false;
            _headerBar.IsHitTestVisible = false;
            _headerBar.HideWindowControls();
            _headerBar.HideFullscreenClose();

            // Show controls immediately on entering fullscreen
            if (hasMedia) ShowUiControls();
        }
        else
        {
            ExtendClientAreaToDecorationsHint = true;
            _headerBar.IsVisible = true;
            _headerBar.IsHitTestVisible = true;
            _fullscreenHeader.Hide();
            _headerBar.ShowWindowControls();

            // Restore controls to visible state after leaving fullscreen
            if (hasMedia) ShowUiControls();
        }
        _headerBar.UpdateMaximizeIcon(WindowState == WindowState.Maximized);
    }

    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();

    private async Task ShowErrorDialog(string message, string details)
    {
        await Dispatcher.UIThread.OnUiThreadAsync(async () =>
        {
            try
            {
                var dialog = new Window
                {
                    Title = "Cine — Error",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    ShowInTaskbar = false,
                    Background = this.Background ?? global::Avalonia.Media.Brushes.Black
                };

                var textPanel = new StackPanel
                {
                    Margin = new Thickness(24),
                    Spacing = 12,
                    VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
                };

                textPanel.Children.Add(new TextBlock
                {
                    Text = "⚠️ " + message,
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = global::Avalonia.Media.Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                });

                if (!string.IsNullOrEmpty(details))
                {
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = details,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(180, 255, 255, 255)),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                var closeButton = new global::Avalonia.Controls.Button
                {
                    Content = "Close",
                    HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(16, 6),
                    Classes = { "circular-sm" }
                };
                closeButton.Click += (_, _) => dialog.Close();
                textPanel.Children.Add(closeButton);

                dialog.Content = textPanel;
                dialog.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) dialog.Close();
                };

                await dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                // Last resort - can't show error dialog, log to debug
                System.Diagnostics.Debug.WriteLine($"[Cine] Fatal error: {message} - {ex.Message}");
            }
        });
    }

    // ─────────────────────────────────────────────────────
    //  Auto-Hide (merged from MainWindow.AutoHide.cs)
    // ─────────────────────────────────────────────────────

    // Hover state — set by PointerEntered/Exited on each overlay element.
    // Mirrors Python's contains_pointer checks.
    private bool _hoverHeader;
    private bool _hoverControls;
    private bool _hoverFullscreenHeader;

    private void InitializeAutoHide()
    {
        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(3000)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        // Timer is started on first ShowUiControls when media is loaded
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        if (_hoverHeader || _hoverControls || _hoverFullscreenHeader)
        {
            _autoHideTimer?.Start();
            return;
        }

        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (!hasMedia) return;

        bool isFlyoutOpen = _controlsBox.HasActiveFlyouts ||
                            _fullscreenHeader.HasActiveFlyouts ||
                            _headerBar.HasActiveFlyouts ||
                            _dropIndicator.IsShowing;
        if (isFlyoutOpen)
        {
            _autoHideTimer?.Start();
            return;
        }

        HideUiControls();
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;

        if (!_uiVisible)
            ShowUiControls();

        if (Math.Abs(pos.X - _lastMousePosition.X) > 3 ||
            Math.Abs(pos.Y - _lastMousePosition.Y) > 3)
        {
            _lastMousePosition = pos;
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        }
    }

    private void OnHeaderPointerEntered(object? sender, PointerEventArgs e) => _hoverHeader = true;
    private void OnHeaderPointerExited(object? sender, PointerEventArgs e) => _hoverHeader = false;
    private void OnControlsPointerEntered(object? sender, PointerEventArgs e) => _hoverControls = true;
    private void OnControlsPointerExited(object? sender, PointerEventArgs e) => _hoverControls = false;
    private void OnFullscreenHeaderPointerEntered(object? sender, PointerEventArgs e) => _hoverFullscreenHeader = true;
    private void OnFullscreenHeaderPointerExited(object? sender, PointerEventArgs e) => _hoverFullscreenHeader = false;

    private void ShowUiControls()
    {
        _uiVisible = true;
        _autoHideTimer?.Stop();

        bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;

        if (_headerBar.HeaderBar != null)
        {
            _headerBar.HeaderBar.IsVisible = !isFullscreen;
            _headerBar.HeaderBar.Opacity = isFullscreen ? 0 : 1;
            _headerBar.HeaderBar.IsHitTestVisible = !isFullscreen;
        }
        if (_fullscreenHeader.FullscreenHeader != null)
        {
            _fullscreenHeader.FullscreenHeader.IsVisible = isFullscreen;
            _fullscreenHeader.FullscreenHeader.Opacity = isFullscreen ? 1 : 0;
            _fullscreenHeader.FullscreenHeader.IsHitTestVisible = isFullscreen;
        }
        if (_controlsBox.ControlsBox != null)
        {
            _controlsBox.ControlsBox.IsVisible = true;
            _controlsBox.ControlsBox.Opacity = 1;
            _controlsBox.ControlsBox.IsHitTestVisible = true;
            _controlsBox.ControlsBox.InvalidateMeasure();
        }

        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (hasMedia)
            _autoHideTimer?.Start();
    }

    private void HideUiControls()
    {
        _uiVisible = false;
        _autoHideTimer?.Stop();

        bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;

        if (isFullscreen)
        {
            _headerBar.HeaderBar.IsVisible = false;
            _headerBar.HeaderBar.Opacity = 0;
            _headerBar.HeaderBar.IsHitTestVisible = false;
            _fullscreenHeader.FullscreenHeader.IsVisible = false;
            _fullscreenHeader.FullscreenHeader.Opacity = 0;
            _fullscreenHeader.FullscreenHeader.IsHitTestVisible = false;
        }
        else
        {
            _headerBar.HeaderBar.Opacity = 0;
            _headerBar.HeaderBar.IsHitTestVisible = false;
            _fullscreenHeader.FullscreenHeader.IsVisible = false;
            _fullscreenHeader.FullscreenHeader.Opacity = 0;
            _fullscreenHeader.FullscreenHeader.IsHitTestVisible = false;
        }
        _controlsBox.ControlsBox.IsVisible = false;
        _controlsBox.ControlsBox.Opacity = 0;
        _controlsBox.ControlsBox.IsHitTestVisible = false;
    }

    private async void FadeHeaderAndControls(double targetOpacity)
    {
        Cine.Avalonia.Services.ErrorBoundary.Run(async () =>
        {
            var headerBar = _headerBar.HeaderBar;
            var controlsBox = _controlsBox.ControlsBox;
            if (headerBar == null && controlsBox == null) return;

            double startHeader = headerBar?.Opacity ?? 0;
            double startControls = controlsBox?.Opacity ?? 0;
            int steps = 6;

            for (int i = 1; i <= steps; i++)
            {
                double t = i / (double)steps;
                await Dispatcher.UIThread.OnUiThreadAsync(() =>
                {
                    if (headerBar != null)
                        headerBar.Opacity = startHeader + (targetOpacity - startHeader) * t;
                    if (controlsBox != null)
                        controlsBox.Opacity = startControls + (targetOpacity - startControls) * t;
                });
                await Task.Delay(16);
            }

            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                if (headerBar != null) headerBar.Opacity = targetOpacity;
                if (controlsBox != null) controlsBox.Opacity = targetOpacity;
            });
        });
    }
}
