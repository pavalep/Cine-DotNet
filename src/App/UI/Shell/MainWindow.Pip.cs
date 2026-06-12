using Cine.Avalonia.ViewModels;
using Cine.Media.Events;

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
        }
        else
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.FilePath))
            {
                ShowOsdNotification("No media loaded");
                return;
            }

            DebugLog("OnPipToggled: entering PIP");
            var pipWindow = _pipService?.EnterPip();

            if (pipWindow != null)
            {
                DebugLog("OnPipToggled: PIP started successfully");
                _headerBar.SetPipChecked(true);

                // Sync initial play state
                pipWindow.SetPlayingState(_viewModel.IsPlaying);
                pipWindow.SetMuted(_viewModel.IsMuted);

                // Pass file info
                string fileName = Path.GetFileName(_viewModel.FilePath);
                string folder = Path.GetFileName(Path.GetDirectoryName(_viewModel.FilePath)) ?? "";
                pipWindow.SetFileName(fileName, folder);

                // Set aspect ratio from video dimensions
                var player = _playerService?.Player;
                if (player != null)
                {
                    player.GetVideoSize(out int vw, out int vh);
                    if (vw > 0 && vh > 0)
                        pipWindow.SetAspectRatio((double)vw / vh);

                    // Push current position immediately
                    pipWindow.UpdatePosition(
                        _viewModel.Position.TotalSeconds,
                        _viewModel.Duration.TotalSeconds);
                }
            }
            else
            {
                DebugLog("OnPipToggled: PIP returned null");
                ShowOsdNotification("PiP failed — check cine_pip.log");
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

    private void SyncPipPlayState()
    {
        if (_pipService is not { IsActive: true }) return;
        _pipService.PipWindow?.SetPlayingState(_viewModel?.IsPlaying == true);
    }

    private void SyncPipReplayMode(bool isEnded)
    {
        if (_pipService is not { IsActive: true }) return;
        _pipService.PipWindow?.SetReplayMode(isEnded);
    }

    /// <summary>Restores main window video when PIP window is closed by user (close button).</summary>
    private void OnPipClosed(object? sender, EventArgs e)
    {
        _headerBar.SetPipChecked(false);
    }
}
