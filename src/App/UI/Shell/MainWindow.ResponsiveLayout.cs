using System;
using Avalonia;
using Avalonia.Controls;
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
