using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Key = Avalonia.Input.Key;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using Cine.Avalonia.Helpers;
using PointerEventArgs = Avalonia.Input.PointerEventArgs;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using PointerReleasedEventArgs = Avalonia.Input.PointerReleasedEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using Cine.Avalonia.Controls;
using Cine.Avalonia.ViewModels;
using Cine.Media.Events;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PipWindow : Window
{
    private readonly IMediaPlayer _pipPlayer;
    private readonly IMediaPlayer _mainPlayer;
    private readonly string _filePath;
    private readonly PipService _pipService;
    private D3D11VideoHost? _videoHost;
    private bool _initialized;
    private CancellationTokenSource? _initCts;

    // ───── Aspect ratio lock ─────
    private double _videoAspectRatio;   // width/height of video
    private bool _aspectLocked = true;
    private bool _isResizingInternally; // prevent re-entrant resize
    private double _lastWidth;
    private double _lastHeight;

    // ───── Event-driven sync (no polling timer needed) ─────
    private bool _isSyncing;
    private const double SyncThresholdSeconds = 0.5;
    private const int SeekDebounceMs = 100; // min gap between seeks
    private DateTime _lastSeekTime = DateTime.MinValue;

    // Auto-hide control timer
    private DispatcherTimer? _autoHideTimer;
    private bool _controlsVisible;
    private bool _isMouseOverControls;
    private const double AutoHideDelaySeconds = 2.5;

    // Seek interaction
    private bool _isSeeking;
    private double _seekNormalized;

    // Volume interaction
    private bool _isVolumeDragging;
    private int _pipVolume;
    private const int VolumeMaxDefault = 100;

    // Double-tap to exit (VLC pattern)
    private DateTime _lastTapTime = DateTime.MinValue;
    private const double DoubleTapThresholdMs = 350;

    // Big play button visibility
    private DispatcherTimer? _bigButtonTimer;
    private bool _bigButtonVisible;

    // Always-on-top
    private bool _pinned = true;

    // Animation
    private bool _isClosing;

    // ───── Auto-hide: only show controls when user is active ─────
    private bool _mouseWasActive;
    private DispatcherTimer? _mouseActivityTimer;
    private const double MouseActivityShowDelayMs = 300;

    // ───── State persistence path ─────
    private static readonly string PipStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "pip_state.json");

    public PipWindow()
    {
        _pipPlayer = null!;
        _mainPlayer = null!;
        _filePath = string.Empty;
        _pipService = null!;
        InitializeComponent();
    }

    public PipWindow(IMediaPlayer pipPlayer, IMediaPlayer mainPlayer, string filePath, PipService pipService)
        : this()
    {
        _pipPlayer = pipPlayer;
        _mainPlayer = mainPlayer;
        _filePath = filePath;
        _pipService = pipService;
    }

    // =========================================================================
    // OPENED
    // =========================================================================

    private async void OnOpened(object? sender, EventArgs e)
    {
        _videoHost = this.FindControl<D3D11VideoHost>("PipVideoHost");
        if (_videoHost == null) return;

        _videoHost.ChildWindowCreated += OnPipVideoHostReady;

        // Set the PipWindow's own native HWND as the video host's parent,
        // which triggers child HWND creation + ChildWindowCreated event.
        var pipHwnd = GetPlatformHwnd();
        if (pipHwnd != IntPtr.Zero)
        {
            _videoHost.ParentHwnd = pipHwnd;
        }

        // Restore saved state (position, size, pin)
        RestoreState();

        // Show file name in header
        if (PipFileNameLabel != null)
            PipFileNameLabel.Text = Path.GetFileName(_filePath);

        // Animate enter: show at main window center position, scale down
        await AnimateEnter();

        // Start auto-hide + mouse activity tracking
        StartMouseActivityTimer();
    }

    private IntPtr GetPlatformHwnd()
    {
        try
        {
            var handle = TryGetPlatformHandle();
            if (handle is { Handle: not 0 })
                return handle.Handle;
        }
        catch { }
        return IntPtr.Zero;
    }

    // =========================================================================
    // STATE PERSISTENCE
    // =========================================================================

    private void RestoreState()
    {
        try
        {
            if (!File.Exists(PipStatePath)) return;
            var json = File.ReadAllText(PipStatePath);
            var state = JsonSerializer.Deserialize<PipState>(json);
            if (state == null) return;

            if (state.X >= 0 && state.Y >= 0)
                Position = new PixelPoint(state.X, state.Y);
            if (state.Width > 0) Width = state.Width;
            if (state.Height > 0) Height = state.Height;
            _pinned = state.Pinned;
            Topmost = _pinned;
            if (PipPinIcon != null)
                PipPinIcon.Kind = _pinned
                    ? Material.Icons.MaterialIconKind.Pin
                    : Material.Icons.MaterialIconKind.PinOff;
        }
        catch { /* state restore is best-effort */ }
    }

    private void SaveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(PipStatePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var state = new PipState
            {
                X = Position.X,
                Y = Position.Y,
                Width = (int)Width,
                Height = (int)Height,
                Pinned = _pinned
            };
            File.WriteAllText(PipStatePath, JsonSerializer.Serialize(state));
        }
        catch { /* state save is best-effort */ }
    }

    private class PipState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Pinned { get; set; }
    }

    // =========================================================================
    // SMOOTH ENTER ANIMATION (scale-down from main window)
    // =========================================================================

    private async Task AnimateEnter()
    {
        // Start from center of main window (estimated)
        var startW = Width * 1.8;
        var startH = Height * 1.8;

        var screenW = Screens.Primary?.Bounds.Width ?? 1920;
        var screenH = Screens.Primary?.Bounds.Height ?? 1080;
        var startX = (int)((screenW - startW) / 2);
        var startY = (int)((screenH - startH) / 2);

        // Set initial large size
        Width = startW;
        Height = startH;
        Position = new PixelPoint(startX, startY);
        Opacity = 0.7;

        var targetW = 400.0;
        var targetH = 300.0;
        var targetX = Position.X;
        var targetY = Position.Y;

        // If we have a saved position, use it as target
        try
        {
            if (File.Exists(PipStatePath))
            {
                var json = File.ReadAllText(PipStatePath);
                var state = JsonSerializer.Deserialize<PipState>(json);
                if (state != null)
                {
                    if (state.X >= 0 && state.Y >= 0)
                    {
                        targetX = state.X;
                        targetY = state.Y;
                    }
                    if (state.Width > 0) targetW = state.Width;
                    if (state.Height > 0) targetH = state.Height;
                }
            }
        }
        catch { }

        var steps = 12;
        for (int i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var ease = 1 - Math.Pow(1 - t, 3); // cubic ease-out
            Width = startW - (startW - targetW) * ease;
            Height = startH - (startH - targetH) * ease;
            Opacity = 0.7 + 0.3 * ease;
            var cx = startX + (startW - Width) / 2;
            var cy = startY + (startH - Height) / 2;
            Position = new PixelPoint((int)(targetX - (targetX - cx) * (1 - ease)), (int)(targetY - (targetY - cy) * (1 - ease)));
            await Task.Delay(16); // ~60fps
        }

        // Final snap to target
        Width = targetW;
        Height = targetH;
        Position = new PixelPoint((int)targetX, (int)targetY);
        Opacity = 1.0;
    }

    // =========================================================================
    // MOUSE ACTIVITY - SHOW CONTROLS ON ANY MOUSE MOVE
    // =========================================================================

    private void StartMouseActivityTimer()
    {
        _mouseActivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(MouseActivityShowDelayMs)
        };
        _mouseActivityTimer.Tick += OnMouseActivityTimerTick;
        _mouseActivityTimer.Start();
    }

    private void OnMouseActivityTimerTick(object? sender, EventArgs e)
    {
        _mouseActivityTimer?.Stop();
        if (_mouseWasActive && !_controlsVisible && !_isMouseOverControls)
        {
            ShowControlsAndBigButton();
        }
        _mouseWasActive = false;
    }

    private void OnPipPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_controlsVisible && !_isMouseOverControls)
        {
            _mouseWasActive = true;
            _mouseActivityTimer?.Stop();
            _mouseActivityTimer?.Start();
        }

        // Restart auto-hide timer on any mouse activity
        _autoHideTimer?.Stop();
        _autoHideTimer?.Start();
    }

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    private async void OnPipVideoHostReady(object? sender, EventArgs e)
    {
        if (_initialized || _videoHost == null) return;
        _initialized = true;

        var hwnd = _videoHost.VideoHwnd;
        if (hwnd == IntPtr.Zero) return;

        _initCts = new CancellationTokenSource();
        var ct = _initCts.Token;

        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                _pipPlayer.InitializeRenderer(hwnd);
            }, ct);

            ct.ThrowIfCancellationRequested();
            _pipPlayer.Mute(true);

            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                _pipPlayer.Open(_filePath);
            }, ct);

            ct.ThrowIfCancellationRequested();

            var mainPos = _mainPlayer.Position;
            if (mainPos.TotalSeconds > 0)
                _pipPlayer.Seek(mainPos);

            // Inherit properties
            _pipPlayer.Speed = _mainPlayer.Speed;
            _pipPlayer.SubtitleDelay = _mainPlayer.SubtitleDelay;
            _pipPlayer.AudioDelay = _mainPlayer.AudioDelay;

            // Capture video aspect ratio for aspect-ratio-locked resize
            _videoAspectRatio = _mainPlayer.AspectRatio > 0
                ? _mainPlayer.AspectRatio
                : 16.0 / 9.0; // fallback to 16:9

            _pipVolume = (int)(_mainPlayer.Volume / _mainPlayer.VolumeMax * VolumeMaxDefault);

            Dispatcher.UIThread.OnUiThread(() =>
            {
                if (!ct.IsCancellationRequested && _videoHost != null)
                {
                    _videoHost.IsVideoSurfaceVisible = true;
                    UpdatePlayPauseIcon();
                    UpdateBigPlayPauseIcon();
                    UpdateVolumeDisplay();
                }
            });

            _mainPlayer.PositionChanged += OnMainPositionChanged;
            _pipPlayer.PositionChanged += OnPipPositionChanged;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                if (ct.IsCancellationRequested) return;

                // P2.1: Notify PipService so it cleans up state properly
                _pipService?.NotifyInitFailed();

                if (PipTitleLabel != null) PipTitleLabel.Text = "PIP Error";
                if (PipTimeLabel != null) PipTimeLabel.Text = ex.Message.Length > 25 ? ex.Message[..25] + ".." : ex.Message;
                await Task.Delay(2000);
                await CloseWithAnimation();
            });
        }
    }

    // =========================================================================
    // EVENT-DRIVEN SYNC (pure PositionChanged — no polling timer)
    // =========================================================================
    //
    // Both mpv instances fire PositionChanged ~every 100ms from their event loops.
    // We subscribe to both:
    //   - Main player events → sync PIP position if gap exceeds threshold
    //   - PIP player events → update seek bar + time label
    // No DispatcherTimer needed.

    private void OnMainPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (_isSeeking) return;

        try
        {
            var mainPos = e.Position;
            var pipPos = _pipPlayer.Position;
            var now = DateTime.UtcNow;

            if (Math.Abs((mainPos - pipPos).TotalSeconds) > SyncThresholdSeconds
                && (now - _lastSeekTime).TotalMilliseconds >= SeekDebounceMs)
            {
                _pipPlayer.Seek(mainPos);
                _lastSeekTime = now;
            }
        }
        catch { /* sync is best-effort */ }
    }

    private void OnPipPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (_isSeeking) return;

        try
        {
            var dur = e.Duration;
            var pos = e.Position;

            if (dur.TotalSeconds > 0)
            {
                var width = PipSeekTrack?.Bounds.Width ?? 0;
                if (width > 0)
                {
                    var pct = Math.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0.0, 1.0);
                    if (PipSeekFill != null)
                        PipSeekFill.Width = pct * width;
                    if (PipSeekThumb != null)
                        PipSeekThumb.Margin = new Thickness(pct * width - 4, 0, 0, 0);
                }
            }

            if (PipTimeLabel != null)
                PipTimeLabel.Text = $"{(int)pos.TotalMinutes:D2}:{pos.Seconds:D2} / {(int)dur.TotalMinutes:D2}:{dur.Seconds:D2}";
        }
        catch { /* UI update is best-effort */ }
    }

    public void SyncFromMain()
    {
        try
        {
            var mainPos = _mainPlayer.Position;
            var pipPos = _pipPlayer.Position;
            if (Math.Abs((mainPos - pipPos).TotalSeconds) > 0.3)
                _pipPlayer.Seek(mainPos);
        }
        catch { }
    }

    // =========================================================================
    // DOUBLE-TAP TO EXIT (VLC pattern)
    // =========================================================================

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var now = DateTime.UtcNow;
        if ((now - _lastTapTime).TotalMilliseconds < DoubleTapThresholdMs)
        {
            // Double-tap detected → exit PIP (VLC: onDoubleTapped: playerView = true)
            _ = CloseWithAnimation();
            e.Handled = true;
        }
        _lastTapTime = now;
    }

    // =========================================================================
    // DRAG-TO-MOVE
    // =========================================================================

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e) { }
    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e) { }

    // =========================================================================
    // AUTO-HIDE CONTROLS (VLC-style hover-based visibility)
    // =========================================================================

    private void ShowControlsAndBigButton()
    {
        if (_controlsVisible) return;
        _controlsVisible = true;

        PipHeader.IsVisible = true;
        PipHeader.Opacity = 0;
        _ = AnimateOpacity(PipHeader, 0, 1);

        PipControlsBar.IsVisible = true;
        PipControlsBar.Opacity = 0;
        _ = AnimateOpacity(PipControlsBar, 0, 1);

        PipBigPlayPause.IsHitTestVisible = true;
        _bigButtonVisible = true;
        _ = AnimateOpacity(PipBigPlayPause, 0, 0.9);

        // Auto-hide timer
        _autoHideTimer?.Stop();
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoHideDelaySeconds) };
        _autoHideTimer.Tick += (s, args) =>
        {
            _autoHideTimer?.Stop();
            if (!_isMouseOverControls)
                HideControlsAndBigButton();
        };
        _autoHideTimer.Start();
    }

    private async void HideControlsAndBigButton()
    {
        if (!_controlsVisible) return;
        _controlsVisible = false;

        PipBigPlayPause.IsHitTestVisible = false;
        _bigButtonVisible = false;

        await Task.WhenAll(
            AnimateOpacity(PipHeader, PipHeader?.Opacity ?? 1, 0),
            AnimateOpacity(PipControlsBar, PipControlsBar?.Opacity ?? 1, 0),
            AnimateOpacity(PipBigPlayPause, PipBigPlayPause?.Opacity ?? 0.9, 0));

        PipHeader.IsVisible = false;
        PipControlsBar.IsVisible = false;
    }

    private async Task AnimateOpacity(Visual? target, double from, double to)
    {
        if (target == null) return;
        var steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            target.Opacity = from + (to - from) * ((double)i / steps);
            await Task.Delay(18);
        }
        target.Opacity = to;
    }

    private void OnControlsPointerEntered(object? sender, PointerEventArgs e)
    {
        _isMouseOverControls = true;
        _autoHideTimer?.Stop();
    }

    private void OnControlsPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseOverControls = false;
        _autoHideTimer?.Stop();
        _autoHideTimer?.Start();
    }

    // =========================================================================
    // BIG PLAY BUTTON HOVER FADE
    // =========================================================================

    private void OnBigPlayButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        // Big button is already visible via ShowControlsAndBigButton
    }

    private void OnBigPlayButtonPointerExited(object? sender, PointerEventArgs e)
    {
        // Will auto-hide via timer
    }

    // =========================================================================
    // PLAYBACK CONTROLS
    // =========================================================================

    private void OnPipPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_pipPlayer.IsPlaying)
            _pipPlayer.Pause();
        else
            _pipPlayer.Play();

        UpdatePlayPauseIcon();
        UpdateBigPlayPauseIcon();
    }

    private void OnPipPrevious(object? sender, RoutedEventArgs e)
    {
        try { _mainPlayer.SeekBackward(30); SyncFromMain(); } catch { }
    }

    private void OnPipNext(object? sender, RoutedEventArgs e)
    {
        try { _mainPlayer.SeekForward(30); SyncFromMain(); } catch { }
    }

    private void UpdatePlayPauseIcon()
    {
        if (PipPlayPauseIcon == null) return;
        PipPlayPauseIcon.Kind = _pipPlayer.IsPlaying
            ? Material.Icons.MaterialIconKind.Pause
            : Material.Icons.MaterialIconKind.Play;
    }

    private void UpdateBigPlayPauseIcon()
    {
        if (PipBigPlayPauseIcon == null) return;
        PipBigPlayPauseIcon.Kind = _pipPlayer.IsPlaying
            ? Material.Icons.MaterialIconKind.Pause
            : Material.Icons.MaterialIconKind.Play;
    }

    // =========================================================================
    // VOLUME CONTROL (slider + mute toggle + mouse wheel)
    // =========================================================================

    private void OnPipMuteClick(object? sender, RoutedEventArgs e)
    {
        _mainPlayer.ToggleMute();
        UpdateVolumeDisplay();
    }

    private void OnVolumePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isVolumeDragging = true;
        UpdateVolumeFromPointer(e);
    }

    private void OnVolumePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isVolumeDragging) UpdateVolumeFromPointer(e);
    }

    private void OnVolumePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isVolumeDragging) return;
        _isVolumeDragging = false;
        UpdateVolumeFromPointer(e);
    }

    private void UpdateVolumeFromPointer(PointerEventArgs e)
    {
        var track = PipVolumeTrack;
        if (track == null) return;

        var pos = e.GetPosition(track);
        var width = track.Bounds.Width;
        if (width <= 0) return;

        var normalized = Math.Clamp(pos.X / width, 0.0, 1.0);
        _pipVolume = (int)(normalized * VolumeMaxDefault);
        var actualVolume = normalized * _mainPlayer.VolumeMax;

        _mainPlayer.Volume = actualVolume;
        if (actualVolume > 0 && _mainPlayer.IsMuted)
            _mainPlayer.Mute(false);

        UpdateVolumeDisplay();
    }

    private void OnPipPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = e.Delta.Y > 0 ? 5 : -5;
        _pipVolume = Math.Clamp(_pipVolume + delta, 0, VolumeMaxDefault);

        var normalized = (double)_pipVolume / VolumeMaxDefault;
        _mainPlayer.Volume = normalized * _mainPlayer.VolumeMax;

        if (_pipVolume > 0 && _mainPlayer.IsMuted)
            _mainPlayer.Mute(false);
        if (_pipVolume == 0 && !_mainPlayer.IsMuted)
            _mainPlayer.Mute(true);

        UpdateVolumeDisplay();
        e.Handled = true;
    }

    private void UpdateVolumeDisplay()
    {
        if (PipVolumeFill == null || PipVolumeTrack == null) return;

        _pipVolume = _mainPlayer.IsMuted ? 0 : (int)(_mainPlayer.Volume / (_mainPlayer.VolumeMax > 0 ? _mainPlayer.VolumeMax : 1) * VolumeMaxDefault);
        var fillWidth = _pipVolume * PipVolumeTrack.Bounds.Width / VolumeMaxDefault;
        PipVolumeFill.Width = fillWidth;

        if (PipVolumeIcon != null)
        {
            PipVolumeIcon.Kind = _mainPlayer.IsMuted
                ? Material.Icons.MaterialIconKind.VolumeOff
                : _pipVolume > 50
                    ? Material.Icons.MaterialIconKind.VolumeHigh
                    : _pipVolume > 0
                        ? Material.Icons.MaterialIconKind.VolumeLow
                        : Material.Icons.MaterialIconKind.VolumeOff;
        }
    }

    // =========================================================================
    // SEEK BAR
    // =========================================================================

    private void OnSeekPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isSeeking = true;
        UpdateSeekFromPointer(e);
    }

    private void OnSeekPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isSeeking) UpdateSeekFromPointer(e);
    }

    private void OnSeekPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeeking) return;
        _isSeeking = false;

        var dur = _pipPlayer.Duration;
        if (dur.TotalSeconds > 0)
        {
            var seekPos = TimeSpan.FromSeconds(_seekNormalized * dur.TotalSeconds);
            _pipPlayer.Seek(seekPos);
        }
    }

    private void UpdateSeekFromPointer(PointerEventArgs e)
    {
        var track = PipSeekTrack;
        if (track == null) return;

        var pos = e.GetPosition(track);
        var width = track.Bounds.Width;
        if (width <= 0) return;

        _seekNormalized = Math.Clamp(pos.X / width, 0.0, 1.0);

        if (PipSeekFill != null)
            PipSeekFill.Width = _seekNormalized * width;
        if (PipSeekThumb != null)
            PipSeekThumb.Margin = new Thickness(_seekNormalized * width - 4, 0, 0, 0);

        var dur = _pipPlayer.Duration;
        if (dur.TotalSeconds > 0 && PipTimeLabel != null)
        {
            var seekTime = TimeSpan.FromSeconds(_seekNormalized * dur.TotalSeconds);
            PipTimeLabel.Text = $"{(int)seekTime.TotalMinutes:D2}:{seekTime.Seconds:D2} / {(int)dur.TotalMinutes:D2}:{dur.Seconds:D2}";
        }
    }

    // =========================================================================
    // PIN / ALWAYS-ON-TOP TOGGLE
    // =========================================================================

    private void OnPinToggleClick(object? sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        Topmost = _pinned;
        if (PipPinIcon != null)
            PipPinIcon.Kind = _pinned
                ? Material.Icons.MaterialIconKind.Pin
                : Material.Icons.MaterialIconKind.PinOff;
    }

    // =========================================================================
    // ASPECT RATIO LOCK DURING RESIZE
    // =========================================================================

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);

        if (!_aspectLocked || _isResizingInternally || _videoAspectRatio <= 0)
            return;

        _isResizingInternally = true;

        try
        {
            var newW = Bounds.Width;
            var newH = Bounds.Height;

            // Determine which dimension changed and constrain the other
            var expectedH = newW / _videoAspectRatio;
            var expectedW = newH * _videoAspectRatio;

            if (Math.Abs(newH - expectedH) > Math.Abs(newW - expectedW))
            {
                // Height changed more — lock height
                Height = expectedH;
            }
            else
            {
                // Width changed more — lock width
                Width = expectedW;
            }
        }
        catch { }
        finally
        {
            _isResizingInternally = false;
        }
    }

    private void OnAspectLockToggle(object? sender, RoutedEventArgs e)
    {
        _aspectLocked = !_aspectLocked;
        if (PipAspectLockIcon != null)
        {
            // Dim the icon when unlocked to indicate inactive state
            PipAspectLockIcon.Opacity = _aspectLocked ? 1.0 : 0.5;
            PipAspectLockIcon.Kind = Material.Icons.MaterialIconKind.AspectRatio;
        }
    }

    // =========================================================================
    // KEYBOARD SHORTCUTS (Gold Standard)
    // =========================================================================

    /// <summary>
    /// Called by MainWindow when PIP is active to forward a key press.
    /// P2.5: Ensures keyboard shortcuts work on PIP when main window has focus.
    /// </summary>
    public void SimulateKeyPress(Key key)
    {
        switch (key)
        {
            case Key.Space:
                OnPipPlayPause(null, new RoutedEventArgs());
                break;
            case Key.Escape:
                _ = CloseWithAnimation();
                break;
            case Key.Left:
                try { _mainPlayer.SeekBackward(5); SyncFromMain(); } catch { }
                break;
            case Key.Right:
                try { _mainPlayer.SeekForward(5); SyncFromMain(); } catch { }
                break;
            case Key.Up:
                _pipVolume = Math.Clamp(_pipVolume + 10, 0, VolumeMaxDefault);
                _mainPlayer.Volume = (double)_pipVolume / VolumeMaxDefault * _mainPlayer.VolumeMax;
                UpdateVolumeDisplay();
                break;
            case Key.Down:
                _pipVolume = Math.Clamp(_pipVolume - 10, 0, VolumeMaxDefault);
                _mainPlayer.Volume = (double)_pipVolume / VolumeMaxDefault * _mainPlayer.VolumeMax;
                UpdateVolumeDisplay();
                break;
            case Key.M:
                OnPipMuteClick(null, new RoutedEventArgs());
                break;
            case Key.F:
                _ = CloseWithAnimation();
                break;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                OnPipPlayPause(sender, e);
                e.Handled = true;
                break;
            case Key.Escape:
                _ = CloseWithAnimation();
                e.Handled = true;
                break;
            case Key.Left:
                try { _mainPlayer.SeekBackward(5); SyncFromMain(); } catch { }
                e.Handled = true;
                break;
            case Key.Right:
                try { _mainPlayer.SeekForward(5); SyncFromMain(); } catch { }
                e.Handled = true;
                break;
            case Key.Up:
                _pipVolume = Math.Clamp(_pipVolume + 10, 0, VolumeMaxDefault);
                _mainPlayer.Volume = (double)_pipVolume / VolumeMaxDefault * _mainPlayer.VolumeMax;
                UpdateVolumeDisplay();
                e.Handled = true;
                break;
            case Key.Down:
                _pipVolume = Math.Clamp(_pipVolume - 10, 0, VolumeMaxDefault);
                _mainPlayer.Volume = (double)_pipVolume / VolumeMaxDefault * _mainPlayer.VolumeMax;
                UpdateVolumeDisplay();
                e.Handled = true;
                break;
            case Key.M:
                OnPipMuteClick(sender, e);
                e.Handled = true;
                break;
            case Key.F:
                _ = CloseWithAnimation();
                e.Handled = true;
                break;
            case Key.A:
                OnAspectLockToggle(sender, e);
                e.Handled = true;
                break;
        }
    }

    // =========================================================================
    // CLOSE ANIMATION
    // =========================================================================

    private async void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimation();
    }

    /// <summary>
    /// Back to Main Window — exits PIP and restores main window focus.
    /// Like VLC's fullscreen button in PIP, YouTube's "return to player".
    /// </summary>
    private async void OnBackToMainClick(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimation();
    }

    private async Task CloseWithAnimation()
    {
        if (_isClosing) return;
        _isClosing = true;

        // Save state before closing
        SaveState();

        var startW = Width;
        var startH = Height;
        var startX = Position.X;
        var startY = Position.Y;
        var centerX = startX + startW / 2;
        var centerY = startY + startH / 2;
        var targetW = startW * 0.3;
        var targetH = startH * 0.3;
        var steps = 10;

        for (int i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var ease = 1 - Math.Pow(1 - t, 3);
            Width = startW - (startW - targetW) * ease;
            Height = startH - (startH - targetH) * ease;
            Opacity = 1 - ease;
            var px = centerX - Width / 2;
            var py = centerY - Height / 2;
            Position = new PixelPoint((int)px, (int)py);
            await Task.Delay(20);
        }

        Close();
    }

    // =========================================================================
    // CLEANUP
    // =========================================================================

    protected override void OnClosed(EventArgs e)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;

        _mouseActivityTimer?.Stop();
        _mouseActivityTimer = null;

        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;

        _mainPlayer.PositionChanged -= OnMainPositionChanged;
        _pipPlayer.PositionChanged -= OnPipPositionChanged;

        try
        {
            _pipPlayer.Stop();
            (_pipPlayer as IDisposable)?.Dispose();
        }
        catch { }

        base.OnClosed(e);
    }
}
