using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

namespace Cine.Avalonia;

public partial class MainWindow : Window
{
    private PlayerService? _playerService;
    private MainViewModel? _viewModel;
    private D3D11VideoHost? _videoHost;
    #region debug-point main-window-log
    private static readonly string DebugLogFile = Path.Combine(
        AppContext.BaseDirectory,
        "cine_startup.log");

    private static void DebugLog(string message)
    {
        try
        {
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
    #endregion

    public MainWindow()
    {
        #region debug-point main-window-ctor
        DebugLog("ctor enter");
        #endregion
        InitializeComponent();
        #region debug-point main-window-ctor-exit
        DebugLog("ctor exit");
        #endregion
    }

    private void InitializeComponent()
    {
        #region debug-point main-window-init-start
        DebugLog("InitializeComponent start");
        #endregion
        AvaloniaXamlLoader.Load(this);
        #region debug-point main-window-xaml-loaded
        DebugLog("XAML loaded");
        #endregion
        #region debug-point main-window-find-videohost
        _videoHost = this.FindControl<D3D11VideoHost>("VideoHost");
        DebugLog($"VideoHost resolved null={_videoHost is null}");
        #endregion
        if (_videoHost == null)
            throw new InvalidOperationException("VideoHost control was not found in MainWindow.axaml.");

        // Initialize player service
        _playerService = new PlayerService();
        #region debug-point main-window-player-service-created
        DebugLog("PlayerService created");
        #endregion
        _playerService.Initialize();
        #region debug-point main-window-player-service-initialized
        DebugLog("PlayerService initialized");
        #endregion

        var player = _playerService.Player;
        if (player == null)
        {
            #region debug-point main-window-player-null
            DebugLog("PlayerService.Player is null");
            #endregion
            return;
        }

        // Create view model
        _viewModel = new MainViewModel(player);
        DataContext = _viewModel;
        #region debug-point main-window-datacontext
        DebugLog("MainViewModel created and DataContext assigned");
        #endregion

        // Wire events
        player.Opened += OnMediaOpened;
        player.PositionChanged += OnPositionChanged;
        player.ChapterListChanged += OnChapterListChanged;
        #region debug-point main-window-events-wired
        DebugLog("Player events wired");
        #endregion

        // Initialize renderer when the native child HWND is ready
        _videoHost.ChildWindowCreated += OnVideoHostChildCreated;
        #region debug-point main-window-videohost-wired
        DebugLog("VideoHost.ChildWindowCreated subscribed");
        #endregion

        // Keyboard shortcuts
        KeyDown += OnKeyDown;
        #region debug-point main-window-init-finish
        DebugLog("InitializeComponent finish");
        #endregion
    }

    // ========================
    //  Video rendering init
    // ========================

    /// <summary>Called once the D3D11VideoHost has created its child HWND.</summary>
    private void OnVideoHostChildCreated(object? sender, EventArgs e)
    {
        var videoHwnd = _videoHost?.VideoHwnd ?? IntPtr.Zero;
        #region debug-point main-window-child-created
        DebugLog($"OnVideoHostChildCreated hwnd={videoHwnd}");
        #endregion
        if (videoHwnd == IntPtr.Zero) return;

        var player = _playerService?.Player;
        if (player is MediaFoundationPlayer mfPlayer)
        {
            #region debug-point main-window-init-renderer
            DebugLog("Calling MediaFoundationPlayer.InitializeRenderer");
            #endregion
            mfPlayer.InitializeRenderer(videoHwnd);
            #region debug-point main-window-init-renderer-return
            DebugLog("MediaFoundationPlayer.InitializeRenderer returned");
            #endregion
        }
    }

    // ========================
    //  Button handlers
    // ========================
    private void OnPlayPause(object? sender, RoutedEventArgs e)
        => _viewModel?.PlayPause();

    private void OnStop(object? sender, RoutedEventArgs e)
        => _viewModel?.Stop();

    private void OnSeekBack(object? sender, RoutedEventArgs e)
        => _viewModel?.SeekBackward();

    private void OnSeekForward(object? sender, RoutedEventArgs e)
        => _viewModel?.SeekForward();

    private void OnToggleMute(object? sender, RoutedEventArgs e)
        => _viewModel?.ToggleMute();

    private void OnToggleFullscreen(object? sender, RoutedEventArgs e)
        => _viewModel?.ToggleFullscreen();

    private void OnScreenshot(object? sender, RoutedEventArgs e)
        => _viewModel?.Screenshot();

    private void OnNextChapter(object? sender, RoutedEventArgs e)
        => _viewModel?.NextChapter();

    private void OnPrevChapter(object? sender, RoutedEventArgs e)
        => _viewModel?.PreviousChapter();

    // ========================
    //  Event handlers
    // ========================
    private void OnMediaOpened(object? sender, EventArgs e)
    {
        _viewModel?.RefreshState();
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e) { }

    private void OnChapterListChanged(object? sender, ChapterListChangedEventArgs e)
    {
        _viewModel?.RefreshState();
    }

    // ========================
    //  Keyboard shortcuts
    // ========================
    private void OnKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                _viewModel?.PlayPause();
                e.Handled = true;
                break;
            case Key.F:
                _viewModel?.ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.M:
                _viewModel?.ToggleMute();
                e.Handled = true;
                break;
            case Key.Left:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _viewModel?.SeekLargeBackward();
                else
                    _viewModel?.SeekBackward();
                e.Handled = true;
                break;
            case Key.Right:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _viewModel?.SeekLargeForward();
                else
                    _viewModel?.SeekForward();
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel?.IncreaseVolume();
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel?.DecreaseVolume();
                e.Handled = true;
                break;
            case Key.OemOpenBrackets:
            case Key.OemMinus:
                // Speed down — TODO
                break;
            case Key.OemCloseBrackets:
            case Key.OemPlus:
                // Speed up — TODO
                break;
            case Key.Back:
                // Reset speed — TODO
                break;
            case Key.L:
                // Loop toggle — TODO
                break;
            case Key.S:
                _viewModel?.Screenshot();
                e.Handled = true;
                break;
            case Key.P:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _viewModel?.PreviousChapter();
                else
                    _viewModel?.NextChapter();
                e.Handled = true;
                break;
            case Key.PageDown:
                // Next playlist item — TODO
                break;
            case Key.PageUp:
                // Previous playlist item — TODO
                break;
            case Key.Escape:
                // Exit fullscreen or stop — TODO
                break;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        #region debug-point main-window-opened
        DebugLog("OnOpened enter");
        #endregion

        // Provide the parent HWND to the D3D11VideoHost so it can create
        // its child window. 'this' is the Window itself.
        var handle = this.PlatformImpl?.GetType().GetProperty("Handle")?.GetValue(this.PlatformImpl) as IntPtr?;
        #region debug-point main-window-platform-handle
        DebugLog($"Platform handle has value={handle.HasValue} value={handle.GetValueOrDefault()}");
        #endregion
        if (handle.HasValue && handle.Value != IntPtr.Zero && _videoHost != null)
        {
            _videoHost.ParentHwnd = handle.Value;
            #region debug-point main-window-parent-hwnd-set
            DebugLog("VideoHost.ParentHwnd assigned");
            #endregion
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        var player = _playerService?.Player;
        if (player is MediaFoundationPlayer mfPlayer)
            mfPlayer.Dispose();
        _playerService?.Dispose();
        base.OnClosed(e);
    }
}
