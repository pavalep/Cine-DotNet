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

    // UI Auto-hide
    private DispatcherTimer? _autoHideTimer;
    private bool _uiVisible = true;
    private const double AutoHideDelaySeconds = 3.0;
    private global::Avalonia.Point _lastMousePosition;
    private bool _isMouseOverControls;
    private DateTime _lastSeekWheel = DateTime.MinValue;
    private int _activeFlyouts;

    // Responsive breakpoints
    private const double NarrowBreakpoint = 600.0;
    private const double MediumBreakpoint = 1024.0;

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
        MaximizeRestoreIconPath = this.FindControl<global::Avalonia.Controls.Shapes.Path>("MaximizeRestoreIconPath");
        BtnClose = this.FindControl<global::Avalonia.Controls.Button>("BtnClose");
        ControlsBox = this.FindControl<Border>("ControlsBox");
        BtnPrevious = this.FindControl<global::Avalonia.Controls.Button>("BtnPrevious");
        BtnPlayPause = this.FindControl<global::Avalonia.Controls.Button>("BtnPlayPause");
        BtnNext = this.FindControl<global::Avalonia.Controls.Button>("BtnNext");
        BtnVolumeMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnVolumeMenu");
        BtnSubtitlesMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnSubtitlesMenu");
        SubtitleIconPath = this.FindControl<global::Avalonia.Controls.Shapes.Path>("SubtitleIconPath");
        BtnAudioMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnAudioMenu");
        AudioIconPath = this.FindControl<global::Avalonia.Controls.Shapes.Path>("AudioIconPath");
        BtnVideoMenu = this.FindControl<global::Avalonia.Controls.Button>("BtnVideoMenu");
        BtnLoopPlaylist = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnLoopPlaylist");
        BtnLoopFile = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnLoopFile");
        BtnMuteToggle = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnMuteToggle");
        BtnOptionsMenu = this.FindControl<global::Cine.Avalonia.Components.OptionsMenuButton>("BtnOptionsMenu");
        BtnFullscreen = this.FindControl<global::Avalonia.Controls.Primitives.ToggleButton>("BtnFullscreen");
        FullscreenIconPath = this.FindControl<global::Avalonia.Controls.Shapes.Path>("FullscreenIconPath");
        SeekArea = this.FindControl<Grid>("SeekArea");
        ChapterPreviewPopover = this.FindControl<Border>("ChapterPreviewPopover");
        ChapterPreviewText = this.FindControl<TextBlock>("ChapterPreviewText");
        PositionTimeLabel = this.FindControl<TextBlock>("PositionTimeLabel");
        DurationTimeLabel = this.FindControl<TextBlock>("DurationTimeLabel");
        DropIndicatorOverlay = this.FindControl<Border>("DropIndicatorOverlay");
        DropIndicatorIcon = this.FindControl<global::Avalonia.Controls.Shapes.Path>("DropIndicatorIcon");
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
        _playerService.Initialize();

        var player = _playerService.Player;
        if (player == null)
            return;

        _viewModel = new MainViewModel(player);
        DataContext = _viewModel;

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

        _videoHost.ChildWindowCreated += OnVideoHostChildCreated;
        KeyDown += OnKeyDown;

        // Start page - show on initial launch, hide when media opens
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        InitializeAutoHide();
        InitializeResponsiveLayout();
        InitializeFlyoutTracking();

        // Drag and Drop
        AddHandler(global::Avalonia.Input.DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(global::Avalonia.Input.DragDrop.DragLeaveEvent, OnWindowDragLeave);
        AddHandler(global::Avalonia.Input.DragDrop.DropEvent, OnWindowDrop);

        ReportWindowState("MainWindow.InitializeComponent.Finish");
        DebugLog("InitializeComponent finish");
    }

    // ========================
    //  WINDOW-LEVEL DRAG-AND-DROP
    // ========================

    private void OnWindowDragEnter(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.Copy;
            
            var sp = this.FindControl<StartPage>("StartPage");
            if (sp != null && sp.IsVisible)
            {
                var dt = sp.FindControl<Border>("DropTarget");
                if (dt != null)
                {
                    dt.BorderBrush = new global::Avalonia.Media.SolidColorBrush(
                        global::Avalonia.Media.Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));
                    dt.Background = new global::Avalonia.Media.SolidColorBrush(
                        global::Avalonia.Media.Color.FromArgb(0x40, 0x00, 0x78, 0xD7));
                }
            }

            UpdateDropIndicator(e, show: true);
        }
        else
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.None;
            UpdateDropIndicator(e, show: false);
        }
    }

    private void OnWindowDragLeave(object? sender, RoutedEventArgs e)
    {
        ResetStartPageDragVisuals();
        UpdateDropIndicator(null, show: false);
    }

    private void OnWindowDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        ResetStartPageDragVisuals();
        UpdateDropIndicator(null, show: false);

        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToArray();
                var videoFiles = StartPage.FilterVideoFiles(paths).ToList();
            var subtitleFiles = paths.Where(f => f.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (videoFiles.Any())
            {
                _viewModel?.OpenFiles(videoFiles.ToArray());
            }
            
            if (subtitleFiles.Any() && _viewModel != null && !string.IsNullOrEmpty(_viewModel.FilePath))
            {
                foreach (var subFile in subtitleFiles)
                {
                    _playerService?.Player?.AddSubtitle(subFile);
                }
            }
            }
        }
    }

    private void ResetStartPageDragVisuals()
    {
        var sp = this.FindControl<StartPage>("StartPage");
        if (sp == null) return;
        var dt = sp.FindControl<Border>("DropTarget");
        if (dt != null)
        {
            dt.BorderBrush = new global::Avalonia.Media.SolidColorBrush(
                global::Avalonia.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            dt.Background = new global::Avalonia.Media.SolidColorBrush(
                global::Avalonia.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        }
    }

    private bool _isDropIndicatorVisible = false;
    private async void UpdateDropIndicator(global::Avalonia.Input.DragEventArgs? e, bool show)
    {
        if (DropIndicatorOverlay == null || DropIndicatorText == null || DropIndicatorIcon == null)
            return;

        if (show == _isDropIndicatorVisible) return;
        _isDropIndicatorVisible = show;

        if (show)
        {
            bool subtitleDrop = false;
            try
            {
                var files = e?.DataTransfer?.TryGetFiles();
                var first = files?.FirstOrDefault()?.Path.LocalPath;
                if (!string.IsNullOrWhiteSpace(first))
                {
                    var ext = Path.GetExtension(first).ToLowerInvariant();
                    subtitleDrop = ext is ".srt" or ".ass" or ".ssa" or ".vtt" or ".sub" or ".idx";
                }
            }
            catch { }

            if (subtitleDrop && !string.IsNullOrWhiteSpace(_viewModel?.FilePath))
            {
                DropIndicatorText.Text = "Add Subtitle Track";
                TrySetIcon(DropIndicatorIcon, "SubtitlesIcon");
            }
            else
            {
                DropIndicatorText.Text = "Play";
                TrySetIcon(DropIndicatorIcon, "PlayIcon");
            }

            DropIndicatorOverlay.IsVisible = true;
            await FadeVisual(DropIndicatorOverlay, DropIndicatorOverlay.Opacity, 1, 200, true);
        }
        else
        {
            await FadeVisual(DropIndicatorOverlay, DropIndicatorOverlay.Opacity, 0, 200, false);
            if (!_isDropIndicatorVisible)
                DropIndicatorOverlay.IsVisible = false;
        }
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

    /// <summary>
    /// Updates the layout based on current window width.
    /// Breakpoints: Narrow (&lt;495px)
    /// Matches Python Adw.Breakpoint in window.blp.
    /// </summary>
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

        // Button sizes are generally constant in the reference but we can tweak if needed
        double btnSize = isNarrow ? 36 : 40;
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

        if (ControlsBox != null)
            ControlsBox.Height = isNarrow ? 90 : 120;
        if (HeaderBar != null)
            HeaderBar.Height = isNarrow ? 40 : 50;
        if (TitleText != null)
            TitleText.FontSize = isNarrow ? 12 : 14;
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
    //  UI Auto-hide Behavior
    //  (No PointerEnter/PointerLeave in this Avalonia version -
    //   use bounds checking in PointerMoved instead)
    // ========================

    private void InitializeAutoHide()
    {
        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AutoHideDelaySeconds)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        PointerMoved += OnWindowPointerMoved;
        SetUiControlsVisibility(true);
        _autoHideTimer?.Start();
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        _autoHideTimer?.Stop();
        
        // Only hide if we have media loaded AND mouse is not over controls
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        var isInteractiveOverlayActive = _activeFlyouts > 0 || (DropIndicatorOverlay?.IsVisible ?? false);
        if (!_isMouseOverControls && hasMedia && !isInteractiveOverlayActive)
            HideUiControls();
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;

        // Check if mouse is over the controls overlay using bounds
        _isMouseOverControls =
            (HeaderBar != null && IsPositionOverElement(pos, HeaderBar)) ||
            (ControlsBox != null && IsPositionOverElement(pos, ControlsBox));

        if (Math.Abs(pos.X - _lastMousePosition.X) > 1 ||
            Math.Abs(pos.Y - _lastMousePosition.Y) > 1)
        {
            _lastMousePosition = pos;
            if (!_uiVisible)
            {
                if (pos.Y >= Math.Max(0, Bounds.Height - 90))
                    ShowUiControls();
                return;
            }
            else
            {
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            }
        }
    }

    /// <summary>Checks if a position in visual coordinates is over a given visual element.</summary>
    private bool IsPositionOverElement(global::Avalonia.Point pos, Visual element)
    {
        try
        {
            var elementOffset = element.TranslatePoint(new global::Avalonia.Point(0, 0), this);
            if (elementOffset.HasValue)
            {
                var elementRect = new Rect(elementOffset.Value, new global::Avalonia.Size(element.Bounds.Width, element.Bounds.Height));
                return elementRect.Contains(pos);
            }
        }
        catch { }
        return false;
    }

    private async void ShowUiControls()
    {
        if (_uiVisible) return;
        if (HeaderBar == null && ControlsBox == null) return;
        _uiVisible = true;
        _autoHideTimer?.Stop();
        if (HeaderBar != null)
        {
            HeaderBar.IsVisible = true;
            await FadeVisual(HeaderBar, 0, 1, 350, true);
        }
        if (ControlsBox != null && !string.IsNullOrEmpty(_viewModel?.FilePath))
        {
            ControlsBox.IsVisible = true;
            await FadeVisual(ControlsBox, 0, 1, 350, true);
        }
        _autoHideTimer?.Start();
    }

    private async void HideUiControls()
    {
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (!_uiVisible || !hasMedia) return;
        if (_activeFlyouts > 0 || (DropIndicatorOverlay?.IsVisible ?? false)) return;
        
        _uiVisible = false;
        _autoHideTimer?.Stop();
        if (HeaderBar != null)
            await FadeVisual(HeaderBar, 1, 0, 300, false);
        if (ControlsBox != null)
            await FadeVisual(ControlsBox, 1, 0, 300, false);
        await Task.Delay(50);
        if (!_uiVisible)
        {
            if (HeaderBar != null) HeaderBar.IsVisible = false;
            if (ControlsBox != null) ControlsBox.IsVisible = false;
        }
    }

    private async Task FadeVisual(Visual visual, double from, double to, double durationMs, bool easeOut)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        visual.Opacity = from;
        while (sw.Elapsed.TotalMilliseconds < durationMs)
        {
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / durationMs, 1.0);
            double eased = easeOut
                ? 1 - Math.Cos(progress * Math.PI / 2)
                : Math.Sin(progress * Math.PI / 2);
            visual.Opacity = from + (to - from) * eased;
            await Task.Delay(16);
        }
        visual.Opacity = to;
    }

    private void SetUiControlsVisibility(bool visible)
    {
        _uiVisible = visible;
        if (HeaderBar != null)
        {
            HeaderBar.IsVisible = visible;
            HeaderBar.Opacity = visible ? 1 : 0;
        }
        if (ControlsBox != null)
        {
            ControlsBox.IsVisible = visible && !string.IsNullOrEmpty(_viewModel?.FilePath);
            ControlsBox.Opacity = visible ? 1 : 0;
        }
    }

    private void ToggleUiControls()
    {
        if (_uiVisible) HideUiControls(); else ShowUiControls();
    }

    private void InitializeFlyoutTracking()
    {
        TrackFlyout(BtnOpenMenu);
        TrackFlyout(BtnPrimaryMenu);
        TrackFlyout(BtnVolumeMenu);
        TrackFlyout(BtnSubtitlesMenu);
        TrackFlyout(BtnAudioMenu);
        TrackFlyout(BtnVideoMenu);
        TrackFlyout(BtnOptionsMenu);
    }

    private void TrackFlyout(global::Avalonia.Controls.Control? control)
    {
        if (control is null) return;
        if (control is global::Avalonia.Controls.Button b && b.Flyout != null)
        {
            b.Flyout.Opened += (_, _) => _activeFlyouts++;
            b.Flyout.Closed += (_, _) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
        }
    }

    // ========================
    //  Seek hover / wheel parity
    // ========================

    private void OnSeekAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel == null || SeekArea == null || ChapterPreviewPopover == null || ChapterPreviewText == null)
            return;
        if (_viewModel.Duration.TotalSeconds <= 0 || _viewModel.Chapters.Count == 0)
            return;

        var p = e.GetPosition(SeekArea);
        var trackStart = 0.0;
        var trackWidth = Math.Max(1.0, SeekArea.Bounds.Width);
        var normalized = Math.Clamp((p.X - trackStart) / trackWidth, 0, 1);
        var seconds = normalized * _viewModel.Duration.TotalSeconds;

        var chapter = _viewModel.Chapters
            .Where(c => c.Time <= seconds)
            .OrderByDescending(c => c.Time)
            .FirstOrDefault();

        if (chapter == null) return;
        ChapterPreviewText.Text = $"{chapter.Title}  ({FormatChapterTime(seconds)})";
        ChapterPreviewPopover.IsVisible = true;
        
        ChapterPreviewPopover.Measure(new global::Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
        var popoverWidth = ChapterPreviewPopover.DesiredSize.Width;
        
        var xPos = trackStart + (normalized * trackWidth) - (popoverWidth / 2);
        xPos = Math.Clamp(xPos, 0, Math.Max(0, SeekArea.Bounds.Width - popoverWidth));

        ChapterPreviewPopover.Margin = new Thickness(xPos, -34, 0, 0);
    }

    private void OnSeekAreaPointerExited(object? sender, PointerEventArgs e)
    {
        if (ChapterPreviewPopover != null)
            ChapterPreviewPopover.IsVisible = false;
    }

    private void OnSeekAreaPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null) return;

        var now = DateTime.UtcNow;
        if ((now - _lastSeekWheel).TotalMilliseconds < 90)
            return;
        _lastSeekWheel = now;

        if (e.Delta.Y > 0)
            _viewModel.SeekForward();
        else if (e.Delta.Y < 0)
            _viewModel.SeekBackward();
        e.Handled = true;
    }

    private void OnSeekAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null || SeekArea == null || _viewModel.Duration.TotalSeconds <= 0) return;
        var p = e.GetPosition(SeekArea);
        var trackStart = 0.0;
        var trackWidth = Math.Max(1.0, SeekArea.Bounds.Width);
        var normalized = Math.Clamp((p.X - trackStart) / trackWidth, 0, 1);
        var target = TimeSpan.FromSeconds(normalized * _viewModel.Duration.TotalSeconds);
        _viewModel.Position = target;
        e.Handled = true;
    }

    private static string FormatChapterTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(ts.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }

    // ========================
    //  TRACK MENU BUILDERS
    //  Programmatically build MenuFlyout items from typed TrackMenuItem collections.
    //  This provides proper command routing and visual styling parity with Python's
    //  track selection menus (active tracks shown bold with accent indicator).
    // ========================

    private void OnSubtitlesMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || BtnSubtitlesMenu == null) return;
        var flyout = BuildTrackMenuFlyout(_viewModel.SubtitleTracks);
        TrackFlyout(flyout);
        _activeFlyouts++; // Increment early for programmatic show
        flyout.Closed += (s, args) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1); // Decrement fallback if Opened doesn't fire
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

    /// <summary>
    /// Builds a MenuFlyout from a collection of TrackMenuItems with proper
    /// visual styling (bold for selected, dimmed for pseudo-entries).
    /// </summary>
    private global::Avalonia.Controls.MenuFlyout BuildTrackMenuFlyout(System.Collections.ObjectModel.ObservableCollection<TrackMenuItem> tracks)
    {
        var flyout = new global::Avalonia.Controls.MenuFlyout();

        foreach (var track in tracks)
        {
            var stack = new global::Avalonia.Controls.StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal
            };
            stack.Children.Add(new global::Avalonia.Controls.TextBlock
            {
                Text = track.DisplayName,
                FontWeight = track.IsSelected ? global::Avalonia.Media.FontWeight.SemiBold : global::Avalonia.Media.FontWeight.Normal,
                FontSize = 12
            });

            if (track.IsSelected && !track.IsPseudoEntry)
            {
                stack.Children.Add(new global::Avalonia.Controls.Border
                {
                    Width = 6, Height = 6,
                    CornerRadius = new global::Avalonia.CornerRadius(3),
                    Background = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xFF, 0x6C, 0xB4, 0xFF)),
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new global::Avalonia.Thickness(4, 0, 0, 0)
                });
            }

            var menuItem = new global::Avalonia.Controls.MenuItem
            {
                Header = stack,
                Opacity = track.DisplayOpacity,
                Command = track.SelectCommand
            };
            menuItem.Classes.Add("track-item");
            if (track.IsPseudoEntry)
            {
                menuItem.Classes.Add("track-pseudo");
            }
            flyout.Items.Add(menuItem);
        }

        return flyout;
    }

    private void TrackFlyout(global::Avalonia.Controls.MenuFlyout flyout)
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
    private void OnNewWindowClick(object? sender, RoutedEventArgs e) => new MainWindow().Show();
    private void OnPreferencesClick(object? sender, RoutedEventArgs e) { } // Placeholder
    private void OnShortcutsClick(object? sender, RoutedEventArgs e) { } // Placeholder
    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var closeButton = new global::Avalonia.Controls.Button
        {
            Content = "Close",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var messageBox = new Window
        {
            Title = "About Cine",
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Cine Media Player", FontSize = 20, FontWeight = FontWeight.Bold, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center },
                    new TextBlock { Text = "A native Windows media player built with Avalonia UI.", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center },
                    new TextBlock { Text = "Version 1.0.0", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center },
                    closeButton
                }
            },
            Width = 350,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            WindowDecorations = global::Avalonia.Controls.WindowDecorations.Full
        };

        closeButton.Click += (s, a) => messageBox.Close();
        await messageBox.ShowDialog(this);
    }
    private void OnSeekBack(object? sender, RoutedEventArgs e) => _viewModel?.SeekBackward();
    private void OnSeekForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekForward();
    private void OnToggleMute(object? sender, RoutedEventArgs e) => _viewModel?.ToggleMute();
    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();
    private void OnFullscreenCloseClick(object? sender, RoutedEventArgs e) => Close();
    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
    private void OnToggleShuffle(object? sender, RoutedEventArgs e) => _viewModel?.ToggleShuffle();
    private void OnNextChapter(object? sender, RoutedEventArgs e) => _viewModel?.NextChapter();
    private void OnPrevChapter(object? sender, RoutedEventArgs e) => _viewModel?.PreviousChapter();
    private void OnPrevious(object? sender, RoutedEventArgs e) => _viewModel?.PreviousChapter();
    private void OnRewind(object? sender, RoutedEventArgs e) => _viewModel?.SeekLargeBackward();
    private void OnForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekLargeForward();
    private void OnNext(object? sender, RoutedEventArgs e) => _viewModel?.NextChapter();
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
            global::Avalonia.Application.Current!.TryGetResource("FullscreenExitIcon", global::Avalonia.Styling.ThemeVariant.Default, out var exitIcon);
            FullscreenIconPath.Data = (global::Avalonia.Media.Geometry)exitIcon!;
            global::Avalonia.Controls.ToolTip.SetTip(BtnFullscreen, "Exit Fullscreen (F)");
            if (BtnFullscreenClose != null) BtnFullscreenClose.IsVisible = true;
            if (WindowControlsPanel != null) WindowControlsPanel.IsVisible = false;
            if (TitleText != null) TitleText.IsVisible = false;
            if (BtnPrimaryMenu != null) BtnPrimaryMenu.IsVisible = false;
            if (BtnPip != null) BtnPip.IsVisible = false;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = false;
        }
        else
        {
            global::Avalonia.Application.Current!.TryGetResource("FullscreenEnterIcon", global::Avalonia.Styling.ThemeVariant.Default, out var enterIcon);
            FullscreenIconPath.Data = (global::Avalonia.Media.Geometry)enterIcon!;
            global::Avalonia.Controls.ToolTip.SetTip(BtnFullscreen, "Fullscreen (F)");
            if (BtnFullscreenClose != null) BtnFullscreenClose.IsVisible = false;
            if (WindowControlsPanel != null) WindowControlsPanel.IsVisible = true;
            if (TitleText != null) TitleText.IsVisible = true;
            if (BtnPrimaryMenu != null) BtnPrimaryMenu.IsVisible = true;
            if (BtnPip != null) BtnPip.IsVisible = Bounds.Width >= MediumBreakpoint;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = !string.IsNullOrEmpty(_viewModel?.FilePath);
            UpdateMaximizeIcon();
        }
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
    private void OnMediaOpened(object? sender, EventArgs e) 
    {
        _viewModel?.RefreshState();
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
        Dispatcher.UIThread.Post(() =>
        {
            if (StartPage != null) StartPage.IsVisible = false;
            if (ControlsBox != null) ControlsBox.IsVisible = true;
            if (VideoHost != null) VideoHost.IsVideoSurfaceVisible = true;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = true;
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        });
    }

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
                global::Avalonia.Application.Current!.TryGetResource(e.IsPaused ? "PlayIcon" : "PauseIcon", global::Avalonia.Styling.ThemeVariant.Default, out var icon);
                if (icon is global::Avalonia.Media.Geometry geo)
                    PlayPauseIconPath.Data = geo;
            }

            if (e.IsPaused)
            {
                // Show pause indicator briefly
                if (PauseIndicator != null)
                {
                    PauseIndicator.IsVisible = true;
                    _ = FadeVisual(PauseIndicator, 0, 1, 150, true).ContinueWith(async t =>
                    {
                        await Task.Delay(350);
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await FadeVisual(PauseIndicator, 1, 0, 150, false);
                            if (PauseIndicator != null) PauseIndicator.IsVisible = false;
                        });
                    });
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
        });
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel?.Duration.TotalSeconds > 0)
            {
                var chars = _viewModel.Duration.TotalSeconds >= 36000 ? 8 :
                            _viewModel.Duration.TotalSeconds >= 3600 ? 7 :
                            _viewModel.Duration.TotalSeconds >= 600 ? 6 : 5;
                PositionTimeLabel.MinWidth = chars * 8;
            }
        });
    }
    private void OnChapterListChanged(object? sender, ChapterListChangedEventArgs e) => _viewModel?.RefreshState();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.FilePath))
        {
            if (!string.IsNullOrEmpty(_viewModel?.FilePath))
            {
                // Media loaded: hide StartPage, show controls and Open button (matching Python)
                if (StartPage?.IsVisible == true) StartPage.IsVisible = false;
                if (ControlsBox != null) ControlsBox.IsVisible = true;
                if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = true;
                if (TitleText != null) TitleText.Text = _viewModel.Title;
                
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
        else if (e.PropertyName == nameof(MainViewModel.IsSubtitleEnabled))
        {
            RefreshSubtitleIcon();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsAudioEnabled))
        {
            RefreshAudioIcon();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsMuted) ||
                 e.PropertyName == nameof(MainViewModel.VolumeValue))
        {
            RefreshVolumeIcon();
        }
    }

    /// <summary>
    /// Updates the volume button icon based on current volume level and mute state.
    /// Matches Python reference _update_volume_icon behavior.
    /// </summary>
    private void RefreshVolumeIcon()
    {
        if (VolumeIconPath == null || _viewModel == null) return;
        bool isMuted = _viewModel.IsMuted;
        double vol = _viewModel.VolumeValue;
        string iconKey = (isMuted || vol == 0)
            ? "VolumeMuteIcon"
            : vol < 33 ? "VolumeMaxIcon"   // no low/mid icon in Icons.axaml, fall back to max
            : "VolumeMaxIcon";
        global::Avalonia.Application.Current!.TryGetResource(iconKey, global::Avalonia.Styling.ThemeVariant.Default, out var icon);
        if (icon is global::Avalonia.Media.Geometry geo)
            VolumeIconPath.Data = geo;
    }

    private void RefreshSubtitleIcon()
    {
        if (SubtitleIconPath == null || _viewModel == null) return;
        TrySetIcon(SubtitleIconPath, _viewModel.IsSubtitleEnabled ? "SubtitlesIcon" : "SubtitlesOffIcon");
    }

    private void RefreshAudioIcon()
    {
        if (AudioIconPath == null || _viewModel == null) return;
        TrySetIcon(AudioIconPath, _viewModel.IsAudioEnabled ? "AudioIcon" : "AudioOffIcon");
    }

    private static void TrySetIcon(global::Avalonia.Controls.Shapes.Path path, string resourceKey)
    {
        global::Avalonia.Application.Current!.TryGetResource(resourceKey, global::Avalonia.Styling.ThemeVariant.Default, out var icon);
        if (icon is global::Avalonia.Media.Geometry geo)
            path.Data = geo;
    }

    // ========================
    //  Keyboard shortcuts
    // ========================
    private void OnKeyDown(object? sender, AvaloniaKeyEventArgs e)
    {
        var key = e.Key;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        void Handle(Action action) { action(); e.Handled = true; }

        // Playback
        if (key == Key.Space || key == Key.K || key == Key.P || key == Key.MediaPlayPause) 
            Handle(() => _viewModel?.PlayPause());
        else if (key == Key.MediaStop) 
            Handle(() => _viewModel?.Stop());

        // Fullscreen
        else if (key == Key.Escape) 
            Handle(() => { if (_playerService?.Player?.IsFullscreen == true) _viewModel?.ToggleFullscreen(); });
        else if (key == Key.F || key == Key.F11) 
            Handle(() => _viewModel?.ToggleFullscreen());

        // Volume / Audio
        else if (key == Key.M || key == Key.VolumeMute) 
            Handle(() => _viewModel?.ToggleMute());
        else if (key == Key.Up || key == Key.VolumeUp) 
            Handle(() => _viewModel?.IncreaseVolume());
        else if (key == Key.Down || key == Key.VolumeDown) 
            Handle(() => _viewModel?.DecreaseVolume());
        else if (ctrl && (key == Key.OemMinus || key == Key.Subtract)) 
            Handle(() => { _playerService?.Player?.DecreaseAudioDelay(); });
        else if (ctrl && (key == Key.OemPlus || key == Key.Add)) 
            Handle(() => { _playerService?.Player?.IncreaseAudioDelay(); });

        // Navigation
        else if (key == Key.Left) 
            Handle(() => { if (ctrl) _viewModel?.PreviousChapter(); else if (shift) _viewModel?.SeekLargeBackward(); else _viewModel?.SeekBackward(); });
        else if (key == Key.Right) 
            Handle(() => { if (ctrl) _viewModel?.NextChapter(); else if (shift) _viewModel?.SeekLargeForward(); else _viewModel?.SeekForward(); });
        else if (key == Key.J) 
            Handle(() => _playerService?.Player?.SeekBackward(10));
        else if (key == Key.L && !shift && !ctrl) 
            Handle(() => _playerService?.Player?.SeekForward(10));
        else if (ctrl && key == Key.OemOpenBrackets) 
            Handle(() => _playerService?.Player?.PreviousFrame());
        else if (ctrl && key == Key.OemCloseBrackets) 
            Handle(() => _playerService?.Player?.NextFrame());
        else if (key == Key.MediaNextTrack)
            Handle(() => _viewModel?.NextChapter());
        else if (key == Key.MediaPreviousTrack)
            Handle(() => _viewModel?.PreviousChapter());

        // Subtitles
        else if (key == Key.C) 
            Handle(() => _playerService?.Player?.CycleSubtitleTrack());
        else if (key == Key.OemComma) 
            Handle(() => _playerService?.Player?.DecreaseSubtitleDelay());
        else if (key == Key.OemPeriod) 
            Handle(() => _playerService?.Player?.IncreaseSubtitleDelay());
        else if (key == Key.PageUp) 
            Handle(() => _playerService?.Player?.SetSubtitlePosition((_playerService?.Player?.SubtitlePosition ?? 50) - 1));
        else if (key == Key.PageDown) 
            Handle(() => _playerService?.Player?.SetSubtitlePosition((_playerService?.Player?.SubtitlePosition ?? 50) + 1));

        // Video / Display
        else if ((key == Key.OemPlus || key == Key.Add) && !ctrl) 
            Handle(() => { if (_playerService?.Player != null) _playerService.Player.Zoom += 0.05; });
        else if ((key == Key.OemMinus || key == Key.Subtract) && !ctrl) 
            Handle(() => { if (_playerService?.Player != null) _playerService.Player.Zoom -= 0.05; });
        else if (key == Key.D1) 
            Handle(() => _playerService?.Player?.DecreaseContrast());
        else if (key == Key.D2) 
            Handle(() => _playerService?.Player?.IncreaseContrast());
        else if (key == Key.D3) 
            Handle(() => _playerService?.Player?.DecreaseBrightness());
        else if (key == Key.D4) 
            Handle(() => _playerService?.Player?.IncreaseBrightness());
        else if (key == Key.D5) 
            Handle(() => _playerService?.Player?.DecreaseGamma());
        else if (key == Key.D6) 
            Handle(() => _playerService?.Player?.IncreaseGamma());
        else if (key == Key.D7) 
            Handle(() => _playerService?.Player?.DecreaseSaturation());
        else if (key == Key.D8) 
            Handle(() => _playerService?.Player?.IncreaseSaturation());
        else if (key == Key.OemOpenBrackets && !ctrl) 
            Handle(() => _playerService?.Player?.DecreaseSpeed());
        else if (key == Key.OemCloseBrackets && !ctrl) 
            Handle(() => _playerService?.Player?.IncreaseSpeed());
        else if (key == Key.Back) 
            Handle(() => _playerService?.Player?.ResetSpeed());

        // Miscellaneous
        else if (key == Key.S) 
            Handle(() => { if (shift) _playerService?.Player?.ScreenshotWithoutSubtitles(); else _playerService?.Player?.ScreenshotWithSubtitles(); });
        else if (key == Key.I) 
            Handle(() => { /* Stats not implemented in MF player */ });
        else if (key == Key.L && shift) 
            Handle(() => _viewModel?.ToggleLoopFile());
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
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
        _playerService?.Dispose();
        base.OnClosed(e);
    }
}
