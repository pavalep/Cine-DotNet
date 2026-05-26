using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Platform.Storage;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
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

    // Responsive breakpoints
    private const double NarrowBreakpoint = 600;
    private const double MediumBreakpoint = 1024;

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

        _videoHost.ChildWindowCreated += OnVideoHostChildCreated;
        KeyDown += OnKeyDown;

        // Start page - show on initial launch, hide when media opens
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        InitializeAutoHide();
        InitializeResponsiveLayout();

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
        }
        else
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.None;
        }
    }

    private void OnWindowDragLeave(object? sender, RoutedEventArgs e)
    {
        ResetStartPageDragVisuals();
    }

    private void OnWindowDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        ResetStartPageDragVisuals();

        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToArray();
                var videoFiles = StartPage.FilterVideoFiles(paths);
                
                if (videoFiles.Any() && _viewModel != null)
                {
                    _viewModel.OpenFiles(videoFiles);
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
    /// Breakpoints: Narrow (&lt;600px), Medium (600-1024px), Wide (&gt;1024px)
    /// Matches Python GTK4 responsive breakpoints.
    /// </summary>
    private void UpdateResponsiveLayout(double width)
    {
        if (!this.IsInitialized) return;

        if (MainGrid != null)
        {
            MainGrid.ColumnDefinitions.Clear();
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            if (width < NarrowBreakpoint)
            {
                if (HeaderGrid != null)
                {
                    HeaderGrid.ColumnDefinitions.Clear();
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                }
                SetButtonSize(BtnOpenMenu, 36);
                SetButtonSize(BtnPrimaryMenu, 36);
                SetButtonSize(BtnPlayPause, 36);
                SetButtonSize(BtnStop, 36);
                SetButtonSize(BtnPrevious, 36);
                SetButtonSize(BtnNext, 36);
                SetButtonSize(BtnRewind, 36);
                SetButtonSize(BtnForward, 36);
                SetButtonSize(BtnVolumeMenu, 36);
                SetButtonSize(BtnFullscreen, 36);
                SetButtonSize(BtnLoopFile, 36);
                SetButtonSize(BtnLoopPlaylist, 36);
                SetVis(BtnPip, false);
                SetVis(BtnSubtitlesMenu, false);
                SetVis(BtnAudioMenu, false);
                SetVis(BtnVideoMenu, false);
                SetVis(BtnScreenshot, false);
                SetFont(PositionTimeLabel, 11);
                SetFont(DurationTimeLabel, 11);
            }
            else if (width < MediumBreakpoint)
            {
                if (HeaderGrid != null)
                {
                    HeaderGrid.ColumnDefinitions.Clear();
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                }
                SetButtonSize(BtnOpenMenu, 38);
                SetButtonSize(BtnPrimaryMenu, 38);
                SetButtonSize(BtnPlayPause, 38);
                SetButtonSize(BtnStop, 38);
                SetButtonSize(BtnPrevious, 38);
                SetButtonSize(BtnNext, 38);
                SetButtonSize(BtnRewind, 38);
                SetButtonSize(BtnForward, 38);
                SetButtonSize(BtnVolumeMenu, 38);
                SetButtonSize(BtnFullscreen, 38);
                SetButtonSize(BtnLoopFile, 38);
                SetButtonSize(BtnLoopPlaylist, 38);
                SetVis(BtnPip, false);
                SetVis(BtnSubtitlesMenu, true);
                SetVis(BtnAudioMenu, true);
                SetVis(BtnVideoMenu, false);
                SetVis(BtnScreenshot, false);
                SetFont(PositionTimeLabel, 12);
                SetFont(DurationTimeLabel, 12);
            }
            else
            {
                if (HeaderGrid != null)
                {
                    HeaderGrid.ColumnDefinitions.Clear();
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                }
                SetButtonSize(BtnOpenMenu, 40);
                SetButtonSize(BtnPrimaryMenu, 40);
                SetButtonSize(BtnPlayPause, 40);
                SetButtonSize(BtnStop, 40);
                SetButtonSize(BtnPrevious, 40);
                SetButtonSize(BtnNext, 40);
                SetButtonSize(BtnRewind, 40);
                SetButtonSize(BtnForward, 40);
                SetButtonSize(BtnVolumeMenu, 40);
                SetButtonSize(BtnFullscreen, 40);
                SetButtonSize(BtnLoopFile, 40);
                SetButtonSize(BtnLoopPlaylist, 40);
                SetVis(BtnPip, true);
                SetVis(BtnSubtitlesMenu, true);
                SetVis(BtnAudioMenu, true);
                SetVis(BtnVideoMenu, true);
                SetVis(BtnScreenshot, true);
                SetFont(PositionTimeLabel, 13);
                SetFont(DurationTimeLabel, 13);
            }
        }

        if (ControlsBox != null)
            ControlsBox.Height = width < NarrowBreakpoint ? 90 :
                                 width < MediumBreakpoint ? 100 : 120;
        if (HeaderBar != null)
            HeaderBar.Height = width < NarrowBreakpoint ? 40 :
                               width < MediumBreakpoint ? 46 : 50;
        if (TitleText != null)
            TitleText.FontSize = width < NarrowBreakpoint ? 12 :
                                 width < MediumBreakpoint ? 13 : 14;
    }

    /// <summary>Sets button size and corner radius directly for responsive layout.</summary>
    private void SetButtonSize(AvaloniaControl? control, double size)
    {
        if (control == null) return;
        control.Width = size;
        control.Height = size;
        if (control is global::Avalonia.Controls.Button btn)
            btn.CornerRadius = new global::Avalonia.CornerRadius(size / 2);
        else if (control is ToggleButton tbtn)
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
        if (!_isMouseOverControls && hasMedia)
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
    private void OnSeekBack(object? sender, RoutedEventArgs e) => _viewModel?.SeekBackward();
    private void OnSeekForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekForward();
    private void OnToggleMute(object? sender, RoutedEventArgs e) => _viewModel?.ToggleMute();
    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();
    private void OnScreenshot(object? sender, RoutedEventArgs e) => _viewModel?.Screenshot();
    private void OnNextChapter(object? sender, RoutedEventArgs e) => _viewModel?.NextChapter();
    private void OnPrevChapter(object? sender, RoutedEventArgs e) => _viewModel?.PreviousChapter();
    private void OnPrevious(object? sender, RoutedEventArgs e) => _viewModel?.PreviousChapter();
    private void OnRewind(object? sender, RoutedEventArgs e) => _viewModel?.SeekLargeBackward();
    private void OnForward(object? sender, RoutedEventArgs e) => _viewModel?.SeekLargeForward();
    private void OnNext(object? sender, RoutedEventArgs e) => _viewModel?.NextChapter();
    private void OnToggleLoopFile(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopFile();
    private void OnToggleLoopPlaylist(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopPlaylist();

    // ========================
    //  Event handlers
    // ========================
    private void OnMediaOpened(object? sender, EventArgs e) => _viewModel?.RefreshState();
    private void OnPositionChanged(object? sender, PositionChangedEventArgs e) { }
    private void OnChapterListChanged(object? sender, ChapterListChangedEventArgs e) => _viewModel?.RefreshState();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.FilePath))
        {
            if (!string.IsNullOrEmpty(_viewModel?.FilePath))
            {
                // Media loaded: hide StartPage, show controls and Open button (matching Python)
                if (StartPage?.IsVisible == true) StartPage?.Hide();
                if (ControlsBox != null) ControlsBox.IsVisible = true;
                if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = true;
                
                // Restart auto-hide timer now that media is playing
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            }
            else
            {
                // No media: show StartPage, hide controls and Open button (matching Python)
                if (StartPage?.IsVisible == false) StartPage?.Show();
                if (ControlsBox != null) ControlsBox.IsVisible = false;
                if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = false;
                
                // Ensure UI stays visible when idle
                ShowUiControls();
            }
        }
    }

    // ========================
    //  Keyboard shortcuts
    // ========================
    private void OnKeyDown(object? sender, AvaloniaKeyEventArgs e)
    {
        var key = e.Key;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        void Handle(Action action) { action(); e.Handled = true; }
        if (key == Key.Space) Handle(() => _viewModel?.PlayPause());
        else if (key == Key.F) Handle(() => _viewModel?.ToggleFullscreen());
        else if (key == Key.M) Handle(() => _viewModel?.ToggleMute());
        else if (key == Key.Left) Handle(() => { if (shift) _viewModel?.SeekLargeBackward(); else _viewModel?.SeekBackward(); });
        else if (key == Key.Right) Handle(() => { if (shift) _viewModel?.SeekLargeForward(); else _viewModel?.SeekForward(); });
        else if (key == Key.Up) Handle(() => _viewModel?.IncreaseVolume());
        else if (key == Key.Down) Handle(() => _viewModel?.DecreaseVolume());
        else if (key == Key.S) Handle(() => _viewModel?.Screenshot());
        else if (key == Key.P) Handle(() => { if (shift) _viewModel?.PreviousChapter(); else _viewModel?.NextChapter(); });
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

        // Initial state: StartPage visible, controls hidden (matching Python reference)
        StartPage?.Show();
        if (ControlsBox != null) ControlsBox.IsVisible = false;
        if (BtnOpenMenu != null) BtnOpenMenu.IsVisible = false;
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