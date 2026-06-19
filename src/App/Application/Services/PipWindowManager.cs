using System;
using System.IO;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia.Services;

/// <summary>
/// Orchestrates Picture-in-Picture lifecycle: toggling, position/state sync,
/// and event forwarding between PipService and MainWindow/MainViewModel.
/// Extracted from MainWindow.Pip.cs to reduce MainWindow partial file count.
/// </summary>
public sealed class PipWindowManager : IDisposable
{
    private readonly PipService _pipService;
    private readonly MainViewModel _viewModel;
    private readonly HeaderBarControl _headerBar;
    private readonly ControlsBoxControl _controlsBox;
    private readonly MpvVideoView _mpvVideoView;
    private readonly PlayerService _playerService;
    private readonly Action<string> _showOsdNotification;
    private bool _disposed;

    public PipWindowManager(
        PipService pipService,
        MainViewModel viewModel,
        HeaderBarControl headerBar,
        ControlsBoxControl controlsBox,
        MpvVideoView mpvVideoView,
        PlayerService playerService,
        Action<string> showOsdNotification)
    {
        _pipService = pipService ?? throw new ArgumentNullException(nameof(pipService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _headerBar = headerBar ?? throw new ArgumentNullException(nameof(headerBar));
        _controlsBox = controlsBox ?? throw new ArgumentNullException(nameof(controlsBox));
        _mpvVideoView = mpvVideoView ?? throw new ArgumentNullException(nameof(mpvVideoView));
        _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
        _showOsdNotification = showOsdNotification ?? throw new ArgumentNullException(nameof(showOsdNotification));

        WireEvents();
    }

    private void WireEvents()
    {
        _pipService.PlayPauseRequested += OnPipPlayPauseRequested;
        _pipService.SeekRequested += OnPipSeekRequested;
        _pipService.MuteToggled += OnPipMuteToggled;
        _pipService.PipClosed += OnPipClosed;
    }

    public bool IsActive => _pipService.IsActive;

    // ── Public entry point — called from MainWindow ──

    /// <summary>
    /// Called when the user toggles PiP (from header bar or fullscreen header).
    /// </summary>
    public void OnPipToggled(object? sender, EventArgs e)
    {
        if (_pipService.IsActive)
        {
            ExitPip();
        }
        else
        {
            EnterPip();
        }
    }

    // ── Position / state sync (called from MainWindow event handlers) ──

    public void SyncPosition(object? sender, PositionChangedEventArgs e)
    {
        if (!_pipService.IsActive) return;
        _pipService.PipWindow?.UpdatePosition(e.Position.TotalSeconds, e.Duration.TotalSeconds);
    }

    public void SyncPlayState(PlaybackState state)
    {
        if (!_pipService.IsActive) return;
        _pipService.PipWindow?.SetPlayingState(state == PlaybackState.Playing);
    }

    public void SyncReplayMode(bool isEnded)
    {
        if (!_pipService.IsActive) return;
        _pipService.PipWindow?.SetReplayMode(isEnded);
    }

    // ── Private helpers ──

    private void EnterPip()
    {
        if (string.IsNullOrEmpty(_viewModel.FilePath))
        {
            _showOsdNotification("No media loaded");
            return;
        }

        _mpvVideoView.DisplayEnabled = false;

        var pipWindow = _pipService.EnterPip();

        if (pipWindow != null)
        {
            _headerBar.SetPipChecked(true);
            pipWindow.SetPlayingState(_viewModel.IsPlaying);
            pipWindow.SetMuted(_viewModel.IsMuted);

            string fileName = Path.GetFileName(_viewModel.FilePath);
            pipWindow.SetFileName(fileName, fileName);

            var player = _playerService.Player;
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
            _mpvVideoView.DisplayEnabled = true;
            _showOsdNotification("PiP failed");
        }
    }

    private void ExitPip()
    {
        _pipService.ExitPip();
        _headerBar.SetPipChecked(false);
        _mpvVideoView.DisplayEnabled = true;
        if (_controlsBox != null)
            _controlsBox.SyncPlayPauseIcon(_viewModel.IsPlaying);
    }

    // ── PipService event handlers ──

    private void OnPipPlayPauseRequested(object? sender, EventArgs e)
    {
        _viewModel.PlayPause();
    }

    private void OnPipSeekRequested(object? sender, double normalizedPos)
    {
        var player = _playerService.Player;
        if (player == null) return;

        var duration = _viewModel.Duration.TotalSeconds;
        if (duration > 0)
        {
            var target = TimeSpan.FromSeconds(normalizedPos * duration);
            player.Seek(target);
        }
    }

    private void OnPipMuteToggled(object? sender, EventArgs e)
    {
        _viewModel.IsMuted = !_viewModel.IsMuted;
    }

    private void OnPipClosed(object? sender, EventArgs e)
    {
        _headerBar.SetPipChecked(false);
        _mpvVideoView.DisplayEnabled = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipService.PlayPauseRequested -= OnPipPlayPauseRequested;
        _pipService.SeekRequested -= OnPipSeekRequested;
        _pipService.MuteToggled -= OnPipMuteToggled;
        _pipService.PipClosed -= OnPipClosed;
    }
}
