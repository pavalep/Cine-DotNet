using System;
using System.IO;
using Cine.Avalonia.Components;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Dialogs;
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
    private readonly IPipService _pipService;
    private readonly MainViewModel _viewModel;
    private readonly HeaderBar _headerBar;
    private readonly ControlsBox _controlsBox;
    private readonly MpvVideoView _mpvVideoView;
    private readonly PlayerService _playerService;
    private readonly Action<string> _showOsdNotification;
    private bool _disposed;

    public PipWindowManager(
        IPipService pipService,
        MainViewModel? viewModel,
        HeaderBar? headerBar,
        ControlsBox? controlsBox,
        MpvVideoView? mpvVideoView,
        PlayerService? playerService,
        Action<string>? showOsdNotification)
    {
        _pipService = pipService ?? throw new ArgumentNullException(nameof(pipService));
        _viewModel = viewModel!;
        _headerBar = headerBar!;
        _controlsBox = controlsBox!;
        _mpvVideoView = mpvVideoView!;
        _playerService = playerService!;
        _showOsdNotification = showOsdNotification ?? (_ => { });

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

        if (_mpvVideoView != null)
            _mpvVideoView.DisplayEnabled = false;

        var pipWindow = _pipService.EnterPip();

        if (pipWindow != null)
        {
            _headerBar?.SetPipChecked(true);
            pipWindow.SetPlayingState(_viewModel.IsPlaying);
            pipWindow.SetMuted(_viewModel.IsMuted);

            string fileName = Path.GetFileName(_viewModel.FilePath);
            pipWindow.SetFileName(fileName, fileName);

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
            if (_mpvVideoView != null)
                _mpvVideoView.DisplayEnabled = true;
            _showOsdNotification("PiP failed");
        }
    }

    private void ExitPip()
    {
        _pipService.ExitPip();
        _headerBar?.SetPipChecked(false);
        if (_mpvVideoView != null)
            _mpvVideoView.DisplayEnabled = true;
        if (_controlsBox != null)
            _controlsBox.SyncPlayPauseIcon(_viewModel?.IsPlaying ?? false);
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
        _headerBar?.SetPipChecked(false);
        if (_mpvVideoView != null)
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
        _pipService.Dispose();
    }
}
