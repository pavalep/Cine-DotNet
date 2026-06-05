using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Events;
using App = global::Avalonia.Application;
using Cine.Avalonia.Helpers;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using ToolTip = Avalonia.Controls.ToolTip;

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
        await Task.CompletedTask; // P12: placeholder — will show proper error dialog
    }
}
