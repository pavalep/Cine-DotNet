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
    private TimeSpan _lastPositionTextTime = TimeSpan.Zero;
    private TimeSpan _lastReportedDuration = TimeSpan.Zero;

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

            // Show video surface now that media is loaded
            _videoHost?.ShowVideoSurface();

            // Clear replay mode when new media opens
            _controlsBox.SetReplayMode(false);
            _replayOverlay.Hide();

            // Hide start page
            if (StartPage != null)
            {
                StartPage.Opacity = 0;
                // Don't hide immediately — let fade transition complete
                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);
                    await Dispatcher.UIThread.OnUiThreadAsync(() =>
                    {
                        if (StartPage != null) StartPage.IsVisible = false;
                        // Keep PlaybackBackground visible during start page fade,
                        // then hide after fade completes so there's never a flash of
                        // the raw window background between layers.
                        PlaybackBackground.IsVisible = false;
                    });
                });
            }
            // If no start page (already hidden), hide playback background immediately
            if (StartPage == null || !StartPage.IsVisible)
                PlaybackBackground.IsVisible = false;

            if (_dropIndicator.IsShowing)
                _ = _dropIndicator.Hide();

            if (_videoHost != null)
            {
                // Resize hidden window to match actual video dimensions
                var player = _playerService?.Player;
                if (player != null)
                {
                    player.GetVideoSize(out int vw, out int vh);
                    _videoHost.SetVideoSize(vw, vh);
                }

                _videoHost.IsVideoSurfaceVisible = true;
                _videoHost.Opacity = 1;
                SyncVideoRect();

                DebugLog($"OnMediaOpened VideoHost: Opacity={_videoHost.Opacity} IsVisible={_videoHost.IsVisible} Bounds={_videoHost.Bounds}");
            }

            // Layer stack dump
            DebugLog($"OnMediaOpened layers: StartPage.Visible={StartPage?.IsVisible} StartPage.Opacity={StartPage?.Opacity} " +
                     $"dropIndicator.Showing={_dropIndicator.IsShowing} VideoHost.Opacity={_videoHost?.Opacity} " +
                     $"VideoHost.Visible={_videoHost?.IsVisible}");

            // Delay controls appearance to avoid overlap with fading start page.
            // ShowUiControls calls InvalidateMeasure() to ensure correct height.
            _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                await Task.Delay(250);
                ShowUiControls();
            });
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
            // Clear replay mode when playback resumes (either by user click or auto)
            if (!e.IsPaused && e.State == PlaybackState.Playing)
            {
                _controlsBox.SetReplayMode(false);
                SyncPipReplayMode(false);
            }

            if (e.IsPaused)
                _pauseOverlay.Show();
            else
                _pauseOverlay.Hide();

            // Show replay overlay when playback ends (end of file)
            bool isEnded = e.State == PlaybackState.Stopped && _viewModel?.FilePath != null;
            if (isEnded)
            {
                _replayOverlay.Show();
            }

            // Hide video surface when fully stopped with no file (user closed video)
            if (e.State == PlaybackState.Stopped && _viewModel?.FilePath == null)
                _videoHost?.HideVideoSurface();

            _controlsBox.UpdatePlayPauseIcon();

            // Sync PIP play state BEFORE replay mode so the final icon state wins
            SyncPipPlayState();
            SyncPipReplayMode(isEnded);
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            _replayOverlay.Show();
            _controlsBox.SetReplayMode(true);
            _controlsBox.UpdatePlayPauseIcon();
            SyncPipReplayMode(true);
        });
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            _lastPosition = e.Position;
            _lastDuration = e.Duration;

            var seekBar = _controlsBox?.SeekBarControl;
            if (seekBar == null) return;

            // Update seek bar visual (throttled internally to ~30fps)
            seekBar.UpdatePosition(_lastPosition);

            // Duration rarely changes during playback — update only on meaningful change
            if (Math.Abs((_lastDuration - _lastReportedDuration).TotalSeconds) >= 0.5)
            {
                _lastReportedDuration = _lastDuration;
                seekBar.UpdateDuration(_lastDuration);
            }

            // Throttle text updates to ~10fps (only update when the second changes)
            if (Math.Abs((e.Position - _lastPositionTextTime).TotalSeconds) >= 0.1)
            {
                _lastPositionTextTime = e.Position;
                seekBar.SetPositionText(SeekBarControl.FormatTimeSpan(_lastPosition));
                seekBar.SetDurationText(SeekBarControl.FormatTimeSpan(_lastDuration));
            }

            // Sync PIP position if active
            SyncPipPosition(sender, e);
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
                var player = _playerService?.Player;
                if (player == null) return;

                EventHandler? handler = null;
                handler = (_, _) =>
                {
                    player.Seek(pos);
                    player.Play();
                    player.Opened -= handler;
                };
                player.Opened += handler;
            }
        }
    }
}
