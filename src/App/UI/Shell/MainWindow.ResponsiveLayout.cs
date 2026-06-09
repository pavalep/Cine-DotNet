using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using App = global::Avalonia.Application;
using SizeChangedEventArgs = Avalonia.Controls.SizeChangedEventArgs;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void InitializeResponsiveLayout()
    {
        this.SizeChanged += OnWindowSizeChanged;
        _controlsBox.UpdateResponsiveLayout(Bounds.Width, _viewModel?.HasMultipleVideoTracks ?? false);
        _headerBar.UpdateResponsiveLayout(Bounds.Width);
        UpdateSubtitleAudioOverlayVisibility(Bounds.Width);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _controlsBox.UpdateResponsiveLayout(e.NewSize.Width, _viewModel?.HasMultipleVideoTracks ?? false);
        _headerBar.UpdateResponsiveLayout(e.NewSize.Width);
        UpdateSubtitleAudioOverlayVisibility(e.NewSize.Width);
        App.DebugReport("VT", "MainWindow.OnWindowSizeChanged", "SizeChangedEvent.", new
        {
            newSize = e.NewSize.ToString(),
            windowState = WindowState.ToString(),
            videoHostBounds = _videoHost?.Bounds.ToString(),
            renderScaling = RenderScaling,
            videoSurfaceVisible = _videoHost?.IsVideoSurfaceVisible
        }, runId: "pre-fix");
        if (_videoHost != null && _videoHost.IsVideoSurfaceVisible && _playerService?.Player is { } player)
        {
            int w = (int)(_videoHost.Bounds.Width * RenderScaling);
            int h = (int)(_videoHost.Bounds.Height * RenderScaling);
            if (w > 0 && h > 0)
                player.NotifyResize(w, h);
        }
    }

    /// <summary>
    /// Shows/hides the standalone subtitle and audio selector overlay buttons
    /// based on window width, matching the ControlsBox responsive behavior.
    /// </summary>
    private void UpdateSubtitleAudioOverlayVisibility(double width)
    {
        bool isNarrow = width < 495;
        if (_controlsBox?.SubtitleOverlayCtrl != null)
            _controlsBox.SubtitleOverlayCtrl.IsVisible = !isNarrow;
        if (_controlsBox?.AudioTrackSelectorCtrl != null)
            _controlsBox.AudioTrackSelectorCtrl.IsVisible = !isNarrow;
    }
}
