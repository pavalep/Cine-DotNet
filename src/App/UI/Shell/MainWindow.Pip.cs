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
            _pipService.ExitPip();
        }
        else
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.FilePath))
            {
                ShowOsdNotification("No media loaded");
                return;
            }

            var pipWindow = _pipService.EnterPip();

            if (pipWindow != null)
            {
                // Sync initial play state
                pipWindow.SetPlayingState(_viewModel.IsPlaying);

                // Wire position updates
                var player = _playerService?.Player;
                if (player != null)
                {
                    // Push current position immediately
                    pipWindow.UpdatePosition(
                        _viewModel.Position.TotalSeconds,
                        _viewModel.Duration.TotalSeconds);
                }
            }
            else
            {
                ShowOsdNotification("PIP failed to start");
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
}
