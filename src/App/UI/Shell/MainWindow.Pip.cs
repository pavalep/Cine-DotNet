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
            // Restore video in main window + resync thumbnail rect
            if (_videoHost != null)
            {
                _videoHost.IsVideoSurfaceVisible = true;
                SyncThumbnailRect();
            }
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
            if (_dwmManager is { SourceHwnd: var source } && source == IntPtr.Zero)
            {
                DebugLog("OnPipToggled: DWM source missing, retrying registration");
                _videoHost?.EnsureHiddenWindowCreated();
                TryRegisterDwmThumbnail();
            }
            var pipWindow = _pipService?.EnterPip();

            if (pipWindow != null)
            {
                DebugLog("OnPipToggled: PIP started successfully");
                // Hide video in main window — video only visible in PIP
                if (_videoHost != null) _videoHost.IsVideoSurfaceVisible = false;

                _headerBar.SetPipChecked(true);

                // Sync initial play state
                pipWindow.SetPlayingState(_viewModel.IsPlaying);

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

    /// <summary>Restores main window video when PIP window is closed by user (close button).</summary>
    private void OnPipClosed(object? sender, EventArgs e)
    {
        if (_videoHost != null)
        {
            _videoHost.IsVideoSurfaceVisible = true;
            SyncThumbnailRect();
        }
        _headerBar.SetPipChecked(false);
    }
}
