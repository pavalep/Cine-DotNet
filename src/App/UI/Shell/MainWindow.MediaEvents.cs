using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Core;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia;

/// <summary>
/// Media event handlers: OnMediaOpened, OnPositionChanged, OnPlaybackStateChanged,
/// OnManagerStateChanged, OnChapterListChanged, OnOsdNotificationClicked.
/// Extracted from MainWindow.Core.cs to keep partial files manageable.
/// </summary>
public partial class MainWindow
{
    private TimeSpan _lastPositionTextTime = TimeSpan.Zero;
    private TimeSpan _lastReportedDuration = TimeSpan.Zero;

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        ErrorBoundary.Run(async () =>
        {
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                _viewModel?.RefreshState();
                _isLoading = false;
                _spinnerOverlay.Stop();

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

                // Video is displayed via the OpenGL render API (ANGLE + pixel readback
                // to VideoFrameImage). No child HWND or video host needed.

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
                // Always sync icon from the manager after media opens — this is the
                // authoritative source that's guaranteed to reflect the player's real state.
                _stateManager?.Refresh();
                if (_stateManager != null)
                    _controlsBox.SyncPlayPauseIcon(_stateManager.IsPlaying);
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            });
        });
    }

    /// <summary>
    /// Called from PlaybackStateManager.StateChanged — the SINGLE authoritative
    /// handler for play/pause/stop transitions. All icon updates and PIP sync
    /// flow through here, eliminating desync from competing state sources.
    /// </summary>
    private void OnManagerStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.OnUiThread(() => OnManagerStateChanged(sender, e));
            return;
        }

        DebugLog($"OnManagerStateChanged: e.State={e.State}");

        _controlsBox.SyncPlayPauseIcon(e.State == PlaybackState.Playing);

        SyncPipPlayState(e.State);
        bool isEnded = e.State == PlaybackState.Stopped && _viewModel?.FilePath != null;
        SyncPipReplayMode(isEnded);
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            DebugLog($"OnPlaybackStateChanged: e.State={e.State} e.IsPaused={e.IsPaused}");

            if (!e.IsPaused && e.State == PlaybackState.Playing)
            {
                _controlsBox.SetReplayMode(false);
            }

            if (e.IsPaused)
                _pauseOverlay.Show();
            else
                _pauseOverlay.Hide();

            bool isEnded = e.State == PlaybackState.Stopped && _viewModel?.FilePath != null;
            if (isEnded)
            {
                _replayOverlay.Show();
            }
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            _replayOverlay.Show();
            _controlsBox.SetReplayMode(true);
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

            seekBar.UpdatePosition(_lastPosition);

            if (Math.Abs((_lastDuration - _lastReportedDuration).TotalSeconds) >= 0.5)
            {
                _lastReportedDuration = _lastDuration;
                seekBar.UpdateDuration(_lastDuration);
            }

            if (Math.Abs((e.Position - _lastPositionTextTime).TotalSeconds) >= 0.1)
            {
                _lastPositionTextTime = e.Position;
                seekBar.SetPositionText(SeekBarControl.FormatTimeSpan(_lastPosition));
                seekBar.SetDurationText(SeekBarControl.FormatTimeSpan(_lastDuration));
            }

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
