using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cine.Avalonia.Components;
using Cine.Avalonia.Services;
using Material.Icons;
using Cine.Avalonia.State;
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

        _startupTimer.Mark("input-router");
        _inputRouter ??= new InputRoutingService();
        RegisterKeyboardShortcuts();

        _startupTimer.Mark("player-init");

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

        _startupTimer.Mark("managers");

        // Create domain managers — single source of truth for their domains
        _audioManager = new AudioManager(player);
        _videoManager = new VideoManager(player);
        _subtitleManager = new SubtitleManager(player);

        // Phase 7: Wire OSD feedback for track switching
        _subtitleManager.TrackChangedMessage = msg =>
            ShowOsdNotification(MaterialIconKind.ClosedCaption, $"Subtitle: {msg}");
        _audioManager.TrackChangedMessage = msg =>
            ShowOsdNotification(MaterialIconKind.Music, $"Audio: {msg}");

        // Init centralized file-dialog handler (Avalonia #21433 workaround applied)
        _dialogHandler = new FileDialogHandler(this);

        // Flyout ecosystem manager — ensures only ONE flyout is open at a time,
        // creating the professional "close previous → open next" UX contract.
        _flyoutManager = new FlyoutManager();
        _controlsBox.FlyoutManager = _flyoutManager;
        _headerBar.FlyoutManager = _flyoutManager;
        _fullscreenHeader.FlyoutManager = _flyoutManager;

        // Wire reopen actions for flyouts that trigger native dialogs (Avalonia #18969):
        // after the dialog completes, the flyout is shown again automatically.
        _flyoutManager.SetReopen("open-menu", () => _headerBar.ReopenFlyout());
        _flyoutManager.SetReopen("subtitle", () => _controlsBox.SubtitleOverlay?.ReopenFlyout());
        _flyoutManager.SetReopen("audio", () => _controlsBox.AudioTrackSelector?.ReopenFlyout());

        // Update OnBeforeOpen to use the centralized manager
        _dialogHandler.OnBeforeOpen = () => _flyoutManager.CloseAll();

        var fileDialogService = new FileDialogService(_dialogHandler);

        _viewModel = new MainViewModel(player, null, null, _audioManager, _videoManager, _subtitleManager,
            rendererService: null, mediaFileService: null, fileDialogService: fileDialogService);
        DataContext = _viewModel;

        _startupTimer.Mark("viewmodel");

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

        // Wire all player events, watchers, and component subscriptions
        InitializeWiring(player);

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

        // Initialize OpenGL render API (no fallback — this MUST succeed)
        _startupTimer.Mark("video-renderer");
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

        ReportWindowState("MainWindow.OnWindowInitialized.Finish");
        _startupTimer.Mark("init-complete");
        DebugLog("OnWindowInitialized finish");
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_isDisposed) return;
        DebugLog("OnOpened enter");
        ReportWindowState("MainWindow.OnOpened.Enter");

        _startupTimer.Mark("opened");

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
        if (_headerBar.HeaderBarElement != null)
        {
            bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;
            _headerBar.HeaderBarElement.IsVisible = !isFullscreen;
            _headerBar.HeaderBarElement.Opacity = isFullscreen ? 0 : 1;
            _headerBar.HeaderBarElement.IsHitTestVisible = !isFullscreen;
        }

        // P5.1a: Restore playlist items (does not open any file — purely UI)
        _viewModel?.LoadPlaylist();

        // P5.1b: Resume session only if no command-line file was queued
        if (string.IsNullOrEmpty(_queuedOpenPath))
            _viewModel?.LoadSession();

        _headerBar.UpdateMaximizeIcon(WindowState == global::Avalonia.Controls.WindowState.Maximized);
        _controlsBox?.SubtitleOverlay?.RefreshIcon();
        _controlsBox?.AudioTrackSelector?.RefreshIcon();
        _controlsBox?.VolumeFlyoutCtrl?.RefreshIcon();

        // Wire flyout dismissal before file dialogs open (prevents dialog overlap)
        if (_viewModel?.Subtitles is { } subMgr)
            subMgr.DismissFlyoutAsync = () =>
            {
                _controlsBox?.SubtitleOverlay?.HideFlyout();
                return Task.CompletedTask;
            };
        if (_viewModel?.Audio is { } audMgr)
            audMgr.DismissFlyoutAsync = () =>
            {
                _controlsBox?.AudioTrackSelector?.HideFlyout();
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

        // P10.4: Log startup timing breakdown
        var perfSummary = _startupTimer.Finalize();
        DebugLog(perfSummary);

        // P10.4: Deferred non-critical init — runs after window is fully painted
        Dispatcher.UIThread.OnUiThread(async () =>
        {
            await Task.Delay(100); // Let the first frame render
            _controlsBox?.SeekBarControl.InitializeSeekBar();
            DebugLog("Deferred init complete");
        }, DispatcherPriority.Background);
    }
}
