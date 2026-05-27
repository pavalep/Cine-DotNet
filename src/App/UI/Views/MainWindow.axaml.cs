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
using Avalonia.Media;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

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
    private static readonly string DebugLogFile = Path.Combine(
        AppContext.BaseDirectory,
        "cine_startup.log");

    private static void DebugLog(string message)
    {
        try
        {
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}");
        }
        catch { }
    }
    #endregion

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        DebugLog("InitializeComponent start");
        AvaloniaXamlLoader.Load(this);
        DebugLog("XAML loaded");

        _videoHost = this.FindControl<D3D11VideoHost>("VideoHost");
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
        
        // Setup new observer parity events
        if (player is MediaFoundationPlayer mfPlayer)
        {
            mfPlayer.PlaybackStateChangedEvent += OnPlaybackStateChanged;
            mfPlayer.MediaEnded += OnMediaEnded;
        }

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
                DropIndicatorIcon.Data = Geometry.Parse("M 4 4H 20V 20H 4V 4 Z M 8 8H 12V 16H 8V 8 Z M 14 8H 18V 16H 14V 8 Z");
            }
            else
            {
                DropIndicatorText.Text = "Play";
                DropIndicatorIcon.Data = Geometry.Parse("M 8 5V 19L 16 12L 8 5 Z");
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
        SetButtonSize(BtnRewind, btnSize);
        SetButtonSize(BtnForward, btnSize);
        SetButtonSize(BtnVolumeMenu, btnSize);
        SetButtonSize(BtnFullscreen, btnSize);
        SetButtonSize(BtnLoopFile, btnSize);
        SetButtonSize(BtnLoopPlaylist, btnSize);

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
        if (UiControlsOverlay != null)
            _isMouseOverControls = IsPositionOverElement(pos, UiControlsOverlay);

        if (Math.Abs(pos.X - _lastMousePosition.X) > 1 ||
            Math.Abs(pos.Y - _lastMousePosition.Y) > 1)
        {
            _lastMousePosition = pos;
            if (!_uiVisible)
                ShowUiControls();
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
        if (_uiVisible || UiControlsOverlay == null) return;
        _uiVisible = true;
        _autoHideTimer?.Stop();
        UiControlsOverlay.IsVisible = true;
        await FadeVisual(UiControlsOverlay, 0, 1, 350, true);
        _autoHideTimer?.Start();
    }

    private async void HideUiControls()
    {
        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (!_uiVisible || UiControlsOverlay == null || !hasMedia) return;
        if (_activeFlyouts > 0 || (DropIndicatorOverlay?.IsVisible ?? false)) return;
        
        _uiVisible = false;
        _autoHideTimer?.Stop();
        await FadeVisual(UiControlsOverlay, 1, 0, 300, false);
        await Task.Delay(50);
        if (!_uiVisible && UiControlsOverlay != null)
            UiControlsOverlay.IsVisible = false;
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
        if (UiControlsOverlay == null) return;
        _uiVisible = visible;
        UiControlsOverlay.IsVisible = visible;
        UiControlsOverlay.Opacity = visible ? 1 : 0;
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
        if (player is MediaFoundationPlayer mfPlayer)
        {
            DebugLog("Calling InitializeRenderer");
            mfPlayer.InitializeRenderer(videoHwnd);
            DebugLog("InitializeRenderer returned");
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
            MaximizeRestoreIconPath.Data = Geometry.Parse("M 6 4H 18V 6H 20V 18H 18V 20H 6V 18H 4V 6H 6V 4 Z M 6 8V 18H 16V 8H 6 Z M 18 6V 16H 16V 6H 8V 4H 18V 6 Z");
            if (BtnMaximizeRestore != null) global::Avalonia.Controls.ToolTip.SetTip(BtnMaximizeRestore, "Restore");
        }
        else
        {
            MaximizeRestoreIconPath.Data = Geometry.Parse("M 4 4H 20V 20H 4V 4 Z M 6 6V 18H 18V 6H 6 Z");
            if (BtnMaximizeRestore != null) global::Avalonia.Controls.ToolTip.SetTip(BtnMaximizeRestore, "Maximize");
        }
    }

    // ========================
    //  Event handlers
    // ========================
    private void OnMediaOpened(object? sender, EventArgs e) 
    {
        _viewModel?.RefreshState();
        Dispatcher.UIThread.Post(() =>
        {
            if (StartPage != null) StartPage.IsVisible = false;
            if (ControlsBox != null) ControlsBox.IsVisible = true;
            if (VideoHost != null) VideoHost.IsVisible = true;
            if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = true;
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        });
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
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
                if (VideoHost != null) VideoHost.IsVisible = true;
                
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
                if (VideoHost != null) VideoHost.IsVisible = false;
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
    }

    private void RefreshSubtitleIcon()
    {
        if (SubtitleIconPath == null || _viewModel == null) return;
        SubtitleIconPath.Data = _viewModel.IsSubtitleEnabled
            ? Geometry.Parse("M 4 4H 20V 20H 4V 4 Z M 8 8H 12V 16H 8V 8 Z M 14 8H 18V 16H 14V 8 Z")
            : Geometry.Parse("M 4 4H 20V 20H 4V 4 Z M 7 7L17 17 M 17 7L7 17");
    }

    private void RefreshAudioIcon()
    {
        if (AudioIconPath == null || _viewModel == null) return;
        AudioIconPath.Data = _viewModel.IsAudioEnabled
            ? Geometry.Parse("M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 15 7V 17L 13 15Z")
            : Geometry.Parse("M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5 M 17 19L 13 15");
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
        if (VideoHost != null) VideoHost.IsVisible = false;
        RefreshFullscreenUi();
        RefreshSubtitleIcon();
        RefreshAudioIcon();
    }

    private IntPtr? PlatformImplHandle()
    {
        try
        {
#if WINDOWS
            if (PlatformImpl is not null)
            {
                var handleProp = PlatformImpl.GetType().GetProperty("Handle");
                if (handleProp is not null)
                {
                    var val = handleProp.GetValue(PlatformImpl);
                    if (val is IntPtr ptr) return ptr;
                }
            }
#endif
        }
        catch { }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        if (_playerService?.Player is MediaFoundationPlayer mfPlayer)
            mfPlayer.Dispose();
        _playerService?.Dispose();
        base.OnClosed(e);
    }
}
