using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Events;
using App = global::Avalonia.Application;
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
        Dispatcher.UIThread.Post(() =>
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
        _controlsBox.UpdateFullscreenIcon(isFullscreen);

        if (isFullscreen)
        {
            ExtendClientAreaToDecorationsHint = false;
            ToolTip.SetTip(_controlsBox.BtnFullscreen, "Exit Fullscreen (F)");
            _headerBar.IsVisible = false;
            _headerBar.IsHitTestVisible = false;
            _fullscreenHeader.Show();
            _headerBar.HideOpenMenu();
            _headerBar.HidePrimaryMenu();
            _headerBar.HideWindowControls();
            _headerBar.HideFullscreenClose();
            _headerBar.SetPipVisibility(false);
        }
        else
        {
            ExtendClientAreaToDecorationsHint = true;
            ToolTip.SetTip(_controlsBox.BtnFullscreen, "Fullscreen (F)");
            _headerBar.IsVisible = true;
            _headerBar.IsHitTestVisible = true;
            _fullscreenHeader.Hide();
            _headerBar.ShowWindowControls();
            _headerBar.ShowPrimaryMenu();
            _headerBar.SetPipVisibility(Bounds.Width >= MediumBreakpoint);
            if (!string.IsNullOrEmpty(_viewModel?.FilePath))
                _headerBar.ShowOpenMenu();
            else
                _headerBar.HideOpenMenu();
        }
        _headerBar.UpdateMaximizeIcon(WindowState == WindowState.Maximized);
    }

    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();
}
