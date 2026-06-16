using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void OnPipToggled(object? sender, EventArgs e)
    {
        if (_pipService == null) return;

        if (_pipService.IsActive)
        {
            DebugLog("OnPipToggled: exiting PIP");
            _pipService.ExitPip();
            _headerBar.SetPipChecked(false);
            // Resume showing video in main window
            MpvVideoView.DisplayEnabled = true;
            // Re-sync icon from the authoritative source
            if (_stateManager != null)
                _controlsBox.SyncPlayPauseIcon(_stateManager.IsPlaying);
        }
        else
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.FilePath))
            {
                ShowOsdNotification("No media loaded");
                return;
            }

            DebugLog("OnPipToggled: entering PIP");
            // Stop showing video in main window — it will go dark naturally
            MpvVideoView.DisplayEnabled = false;

            var pipWindow = _pipService?.EnterPip();

            if (pipWindow != null)
            {
                DebugLog("OnPipToggled: PIP started successfully");
                _headerBar.SetPipChecked(true);

                // Sync initial state
                pipWindow.SetPlayingState(_viewModel.IsPlaying);
                pipWindow.SetMuted(_viewModel.IsMuted);

                // File info for display
                string fileName = Path.GetFileName(_viewModel.FilePath);
                pipWindow.SetFileName(fileName, fileName);

                // Aspect ratio and position
                var player = _playerService?.Player;
                if (player != null)
                {
                    player.GetVideoSize(out int vw, out int vh);
                    if (vw > 0 && vh > 0)
                        pipWindow.SetAspectRatio((double)vw / vh);

                    pipWindow.UpdatePosition(
                        _viewModel.Position.TotalSeconds,
                        _viewModel.Duration.TotalSeconds);
                }
            }
            else
            {
                // PiP failed — restore main window display
                MpvVideoView.DisplayEnabled = true;
                DebugLog("OnPipToggled: PIP returned null");
                ShowOsdNotification("PiP failed");
            }
        }
    }

    private void OnPipPlayPauseRequested(object? sender, EventArgs e)
    {
        _viewModel?.PlayPause();
    }

    private void OnPipSeekRequested(object? sender, double normalizedPos)
    {
        var player = _playerService?.Player;
        if (player == null) return;

        var duration = _viewModel?.Duration.TotalSeconds ?? 0;
        if (duration > 0)
        {
            var target = TimeSpan.FromSeconds(normalizedPos * duration);
            player.Seek(target);
        }
    }

    private void OnPipMuteToggled(object? sender, EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.IsMuted = !_viewModel.IsMuted;
    }

    private void SyncPipPosition(object? sender, PositionChangedEventArgs e)
    {
        if (_pipService is not { IsActive: true }) return;
        _pipService.PipWindow?.UpdatePosition(e.Position.TotalSeconds, e.Duration.TotalSeconds);
    }

    private void SyncPipPlayState(PlaybackState state)
    {
        if (_pipService is not { IsActive: true }) return;
        _pipService.PipWindow?.SetPlayingState(state == PlaybackState.Playing);
    }

    private void SyncPipReplayMode(bool isEnded)
    {
        if (_pipService is not { IsActive: true }) return;
        _pipService.PipWindow?.SetReplayMode(isEnded);
    }

    private void OnPipClosed(object? sender, EventArgs e)
    {
        _headerBar.SetPipChecked(false);
        // Resume showing video in main window
        MpvVideoView.DisplayEnabled = true;
    }
}
