/// Lifecycle: initialization, startup, and event wiring

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Core;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Features;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Models;
using Cine.Avalonia.Core.Navigation;
using Cine.Avalonia.Services;
using Cine.Avalonia.Services.UI;
using Cine.Avalonia.Storage;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.ViewModels.Pages;
using Cine.Avalonia.Views.Components;
using Cine.Avalonia.Views.Dialogs;
using Cine.Core;
using Material.Icons;
using Cine.Media.Events;
using Cine.Media.Implementations;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cine.Avalonia.Views.Shell;

public partial class MainWindow
{
    /// <summary>Invoked once from OnTemplateApplied or constructor.</summary>
    private void OnWindowInitialized()
    {
        DebugLog("OnWindowInitialized start");

        // Resolve component references
        _osdService = _serviceProvider.GetRequiredService<IOsdService>();

        ReportWindowState("MainWindow.OnWindowInitialized.AfterResolve");

        _mediaFileService = _serviceProvider.GetRequiredService<IMediaFileService>();

        _startupTimer.Mark("input-router");
        _inputRouter = _serviceProvider.GetRequiredService<InputRoutingService>();
        RegisterKeyboardShortcuts();

        _startupTimer.Mark("player-init");

        _playerService = _serviceProvider.GetRequiredService<PlayerService>();
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

        // Resolve domain managers from DI — local vars, DI owns lifetimes
        var audioManager = _serviceProvider.GetRequiredService<IAudioManager>();
        var videoManager = _serviceProvider.GetRequiredService<VideoManager>();
        var subtitleManager = _serviceProvider.GetRequiredService<ISubtitleManager>();

        // Wire OSD feedback for track switching via EventBus (Phase 5.4)
        var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
        eventBus.Subscribe<TrackChangedEvent>(e =>
        {
            var icon = e.TrackType == "Subtitle"
                ? MaterialIconKind.ClosedCaption
                : MaterialIconKind.Music;
            _osdService.ShowWithIcon(icon, $"{e.TrackType}: {e.DisplayName}");
        });

        // Init centralized file-dialog handler (Avalonia #21433 workaround applied)
        _dialogHandler = new FileDialogHandler(this);

        // Flyout ecosystem manager — ensures only ONE flyout is open at a time,
        // creating the professional "close previous → open next" UX contract.
        _flyoutManager = _serviceProvider.GetRequiredService<IFlyoutService>();
        PlayerPage.ControlsBoxControl.FlyoutManager = _flyoutManager;
        PlayerPage.HeaderBarControl.FlyoutManager = _flyoutManager;
        PlayerPage.FullscreenHeaderControl.FlyoutManager = _flyoutManager;

        // Set EventBus on components that publish events (Phase 9)
        PlayerPage.HeaderBarControl.EventBus = eventBus;
        PlayerPage.FullscreenHeaderControl.EventBus = eventBus;
        PlayerPage.ReplayOverlay.EventBus = eventBus;
        PlayerPage.OsdNotificationControl.EventBus = eventBus;
        if (PlayerPage.ControlsBoxControl.SubtitleOverlay != null) PlayerPage.ControlsBoxControl.SubtitleOverlay.EventBus = eventBus;
        if (PlayerPage.ControlsBoxControl.AudioTrackSelector != null) PlayerPage.ControlsBoxControl.AudioTrackSelector.EventBus = eventBus;

        // Wire reopen actions for flyouts that trigger native dialogs (Avalonia #18969):
        // after the dialog completes, the flyout is shown again automatically.
        _flyoutManager.SetReopen("subtitle", () => PlayerPage.ControlsBoxControl.SubtitleOverlay?.ReopenFlyout());
        _flyoutManager.SetReopen("audio", () => PlayerPage.ControlsBoxControl.AudioTrackSelector?.ReopenFlyout());

        // Wire HeaderBar primary menu events → window-level handlers
        PlayerPage.HeaderBarControl.PrimaryPipToggled += (_, _) =>
            OnPipToggled(null, EventArgs.Empty);
        PlayerPage.HeaderBarControl.PrimaryAlwaysOnTopToggled += (_, _) =>
            Topmost = !Topmost;
        PlayerPage.HeaderBarControl.PrimaryShortcutsRequested += (_, _) =>
            new KeyboardShortcutsDialog().Show(this);
        PlayerPage.HeaderBarControl.PrimaryPreferencesRequested += (_, _) =>
            new PreferencesWindow().Show(this);
        PlayerPage.HeaderBarControl.PrimaryAboutRequested += (_, _) =>
            new PreferencesWindow().Show(this);

        // Update OnBeforeOpen to use the centralized manager
        _dialogHandler.OnBeforeOpen = () => _flyoutManager.CloseAll();

        // Navigation abstraction — commands flow through here instead of file-path watchers
        _navigationService = _serviceProvider.GetRequiredService<INavigationService>();
        _navigationService.Navigated += OnNavigated;

        var rendererService = _serviceProvider.GetRequiredService<IRendererService>();
        var sessionService = _serviceProvider.GetRequiredService<ISessionService>();
        var playlistService = _serviceProvider.GetRequiredService<IPlaylistService>();
        var mediaFileService = _serviceProvider.GetRequiredService<IMediaFileService>();
        var dragDropService = _serviceProvider.GetRequiredService<IDragDropService>();
        var fileDialogService = new FileDialogService(_dialogHandler);
        var featureService = _serviceProvider.GetRequiredService<IFeatureService>();
        var licensingService = _serviceProvider.GetRequiredService<ILicensingService>();
        var recentFilesService = _serviceProvider.GetRequiredService<IRecentFilesService>();

        _viewModel = new MainViewModel(player, sessionService, playlistService,
            audioManager, videoManager, subtitleManager,
            rendererService, mediaFileService, dragDropService, _navigationService,
            recentFilesService, _osdService, fileDialogService, featureService, licensingService);
        DataContext = _viewModel;
        ((OsdService)_osdService).NotificationControl = PlayerPage.OsdNotificationControl;

        // Resolve StartPageViewModel and assign as StartPage's DataContext (Phase 4)
        if (StartPage != null)
        {
            var startPageVm = new StartPageViewModel(mediaFileService, _navigationService, recentFilesService, fileDialogService);
            StartPage.DataContext = startPageVm;
        }

        _startupTimer.Mark("viewmodel");

        _viewModel.SessionResumeRequested = (path, pos) =>
        {
            _queuedOpenPath = path;
            _sessionResumePosition = pos;
            _osdService.ShowWithIcon(MaterialIconKind.PlayArrow, $"Resume {Path.GetFileName(path)} from {pos.Minutes:D2}:{pos.Seconds:D2}?", 5000);
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
        InitializeWiring(player, eventBus);

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

        if (StartPage != null) (StartPage as INavigable)?.OnNavigatedTo(null);

        PlayerPage.HeaderBarControl.HideOpenMenu();
        PlayerPage.HeaderBarControl.HidePrimaryMenu();
        PlayerPage.HeaderBarControl.SetPipVisibility(false);
        if (PlayerPage.ControlsBoxControl != null) PlayerPage.ControlsBoxControl.SetControlsVisibility(false);

        // Show header bar only (window controls + title), not playback controls.
        // ShowUiControls shows everything — only call when media is loaded.
        if (PlayerPage.HeaderBarControl.HeaderBarElement != null)
        {
            bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;
            PlayerPage.HeaderBarControl.HeaderBarElement.IsVisible = !isFullscreen;
            PlayerPage.HeaderBarControl.HeaderBarElement.Opacity = isFullscreen ? 0 : 1;
            PlayerPage.HeaderBarControl.HeaderBarElement.IsHitTestVisible = !isFullscreen;
        }

        // P5.1a: Restore playlist items (does not open any file — purely UI)
        _viewModel?.LoadPlaylist();

        // P5.1b: Resume session only if no command-line file was queued
        if (string.IsNullOrEmpty(_queuedOpenPath))
            _viewModel?.LoadSession();

        PlayerPage.HeaderBarControl.UpdateMaximizeIcon(WindowState == global::Avalonia.Controls.WindowState.Maximized);
        StartPage?.UpdateMaximizeIcon(WindowState == global::Avalonia.Controls.WindowState.Maximized);
        PlayerPage.ControlsBoxControl?.SubtitleOverlay?.RefreshIcon();
        PlayerPage.ControlsBoxControl?.AudioTrackSelector?.RefreshIcon();
        PlayerPage.ControlsBoxControl?.VolumeFlyoutCtrl?.RefreshIcon();

        // Wire flyout dismissal before file dialogs open (prevents dialog overlap)
        if (_viewModel?.Subtitles is { } subMgr)
            subMgr.DismissFlyoutAsync = () =>
            {
                PlayerPage.ControlsBoxControl?.SubtitleOverlay?.HideFlyout();
                return Task.CompletedTask;
            };
        if (_viewModel?.Audio is { } audMgr)
            audMgr.DismissFlyoutAsync = () =>
            {
                PlayerPage.ControlsBoxControl?.AudioTrackSelector?.HideFlyout();
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
            PlayerPage.ControlsBoxControl?.SeekBarControl.InitializeSeekBar();
            DebugLog("Deferred init complete");
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Initializes the ANGLE/OpenGL video renderer attached to the mpv player.
    /// PlayerPage.MpvVideoView creates its own ANGLE context and runs a dedicated render thread.
    /// </summary>
    private void InitVideoRenderer()
    {
        var player = _playerService?.Player as MpvPlayer;
        if (player == null || _viewModel == null)
        {
            DebugLog("InitVideoRenderer: player or viewModel is null");
            return;
        }

        DebugLog("InitVideoRenderer: initializing PlayerPage.MpvVideoView (ANGLE + render API)");

        // Main window uses ANGLE/OpenGL render API by default.
        // PlayerPage.MpvVideoView creates its own ANGLE context, initializes mpv render API,
        // and runs a dedicated render thread that updates a WriteableBitmap Image.
        // This bypasses Avalonia's OpenGlControlBase which can fail silently in v12.
        try
        {
            PlayerPage.MpvVideoView.Initialize(player);

            // Phase 2 premium: wire performance services
            var perfMonitor = new PerformanceMonitor();
            var renderThrottle = new RenderThrottleService();
            PlayerPage.MpvVideoView.SetPerformanceServices(perfMonitor, renderThrottle);
            DebugLog("InitVideoRenderer: performance services wired");
        }
        catch (System.DllNotFoundException dllEx)
        {
            // Missing native ANGLE/GL DLLs — continue without fatal crash and log clear guidance.
            DebugLog($"InitVideoRenderer FAILED: {dllEx}");
            DebugLog("ANGLE/GL not available. Video rendering disabled. To enable, install runtime DLLs (libEGL.dll/libGLESv2.dll) or unset CINE_DEV_MODE.");
            // Detach player so other systems can still operate.
            try { PlayerPage.MpvVideoView.DetachPlayer(); } catch (Exception detEx) { DebugLog($"DetachPlayer failed: {detEx}"); }
        }
        catch (Exception ex)
        {
            // Generic fallback — avoid crashing the whole UI if renderer init fails.
            DebugLog($"InitVideoRenderer FAILED: {ex}");
            DebugLog("Video renderer initialization failed; continuing without hardware-backed video.");
            try { PlayerPage.MpvVideoView.DetachPlayer(); } catch (Exception detEx) { DebugLog($"DetachPlayer failed: {detEx}"); }
        }
    }

    /// <summary>
    /// Sets up a 15-second interval timer that persists playback session state.
    /// </summary>
    private void InitializeSessionSave()
    {
        _sessionSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _sessionSaveTimer.Tick += (_, _) => _viewModel?.SaveSession();
        _sessionSaveTimer.Start();
    }

    /// <summary>
    /// Wires all player events, property watchers, pointer events, and
    /// component subscriptions after initialization is complete.
    /// Called from OnWindowInitialized() after managers are created.
    /// </summary>
    private void InitializeWiring(Cine.Media.Interfaces.IMediaPlayer player, IEventBus eventBus)
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
        PlayerPage.ControlsBoxControl.SyncPlayPauseIcon(_stateManager.IsPlaying);
        SyncPipPlayState(_stateManager.State);

        // Phase 9: Player errors handled via EventBus
        eventBus.Subscribe<PlayerErrorEvent>(e =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                PlayerPage.SpinnerOverlay.Stop();
                _isLoading = false;
                _osdService.ShowWithIcon(MaterialIconKind.AlertCircleOutline, $"Error: {e.ErrorMessage}", 4000);
            });
        });

