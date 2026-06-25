using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Services;
using Material.Icons;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Models;
using Cine.Avalonia.ViewModels;
using Cine.Core;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia;

/// <summary>
/// Window initialization, ANGLE/mpv setup, event wiring, and startup (OnOpened).
/// Extracted from MainWindow.Core.cs to keep partial files manageable.
/// </summary>
public partial class MainWindow
{
    /// <summary>Invoked once from OnTemplateApplied or constructor.</summary>
    private void OnWindowInitialized()
    {
        DebugLog("OnWindowInitialized start");

        // Resolve component references
        _headerBar = HeaderBarControl;
        _controlsBox = ControlsBoxControl;
        _fullscreenHeader = FullscreenHeaderControl;
        _spinnerOverlay = LoadingSpinnerOverlay;
        _pauseOverlay = PauseOverlay;
        _replayOverlay = ReplayOverlay;
        _dropIndicator = DropIndicatorOverlay;
        _osdNotification = OsdNotificationControl;

        ReportWindowState("MainWindow.OnWindowInitialized.AfterResolve");

        _inputRouter ??= new InputRoutingService();
        RegisterKeyboardShortcuts();

        _playerService ??= new PlayerService();
        try
        {
            _playerService.Initialize();
        }
        catch (Exception ex)
        {
            DebugLog($"Player initialization FAILED: {ex}");
            Log.ForContext<MainWindow>().Error(ex, "Player initialization failed");
            _isDisposed = true;
            _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                await ShowErrorDialog("Failed to initialize media player.", ex.Message);
                Close();
            });
            return;
        }

        var player = _playerService.Player;
        if (player == null)
        {
            _isDisposed = true;
            _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                await ShowErrorDialog("Media player returned null.", "The application cannot continue.");
                Close();
            });
            return;
        }

        if (_isDisposed) return;

        // Create domain managers — single source of truth for their domains
        _audioManager = new AudioManager(player);
        _videoManager = new VideoManager(player);
        _subtitleManager = new SubtitleManager(player);

        // Init centralized file-dialog handler (Avalonia #21433 workaround applied)
        _dialogHandler = new FileDialogHandler(this);

        // Flyout ecosystem manager — ensures only ONE flyout is open at a time,
        // creating the professional "close previous → open next" UX contract.
        _flyoutManager = new FlyoutManager();
        _controlsBox.FlyoutManager = _flyoutManager;
        _headerBar.FlyoutManager = _flyoutManager;

        // Wire reopen actions for flyouts that trigger native dialogs (Avalonia #18969):
        // after the dialog completes, the flyout is shown again automatically.
        _flyoutManager.SetReopen("open-menu", () => _headerBar.ReopenFlyout());
        _flyoutManager.SetReopen("subtitle", () => _controlsBox.SubtitleOverlayCtrl?.ReopenFlyout());
        _flyoutManager.SetReopen("audio", () => _controlsBox.AudioTrackSelectorCtrl?.ReopenFlyout());

        // Update OnBeforeOpen to use the centralized manager
        _dialogHandler.OnBeforeOpen = () => _flyoutManager.CloseAll();

        var fileDialogService = new FileDialogService(_dialogHandler);

        _viewModel = new MainViewModel(player, null, null, _audioManager, _videoManager, _subtitleManager,
            rendererService: null, mediaFileService: null, fileDialogService: fileDialogService);
        DataContext = _viewModel;

        _viewModel.SessionResumeRequested = (path, pos) =>
        {
            _queuedOpenPath = path;
            _sessionResumePosition = pos;
            ShowOsdNotification($"Resume {Path.GetFileName(path)} from {pos.Minutes:D2}:{pos.Seconds:D2}?", 5000);
            _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                await Task.Delay(4000);
                if (!string.IsNullOrEmpty(_queuedOpenPath) && File.Exists(_queuedOpenPath))
                {
                    var p = _queuedOpenPath;
                    var resumePos = _sessionResumePosition;
                    _queuedOpenPath = null;
                    _sessionResumePosition = TimeSpan.Zero;
                    if (_viewModel != null)
                    {
                        _ = _viewModel.OpenFile(p);
                        _viewModel.ClearSession();
                    }
                    if (resumePos.TotalSeconds > 0)
                    {
                        EventHandler? handler = null;
                        handler = (s, args) =>
                        {
                            _playerService?.Player?.Seek(resumePos);
                            var playerInstance = _playerService?.Player;
                            if (playerInstance != null) playerInstance.Opened -= handler;
                        };
                        var playerInstance = _playerService?.Player;
                        if (playerInstance != null) playerInstance.Opened += handler;
                    }
                }
            });
        };
        // P5.1: Session resume moved to OnOpened to let start page show first

        _viewModel.Playlist.CollectionChanged += (_, _) => _viewModel?.SaveSession();

        player.Opened += OnMediaOpened;
        player.PlaybackStateChangedEvent += OnPlaybackStateChanged;
        player.PositionChanged += OnPositionChanged;
        player.ChapterListChanged += OnChapterListChanged;
        player.FullscreenChangedEvent += OnPlayerFullscreenChanged;

        // Create PlaybackStateManager — the single authoritative source for
        // playback state. All UI consumers read from, this, not from player directly.
        _stateManager = new PlaybackStateManager(player);
        _stateManager.StateChanged += OnManagerStateChanged;

        // Sync initial icon state. StateChanged won't fire for the current state
        // since it was already set in the PlaybackStateManager constructor before
        // our handler was subscribed.
        _controlsBox.SyncPlayPauseIcon(_stateManager.IsPlaying);
        SyncPipPlayState(_stateManager.State);

        _playerService.Error += (_, error) =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                _spinnerOverlay.Stop();
                _isLoading = false;
                ShowOsdNotification($"Error: {error}", 4000);
            });
        };

        // Initialize OpenGL render API (no fallback — this MUST succeed)
        try
        {
            InitVideoRenderer();
        }
        catch (Exception ex)
        {
            DebugLog($"InitVideoRenderer FAILED: {ex}");
            _isDisposed = true;
            _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                await ShowErrorDialog("Video renderer initialization failed.",
                    "The OpenGL render API could not be initialized.\n" +
                    "This usually means ANGLE (libEGL.dll/libGLESv2.dll) was not found.\n" +
                    $"Details: {ex.Message}");
                Close();
            });
            return;
        }

        KeyDown += OnKeyDown;

        // Pointer events on transparent overlay (topmost, catches all mouse activity)
        VideoClickOverlay.PointerMoved += OnWindowPointerMoved;

        // P12: Hover tracking — direct PointerEntered/Exited on each overlay element
        // Mirrors Python's EventController + contains_pointer checks
        _headerBar.HeaderBar.PointerEntered += OnHeaderPointerEntered;
        _headerBar.HeaderBar.PointerExited += OnHeaderPointerExited;
        _controlsBox.ControlsBox.PointerEntered += OnControlsPointerEntered;
        _controlsBox.ControlsBox.PointerExited += OnControlsPointerExited;
        _fullscreenHeader.FullscreenHeader.PointerEntered += OnFullscreenHeaderPointerEntered;
        _fullscreenHeader.FullscreenHeader.PointerExited += OnFullscreenHeaderPointerExited;

        // P6.6: Window backdrop opacity
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;

        if (_viewModel != null)
        {
            SetupPropertyWatchers();
        }

        // Wire up component events
        _replayOverlay.ReplayRequested += (_, _) =>
        {
            var player = _playerService?.Player;
            if (player == null) return;
            // Force reset from EOF state: stop, seek to start, then play
            player.Stop();
            player.Seek(TimeSpan.Zero);
            player.Play();
        };

        _osdNotification.NotificationClicked += OnOsdNotificationClicked;

        // Wire external file drop events from standalone overlay controls
        if (_controlsBox.SubtitleOverlayCtrl != null)
            _controlsBox.SubtitleOverlayCtrl.ExternalFileDropped += (_, path) =>
                ShowOsdNotification(MaterialIconKind.ClosedCaption,
                    $"Subtitle loaded: {Path.GetFileName(path)}");

        // SubtitleManager OSD feedback (font size, position, delay changes) — REMOVED
        // Settings changes are now handled via the SubtitleSettingsDialog.
        if (_controlsBox.AudioTrackSelectorCtrl != null)
            _controlsBox.AudioTrackSelectorCtrl.ExternalFileDropped += (_, path) =>
                ShowOsdNotification(MaterialIconKind.Music,
                    $"Audio track loaded: {Path.GetFileName(path)}");

        _controlsBox.SeekBarControl.InitializeSeekBar();
        _controlsBox.SeekBarControl.SeekWheelChanged += (_, delta) =>
        {
            if (delta > 0) _viewModel?.SeekForward();
            else _viewModel?.SeekBackward();
        };

        InitializeAutoHide();
        InitializeSessionSave();
        InitializeResponsiveLayout();

        // Initialize PIP manager — owns PipService + bridges events to UI
        _pipWindowManager = new PipWindowManager(
            new PipService(MpvVideoView),
            _viewModel!,
            _headerBar,
            _controlsBox,
            MpvVideoView,
            _playerService!,
            msg => ShowOsdNotification(msg));

        // Wire header toggle buttons → OnPipToggled → PipWindowManager
        _headerBar.PipToggled += OnPipToggled;
        _fullscreenHeader.PipToggled += OnPipToggled;

        AddHandler(global::Avalonia.Input.DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(global::Avalonia.Input.DragDrop.DragLeaveEvent, OnWindowDragLeave);
        AddHandler(global::Avalonia.Input.DragDrop.DropEvent, OnWindowDrop);

        ReportWindowState("MainWindow.OnWindowInitialized.Finish");
        DebugLog("OnWindowInitialized finish");
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_isDisposed) return;
        DebugLog("OnOpened enter");
        ReportWindowState("MainWindow.OnOpened.Enter");

        var startupWatch = Stopwatch.StartNew();

        try
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            // Only center manually if no saved state exists — prevents race with restore
            if (!File.Exists(WindowStatePath))
            {
                var primary = Screens?.Primary;
                if (primary != null)
                {
                    var work = primary.WorkingArea;
                    double scale = RenderScaling;
                    int w = (int)Math.Max(600 * scale, Bounds.Width * scale);
                    int h = (int)Math.Max(337 * scale, Bounds.Height * scale);
                    int x = work.X + Math.Max(0, (work.Width - w) / 2);
                    int y = work.Y + Math.Max(0, (work.Height - h) / 2);
                    Position = new PixelPoint(x, y);
                }
            }

            Activate();
        }
        catch (Exception ex) { Log.ForContext<MainWindow>().Error(ex, "Centering/Activate failed"); }

        if (StartPage != null) StartPage.IsVisible = true;

        _headerBar.HideOpenMenu();
        _headerBar.HidePrimaryMenu();
        _headerBar.SetPipVisibility(false);
        if (_controlsBox != null) _controlsBox.SetControlsVisibility(false);

        // Show header bar only (window controls + title), not playback controls.
        // ShowUiControls shows everything — only call when media is loaded.
        if (_headerBar.HeaderBar != null)
        {
            bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;
            _headerBar.HeaderBar.IsVisible = !isFullscreen;
            _headerBar.HeaderBar.Opacity = isFullscreen ? 0 : 1;
            _headerBar.HeaderBar.IsHitTestVisible = !isFullscreen;
        }

        // P5.1a: Restore playlist items (does not open any file — purely UI)
        _viewModel?.LoadPlaylist();

        // P5.1b: Resume session only if no command-line file was queued
        if (string.IsNullOrEmpty(_queuedOpenPath))
            _viewModel?.LoadSession();

        _headerBar.UpdateMaximizeIcon(WindowState == global::Avalonia.Controls.WindowState.Maximized);
        _controlsBox?.SubtitleOverlayCtrl?.RefreshIcon();
        _controlsBox?.AudioTrackSelectorCtrl?.RefreshIcon();
        _controlsBox?.RefreshVolumeIcon();

        // Wire flyout dismissal before file dialogs open (prevents dialog overlap)
        if (_viewModel?.Subtitles is { } subMgr)
            subMgr.DismissFlyoutAsync = () =>
            {
                _controlsBox?.SubtitleOverlayCtrl?.HideFlyout();
                return Task.CompletedTask;
            };
        if (_viewModel?.Audio is { } audMgr)
            audMgr.DismissFlyoutAsync = () =>
            {
                _controlsBox?.AudioTrackSelectorCtrl?.HideFlyout();
                return Task.CompletedTask;
            };
        ReportWindowState("MainWindow.OnOpened.AfterInitialState");
        Dispatcher.UIThread.OnUiThread(() => ReportWindowState("MainWindow.OnOpened.PostLayout"), DispatcherPriority.Background);

        // P5.2: Restore window position and size
        Dispatcher.UIThread.OnUiThread(() =>
        {
            Result.From(() =>
            {
                if (!File.Exists(WindowStatePath)) return;
                var json = File.ReadAllText(WindowStatePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Read maximized flag first to decide whether to set Width/Height
                var shouldBeMaximized = root.TryGetProperty("Maximized", out var maxEl) && maxEl.GetBoolean();

                if (!shouldBeMaximized)
                {
                    if (root.TryGetProperty("Width", out var wEl) && root.TryGetProperty("Height", out var hEl))
                    {
                        var w = wEl.GetDouble();
                        var h = hEl.GetDouble();
                        if (w >= 800 && h >= 400) { Width = w; Height = h; }
                    }
                    if (root.TryGetProperty("X", out var xEl) && root.TryGetProperty("Y", out var yEl))
                    {
                        var x = xEl.GetInt32();
                        var y = yEl.GetInt32();
                        // Validate position is visible on at least one screen
                        var proposedPos = new PixelPoint(x, y);
                        var isOnScreen = Screens?.All.Any(s =>
                        {
                            var b = s.Bounds;
                            return proposedPos.X >= b.X && proposedPos.X < b.X + b.Width - 100 &&
                                   proposedPos.Y >= b.Y && proposedPos.Y < b.Y + b.Height - 50;
                        }) ?? false;
                        if (isOnScreen)
                            Position = proposedPos;
                        else
                            WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }

                if (shouldBeMaximized)
                    WindowState = WindowState.Maximized;
            });
        }, DispatcherPriority.Background);

        // P10.4: Log startup timing
        startupWatch.Stop();
        DebugLog($"Startup complete in {startupWatch.Elapsed.TotalMilliseconds:F0}ms");

        // P10.4: Deferred non-critical init — runs after window is fully painted
        Dispatcher.UIThread.OnUiThread(async () =>
        {
            await Task.Delay(100); // Let the first frame render
            _controlsBox?.SeekBarControl.InitializeSeekBar();
            DebugLog("Deferred init complete");
        }, DispatcherPriority.Background);
    }

    private void InitVideoRenderer()
    {
        var player = _playerService?.Player as MpvPlayer;
        if (player == null || _viewModel == null)
        {
            DebugLog("InitVideoRenderer: player or viewModel is null");
            return;
        }

        DebugLog("InitVideoRenderer: initializing MpvVideoView (ANGLE + render API)");

        // Main window uses ANGLE/OpenGL render API.
        // MpvVideoView creates its own ANGLE context, initializes mpv render API,
        // and runs a dedicated render thread that updates a WriteableBitmap Image.
        // This bypasses Avalonia's OpenGlControlBase which can fail silently in v12.
        MpvVideoView.Initialize(player);
    }

    private void InitializeSessionSave()
    {
        _sessionSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _sessionSaveTimer.Tick += (_, _) => _viewModel?.SaveSession();
        _sessionSaveTimer.Start();
    }
}
