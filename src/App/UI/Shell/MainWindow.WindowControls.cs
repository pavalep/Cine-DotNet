using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;
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
            videoHostBounds = _videoHost?.Bounds.ToString(),
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
}