        PlayerPage.VideoClickOverlay.PointerMoved += OnWindowPointerMoved;
        PlayerPage.VideoClickOverlay.PointerPressed += OnVideoPointerPressed;
        PlayerPage.VideoClickOverlay.AddHandler(InputElement.DoubleTappedEvent, OnVideoDoubleTapped, handledEventsToo: true);
        PlayerPage.VideoClickOverlay.AddHandler(InputElement.RightTappedEvent, OnVideoRightTapped, handledEventsToo: true);

        // Hover tracking
        PlayerPage.HeaderBarControl.HeaderBarElement.PointerEntered += OnHeaderPointerEntered;
        PlayerPage.HeaderBarControl.HeaderBarElement.PointerExited += OnHeaderPointerExited;
        PlayerPage.HeaderBarControl.HeaderBarElement.PointerPressed += OnHeaderPointerPressed;
        PlayerPage.ControlsBoxControl.ControlsBoxElement.PointerEntered += OnControlsPointerEntered;
        PlayerPage.ControlsBoxControl.ControlsBoxElement.PointerExited += OnControlsPointerExited;
        PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.PointerEntered += OnFullscreenHeaderPointerEntered;
        PlayerPage.FullscreenHeaderControl.FullscreenHeaderElement.PointerExited += OnFullscreenHeaderPointerExited;

