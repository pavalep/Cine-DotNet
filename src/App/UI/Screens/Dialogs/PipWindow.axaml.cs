using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Cine.Core;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Serialization;
using Cine.Avalonia.Services;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PipWindow : Window, IPipWindow
{
    private bool _isPinned;
    private bool _isClosing;
    private bool _isPlaying = true;
    private bool _isEnded;
    private bool _isMuted;
    private bool _controlsVisible = true;
    private bool _isSeeking;
    private double _seekNormalized;
    private double _aspectRatio = 16.0 / 9.0;
    private bool _isApplyingAspectRatio;
    private WriteableBitmap? _pipFrameBitmap;

    // Auto-hide
    private DispatcherTimer? _hoverTimer;
    private bool _hoverTopBar;
    private bool _hoverCenter;
    private bool _hoverBottomBar;

    public bool IsClosed { get; private set; }

    // ────── Player control events ──────
    public event EventHandler? PlayPauseRequested;
    public event EventHandler<double>? SeekRequested;
    public event EventHandler? MuteToggled;

    // ────── State persistence ──────
    private static readonly string PipStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "pip_state.json");

    public PipWindow()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        InitializeComponent();
        KeyDown += OnKeyDown;
        SetupHoverTimer();
        ShowAllControls();

        this.SizeChanged += (_, _) =>
        {
            if (!_isApplyingAspectRatio) ApplyAspectRatioConstraint();
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // AUTO-HIDE OVERLAY
    // ═══════════════════════════════════════════════════════════════

    private void SetupHoverTimer()
    {
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
        _hoverTimer.Tick += (_, _) =>
        {
            if (_hoverTopBar || _hoverCenter || _hoverBottomBar) { _hoverTimer?.Start(); return; }
            HideAllControls();
        };
    }

    public void ShowAllControls()
    {
        _controlsVisible = true;
        _hoverTimer?.Stop();

        if (HoverOverlay != null)
        {
            HoverOverlay.IsVisible = true;
            HoverOverlay.IsHitTestVisible = true;
            HoverOverlay.Opacity = 1;
        }
        if (PipSeekThumb != null) PipSeekThumb.IsVisible = true;

        _hoverTimer?.Start();
    }

    public void HideAllControls()
    {
        _controlsVisible = false;
        // Keep timer stopped — it will be restarted by ShowAllControls on mouse move
        _hoverTimer?.Stop();

        if (HoverOverlay != null)
        {
            HoverOverlay.Opacity = 0;
            HoverOverlay.IsHitTestVisible = false;
            _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
            {
                await Task.Delay(250);
                if (!_controlsVisible && HoverOverlay != null)
                    HoverOverlay.IsVisible = false;
            });
        }
    }

    public void StartHoverTimer() { _hoverTimer?.Stop(); _hoverTimer?.Start(); }

    private void ResetHoverTimer() { _hoverTimer?.Stop(); _hoverTimer?.Start(); }

    private void OnPipWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_controlsVisible) ShowAllControls();
        ResetHoverTimer();
    }

    private void OnTopBarPointerEntered(object? sender, PointerEventArgs e) => _hoverTopBar = true;
    private void OnTopBarPointerExited(object? sender, PointerEventArgs e) => _hoverTopBar = false;
    private void OnTopBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !_resizing)
            BeginMoveDrag(e);
    }
    private void OnCenterPointerEntered(object? sender, PointerEventArgs e) => _hoverCenter = true;
    private void OnCenterPointerExited(object? sender, PointerEventArgs e) => _hoverCenter = false;
    private void OnBottomBarPointerEntered(object? sender, PointerEventArgs e) => _hoverBottomBar = true;
    private void OnBottomBarPointerExited(object? sender, PointerEventArgs e) => _hoverBottomBar = false;

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        ResetHoverTimer();
    }

    private void OnMuteToggle(object? sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        SetMuted(_isMuted);
        MuteToggled?.Invoke(this, EventArgs.Empty);
        ResetHoverTimer();
    }

    private void OnPinToggle(object? sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        SaveState();
        ResetHoverTimer();
    }

    private void OnExpandClick(object? sender, RoutedEventArgs e) => Close();

    // ═══════════════════════════════════════════════════════════════
    // SEEK BAR
    // ═══════════════════════════════════════════════════════════════

    private double GetNormalizedFromPointer(PointerEventArgs e)
    {
        if (PipSeekArea == null || PipSeekThumb == null) return 0;
        var pos = e.GetCurrentPoint(PipSeekArea).Position.X;
        double w = PipSeekArea.Bounds.Width > 0 ? PipSeekArea.Bounds.Width : PipSeekArea.DesiredSize.Width;
        if (w <= 0) return 0;

        double thumbWidth = PipSeekThumb.Bounds.Width;
        if (thumbWidth <= 0) thumbWidth = 12; // fallback
        double thumbHalf = thumbWidth / 2.0;

        double activeWidth = w - thumbWidth;
        if (activeWidth <= 0) return 0;

        return Math.Clamp((pos - thumbHalf) / activeWidth, 0.0, 1.0);
    }

    private void OnPipSeekPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isSeeking = true;
        _seekNormalized = GetNormalizedFromPointer(e);
        UpdateSeekVisuals(_seekNormalized);
        e.Pointer.Capture(PipSeekArea);
    }

    private void OnPipSeekPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeeking) return;
        _isSeeking = false;
        SeekRequested?.Invoke(this, _seekNormalized);
        e.Pointer.Capture(null);
        ResetHoverTimer();
    }

    private void OnPipSeekPointerMoved(object? sender, PointerEventArgs e)
    {
        var n = GetNormalizedFromPointer(e);
        if (_isSeeking) { _seekNormalized = n; UpdateSeekVisuals(n); }
        ResetHoverTimer();
    }

    private void OnPipSeekPointerExited(object? sender, PointerEventArgs e) { }

    private void UpdateSeekVisuals(double normalized)
    {
        if (PipSeekArea == null || PipSeekFill == null || PipSeekThumb == null) return;
        double w = PipSeekArea.Bounds.Width > 0 ? PipSeekArea.Bounds.Width : PipSeekArea.DesiredSize.Width;
        if (w <= 0) return;

        double thumbWidth = PipSeekThumb.Bounds.Width;
        if (thumbWidth <= 0) thumbWidth = 12; // fallback
        double thumbHalf = thumbWidth / 2.0;

        double thumbLeft = normalized * (w - thumbWidth);
        PipSeekThumb.Margin = new Thickness(thumbLeft, 0, 0, 0);

        PipSeekFill.Width = thumbLeft + thumbHalf;
        PipSeekThumb.IsVisible = _isSeeking || HoverOverlay?.Opacity > 0.5;
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    public void SetFileName(string fileName, string folderOrCodec)
    {
        if (PipFileName != null) PipFileName.Text = fileName;
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        if (MuteIcon != null)
            MuteIcon.Kind = muted ? Material.Icons.MaterialIconKind.VolumeOff : Material.Icons.MaterialIconKind.VolumeHigh;
    }

    private DateTime _lastFrameTime = DateTime.MinValue;
    private static readonly TimeSpan MinFrameInterval = TimeSpan.FromMilliseconds(33); // ~30fps for PiP

    /// <summary>
    /// Update the video frame displayed in the PiP window.
    /// Called from the UI dispatcher thread. Data is BGRA byte array.
    /// Frames are throttled to ~30fps — PiP doesn't need 60fps.
    /// Uses unsafe bulk copy (Buffer.MemoryCopy) instead of row-by-row Marshal.Copy.
    /// </summary>
    public void UpdateFrame(byte[] pixels, int width, int height)
    {
        // Throttle to ~30fps to reduce UI thread pressure
        var now = DateTime.UtcNow;
        if ((now - _lastFrameTime) < MinFrameInterval)
            return;
        _lastFrameTime = now;

        try
        {
            var image = PipVideoFrame;
            if (image == null) return;

            if (_pipFrameBitmap == null || _pipFrameBitmap.PixelSize.Width != width || _pipFrameBitmap.PixelSize.Height != height)
            {
                _pipFrameBitmap?.Dispose();
                _pipFrameBitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);
            }

            using (var fb = _pipFrameBitmap.Lock())
            {
                unsafe
                {
                    var byteCount = (uint)(width * height * 4);
                    fixed (byte* src = pixels)
                    {
                        Buffer.MemoryCopy(src, (void*)fb.Address, byteCount, byteCount);
                    }
                }
            }

            image.Source = _pipFrameBitmap;
            image.IsVisible = true;
            image.InvalidateVisual();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PiP] UpdateFrame error: {ex.Message}");
        }
    }

    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        // Defer to next tick — the control tree may not be ready yet
        // if this is called right after window creation.
        Dispatcher.UIThread.Post(() =>
        {
            if (PlayPauseIcon == null) return;
            if (_isEnded) PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Replay;
            else PlayPauseIcon.Kind = isPlaying ? Material.Icons.MaterialIconKind.Pause : Material.Icons.MaterialIconKind.Play;
        }, DispatcherPriority.Input);
    }

    public void SetReplayMode(bool showReplay)
    {
        _isEnded = showReplay;
        if (showReplay && PlayPauseIcon != null) PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Replay;
        else if (!showReplay) SetPlayingState(_isPlaying);
    }

    public void UpdatePosition(double positionSec, double durationSec)
    {
        if (durationSec > 0 && !_isSeeking)
        {
            _seekNormalized = Math.Clamp(positionSec / durationSec, 0, 1);
            UpdateSeekVisuals(_seekNormalized);
        }
    }

    public void SetAspectRatio(double ar) { if (ar > 0) { _aspectRatio = ar; ApplyAspectRatioConstraint(); } }

    private void ApplyAspectRatioConstraint()
    {
        if (_isApplyingAspectRatio || _resizing || _aspectRatio <= 0 || Width <= 0 || Height <= 0) return;
        if (Math.Abs(Width / Height - _aspectRatio) > 0.01) { _isApplyingAspectRatio = true; Width = Height * _aspectRatio; _isApplyingAspectRatio = false; }
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RestoreState();
        ApplyAspectRatioConstraint();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        _hoverTimer?.Stop(); _hoverTimer = null;
        SaveState();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e) { IsClosed = true; base.OnClosed(e); }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _ = Dispatcher.UIThread.OnUiThreadAsync(async () => { await Task.Delay(150); SnapToEdge(); });
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Space) PlayPauseRequested?.Invoke(this, EventArgs.Empty);
    }

    public new void Close()
    {
        if (!_isClosing) { _isClosing = true; _hoverTimer?.Stop(); _hoverTimer = null; SaveState(); }
        base.Close();
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE PERSISTENCE
    // ═══════════════════════════════════════════════════════════════

    private void SaveState()
    {
        try { File.WriteAllText(PipStatePath, JsonSerializer.Serialize(new PipState(Position.X, Position.Y, (int)Width, (int)Height, _isPinned), CineJsonContext.Default.PipState)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PiP] SaveState failed: {ex.Message}"); }
    }

    private void RestoreState()
    {
        try
        {
            if (!File.Exists(PipStatePath)) return;
            var state = JsonSerializer.Deserialize(File.ReadAllText(PipStatePath), CineJsonContext.Default.PipState);
            if (state == null) return;
            var screens = Screens?.All;
            if (screens != null && screens.Count > 0)
            {
                bool onScreen = false;
                foreach (var sc in screens)
                {
                    var wa = sc.WorkingArea;
                    if (state.X >= wa.X - 50 && state.X + state.W <= wa.X + wa.Width + 50 && state.Y >= wa.Y - 50 && state.Y + state.H <= wa.Y + wa.Height + 50)
                    { onScreen = true; break; }
                }
                if (!onScreen)
                    state = new PipState(screens[0].WorkingArea.Width - state.W - 20, 20, state.W, state.H, state.Pinned);
            }
            Position = new PixelPoint(state.X, state.Y); Width = state.W; Height = state.H;
            _isPinned = state.Pinned; Topmost = _isPinned;
        }
        catch
        {
            Log.ForContext<PipWindow>().Warning("Failed to restore PiP window state");
        }
    }

    public void SnapToEdge()
    {
        var screens = Screens?.All;
        if (screens == null || screens.Count == 0) return;
        var currentScreen = screens.FirstOrDefault(s =>
            Position.X >= s.WorkingArea.X && Position.X <= s.WorkingArea.X + s.WorkingArea.Width &&
            Position.Y >= s.WorkingArea.Y && Position.Y <= s.WorkingArea.Y + s.WorkingArea.Height) ?? screens[0];
        var work = currentScreen.WorkingArea;
        int x = Position.X, y = Position.Y;
        const int snapThreshold = 50;
        if (Math.Abs(x - work.X) < snapThreshold) x = work.X;
        else if (Math.Abs((x + Width) - (work.X + work.Width)) < snapThreshold) x = (int)(work.X + work.Width - Width);
        if (Math.Abs(y - work.Y) < snapThreshold) y = work.Y;
        else if (Math.Abs((y + Height) - (work.Y + work.Height)) < snapThreshold) y = (int)(work.Y + work.Height - Height);
        Position = new PixelPoint(x, y);
        SaveState();
    }

    // ═══════════════════════════════════════════════════════════════
    // RESIZE
    // ═══════════════════════════════════════════════════════════════

    private bool _resizing;
    private double _resizeStartX, _resizeStartY, _resizeStartW, _resizeStartH;
    private int _resizeEdge;

    private void BeginResize(PointerPressedEventArgs e, int edge)
    {
        _resizing = true; _resizeEdge = edge;
        _resizeStartX = e.GetCurrentPoint(this).Position.X; _resizeStartY = e.GetCurrentPoint(this).Position.Y;
        _resizeStartW = Width; _resizeStartH = Height;
        this.PointerMoved += OnResizePointerMoved; this.PointerReleased += OnResizePointerReleased;
        e.Pointer.Capture(this);
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_resizing) return;

        double dx = e.GetCurrentPoint(this).Position.X - _resizeStartX;
        double dy = e.GetCurrentPoint(this).Position.Y - _resizeStartY;
        double nw = _resizeStartW, nh = _resizeStartH;
        double nx = Position.X, ny = Position.Y;

        bool left   = (_resizeEdge & 1) != 0;
        bool right  = (_resizeEdge & 2) != 0;
        bool top    = (_resizeEdge & 4) != 0;
        bool bottom = (_resizeEdge & 8) != 0;

        bool hasHorizontal = left || right;
        bool hasVertical = top || bottom;

        if (_aspectRatio > 0)
        {
            // Aspect ratio locked — use the dominant axis to drive resize
            if (!hasHorizontal && hasVertical)
            {
                // Top/bottom only — height drives width
                if (top)    nh = Math.Max(MinHeight, _resizeStartH - dy);
                if (bottom) nh = Math.Max(MinHeight, _resizeStartH + dy);
                nh = Math.Min(MaxHeight, nh);
                nw = nh * _aspectRatio;
            }
            else
            {
                // Left/right or corner — width drives height (primary axis)
                if (left)   nw = Math.Max(MinWidth, _resizeStartW - dx);
                if (right)  nw = Math.Max(MinWidth, _resizeStartW + dx);
                nw = Math.Min(MaxWidth, nw);
                nh = nw / _aspectRatio;
                if (left)  nx = Position.X + (_resizeStartW - nw);
                if (top)   ny = Position.Y + (_resizeStartH - nh);
            }
        }
        else
        {
            // No aspect ratio constraint
            if (left)   nw = Math.Max(MinWidth, _resizeStartW - dx);
            if (right)  nw = Math.Max(MinWidth, _resizeStartW + dx);
            if (top)    nh = Math.Max(MinHeight, _resizeStartH - dy);
            if (bottom) nh = Math.Max(MinHeight, _resizeStartH + dy);
            nw = Math.Min(MaxWidth, nw);
            nh = Math.Min(MaxHeight, nh);
            if (left)  nx = Position.X + (_resizeStartW - nw);
            if (top)   ny = Position.Y + (_resizeStartH - nh);
        }

        Width = nw;
        Height = nh;
        Position = new PixelPoint((int)Math.Round(nx), (int)Math.Round(ny));
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _resizing = false;
        this.PointerMoved -= OnResizePointerMoved; this.PointerReleased -= OnResizePointerReleased;
        e.Pointer.Capture(null);
    }

    private void OnTopEdgePointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 4);
    private void OnBottomEdgePointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 8);
    private void OnLeftEdgePointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 1);
    private void OnRightEdgePointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 2);
    private void OnTopLeftCornerPointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 1 | 4);
    private void OnTopRightCornerPointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 2 | 4);
    private void OnBottomLeftCornerPointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 1 | 8);
    private void OnBottomRightCornerPointerPressed(object? s, PointerPressedEventArgs e) => BeginResize(e, 2 | 8);
}
