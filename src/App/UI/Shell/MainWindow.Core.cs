using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Helpers;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Interfaces;
using App = global::Avalonia.Application;
using Button = Avalonia.Controls.Button;
using Control = Avalonia.Controls.Control;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using PointerEventArgs = Avalonia.Input.PointerEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using SizeChangedEventArgs = Avalonia.Controls.SizeChangedEventArgs;
using TextBlock = Avalonia.Controls.TextBlock;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private PlayerService? _playerService;
    private MainViewModel? _viewModel;
    private D3D11VideoHost? _videoHost;
    private string? _queuedOpenPath;
    private TimeSpan _sessionResumePosition;

    // UI Auto-hide
    private DispatcherTimer? _autoHideTimer;
    private bool _uiVisible = true;
    private const double AutoHideDelaySeconds = 3.0;
    private global::Avalonia.Point _lastMousePosition;
    private DateTime _lastSeekWheel = DateTime.MinValue;

    // Seek bar
    private TimeSpan _lastPosition;
    private TimeSpan _lastDuration;

    // Loading guard
    private bool _isLoading;

    // Keyboard repeat guard
    private DateTime _lastSeekRepeat = DateTime.MinValue;

    // Double-tap detection
    private DateTime _lastTapTime = DateTime.MinValue;

    // Responsive breakpoints
    private const double NarrowBreakpoint = 600.0;
    private const double MediumBreakpoint = 1024.0;

    // PIP / compact mini-player mode
    private PipService? _pipService;
    private DwmThumbnailManager? _dwmManager;

    // Session save
    private DispatcherTimer? _sessionSaveTimer;

    // Startup error guard
    private bool _isDisposed;

    // Component references (set in InitializeComponent)
    private HeaderBarControl _headerBar = null!;
    private ControlsBoxControl _controlsBox = null!;
    private FullscreenHeaderControl _fullscreenHeader = null!;
    private SpinnerOverlayControl _spinnerOverlay = null!;
    private PauseOverlayControl _pauseOverlay = null!;
    private ReplayOverlayControl _replayOverlay = null!;
    private DragDropOverlayControl _dropIndicator = null!;
    private OsdNotificationControl _osdNotification = null!;

    #region debug-log
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

    private static void DebugLog(string message)
    {
        Result.From(() =>
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}")
        );
    }
    #endregion

    #region debug-point B:startup-visual-state
    private void ReportWindowState(string location, string hypothesisId = "B")
    {
        try
        {
            var startPage = this.FindControl<Control>("StartPage");
            var mainOverlay = this.FindControl<Control>("MainOverlay");
            var videoHost = this.FindControl<Control>("VideoHost");
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
                startPageVisible = startPage?.IsVisible,
                videoHostFound = videoHost is not null,
                videoHostVisible = videoHost?.IsVisible
            });
        }
        catch { Debug.WriteLine("DumpState failed"); }
    }
    #endregion

    public static void TrySetIcon(Material.Icons.Avalonia.MaterialIcon icon, string resourceKey)
    {
        icon.Kind = resourceKey switch
        {
            "FullscreenEnterIcon" => Material.Icons.MaterialIconKind.Fullscreen,
            "FullscreenExitIcon" => Material.Icons.MaterialIconKind.FullscreenExit,
            "MaxRestoreIcon" => Material.Icons.MaterialIconKind.WindowMaximize,
            "MaximizeIcon" => Material.Icons.MaterialIconKind.WindowMaximize,
            "PlayIcon" => Material.Icons.MaterialIconKind.Play,
            "PauseIcon" => Material.Icons.MaterialIconKind.Pause,
            "SubtitlesIcon" => Material.Icons.MaterialIconKind.Subtitles,
            "SubtitlesOffIcon" => Material.Icons.MaterialIconKind.ClosedCaptionOutline,
            "AudioIcon" => Material.Icons.MaterialIconKind.Music,
            "AudioOffIcon" => Material.Icons.MaterialIconKind.MusicOff,
            _ => icon.Kind
        };
    }

    public void QueueStartupOpen(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        _queuedOpenPath = path;
    }

    private void OnWindowInitialized()
    {
        DebugLog("OnWindowInitialized start");

        // Resolve component references
        _videoHost = VideoHost;
        _headerBar = HeaderBarControl;
        _controlsBox = ControlsBoxControl;
        _fullscreenHeader = FullscreenHeaderControl;
        _spinnerOverlay = LoadingSpinnerOverlay;
        _pauseOverlay = PauseOverlay;
        _replayOverlay = ReplayOverlay;
        _dropIndicator = DropIndicatorOverlay;
        _osdNotification = OsdNotificationControl;

        DebugLog($"VideoHost resolved null={_videoHost is null}");
        if (_videoHost == null)
            throw new InvalidOperationException("VideoHost control was not found in MainWindow.axaml.");

        ReportWindowState("MainWindow.OnWindowInitialized.AfterResolve");

        _playerService = new PlayerService();
        try
        {
            _playerService.Initialize();
        }
        catch (Exception ex)
        {
            DebugLog($"Player initialization FAILED: {ex}");
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

        _viewModel = new MainViewModel(player);
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
                        _viewModel.OpenFile(p);
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

        _viewModel.RequestOpenFilesAsync = OpenFileDialogAsync;
        _viewModel.RequestOpenFolderAsync = OpenFolderDialogAsync;
        _viewModel.RequestAddFilesAsync = OpenAddFilesDialogAsync;
        _viewModel.RequestSubtitleFileAsync = OpenSubtitleDialogAsync;
        _viewModel.RequestAudioFileAsync = OpenAudioDialogAsync;

        player.Opened += OnMediaOpened;
        player.PlaybackStateChangedEvent += OnPlaybackStateChanged;
        player.PositionChanged += OnPositionChanged;
        player.ChapterListChanged += OnChapterListChanged;
        player.FullscreenChangedEvent += OnPlayerFullscreenChanged;

        _playerService.Error += (_, error) =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                _spinnerOverlay.Stop();
                _isLoading = false;
                ShowOsdNotification($"Error: {error}", 4000);
            });
        };

        _videoHost.ChildWindowCreated += OnVideoHostChildCreated;
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

        _controlsBox.SeekBarControl.InitializeSeekBar();
        _controlsBox.SeekBarControl.SeekWheelChanged += (_, delta) =>
        {
            if (delta > 0) _viewModel?.SeekForward();
            else _viewModel?.SeekBackward();
        };

        InitializeAutoHide();
        InitializeSessionSave();
        InitializeResponsiveLayout();

        // Initialize DWM thumbnail manager and PIP service
        _dwmManager = new DwmThumbnailManager();
        _pipService = new PipService(_dwmManager);

        // Wire PIP player controls
        _headerBar.PipToggled += OnPipToggled;
        _pipService.PlayPauseRequested += OnPipPlayPauseRequested;
        _pipService.SeekRequested += OnPipSeekRequested;
        _pipService.PipClosed += OnPipClosed;

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
        catch { Debug.WriteLine("Centering/Activate failed"); }

        if (StartPage != null) StartPage.IsVisible = true;

        // Create hidden window immediately — mpv needs it before OnMediaOpened
        _videoHost?.CreateHiddenWindow();

        // Register DWM thumbnail on main window
        TryRegisterDwmThumbnail();

        _headerBar.HideOpenMenu();
        _headerBar.HidePrimaryMenu();
        _headerBar.SetPipVisibility(false);
        if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = false;
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

        // P5.1: Resume session only if no command-line file was queued
        if (string.IsNullOrEmpty(_queuedOpenPath))
            _viewModel?.LoadSession();

        _headerBar.UpdateMaximizeIcon(WindowState == global::Avalonia.Controls.WindowState.Maximized);
        _controlsBox?.RefreshSubtitleIcon();
        _controlsBox?.RefreshAudioIcon();
        _controlsBox?.RefreshVolumeIcon();
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
                        if (x >= 0 && y >= 0) Position = new PixelPoint(x, y);
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

    private static string WindowStatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "window_state.json");

    protected override void OnClosed(EventArgs e)
    {
        // P5.2: Save window position, size, and state
        Result.From(() =>
        {
            var dir = Path.GetDirectoryName(WindowStatePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var state = new
            {
                Width,
                Height,
                X = Position.X,
                Y = Position.Y,
                Maximized = WindowState == WindowState.Maximized
            };
            File.WriteAllText(WindowStatePath, JsonSerializer.Serialize(state));
        });

        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        _sessionSaveTimer?.Stop();
        _sessionSaveTimer = null;
        _propertyWatcher?.Dispose();
        _propertyWatcher = null;
        _viewModel?.SaveSession();
        _playerService?.Dispose();
        _pipService?.Dispose();
        _dwmManager?.Dispose();
        base.OnClosed(e);
    }

    private IntPtr? PlatformImplHandle()
    {
        try
        {
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle is { Handle: not 0 })
            {
                DebugLog($"TryGetPlatformHandle descriptor={platformHandle.HandleDescriptor}");
                return platformHandle.Handle;
            }
        }
        catch (Exception ex)
        {
            DebugLog($"TryGetPlatformHandle failed: {ex.Message}");
        }
        return IntPtr.Zero;
    }

    /// <summary>Updates DWM thumbnail rcDestination — video area only, excluding header/controls so DWM doesn't paint over them.</summary>
    private void SyncThumbnailRect()
    {
        if (_videoHost == null) return;

        var pt = _videoHost.TranslatePoint(new global::Avalonia.Point(0, 0), this);
        if (pt == null) return;

        double scale = RenderScaling;
        int x = (int)(pt.Value.X * scale);
        int y = (int)(pt.Value.Y * scale);
        int w = (int)(_videoHost.Bounds.Width * scale);
        int h = (int)(_videoHost.Bounds.Height * scale);

        // Measure actual header + controls heights at runtime instead of hardcoding 44/120px
        double headerH = _uiVisible
            ? (_viewModel?.IsFullscreen == true
                ? _fullscreenHeader.Bounds.Height
                : _headerBar.Bounds.Height)
            : 0;
        double controlsH = _uiVisible ? _controlsBox.Bounds.Height : 0;

        // If measured values are 0 (not yet laid out), fall back to reasonable defaults
        if (headerH <= 0) headerH = _viewModel?.IsFullscreen == true ? 44 : 56;
        if (controlsH <= 0) controlsH = 84;

        int headerPx = (int)(headerH * scale);
        int controlsPx = (int)(controlsH * scale);

        _videoHost.SetThumbnailDestRect(
            x, y + headerPx,               // top starts below header
            x + w, y + h - controlsPx);    // bottom ends above controls
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SyncThumbnailRect();
    }

    private void TryRegisterDwmThumbnail()
    {
        if (_dwmManager == null || _videoHost == null) return;

        var handle = PlatformImplHandle();
        if (handle.HasValue && handle.Value != IntPtr.Zero)
        {
            _videoHost.RegisterMainWindow(handle.Value, _dwmManager);
            return;
        }

        // Retry once after layout completes
        Dispatcher.UIThread.Post(() =>
        {
            var h = PlatformImplHandle();
            if (h.HasValue && h.Value != IntPtr.Zero)
                _videoHost!.RegisterMainWindow(h.Value, _dwmManager!);
        }, DispatcherPriority.Render);
    }

    private void OnVideoHostChildCreated(object? sender, EventArgs e)
    {
        var videoHwnd = _videoHost?.VideoHwnd ?? IntPtr.Zero;
        DebugLog($"OnVideoHostChildCreated hwnd={videoHwnd}");
        if (videoHwnd == IntPtr.Zero) return;
        var player = _playerService?.Player;
        if (player != null)
        {
            DebugLog("Calling InitializeRenderer");
            player.InitializeRenderer(videoHwnd);
            DebugLog("InitializeRenderer returned");
        }

        if (!string.IsNullOrWhiteSpace(_queuedOpenPath) && File.Exists(_queuedOpenPath))
        {
            var path = _queuedOpenPath;
            _queuedOpenPath = null;
            Dispatcher.UIThread.OnUiThread(() => _viewModel?.OpenFile(path));
        }
    }

    private PropertyWatcher? _propertyWatcher;

    private void InitializeSessionSave()
    {
        _sessionSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _sessionSaveTimer.Tick += (_, _) => _viewModel?.SaveSession();
        _sessionSaveTimer.Start();
    }

    // --- MediaEvents helper for OnPlaybackStateChanged ---
    private void UpdatePlayPauseFromState(bool isPaused)
    {
        _controlsBox?.UpdatePlayPauseIcon();
    }

    // =========================================================================
    // P6.6: Window backdrop opacity — reduce controls opacity when unfocused
    // =========================================================================

    private const double FocusedOpacity = 1.0;
    private const double UnfocusedOpacity = 0.66;

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        FadeHeaderAndControls(FocusedOpacity);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        FadeHeaderAndControls(UnfocusedOpacity);
    }

    // =========================================================================
    // OSD Notification helpers
    // =========================================================================

    private void ShowOsdNotification(string text, double durationMs = 2000)
        => OsdNotificationControl.Show(text, durationMs);
    private void ShowOsdNotification(MaterialIconKind icon, string text, double durationMs = 2000)
        => OsdNotificationControl.ShowWithIcon(icon, text, durationMs);

    // =========================================================================
    // P8.3: Typed property watchers — replaces string-based PropertyChanged switch
    // =========================================================================

    private void SetupPropertyWatchers()
    {
        if (_viewModel == null) return;
        _propertyWatcher?.Dispose();
        _propertyWatcher = new PropertyWatcher(_viewModel);

        _propertyWatcher
            .Watch(() => _viewModel.FilePath, filePath =>
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (_isLoading) return;
                    _isLoading = true;
                    _spinnerOverlay.Start();
                    // Don't hide StartPage here — OnMediaOpened handles fade-out
                    // once the player actually opens the file. This avoids a race
                    // where the watcher hides StartPage before the video is ready.
                    _headerBar.ShowOpenMenu();
                    _headerBar.ShowPrimaryMenu();
                    _headerBar.SetPipVisibility(Bounds.Width >= MediumBreakpoint);
                    _headerBar.SetTitle(_viewModel.Title);
                    Title = $"Cine — {_viewModel.Title}";
                }
                else
                {
                    _isLoading = false;
                    _spinnerOverlay.Stop();
                    if (StartPage?.IsVisible == false) StartPage.IsVisible = true;
                    PlaybackBackground.IsVisible = true;
                    _controlsBox.SetControlsVisibility(false);
                    _controlsBox.ControlsBox.IsVisible = false;
                    _headerBar.HideOpenMenu();
                    _headerBar.HidePrimaryMenu();
                    _headerBar.SetPipVisibility(false);
                    if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = false;
                    _headerBar.SetTitle("Cine");
                    // ShowUiControls should NOT be called here — when file closes,
                    // controls should stay hidden since StartPage covers them.
                }
            })
            .Watch(nameof(MainViewModel.IsPlaying), () => _controlsBox.UpdatePlayPauseIcon())
            .Watch(nameof(MainViewModel.IsPaused), () => _controlsBox.UpdatePlayPauseIcon())
            .Watch(nameof(MainViewModel.IsSubtitleEnabled), () => _controlsBox.RefreshSubtitleIcon())
            .Watch(nameof(MainViewModel.IsAudioEnabled), () => _controlsBox.RefreshAudioIcon())
            .Watch(nameof(MainViewModel.IsMuted), () =>
            {
                _controlsBox.RefreshVolumeIcon();
                if (_viewModel.IsMuted || _viewModel.VolumeValue == 0)
                    ShowOsdNotification(MaterialIconKind.VolumeOff, "Muted");
                else
                    ShowOsdNotification(MaterialIconKind.VolumeHigh, $"Volume: {_viewModel.VolumeValue}%");
            })
            .Watch(() => _viewModel.VolumeValue, vol =>
            {
                _controlsBox.RefreshVolumeIcon();
                // Show volume notification only if not muted (mute toggle handles its own notification)
                if (vol > 0 && !_viewModel.IsMuted)
                    ShowOsdNotification(MaterialIconKind.VolumeHigh, $"Volume: {vol}%");
            })
            .Watch(() => _viewModel.SpeedValue, speed =>
                ShowOsdNotification(MaterialIconKind.Speedometer, $"Speed: {speed:F1}x", 3000))
            .Watch(() => _viewModel.SeekValue, _ =>
            {
                if (_viewModel is { IsSeeking: false })
                {
                    var seekBar = _controlsBox?.SeekBarControl;
                    if (seekBar != null)
                    {
                        _lastPosition = _viewModel?.Position ?? TimeSpan.Zero;
                        _lastDuration = _viewModel?.Duration ?? TimeSpan.Zero;
                        seekBar.UpdatePosition(_lastPosition);
                        seekBar.UpdateDuration(_lastDuration);
                    }
                }
            });
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }
}