        // Window backdrop opacity
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;

        if (_viewModel != null)
        {
            SetupPropertyWatchers();
        }

        // Phase 9: Component events handled via EventBus
        eventBus.Subscribe<ReplayRequestedEvent>(_ => OnReplayRequested(null, EventArgs.Empty));

        eventBus.Subscribe<OsdClickedEvent>(e =>
            _osdService.ShowWithIcon(MaterialIconKind.Information,
                $"Clicked: {e.Category}", 3000));

        eventBus.Subscribe<ExternalTrackLoadedEvent>(e =>
        {
            var icon = e.TrackType == "Subtitle"
                ? MaterialIconKind.ClosedCaption
                : MaterialIconKind.Music;
            _osdService.ShowWithIcon(icon, $"{e.TrackType} loaded: {Path.GetFileName(e.FilePath)}");
        });

        PlayerPage.ControlsBoxControl.SeekBarControl.InitializeSeekBar();
        PlayerPage.ControlsBoxControl.SeekBarControl.SeekWheelChanged += (_, delta) =>
        {
            if (delta > 0) _viewModel?.SeekForward();
            else _viewModel?.SeekBackward();
        };

        // Pause auto-hide timer while seeking to prevent flicker
        // (time hint popover triggers show/hide cycle during seek)
        PlayerPage.ControlsBoxControl.SeekBarControl.SeekStarted += (_, _) =>
            _autoHideTimer?.Stop();
        PlayerPage.ControlsBoxControl.SeekBarControl.SeekEnded += (_, _) =>
            _autoHideTimer?.Start();

