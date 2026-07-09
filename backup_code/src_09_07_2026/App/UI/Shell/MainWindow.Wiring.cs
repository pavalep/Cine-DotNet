using System;
using System.IO;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.State;
using Cine.Avalonia.Services;
using Cine.Media.Interfaces;
using Material.Icons;

namespace Cine.Avalonia;

/// <summary>
/// Event wiring, property watchers, and component subscriptions.
/// Extracted from MainWindow.Initialization.cs (~444 lines → ~100 lines here).
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Wires all player events, property watchers, pointer events, and
    /// component subscriptions after initialization is complete.
    /// Called from OnWindowInitialized() after managers are created.
    /// </summary>
    private void InitializeWiring(Cine.Media.Interfaces.IMediaPlayer player)
    {
        player.Opened += OnMediaOpened;
        player.PlaybackStateChangedEvent += OnPlaybackStateChanged;
        player.PositionChanged += OnPositionChanged;
        player.ChapterListChanged += OnChapterListChanged;
        player.FullscreenChangedEvent += OnPlayerFullscreenChanged;

        // Create PlaybackStateManager — the single authoritative source for
        // playback state. All UI consumers read from this, not from player directly.
        _stateManager = new PlaybackStateManager(player);
        _stateManager.StateChanged += OnManagerStateChanged;

        // Sync initial icon state
        _controlsBox.SyncPlayPauseIcon(_stateManager.IsPlaying);
        SyncPipPlayState(_stateManager.State);

        if (_playerService != null)
            _playerService.Error += (_, error) =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                _spinnerOverlay.Stop();
                _isLoading = false;
                ShowOsdNotification($"Error: {error}", 4000);
            });
        };

        VideoClickOverlay.PointerMoved += OnWindowPointerMoved;

        // Hover tracking
        _headerBar.HeaderBarElement.PointerEntered += OnHeaderPointerEntered;
        _headerBar.HeaderBarElement.PointerExited += OnHeaderPointerExited;
        _headerBar.HeaderBarElement.PointerPressed += OnHeaderPointerPressed;
        _controlsBox.ControlsBoxElement.PointerEntered += OnControlsPointerEntered;
        _controlsBox.ControlsBoxElement.PointerExited += OnControlsPointerExited;
        _fullscreenHeader.FullscreenHeaderElement.PointerEntered += OnFullscreenHeaderPointerEntered;
        _fullscreenHeader.FullscreenHeaderElement.PointerExited += OnFullscreenHeaderPointerExited;

        // Window backdrop opacity
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;

        if (_viewModel != null)
        {
            SetupPropertyWatchers();
        }

        // Component events
        _replayOverlay.ReplayRequested -= OnReplayRequested;
        _replayOverlay.ReplayRequested += OnReplayRequested;

        _osdNotification.NotificationClicked += OnOsdNotificationClicked;

        // External file drop events from overlay controls
        if (_controlsBox.SubtitleOverlay != null)
            _controlsBox.SubtitleOverlay.ExternalFileDropped += (_, path) =>
                ShowOsdNotification(MaterialIconKind.ClosedCaption,
                    $"Subtitle loaded: {Path.GetFileName(path)}");

        if (_controlsBox.AudioTrackSelector != null)
            _controlsBox.AudioTrackSelector.ExternalFileDropped += (_, path) =>
                ShowOsdNotification(MaterialIconKind.Music,
                    $"Audio track loaded: {Path.GetFileName(path)}");

        _controlsBox.SeekBarControl.InitializeSeekBar();
        _controlsBox.SeekBarControl.SeekWheelChanged += (_, delta) =>
        {
            if (delta > 0) _viewModel?.SeekForward();
            else _viewModel?.SeekBackward();
        };

        // Pause auto-hide timer while seeking to prevent flicker
        // (time hint popover triggers show/hide cycle during seek)
        _controlsBox.SeekBarControl.SeekStarted += (_, _) =>
            _autoHideTimer?.Stop();
        _controlsBox.SeekBarControl.SeekEnded += (_, _) =>
            _autoHideTimer?.Start();

        InitializeAutoHide();
        InitializeSessionSave();
        InitializeResponsiveLayout();

        // PIP manager
        _pipWindowManager = new PipWindowManager(
            new PipService(MpvVideoView),
            _viewModel!,
            _headerBar,
            _controlsBox,
            MpvVideoView,
            _playerService!,
            msg => ShowOsdNotification(msg));

        _headerBar.PipToggled += OnPipToggled;
        _fullscreenHeader.PipToggled += OnPipToggled;

        // Window-level drag & drop — fires even when StartPage is hidden (video playing).
        // handledEventsToo: true ensures these fire even if a child already handled the event.
        AddHandler(DragDrop.DragEnterEvent, OnWindowDragEnter, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent,  OnWindowDragOver,  handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent,      OnWindowDrop,      handledEventsToo: true);
    }

    private void OnReplayRequested(object? sender, EventArgs e)
    {
        var p = _playerService?.Player;
        if (p == null) return;
        p.Stop();
        p.Seek(TimeSpan.Zero);
        p.Play();
    }
}
