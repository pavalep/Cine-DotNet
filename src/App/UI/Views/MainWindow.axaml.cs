using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.Media;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views;
using Cine.Media.Events;
using Cine.Media.Interfaces;

// Resolve type ambiguities with System.Windows.Forms by aliasing conflicting types
using App = global::Avalonia.Application;
using AvaloniaControl = global::Avalonia.Controls.Control;
using AvaloniaTextBlock = global::Avalonia.Controls.TextBlock;
using AvaloniaKeyEventArgs = global::Avalonia.Input.KeyEventArgs;
using AvaloniaStyle = global::Avalonia.Styling.Style;

namespace Cine.Avalonia;

public partial class MainWindow : Window
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
    private bool _isMouseOverControls;
    private DateTime _lastSeekWheel = DateTime.MinValue;
    private int _activeFlyouts;

    // Seek bar
    private bool _isSeeking;
    private const double SeekThumbHalf = 8.0; // half of 16px thumb
    private double _lastSeekNormalized;
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
    private bool _isPipMode;
    private PipWindow? _pipWindow;
    private IMediaPlayer? _pipPlayer;

    // Loading spinner rotation
    private DispatcherTimer? _spinnerTimer;
    private double _spinnerAngle;

    // Startup error guard
    private bool _isDisposed;

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
        try
        {
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}");
        }
        catch { }
    }
    #endregion

    #region debug-point B:startup-visual-state
    private void ReportWindowState(string location, string hypothesisId = "B")
    {
        try
        {
            var startPage = this.FindControl<AvaloniaControl>("StartPage");
            var mainOverlay = this.FindControl<AvaloniaControl>("MainOverlay");
            var videoHost = this.FindControl<AvaloniaControl>("VideoHost");
            var controlsBox = this.FindControl<AvaloniaControl>("ControlsBox");
            var headerBar = this.FindControl<AvaloniaControl>("HeaderBar");
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
                contentBounds = Content is AvaloniaControl contentControl ? contentControl.Bounds.ToString() : null,
                startPageFound = startPage is not null,
                startPageVisible = startPage?.IsVisible,
                startPageBounds = startPage?.Bounds.ToString(),
                mainOverlayFound = mainOverlay is not null,
                mainOverlayBounds = mainOverlay?.Bounds.ToString(),
                videoHostFound = videoHost is not null,
                videoHostVisible = videoHost?.IsVisible,
                videoHostBounds = videoHost?.Bounds.ToString(),
                controlsFound = controlsBox is not null,
                controlsVisible = controlsBox?.IsVisible,
                headerFound = headerBar is not null,
                headerVisible = headerBar?.IsVisible
            });
        }
        catch
        {
        }
    }
    #endregion

    public MainWindow()
    {
        InitializeComponent();
    }

    public void QueueStartupOpen(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        _queuedOpenPath = path;
    }

    private void ResolveNamedControls()
    {
        VideoHost = this.FindControl<D3D11VideoHost>("VideoHost");
        StartPage = this.FindControl<StartPage>("StartPage");
        PauseIndicator = this.FindControl<Border>("PauseIndicator");
        HeaderBar = this.FindControl<Border>("HeaderBar");
        BtnOpenMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnOpenMenu");
        TitleText = this.FindControl<TextBlock>("TitleText");
        BtnPip = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnPip");
        BtnPrimaryMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnPrimaryMenu");
        BtnFullscreenClose = this.FindControl<global::Avalonia.Controls.Button>("BtnFullscreenClose");
        WindowControlsPanel = this.FindControl<StackPanel>("WindowControlsPanel");
        BtnMinimize = this.FindControl<global::Avalonia.Controls.Button>("BtnMinimize");
        BtnMaximizeRestore = this.FindControl<global::Avalonia.Controls.Button>("BtnMaximizeRestore");
        MaximizeRestoreIconPath = this.FindControl<global::Material.Icons.Avalonia.MaterialIcon>("MaximizeRestoreIconPath");
        BtnClose = this.FindControl<global::Avalonia.Controls.Button>("BtnClose");
        ControlsBox = this.FindControl<Border>("ControlsBox");
        FullscreenHeader = this.FindControl<Border>("FullscreenHeader");
        BtnFullscreenExit = this.FindControl<global::Avalonia.Controls.Button>("BtnFullscreenExit");
        BtnFullscreenMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnFullscreenMenu");
        ReplayOverlay = this.FindControl<Border>("ReplayOverlay");
        BtnPrevious = this.FindControl<global::Avalonia.Controls.Button>("BtnPrevious");
        BtnPlayPause = this.FindControl<global::Avalonia.Controls.Button>("BtnPlayPause");
        BtnNext = this.FindControl<global::Avalonia.Controls.Button>("BtnNext");
        BtnVolumeMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnVolumeMenu");
        BtnSubtitlesMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnSubtitlesMenu");
        SubtitleIconPath = this.FindControl<global::Material.Icons.Avalonia.MaterialIcon>("SubtitleIconPath");
        BtnAudioMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnAudioMenu");
        AudioIconPath = this.FindControl<global::Material.Icons.Avalonia.MaterialIcon>("AudioIconPath");
        BtnVideoMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnVideoMenu");
        BtnLoopPlaylist = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnLoopPlaylist");
        BtnLoopFile = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnLoopFile");
        BtnMuteToggle = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnMuteToggle");
        BtnOptionsMenu = this.FindControl<global::Cine.Avalonia.Components.OptionsMenuButton>("BtnOptionsMenu");
        BtnFullscreen = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnFullscreen");
        FullscreenIconPath = this.FindControl<global::Material.Icons.Avalonia.MaterialIcon>("FullscreenIconPath");
        SeekArea = this.FindControl<Grid>("SeekArea");
        ChapterPreviewPopover = this.FindControl<Border>("ChapterPreviewPopover");
        ChapterPreviewText = this.FindControl<TextBlock>("ChapterPreviewText");
        PositionTimeLabel = this.FindControl<TextBlock>("PositionTimeLabel");
        DurationTimeLabel = this.FindControl<TextBlock>("DurationTimeLabel");
        DropIndicatorOverlay = this.FindControl<Border>("DropIndicatorOverlay");
        DropIndicatorIcon = this.FindControl<global::Material.Icons.Avalonia.MaterialIcon>("DropIndicatorIcon");
        DropIndicatorText = this.FindControl<TextBlock>("DropIndicatorText");
    }

    private void InitializeComponent()
    {
        DebugLog("InitializeComponent start");
        App.DebugReport("B", "MainWindow.InitializeComponent", "Before XAML load.", new
        {
            background = Background?.ToString(),
            extendClientArea = ExtendClientAreaToDecorationsHint,
            windowState = WindowState.ToString()
        });
        AvaloniaXamlLoader.Load(this);
        ResolveNamedControls();
        DebugLog("XAML loaded");
        ReportWindowState("MainWindow.InitializeComponent.AfterXamlLoad");

        _videoHost = VideoHost;
        DebugLog($"VideoHost resolved null={_videoHost is null}");
        if (_videoHost == null)
            throw new InvalidOperationException("VideoHost control was not found in MainWindow.axaml.");

        _playerService = new PlayerService();
        try
        {
            _playerService.Initialize();
        }
        catch (Exception ex)
        {
            DebugLog($"Player initialization FAILED: {ex}");
            _isDisposed = true;
            Dispatcher.UIThread.InvokeAsync(async () =>
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
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await ShowErrorDialog("Media player returned null.", "The application cannot continue.");
                Close();
            });
            return;
        }

        if (_isDisposed) return;

        _viewModel = new MainViewModel(player);
        DataContext = _viewModel;

        // Session resume
        _viewModel.SessionResumeRequested = (path, pos) =>
        {
            _queuedOpenPath = path;
            _sessionResumePosition = pos;
            ShowOsdNotification($"Resume {Path.GetFileName(path)} from {pos.Minutes:D2}:{pos.Seconds:D2}?", 5000);
            // Auto-resume after 4 seconds
            Dispatcher.UIThread.InvokeAsync(async () =>
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
                            var player = _playerService?.Player;
                            if (player != null) player.Opened -= handler;
                        };
                        var playerInstance = _playerService?.Player;
                        if (playerInstance != null) playerInstance.Opened += handler;
                    }
                }
            });
        };
        _viewModel.LoadSession();

        _viewModel.Playlist.CollectionChanged += (_, _) => _viewModel?.SaveSession();

        // Wire file dialog callbacks
        _viewModel.RequestOpenFilesAsync = OpenFileDialogAsync;
        _viewModel.RequestOpenFolderAsync = OpenFolderDialogAsync;
        _viewModel.RequestAddFilesAsync = OpenAddFilesDialogAsync;
        _viewModel.RequestSubtitleFileAsync = OpenSubtitleDialogAsync;
        _viewModel.RequestAudioFileAsync = OpenAudioDialogAsync;

        player.Opened += OnMediaOpened;
        player.PositionChanged += OnPositionChanged;
        player.ChapterListChanged += OnChapterListChanged;
        player.FullscreenChangedEvent += OnPlayerFullscreenChanged;

        _playerService.Error += (_, error) =>
        {
            Dispatcher.UIThread.Post(() => ShowOsdNotification($"Error: {error}", 4000));
        };

        _videoHost.ChildWindowCreated += OnVideoHostChildCreated;
        _videoHost.PointerPressed += OnVideoPointerPressed;
        KeyDown += OnKeyDown;

        // Start page - show on initial launch, hide when media opens
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        InitializeAutoHide();
        InitializeSessionSave();
        InitializeResponsiveLayout();
        InitializeFlyoutTracking();

        // Seek bar — track resize to update positions
        SeekArea.SizeChanged += OnSeekAreaSizeChanged;

        // Drag and Drop
        AddHandler(global::Avalonia.Input.DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(global::Avalonia.Input.DragDrop.DragLeaveEvent, OnWindowDragLeave);
        AddHandler(global::Avalonia.Input.DragDrop.DropEvent, OnWindowDrop);

        ReportWindowState("MainWindow.InitializeComponent.Finish");
        DebugLog("InitializeComponent finish");
    }

    // ========================
    //  FILE DIALOGS
    // ========================

    private static readonly FilePickerFileType VideoFilesFilter = new("Video Files")
    {
        Patterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.flv", "*.webm",
                           "*.m4v", "*.mpg", "*.mpeg", "*.3gp", "*.ts", "*.mts", "*.m2ts",
                           "*.vob", "*.ogv", "*.asf", "*.divx", "*.f4v", "*.rm", "*.rmvb" }
    };

    private static readonly FilePickerFileType SubtitleFilesFilter = new("Subtitle Files")
    {
        Patterns = new[] { "*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub", "*.idx" }
    };

    private static readonly FilePickerFileType AudioFilesFilter = new("Audio Files")
    {
        Patterns = new[] { "*.mp3", "*.aac", "*.flac", "*.ogg", "*.wav", "*.wma", "*.m4a", "*.opus" }
    };

    private async Task<string[]?> OpenFileDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Files",
            AllowMultiple = true,
            FileTypeFilter = new[] { VideoFilesFilter }
        });
        return result?.Select(f => f.Path.LocalPath).ToArray();
    }

    private async Task<string?> OpenFolderDialogAsync()
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder"
        });
        return result?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string[]?> OpenAddFilesDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Files",
            AllowMultiple = true,
            FileTypeFilter = new[] { VideoFilesFilter }
        });
        return result?.Select(f => f.Path.LocalPath).ToArray();
    }

    private async Task<string?> OpenSubtitleDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Subtitle Track",
            AllowMultiple = false,
            FileTypeFilter = new[] { SubtitleFilesFilter }
        });
        return result?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> OpenAudioDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Audio Track",
            AllowMultiple = false,
            FileTypeFilter = new[] { AudioFilesFilter }
        });
        return result?.FirstOrDefault()?.Path.LocalPath;
    }

    // ========================
    //  RESPONSIVE LAYOUT
    // ========================

    private void InitializeResponsiveLayout()
    {
        this.SizeChanged += OnWindowSizeChanged;
        UpdateResponsiveLayout(Bounds.Width);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);
        #region debug-point VT-D
        App.DebugReport("VT", "MainWindow.OnWindowSizeChanged", "SizeChangedEvent.", new
        {
            newSize = e.NewSize.ToString(),
            windowState = WindowState.ToString(),
            videoHostBounds = _videoHost?.Bounds.ToString(),
            renderScaling = RenderScaling,
            videoSurfaceVisible = _videoHost?.IsVideoSurfaceVisible
        }, runId: "pre-fix");
        #endregion
        if (_videoHost != null && _videoHost.IsVideoSurfaceVisible && _playerService?.Player is { } player)
        {
            int w = (int)(_videoHost.Bounds.Width * RenderScaling);
            int h = (int)(_videoHost.Bounds.Height * RenderScaling);
            if (w > 0 && h > 0)
                player.NotifyResize(w, h);
        }
    }

    private void UpdateResponsiveLayout(double width)
    {
        if (!this.IsInitialized) return;

        bool isNarrow = width < 495;

        if (isNarrow)
        {
            SetVis(BtnPip, false);
            SetVis(BtnSubtitlesMenu, false);
            SetVis(BtnAudioMenu, false);
            SetVis(BtnVideoMenu, false);
            SetFont(PositionTimeLabel, 11);
            SetFont(DurationTimeLabel, 11);
        }
        else
        {
            SetVis(BtnPip, true);
            SetVis(BtnSubtitlesMenu, true);
            SetVis(BtnAudioMenu, true);
            SetVis(BtnVideoMenu, _viewModel?.HasMultipleVideoTracks ?? false);
            SetFont(PositionTimeLabel, 13);
            SetFont(DurationTimeLabel, 13);
        }

        // Canvas spec: all circular buttons fixed at 34×34
        const double btnSize = 34;
        SetButtonSize(BtnPrimaryMenu, btnSize);
        SetButtonSize(BtnPlayPause, btnSize);
        SetButtonSize(BtnPrevious, btnSize);
        SetButtonSize(BtnNext, btnSize);
        SetButtonSize(BtnVolumeMenu, btnSize);
        SetButtonSize(BtnFullscreen, btnSize);
        SetButtonSize(BtnLoopFile, btnSize);
        SetButtonSize(BtnLoopPlaylist, btnSize);
        SetButtonSize(BtnShufflePlaylist, btnSize);
        SetButtonSize(BtnPlaylistDialog, btnSize);
    }

    /// <summary>Sets button size and corner radius directly for responsive layout.</summary>
    private void SetButtonSize(AvaloniaControl? control, double size)
    {
        if (control == null) return;
        control.Width = size;
        control.Height = size;
        if (control is global::Avalonia.Controls.Button btn)
            btn.CornerRadius = new global::Avalonia.CornerRadius(size / 2);
        else if (control is global::Avalonia.Controls.Primitives.ToggleButton tbtn)
            tbtn.CornerRadius = new global::Avalonia.CornerRadius(size / 2);
    }

    private static void SetVis(global::Avalonia.Controls.Control? c, bool v) { if (c != null) c.IsVisible = v; }
    private static void SetFont(TextBlock? l, double s) { if (l != null) l.FontSize = s; }

    // ========================
    //  TRACK MENU BUILDERS
    //  Build regular Flyout with styled Button items (MenuFlyout+MenuItem doesn't render).
    // ========================

    private void OnSubtitlesMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || BtnSubtitlesMenu == null) return;
        var flyout = BuildTrackMenuFlyout(_viewModel.SubtitleTracks);
        TrackFlyout(flyout);
        _activeFlyouts++;
        flyout.Closed += (s, args) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
        flyout.ShowAt(BtnSubtitlesMenu);
    }

    private void OnAudioMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || BtnAudioMenu == null) return;
        var flyout = BuildTrackMenuFlyout(_viewModel.AudioTracks);
        TrackFlyout(flyout);
        _activeFlyouts++;
        flyout.Closed += (s, args) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
        flyout.ShowAt(BtnAudioMenu);
    }

    private void OnVideoMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || BtnVideoMenu == null) return;
        var flyout = BuildTrackMenuFlyout(_viewModel.VideoTracks);
        TrackFlyout(flyout);
        _activeFlyouts++;
        flyout.Closed += (s, args) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
        flyout.ShowAt(BtnVideoMenu);
    }

    private global::Avalonia.Controls.Flyout BuildTrackMenuFlyout(System.Collections.ObjectModel.ObservableCollection<TrackMenuItem> tracks)
    {
        var stackPanel = new global::Avalonia.Controls.StackPanel();

        foreach (var track in tracks)
        {
            var dot = new global::Avalonia.Controls.Border
            {
                Width = 6, Height = 6,
                CornerRadius = new global::Avalonia.CornerRadius(3),
                Background = track.IsSelected && !track.IsPseudoEntry
                    ? new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xFF, 0x6C, 0xB4, 0xFF))
                    : new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Margin = new global::Avalonia.Thickness(0, 0, 8, 0)
            };

            var text = new global::Avalonia.Controls.TextBlock
            {
                Text = track.DisplayName,
                FontWeight = track.IsSelected ? global::Avalonia.Media.FontWeight.SemiBold : global::Avalonia.Media.FontWeight.Normal,
                FontSize = 12,
                Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xFF, 0xE5, 0xE5, 0xE5))
            };

            var grid = new global::Avalonia.Controls.Grid
            {
                ColumnDefinitions = new global::Avalonia.Controls.ColumnDefinitions
                {
                    new global::Avalonia.Controls.ColumnDefinition(global::Avalonia.Controls.GridLength.Auto),
                    new global::Avalonia.Controls.ColumnDefinition(global::Avalonia.Controls.GridLength.Star)
                }
            };
            grid.Children.Add(dot);
            grid.Children.Add(text);
            global::Avalonia.Controls.Grid.SetColumn(text, 1);

            var button = new global::Avalonia.Controls.Button
            {
                Content = grid,
                Background = global::Avalonia.Media.Brushes.Transparent,
                BorderThickness = new global::Avalonia.Thickness(0),
                Padding = new global::Avalonia.Thickness(10, 7),
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow),
                Opacity = track.DisplayOpacity,
                Command = track.SelectCommand
            };

            button.PointerEntered += (_, _) =>
            {
                button.Background = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
            };
            button.PointerExited += (_, _) =>
            {
                button.Background = global::Avalonia.Media.Brushes.Transparent;
            };

            stackPanel.Children.Add(button);
        }

        var border = new global::Avalonia.Controls.Border
        {
            Background = (global::Avalonia.Media.IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (global::Avalonia.Media.IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new global::Avalonia.Thickness(1),
            CornerRadius = new global::Avalonia.CornerRadius(8),
            Padding = new global::Avalonia.Thickness(4),
            MinWidth = 180,
            Child = stackPanel
        };

        var flyout = new global::Avalonia.Controls.Flyout
        {
            Content = border,
            Placement = global::Avalonia.Controls.PlacementMode.Top
        };

        return flyout;
    }

    private void TrackFlyout(global::Avalonia.Controls.Flyout flyout)
    {
        flyout.Opened += (_, _) => _activeFlyouts++;
        flyout.Closed += (_, _) => _activeFlyouts = System.Math.Max(0, _activeFlyouts - 1);
    }

    // ========================
    //  Video rendering init
    // ========================

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
            Dispatcher.UIThread.Post(() => _viewModel?.OpenFile(path));
        }
    }

    // ========================
    //  Button handlers
    // ========================
    private void OnPlayPause(object? sender, RoutedEventArgs e) => _viewModel?.PlayPause();
    private void OnStop(object? sender, RoutedEventArgs e) => _viewModel?.Stop();
    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = global::Avalonia.Controls.WindowState.Minimized;
    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e) => WindowState = WindowState == global::Avalonia.Controls.WindowState.Maximized ? global::Avalonia.Controls.WindowState.Normal : global::Avalonia.Controls.WindowState.Maximized;
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
    private void OnOsdNotificationClick(object? sender, PointerPressedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_queuedOpenPath) && File.Exists(_queuedOpenPath))
        {
            var path = _queuedOpenPath;
            var pos = _sessionResumePosition;
            _queuedOpenPath = null;
            _sessionResumePosition = TimeSpan.Zero;
            _viewModel?.OpenFile(path);
            _viewModel?.ClearSession();
            if (pos.TotalSeconds > 0)
            {
                // Seek after media opens
                EventHandler? handler = null;
                handler = (s, args) =>
                {
                    var player = _playerService?.Player;
                    if (player != null)
                    {
                        player.Seek(pos);
                        player.Play();
                    }
                    if (player != null)
                        player.Opened -= handler;
                };
                var p = _playerService?.Player;
                if (p != null) p.Opened += handler;
            }
        }
    }
    private void OnNewWindowClick(object? sender, RoutedEventArgs e) => new MainWindow().Show();
    private void OnPreferencesClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new PreferencesDialog { DataContext = _viewModel };
        dlg.Show(this);
    }
    private void OnShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new ShortcutsDialog { DataContext = _viewModel };
        dlg.Show(this);
    }
    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new AboutDialog { DataContext = _viewModel };
        dlg.Show(this);
    }

    private void OnSeekBackward(object? sender, RoutedEventArgs e) => _viewModel?.SeekBackward();
    private void OnSeekForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekForward();
    private void OnToggleMute(object? sender, RoutedEventArgs e) => _viewModel?.ToggleMute();
    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;
    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();
    private void OnTogglePip(object? sender, RoutedEventArgs e)
    {
        if (_isPipMode)
        {
            // Close PIP — Closed event handler will resume main player
            _pipWindow?.Close();
            _pipWindow = null;
            _pipPlayer = null;
            _isPipMode = false;
            if (BtnPip != null) BtnPip.IsChecked = false;
            ShowOsdNotification("PIP closed");
        }
        else
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.FilePath))
            {
                ShowOsdNotification("No media loaded");
                return;
            }

            // Create secondary player instance for PIP
            try
            {
                _pipPlayer = _playerService!.CreateSecondaryPlayer();
                _pipWindow = new PipWindow(_pipPlayer, _playerService.Player!, _viewModel!.FilePath!)
                {
                    DataContext = _viewModel
                };

                // Hide main video surface (shows black), PIP plays the video
                _playerService.Player?.Pause();
                if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = false;

                // Wire PIP sync on seeks
                if (_viewModel != null)
                {
                    _viewModel.NotifyPipSync = () =>
                    {
                        if (_pipWindow is PipWindow pw)
                            pw.SyncFromMain();
                    };
                }

                _pipWindow.Closed += (s, args) =>
                {
                    _pipWindow = null;
                    _pipPlayer = null;
                    _isPipMode = false;
                    if (BtnPip != null) BtnPip.IsChecked = false;
                    if (_viewModel != null) _viewModel.NotifyPipSync = null;
                    if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = true;
                    _playerService?.Player?.Play();
                };

                _pipWindow.Show(this);
                _isPipMode = true;
                if (BtnPip != null) BtnPip.IsChecked = true;
                ShowOsdNotification("PIP mode active");
            }
            catch (Exception ex)
            {
                _pipWindow = null;
                _pipPlayer = null;
                _isPipMode = false;
                ShowOsdNotification($"PIP failed: {ex.Message}");
            }
        }
    }
    private void OnToggleAlwaysOnTop(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        ShowOsdNotification(Topmost ? "Always on Top: On" : "Always on Top: Off");
    }
    private void OnFullscreenCloseClick(object? sender, RoutedEventArgs e) => Close();
    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
    private void OnToggleShuffle(object? sender, RoutedEventArgs e) => _viewModel?.ToggleShuffle();
    private void OnNextChapter(object? sender, RoutedEventArgs e) => _viewModel?.NextChapter();
    private void OnPrevChapter(object? sender, RoutedEventArgs e) => _viewModel?.PreviousChapter();
    private void OnPrevious(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.HasMultiplePlaylistItems)
                _viewModel.PreviousItem();
            else
                _viewModel.PreviousChapter();
        }
    }
    private void OnRewind(object? sender, RoutedEventArgs e) => _viewModel?.SeekLargeBackward();
    private void OnForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekLargeForward();
    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.HasMultiplePlaylistItems)
                _viewModel.NextItem();
            else
                _viewModel.NextChapter();
        }
    }
    private void OnToggleLoopFile(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopFile();
    private void OnToggleLoopPlaylist(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopPlaylist();
    private void OnScreenshot(object? sender, RoutedEventArgs e) => _viewModel?.Screenshot();
    private PlaylistDialog? _playlistDialog;

    private void OnOpenPlaylistDialog(object? sender, RoutedEventArgs e)
    {
        if (_playlistDialog == null)
        {
            _playlistDialog = new PlaylistDialog
            {
                DataContext = _viewModel
            };
            _playlistDialog.Closed += (s, args) => _playlistDialog = null;
            _playlistDialog.Show(this);
        }
        else
        {
            _playlistDialog.Activate();
        }
    }

    private void OnPlayerFullscreenChanged(object? sender, FullscreenChangedEventArgs e)
    {
        #region debug-point VT-C
        App.DebugReport("VT", "MainWindow.OnPlayerFullscreenChanged", "FullscreenChangedEvent.", new
        {
            isFullscreen = e.IsFullscreen,
            beforeWindowState = WindowState.ToString(),
            videoHostBounds = VideoHost?.Bounds.ToString(),
            renderScaling = RenderScaling
        }, runId: "pre-fix");
        #endregion
        Dispatcher.UIThread.Post(() =>
        {
            WindowState = e.IsFullscreen ? global::Avalonia.Controls.WindowState.FullScreen : global::Avalonia.Controls.WindowState.Normal;
            RefreshFullscreenUi();
        });
    }

    protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == global::Avalonia.Controls.Window.WindowStateProperty)
        {
            if (change.NewValue is global::Avalonia.Controls.WindowState state)
            {
                bool isFullscreen = state == global::Avalonia.Controls.WindowState.FullScreen;
                if (_playerService?.Player != null && _playerService.Player.IsFullscreen != isFullscreen)
                {
                    _playerService.Player.SetFullscreen(isFullscreen);
                }
                RefreshFullscreenUi();
            }
        }
    }

    private void RefreshFullscreenUi()
    {
        if (_playerService?.Player == null || FullscreenIconPath == null || BtnFullscreen == null) return;
        if (_playerService.Player.IsFullscreen)
        {
            TrySetIcon(FullscreenIconPath, "FullscreenExitIcon");
            global::Avalonia.Controls.ToolTip.SetTip(BtnFullscreen, "Exit Fullscreen (F)");
            if (HeaderBar != null) { HeaderBar.IsVisible = false; HeaderBar.IsHitTestVisible = false; }
            if (BtnFullscreenClose != null) BtnFullscreenClose.IsVisible = false;
            if (WindowControlsPanel != null) WindowControlsPanel.IsVisible = false;
            if (TitleText != null) TitleText.IsVisible = false;
            if (BtnPrimaryMenu != null) BtnPrimaryMenu.IsVisible = false;
            if (BtnPip != null) BtnPip.IsVisible = false;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = false;
            if (FullscreenHeader != null) { FullscreenHeader.IsVisible = true; FullscreenHeader.Opacity = 1; }
        }
        else
        {
            TrySetIcon(FullscreenIconPath, "FullscreenEnterIcon");
            global::Avalonia.Controls.ToolTip.SetTip(BtnFullscreen, "Fullscreen (F)");
            if (HeaderBar != null) { HeaderBar.IsVisible = true; HeaderBar.IsHitTestVisible = true; }
            if (BtnFullscreenClose != null) BtnFullscreenClose.IsVisible = false;
            if (WindowControlsPanel != null) WindowControlsPanel.IsVisible = true;
            if (TitleText != null) TitleText.IsVisible = true;
            if (BtnPrimaryMenu != null) BtnPrimaryMenu.IsVisible = true;
            if (BtnPip != null) BtnPip.IsVisible = Bounds.Width >= MediumBreakpoint;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = !string.IsNullOrEmpty(_viewModel?.FilePath);
            if (FullscreenHeader != null) FullscreenHeader.IsVisible = false;
        }
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        if (MaximizeRestoreIconPath == null) return;
        if (WindowState == global::Avalonia.Controls.WindowState.Maximized)
        {
            TrySetIcon(MaximizeRestoreIconPath, "MaxRestoreIcon");
            if (BtnMaximizeRestore != null) global::Avalonia.Controls.ToolTip.SetTip(BtnMaximizeRestore, "Restore");
        }
        else
        {
            TrySetIcon(MaximizeRestoreIconPath, "MaximizeIcon");
            if (BtnMaximizeRestore != null) global::Avalonia.Controls.ToolTip.SetTip(BtnMaximizeRestore, "Maximize");
        }
    }

    // ========================
    //  Event handlers
    // ========================
    private async void OnMediaOpened(object? sender, EventArgs e) 
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            #region debug-point VT-A
            App.DebugReport("VT", "MainWindow.OnMediaOpened", "Opened event received.", new
            {
                windowState = WindowState.ToString(),
                startPageVisible = StartPage?.IsVisible,
                videoSurfaceVisible = VideoHost?.IsVideoSurfaceVisible,
                videoHostBounds = VideoHost?.Bounds.ToString(),
                renderScaling = RenderScaling
            }, runId: "pre-fix");
            #endregion
            _viewModel?.RefreshState();
            _isLoading = false;
            StopLoadingSpinner();
        });

        // Crossfade: StartPage out, VideoHost in
        if (StartPage != null)
        {
            await FadeVisual(StartPage, 1, 0, 200, true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StartPage.IsVisible = false;
                StartPage.Opacity = 1;
            });
        }

        if (VideoHost != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                VideoHost.IsVideoSurfaceVisible = true;
                VideoHost.Opacity = 0;
            });
            await FadeVisual(VideoHost, 0, 1, 300, false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (ControlsBox != null) ControlsBox.IsVisible = true;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = true;
            if (_viewModel != null)
            {
                _lastDuration = _viewModel.Duration;
                var d = _lastDuration;
                if (d.TotalSeconds > 0)
                {
                    if (DurationTimeLabel != null) DurationTimeLabel.Text = FormatTimeSpan(d);
                    if (PositionTimeLabel != null) PositionTimeLabel.Text = FormatTimeSpan(_viewModel.Position);
                }
            }
            UpdatePlayPauseIcon();
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        });
    }

    private DispatcherTimer? _pauseIndicatorTimer;

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        #region debug-point VT-B
        App.DebugReport("VT", "MainWindow.OnPlaybackStateChanged", "PlaybackStateChangedEvent.", new
        {
            isPaused = e.IsPaused,
            windowState = WindowState.ToString(),
            videoSurfaceVisible = VideoHost?.IsVideoSurfaceVisible,
            videoHostBounds = VideoHost?.Bounds.ToString()
        }, runId: "pre-fix");
        #endregion
        Dispatcher.UIThread.Post(() =>
        {
            if (PlayPauseIconPath != null)
            {
                PlayPauseIconPath.Kind = e.IsPaused ? global::Material.Icons.MaterialIconKind.Play : global::Material.Icons.MaterialIconKind.Pause;
            }

            if (e.IsPaused)
            {
                // Show pause indicator briefly
                if (PauseIndicator != null)
                {
                    PauseIndicator.IsVisible = true;
                    PauseIndicator.Opacity = 1;
                    _pauseIndicatorTimer?.Stop();
                    _pauseIndicatorTimer = new DispatcherTimer(
                        TimeSpan.FromMilliseconds(500),
                        DispatcherPriority.Normal,
                        async (s, args) =>
                        {
                            _pauseIndicatorTimer?.Stop();
                            if (PauseIndicator != null)
                            {
                                await FadeVisual(PauseIndicator, 1, 0, 150, false);
                                if (PauseIndicator != null) PauseIndicator.IsVisible = false;
                            }
                        });
                    _pauseIndicatorTimer.Start();
                }
                
                // Show UI when paused
                ShowUiControls();
            }
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Allow replay at EOF (GTK parity)
            _playerService?.Player?.Seek(TimeSpan.Zero);
            _playerService?.Player?.Pause();
            ShowUiControls();
            if (ReplayOverlay != null) ReplayOverlay.IsVisible = true;
        });
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        if (ReplayOverlay != null) ReplayOverlay.IsVisible = false;
        _playerService?.Player?.Seek(TimeSpan.Zero);
        _playerService?.Player?.Play();
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        _lastPosition = e.Position;
        _lastDuration = e.Duration;

        Dispatcher.UIThread.Post(() =>
        {
            if (_isSeeking) return;
            UpdateSeekBar();
            UpdateTimeLabels();
        });
    }

    private void UpdateTimeLabels()
    {
        if (PositionTimeLabel != null)
            PositionTimeLabel.Text = FormatTimeSpan(_lastPosition);
        if (DurationTimeLabel != null)
            DurationTimeLabel.Text = FormatTimeSpan(_lastDuration);
    }
    private void OnChapterListChanged(object? sender, ChapterListChangedEventArgs e) => _viewModel?.RefreshState();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.FilePath))
        {
            if (!string.IsNullOrEmpty(_viewModel?.FilePath))
            {
                if (_isLoading) return;
                _isLoading = true;
                // Show loading spinner while media opens
                StartLoadingSpinner();
                // Media loaded: hide StartPage, show controls and Open button (matching Python)
                if (StartPage?.IsVisible == true) StartPage.IsVisible = false;
                if (ControlsBox != null) ControlsBox.IsVisible = true;
                if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = true;
                if (TitleText != null) TitleText.Text = _viewModel.Title;
                Title = $"Cine — {_viewModel.Title}";

                // Restart auto-hide timer now that media is playing
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            }
            else
            {
                // No media: show StartPage, hide controls and Open button (matching Python idle-active)
                if (StartPage?.IsVisible == false) StartPage.IsVisible = true;
                if (ControlsBox != null) ControlsBox.IsVisible = false;
                if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = false;
                if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = false;
                if (TitleText != null) TitleText.Text = "Cine";
                
                // Ensure UI stays visible when idle
                ShowUiControls();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsPlaying) ||
                 e.PropertyName == nameof(MainViewModel.IsPaused))
        {
            UpdatePlayPauseIcon();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsSubtitleEnabled))
        {
            RefreshSubtitleIcon();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsAudioEnabled))
        {
            RefreshVolumeIcon();
            if (_viewModel != null)
            {
                if (_viewModel.IsMuted || _viewModel.VolumeValue == 0)
                    ShowOsdNotification("Muted");
                else
                    ShowOsdNotification($"Volume: {_viewModel.VolumeValue}%");
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.SpeedValue))
        {
            if (_viewModel != null)
                ShowOsdNotification($"Speed: {_viewModel.SpeedValue:F1}x", 3000);
        }
        else if (e.PropertyName == nameof(MainViewModel.SeekValue))
        {
            if (!_isSeeking)
            {
                UpdateSeekBar();
            }
        }
    }

    /// <summary>
    /// Updates the volume button icon based on current volume level and mute state.
    private void RefreshVolumeIcon()
    {
        if (_viewModel == null) return;
        bool isMuted = _viewModel.IsMuted;
        if (VolumeArcsPath != null)
            VolumeArcsPath.IsVisible = !isMuted;
        if (VolumeMuteCrossPath != null)
            VolumeMuteCrossPath.IsVisible = isMuted;
    }

    private void RefreshSubtitleIcon()
    {
        if (SubtitleIconPath == null || _viewModel == null) return;
        SubtitleIconPath.Kind = _viewModel.IsSubtitleEnabled ? global::Material.Icons.MaterialIconKind.Subtitles : global::Material.Icons.MaterialIconKind.ClosedCaptionOutline;
    }

    private void RefreshAudioIcon()
    {
        if (AudioIconPath == null || _viewModel == null) return;
        AudioIconPath.Kind = _viewModel.IsAudioEnabled ? global::Material.Icons.MaterialIconKind.Music : global::Material.Icons.MaterialIconKind.MusicOff;
    }

    private void UpdatePlayPauseIcon()
    {
        if (PlayPauseIconPath == null || _viewModel == null) return;
        PlayPauseIconPath.Kind = _viewModel.IsPlaying ? global::Material.Icons.MaterialIconKind.Pause : global::Material.Icons.MaterialIconKind.Play;
    }

    private static void TrySetIcon(global::Material.Icons.Avalonia.MaterialIcon icon, string resourceKey)
    {
        icon.Kind = resourceKey switch
        {
            "FullscreenEnterIcon" => global::Material.Icons.MaterialIconKind.Fullscreen,
            "FullscreenExitIcon" => global::Material.Icons.MaterialIconKind.FullscreenExit,
            "MaxRestoreIcon" => global::Material.Icons.MaterialIconKind.WindowMaximize,
            "MaximizeIcon" => global::Material.Icons.MaterialIconKind.WindowMaximize,
            "PlayIcon" => global::Material.Icons.MaterialIconKind.Play,
            "PauseIcon" => global::Material.Icons.MaterialIconKind.Pause,
            "SubtitlesIcon" => global::Material.Icons.MaterialIconKind.Subtitles,
            "SubtitlesOffIcon" => global::Material.Icons.MaterialIconKind.ClosedCaptionOutline,
            "AudioIcon" => global::Material.Icons.MaterialIconKind.Music,
            "AudioOffIcon" => global::Material.Icons.MaterialIconKind.MusicOff,
            _ => icon.Kind
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_isDisposed) return;
        DebugLog("OnOpened enter");
        ReportWindowState("MainWindow.OnOpened.Enter");

        try
        {
            if (WindowState == global::Avalonia.Controls.WindowState.Minimized)
                WindowState = global::Avalonia.Controls.WindowState.Normal;

            var primary = Screens?.Primary;
            if (primary != null)
            {
                var work = primary.WorkingArea;
                double scale = RenderScaling;
                int w = (int)Math.Max(332 * scale, Bounds.Width * scale);
                int h = (int)Math.Max(187 * scale, Bounds.Height * scale);
                int x = work.X + Math.Max(0, (work.Width - w) / 2);
                int y = work.Y + Math.Max(0, (work.Height - h) / 2);
                Position = new PixelPoint(x, y);
            }

            Activate();
        }
        catch
        {
        }

        var handle = PlatformImplHandle();
        DebugLog($"Platform handle has value={handle.HasValue} value={handle.GetValueOrDefault()}");
        if (handle.HasValue && handle.Value != IntPtr.Zero && _videoHost != null)
        {
            _videoHost.ParentHwnd = handle.Value;
            DebugLog("VideoHost.ParentHwnd assigned");
        }

        // Initial state: StartPage visible, controls hidden (matching Python reference idle-active)
        if (StartPage != null) StartPage.IsVisible = true;
        if (ControlsBox != null) ControlsBox.IsVisible = false;
        if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = false;
        if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = false;
        RefreshFullscreenUi();
        RefreshSubtitleIcon();
        RefreshAudioIcon();
        RefreshVolumeIcon();
        ReportWindowState("MainWindow.OnOpened.AfterInitialState");
        Dispatcher.UIThread.Post(() => ReportWindowState("MainWindow.OnOpened.PostLayout"), DispatcherPriority.Background);
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

    protected override void OnClosed(EventArgs e)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        _sessionSaveTimer?.Stop();
        _sessionSaveTimer = null;
        _viewModel?.SaveSession();
        _playerService?.Dispose();
        base.OnClosed(e);
    }
}
