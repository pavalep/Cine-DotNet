using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.Views.Resources;
using Cine.Avalonia.Views.Components;
using Cine.Media.Events;
using Material.Icons;
using Material.Icons.Avalonia;
using App = global::Avalonia.Application;
using AvaloniaLayout = Avalonia.Layout;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia.Views.Shell;

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
        if (PlayerPage.ControlsBoxControl == null) return;

        bool isFullscreen = WindowState == WindowState.FullScreen;
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        PlayerPage.ControlsBoxControl.UpdateFullscreenIcon(isFullscreen);

        // Hide resize grips in fullscreen — resizing is meaningless there
        if (ResizeGripPanel != null)
            ResizeGripPanel.IsVisible = !isFullscreen;

        if (isFullscreen)
        {
            PlayerPage.HeaderBarControl.IsVisible = false;
            PlayerPage.HeaderBarControl.IsHitTestVisible = false;
            PlayerPage.HeaderBarControl.HideWindowControls();
            PlayerPage.HeaderBarControl.HideFullscreenClose();

            // Show controls immediately on entering fullscreen
            if (hasMedia) ShowUiControls();
        }
        else
        {
            PlayerPage.HeaderBarControl.IsVisible = true;
            PlayerPage.HeaderBarControl.IsHitTestVisible = true;
            PlayerPage.FullscreenHeaderControl.Hide();
            PlayerPage.HeaderBarControl.ShowWindowControls();

            // Restore controls to visible state after leaving fullscreen
            if (hasMedia) ShowUiControls();
        }
        PlayerPage.HeaderBarControl.UpdateMaximizeIcon(WindowState == WindowState.Maximized);

        // Sync StartPage window control icon if it's visible
        StartPage?.UpdateMaximizeIcon(WindowState == WindowState.Maximized);
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
                    FontSize = Token.Size("font-size-subtitle2"),
                    FontWeight = FontWeight.SemiBold,
                    Foreground = global::Avalonia.Media.Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                });

                if (!string.IsNullOrEmpty(details))
                {
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = details,
                        FontSize = Token.Size("font-size-body1"),
                        Foreground = AppColors.TextSecondary,
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                var closeButton = new global::Avalonia.Controls.Button
                {
                    Content = "Close",
                    HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(16, 8),
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

        bool isFlyoutOpen = AreAnyPanelsOpen();
        if (isFlyoutOpen)
        {
            _autoHideTimer?.Start();
            return;
        }

        if (AreAnyPanelsOpen())
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

        if (PlayerPage.HeaderBarControl.HeaderBarElement != null)
        {
            PlayerPage.HeaderBarControl.HeaderBarElement.IsVisible = !isFullscreen;
            PlayerPage.HeaderBarControl.HeaderBarElement.Opacity = isFullscreen ? 0 : 1;
            PlayerPage.HeaderBarControl.HeaderBarElement.IsHitTestVisible = !isFullscreen;
        }
        if (PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement != null)
        {
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.IsVisible = isFullscreen;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.Opacity = isFullscreen ? 1 : 0;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.IsHitTestVisible = isFullscreen;
        }
        if (PlayerPage.ControlsBoxControl.ControlsBoxElement != null)
        {
            PlayerPage.ControlsBoxControl.ControlsBoxElement.IsVisible = true;
            PlayerPage.ControlsBoxControl.ControlsBoxElement.Opacity = 1;
            PlayerPage.ControlsBoxControl.ControlsBoxElement.IsHitTestVisible = true;
            PlayerPage.ControlsBoxControl.ControlsBoxElement.InvalidateMeasure();
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
            PlayerPage.HeaderBarControl.HeaderBarElement.IsVisible = false;
            PlayerPage.HeaderBarControl.HeaderBarElement.Opacity = 0;
            PlayerPage.HeaderBarControl.HeaderBarElement.IsHitTestVisible = false;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.IsVisible = false;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.Opacity = 0;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.IsHitTestVisible = false;
        }
        else
        {
            PlayerPage.HeaderBarControl.HeaderBarElement.Opacity = 0;
            PlayerPage.HeaderBarControl.HeaderBarElement.IsHitTestVisible = false;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.IsVisible = false;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.Opacity = 0;
            PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.IsHitTestVisible = false;
        }
        PlayerPage.ControlsBoxControl.ControlsBoxElement.Opacity = 0;
        PlayerPage.ControlsBoxControl.ControlsBoxElement.IsHitTestVisible = false;
    }

    private async void FadeHeaderAndControls(double targetOpacity)
    {
        Cine.Avalonia.Services.ErrorBoundary.Run(async () =>
        {
            var headerBar = PlayerPage.HeaderBarControl.HeaderBarElement;
            var controlsBox = PlayerPage.ControlsBoxControl.ControlsBoxElement;
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

    // ─────────────────────────────────────────────────────
    //  Auto-Hide State
    // ─────────────────────────────────────────────────────
    private DispatcherTimer? _autoHideTimer;
    private bool _uiVisible = true;
    private const double AutoHideDelaySeconds = 3.0;
    private global::Avalonia.Point _lastMousePosition;
    private DateTime _lastSeekWheel = DateTime.MinValue;

    // Loading / Volume OSD debounce
    private bool _isLoading;
    private bool _suppressFirstVolumeOsd;
    private DispatcherTimer? _volumeOsdTimer;
    private double _pendingVolumeLevel;

    // ─────────────────────────────────────────────────────
    //  Responsive Breakpoints
    // ─────────────────────────────────────────────────────
    private const double NarrowBreakpoint = 600.0;
    private const double MediumBreakpoint = 1024.0;

    // ─────────────────────────────────────────────────────
    //  Public Utilities
    // ─────────────────────────────────────────────────────
    public static void TrySetIcon(MaterialIcon icon, string resourceKey)
    {
        icon.Kind = resourceKey switch
        {
            "FullscreenEnterIcon" => MaterialIconKind.Fullscreen,
            "FullscreenExitIcon" => MaterialIconKind.FullscreenExit,
            "MaxRestoreIcon" => MaterialIconKind.WindowMaximize,
            "MaximizeIcon" => MaterialIconKind.WindowMaximize,
            "PlayIcon" => MaterialIconKind.Play,
            "PauseIcon" => MaterialIconKind.Pause,
            "SubtitlesIcon" => MaterialIconKind.Subtitles,
            "SubtitlesOffIcon" => MaterialIconKind.ClosedCaptionOutline,
            "AudioIcon" => MaterialIconKind.Music,
            "AudioOffIcon" => MaterialIconKind.MusicOff,
            _ => icon.Kind
        };
    }

    // ─────────────────────────────────────────────────────
    //  Native Imports (user32 window rect)
    // ─────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }
}
