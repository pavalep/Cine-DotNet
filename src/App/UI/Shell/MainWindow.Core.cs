using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Core;
using Cine.Media.Events;
using Cine.Media.Implementations;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Material.Icons;
using Material.Icons.Avalonia;
using App = global::Avalonia.Application;
using Control = Avalonia.Controls.Control;
using SizeChangedEventArgs = Avalonia.Controls.SizeChangedEventArgs;

namespace Cine.Avalonia;

/// <summary>
/// Core fields, constants, debug utilities, and file-dialog delegates.
/// This partial is the thinnest after extracting initialization, media events,
/// and state management into separate partial files.
/// </summary>
public partial class MainWindow
{
    private PlayerService? _playerService;
    private MainViewModel? _viewModel;
    private PlaybackStateManager? _stateManager;
    private AudioManager? _audioManager;
    private VideoManager? _videoManager;
    private SubtitleManager? _subtitleManager;
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
    // Suppress first volume OSD after file load (player fires VolumeChanged during init)
    private bool _suppressFirstVolumeOsd;

    // Keyboard repeat guard
    private DateTime _lastSeekRepeat = DateTime.MinValue;

    // Double-tap detection
    private DateTime _lastTapTime = DateTime.MinValue;

    // Responsive breakpoints
    private const double NarrowBreakpoint = 600.0;
    private const double MediumBreakpoint = 1024.0;

    // PIP / compact mini-player mode — encapsulated in PipWindowManager
    private PipWindowManager? _pipWindowManager;

    // Keyboard shortcut routing
    private InputRoutingService? _inputRouter;

    // Session save
    private DispatcherTimer? _sessionSaveTimer;

    // Phase 10: Per-phase startup timing
    private readonly StartupTimer _startupTimer = new();

    // Phase 11: Command palette command registry
    private readonly List<(string description, Action action)> _paletteCommands = new();

    // Phase 11: Focus mode — hides all chrome except a thin indicator
    private bool _isFocusMode;

    // ── Volume OSD debounce ──
    private DispatcherTimer? _volumeOsdTimer;
    private double _pendingVolumeLevel;

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

    // File-dialog handler
    private FileDialogHandler? _dialogHandler;

    // Flyout ecosystem manager
    private FlyoutManager _flyoutManager = null!;

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

    // ─────────────────────────────────────────────────────
    //  Public Utilities
    // ─────────────────────────────────────────────────────

    public static void TrySetIcon(MaterialIcon icon, string resourceKey)
    {
        icon.Kind = resourceKey switch
        {
            "FullscreenEnterIcon" => MaterialIconKind.Fullscreen,
            "FullscreenExitIcon" => MaterialIconKind.FullscreenExit,
            "MaxRestoreIcon" => MaterialIconKind.WindowMaximize,
            "MaximizeIcon" => MaterialIconKind.WindowMaximize,
            "PlayIcon" => MaterialIconKind.Play,
            "PauseIcon" => MaterialIconKind.Pause,
            "SubtitlesIcon" => MaterialIconKind.Subtitles,
            "SubtitlesOffIcon" => MaterialIconKind.ClosedCaptionOutline,
            "AudioIcon" => MaterialIconKind.Music,
            "AudioOffIcon" => MaterialIconKind.MusicOff,
            _ => icon.Kind
        };
    }

    public void QueueStartupOpen(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        _queuedOpenPath = path;
    }

    // ─────────────────────────────────────────────────────
    //  File Dialog Delegates (used by keyboard shortcuts in MainWindow.Input.cs)
    // ─────────────────────────────────────────────────────

    private Task<string[]?> OpenFileDialogAsync() =>
        _dialogHandler!.OpenFilesAsync()!;

    private Task<string?> OpenFolderDialogAsync() =>
        _dialogHandler!.OpenFolderAsync()!;

    private Task<string[]?> OpenAddFilesDialogAsync() =>
        _dialogHandler!.AddFilesAsync()!;

    private Task<string?> OpenSubtitleDialogAsync() =>
        _dialogHandler!.OpenSubtitleAsync()!;

    private Task<string?> OpenAudioDialogAsync() =>
        _dialogHandler!.OpenAudioAsync()!;

    // ─────────────────────────────────────────────────────
    //  Native Imports (user32 window rect)
    // ─────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }
}