        InitializeAutoHide();
        InitializeSessionSave();

        // PIP manager
        _pipWindowManager = new PipWindowManager(
            new PipService(PlayerPage.MpvVideoView),
            _viewModel!,
            PlayerPage.HeaderBarControl,
            PlayerPage.ControlsBoxControl,
            PlayerPage.MpvVideoView,
            _playerService!,
            msg => _osdService.Show(msg));

        // Phase 9: PIP toggled handled via EventBus
        eventBus.Subscribe<PipToggleEvent>(_ => OnPipToggled(null, EventArgs.Empty));

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

    // ─────────────────────────────────────────────────────
    //  Dependencies (resolved during initialization)
    // ─────────────────────────────────────────────────────
    private IServiceProvider _serviceProvider = null!;
    private IMediaFileService _mediaFileService = null!;
    private PlayerService? _playerService;
    private MainViewModel? _viewModel;
    private PlaybackStateManager? _stateManager;
    private PipWindowManager? _pipWindowManager;
    private InputRoutingService? _inputRouter;
    private IFlyoutService _flyoutManager = null!;
    private IOsdService _osdService = null!;
    private FileDialogHandler? _dialogHandler;
    private INavigationService? _navigationService;

    // Session
    private string? _queuedOpenPath;
    private TimeSpan _sessionResumePosition;
    private DispatcherTimer? _sessionSaveTimer;
    private readonly StartupTimer _startupTimer = new();
    private bool _isDisposed;

    // ─────────────────────────────────────────────────────
    //  Debug Logging
    // ─────────────────────────────────────────────────────
    private static readonly string DebugLogFile = CreateLogFilePath();

    private static string CreateLogFilePath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cine_startup.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "cine_startup.log");
        }
    }

    [Conditional("DEBUG")]
    internal static void DebugLog(string message)
    {
        Result.From(() =>
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}")
        );
    }

    private void ReportWindowState(string location, string hypothesisId = "B")
    {
        try
        {
            var startPage = this.FindControl<Control>("StartPage");
            var mainOverlay = this.FindControl<Control>("MainOverlay");
            App.DebugReport(hypothesisId, location, "Window startup state snapshot.", new
            {
                title = Title,
                background = Background?.ToString(),
                extendClientArea = ExtendClientAreaToDecorationsHint,
                windowState = WindowState.ToString(),
                isVisible = IsVisible,
                width = Bounds.Width,
                height = Bounds.Height,
                contentType = Content?.GetType().FullName,
                startPageFound = startPage is not null,
                startPageVisible = startPage?.IsVisible
            });
        }
        catch (Exception ex) { Log.ForContext<MainWindow>().Error(ex, "DumpState failed"); }
    }

    public void QueueStartupOpen(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        _queuedOpenPath = path;
    }
}
