using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Helpers;
using Cine.Media.Events;
using Cine.Media.Models;
using App = global::Avalonia.Application;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private DispatcherTimer? _pauseIndicatorTimer;

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        await Dispatcher.UIThread.OnUiThreadAsync(() =>
        {
            #region debug-point VT-A
            App.DebugReport("VT", "MainWindow.OnMediaOpened", "Opened event received.", new
            {
                windowState = WindowState.ToString(),
                startPageVisible = StartPage?.IsVisible,
                videoSurfaceVisible = _videoHost?.IsVideoSurfaceVisible,
                videoHostBounds = _videoHost?.Bounds.ToString(),
                renderScaling = RenderScaling
            }, runId: "pre-fix");
            #endregion
            _viewModel?.RefreshState();
            _isLoading = false;
            _spinnerOverlay.Stop();
        });

        if (StartPage != null)
        {
            await FadeVisual(StartPage, 1, 0, 200, true);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                StartPage.IsVisible = false;
                StartPage.Opacity = 1;
            });
        }

        if (_videoHost != null)
        {
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                _videoHost.IsVideoSurfaceVisible = true;
                _videoHost.Opacity = 0;
            });
            await FadeVisual(_videoHost, 0, 1, 300, false);
        }

        await Dispatcher.UIThread.OnUiThreadAsync(() =>
        {
            _controlsBox.SetControlsVisibility(true);
            _headerBar.ShowOpenMenu();

            if (_viewModel != null)
            {
                _lastDuration = _viewModel.Duration;
                var d = _lastDuration;
                var seekBar = _controlsBox.SeekBarControl;
                if (d.TotalSeconds > 0)
                {
                    seekBar.SetDurationText(SeekBarControl.FormatTimeSpan(d));
                    seekBar.SetPositionText(SeekBarControl.FormatTimeSpan(_viewModel.Position));
                }
            }
            // Force icon from player state directly to avoid PropertyChanged races
            var playerState = _playerService?.Player?.State ?? PlaybackState.Stopped;
            _controlsBox.SetPlayPauseIconFromPlayerState(playerState);
            _controlsBox.UpdatePlayPauseIcon();
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        });
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        #region debug-point VT-B
        App.DebugReport("VT", "MainWindow.OnPlaybackStateChanged", "PlaybackStateChangedEvent.", new
        {
            isPaused = e.IsPaused,
            windowState = WindowState.ToString(),
            videoSurfaceVisible = _videoHost?.IsVideoSurfaceVisible,
            videoHostBounds = _videoHost?.Bounds.ToString()
        }, runId: "pre-fix");
        #endregion
        Dispatcher.UIThread.OnUiThread(() =>
        {
            if (e.IsPaused)
                _pauseOverlay.Show();
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            _replayOverlay.Show();
        });
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            _lastPosition = e.Position;
            _lastDuration = e.Duration;

            var seekBar = _controlsBox?.SeekBarControl;
            if (seekBar != null)
            {
                seekBar.UpdatePosition(_lastPosition);
                seekBar.UpdateDuration(_lastDuration);
                seekBar.SetPositionText(SeekBarControl.FormatTimeSpan(_lastPosition));
                seekBar.SetDurationText(SeekBarControl.FormatTimeSpan(_lastDuration));
            }
        });
    }

    private void OnChapterListChanged(object? sender, EventArgs e)
    {
        var seekBar = _controlsBox?.SeekBarControl;
        seekBar?.UpdateChapterMarkers();
    }

    private void OnOsdNotificationClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_queuedOpenPath) && System.IO.File.Exists(_queuedOpenPath))
        {
            var path = _queuedOpenPath;
            var pos = _sessionResumePosition;
            _queuedOpenPath = null;
            _sessionResumePosition = TimeSpan.Zero;
            _viewModel?.OpenFile(path);
            _viewModel?.ClearSession();
            if (pos.TotalSeconds > 0)
            {
                EventHandler? handler = null;
                handler = (s, args) =>
                {
                    var player = _playerService?.Player;
                    if (player != null)
                    {
                        player.Seek(pos);
                        player.Play();
                    }
                    if (player != null)
                        player.Opened -= handler;
                };
                var p = _playerService?.Player;
                if (p != null) p.Opened += handler;
            }
        }
    }
}
