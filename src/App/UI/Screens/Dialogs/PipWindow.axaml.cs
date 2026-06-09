using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Helpers;
using Cine.Core;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PipWindow : Window
{
    private bool _isPinned;
    private bool _isClosing;
    private bool _isPlaying = true;
    private bool _isEnded;
    private bool _isMuted;
    private DwmThumbnailManager? _dwmManager;
    private int _thumbnailId;
    // Hover auto-hide — mirrors MainWindow.AutoHide pattern
    private DispatcherTimer? _hoverTimer;
    private bool _hoverTopBar;
    private bool _hoverCenter;
    private bool _hoverBottomBar;
    private bool _controlsVisible = true;
    private bool _isUpdatingSeekFromExternal;
    private double _aspectRatio = 16.0 / 9.0;
    private bool _isApplyingAspectRatio;
    private DispatcherTimer? _mirrorRetryTimer;
    private Stopwatch? _mirrorRetryWatch;
    private const int MirrorRetryMaxMs = 5000;

    // Custom seek state
    private bool _isSeeking;
    private double _seekNormalized;

    private string _fileName = "";
    private string _fileSubtitle = "";

    /// <summary>True after the window has fully closed (for stale-check in PipService).</summary>
    internal bool IsClosed { get; private set; }

    // ────── Player control events ──────
    public event EventHandler? PlayPauseRequested;
    public event EventHandler<double>? SeekRequested;
    public event EventHandler? MuteToggled;

    // ────── State persistence ──────
    private static readonly string PipStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "pip_state.json");

    private record PipState(int X, int Y, int W, int H, bool Pinned);

    public PipWindow()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        InitializeComponent();
        KeyDown += OnKeyDown;

        // Sync DWM thumbnail on resize
        this.SizeChanged += (_, _) =>
        {
            SyncThumbnailRect();
            if (!_isApplyingAspectRatio)
                ApplyAspectRatioConstraint();
        };
    }

    /// <summary>Sets the file name and folder shown in the top bar / badge.</summary>
    public void SetFileName(string fileName, string folderOrCodec)
    {
        _fileName = fileName;
        _fileSubtitle = folderOrCodec;
        if (PipFileName != null) PipFileName.Text = fileName;
        if (PipFileSubtitle != null) PipFileSubtitle.Text = folderOrCodec;
        if (PipBadgeLabel != null) PipBadgeLabel.Text = fileName;
    }

    /// <summary>Sets the mute state icon.</summary>
    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        if (MuteIcon != null)
            MuteIcon.Kind = muted
                ? Material.Icons.MaterialIconKind.VolumeOff
                : Material.Icons.MaterialIconKind.VolumeHigh;
    }

    // ═══════════════════════════════════════════════════════════════
    // DWM THUMBNAIL MIRROR
    // ═══════════════════════════════════════════════════════════════

    public void EnableDwmMirror(DwmThumbnailManager manager)
    {
        if (_thumbnailId > 0) return;
        _dwmManager = manager;

        if (TryRegisterMirror())
            return;

        Log.ForContext<PipWindow>().Warning(
            "EnableDwmMirror: deferred (source=0x{0:X}, handle=0x{1:X})",
            _dwmManager.SourceHwnd, TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
    }

    private bool TryRegisterMirror()
    {
        if (_dwmManager == null || _thumbnailId > 0 || _isClosing)
            return _thumbnailId > 0;

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || _dwmManager.SourceHwnd == IntPtr.Zero)
            return false;

        DoEnableMirror(handle);
        return _thumbnailId > 0;
    }

    private void StartMirrorRetry()
    {
        if (_mirrorRetryTimer != null) return;

        if (LoadingOverlay != null) LoadingOverlay.IsVisible = true;

        _mirrorRetryWatch = Stopwatch.StartNew();
        _mirrorRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _mirrorRetryTimer.Tick += OnMirrorRetryTick;
        _mirrorRetryTimer.Start();
        this.Activated += OnActivatedRetryMirror;
    }

    private void StopMirrorRetry()
    {
        if (_mirrorRetryTimer == null) return;

        if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;

        _mirrorRetryTimer.Stop();
        _mirrorRetryTimer.Tick -= OnMirrorRetryTick;
        _mirrorRetryTimer = null;
        _mirrorRetryWatch = null;
        this.Activated -= OnActivatedRetryMirror;
    }

    private void OnActivatedRetryMirror(object? sender, EventArgs e)
    {
        this.Activated -= OnActivatedRetryMirror;
        if (TryRegisterMirror()) StopMirrorRetry();
    }

    private void OnMirrorRetryTick(object? sender, EventArgs e)
    {
        if (TryRegisterMirror()) { StopMirrorRetry(); return; }

        if (_mirrorRetryWatch != null && _mirrorRetryWatch.ElapsedMilliseconds >= MirrorRetryMaxMs)
        {
            StopMirrorRetry();
        }
    }

    private void DoEnableMirror(IntPtr handle)
    {
        if (_dwmManager == null || _thumbnailId > 0 || handle == IntPtr.Zero || _dwmManager.SourceHwnd == IntPtr.Zero)
            return;

        _thumbnailId = _dwmManager.RegisterTarget(handle);
        if (_thumbnailId > 0)
        {
            StopMirrorRetry();
            SyncThumbnailRect();
        }
    }

    public void DisableDwmMirror()
    {
        StopMirrorRetry();
        if (_dwmManager != null && _thumbnailId > 0)
            _dwmManager.UnregisterTarget(_thumbnailId);
        _thumbnailId = 0;
    }

    /// <summary>Constrains DWM thumbnail to fill the full window.</summary>
    private void SyncThumbnailRect()
    {
        if (_dwmManager == null || _thumbnailId <= 0 || _isClosing) return;

        double scale = RenderScaling;
        int w = Math.Max(1, (int)(Width * scale));
        int h = Math.Max(1, (int)(Height * scale));

        _dwmManager.UpdateTarget(_thumbnailId, opacity: 255, visible: true,
            destLeft: 0, destTop: 0, destRight: w, destBottom: h);
    }

    /// <summary>Sets the target aspect ratio for resize locking.</summary>
    public void SetAspectRatio(double ar)
    {
        if (ar > 0)
        {
            _aspectRatio = ar;
            if (Width > 0 && Height > 0)
                ApplyAspectRatioConstraint();
        }
    }

    /// <summary>Applies aspect ratio constraint when size changes externally (not during active resize drag).</summary>
    private void ApplyAspectRatioConstraint()
    {
        if (_isApplyingAspectRatio || _resizing) return;
        if (_aspectRatio <= 0 || Width <= 0 || Height <= 0) return;

        var currentRatio = Width / Height;
        const double tolerance = 0.01;

        if (Math.Abs(currentRatio - _aspectRatio) > tolerance)
        {
            _isApplyingAspectRatio = true;
            Width = Height * _aspectRatio;
            _isApplyingAspectRatio = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PLAYBACK SYNC (called externally)
    // ═══════════════════════════════════════════════════════════════

    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        if (PlayPauseIcon != null)
        {
            if (_isEnded)
            {
                PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Replay;
            }
            else
            {
                PlayPauseIcon.Kind = isPlaying
                    ? Material.Icons.MaterialIconKind.Pause
                    : Material.Icons.MaterialIconKind.Play;
            }
        }
    }

    /// <summary>Shows replay icon when video ends (no next track). Clears on resume.</summary>
    public void SetReplayMode(bool showReplay)
    {
        _isEnded = showReplay;
        if (showReplay && PlayPauseIcon != null)
            PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Replay;
        else if (!showReplay)
            SetPlayingState(_isPlaying);
    }

    public void UpdatePosition(double positionSec, double durationSec)
    {
        _isUpdatingSeekFromExternal = true;
        try
        {
            if (durationSec > 0)
            {
                _seekNormalized = Math.Clamp(positionSec / durationSec, 0, 1);
                UpdateSeekVisuals(_seekNormalized);
            }

            if (PipTimeLabel != null)
            {
                var pos = TimeSpan.FromSeconds(positionSec);
                var dur = TimeSpan.FromSeconds(durationSec);
                PipTimeLabel.Text = $"{(int)pos.TotalMinutes:D2}:{pos.Seconds:D2} / {(int)dur.TotalMinutes:D2}:{dur.Seconds:D2}";
            }
        }
        finally
        {
            _isUpdatingSeekFromExternal = false;
        }
    }

    /// <summary>Updates the custom seek bar visuals.</summary>
    private void UpdateSeekVisuals(double normalized)
    {
        if (PipSeekArea == null || PipSeekFill == null || PipSeekThumb == null) return;

        double areaWidth = PipSeekArea.Bounds.Width;
        if (areaWidth <= 0) return;

        double fillWidth = normalized * (areaWidth - 14); // 14 = thumb width
        PipSeekFill.Width = Math.Max(0, fillWidth);

        Canvas.SetLeft(PipSeekThumb, fillWidth);
        PipSeekThumb.IsVisible = _isSeeking || HoverOverlay?.Opacity > 0.5;
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        RestoreState();
        SetupHoverTimer();

        if (_aspectRatio > 0 && Width > 0 && Height > 0)
            ApplyAspectRatioConstraint();

        if (!TryRegisterMirror())
            StartMirrorRetry();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        _hoverTimer?.Stop();
        _hoverTimer = null;
        StopMirrorRetry();
        DisableDwmMirror();
        SaveState();

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        IsClosed = true;
        base.OnClosed(e);
    }

    // ═══════════════════════════════════════════════════════════════
    // HOVER AUTO-HIDE (mirrors MainWindow.AutoHide pattern)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Window-level pointer moved — any mouse movement re-shows controls.</summary>
    private void OnPipWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_controlsVisible)
            ShowAllControls();
        ResetHoverTimer();
    }

    /// <summary>Starts the auto-hide timer (3s idle → hide).</summary>
    public void StartHoverTimer()
    {
        if (_hoverTimer == null) SetupHoverTimer();
        _hoverTimer?.Stop();
        _hoverTimer?.Start();
    }

    private void ResetHoverTimer()
    {
        _hoverTimer?.Stop();
        _hoverTimer?.Start();
    }

    private void SetupHoverTimer()
    {
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
        _hoverTimer.Tick += OnHoverTimerTick;
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        // Don't hide if mouse is still over any control element
        if (_hoverTopBar || _hoverCenter || _hoverBottomBar)
        {
            _hoverTimer?.Start();
            return;
        }
        HideAllControls();
    }

    // ═══ Per-element hover tracking (PointerEntered/Exited on each overlay piece) ═══

    private void OnTopBarPointerEntered(object? sender, PointerEventArgs e) => _hoverTopBar = true;
    private void OnTopBarPointerExited(object? sender, PointerEventArgs e) => _hoverTopBar = false;
    private void OnCenterPointerEntered(object? sender, PointerEventArgs e) => _hoverCenter = true;
    private void OnCenterPointerExited(object? sender, PointerEventArgs e) => _hoverCenter = false;
    private void OnBottomBarPointerEntered(object? sender, PointerEventArgs e) => _hoverBottomBar = true;
    private void OnBottomBarPointerExited(object? sender, PointerEventArgs e) => _hoverBottomBar = false;

    // ═══ Show / Hide ═══

    public void ShowAllControls()
    {
        _controlsVisible = true;
        _hoverTimer?.Stop();

        if (HoverOverlay != null)
        {
            HoverOverlay.IsVisible = true;
            HoverOverlay.Opacity = 1;
            HoverOverlay.IsHitTestVisible = true;
        }
        if (FileBadge != null)
        {
            FileBadge.IsVisible = true;
            FileBadge.Opacity = 1;
        }
        if (PipSeekThumb != null)
            PipSeekThumb.IsVisible = true;

        _hoverTimer?.Start();
    }

    private void HideAllControls()
    {
        _controlsVisible = false;
        _hoverTimer?.Stop();

        if (HoverOverlay != null)
        {
            HoverOverlay.IsVisible = false;
            HoverOverlay.Opacity = 0;
            HoverOverlay.IsHitTestVisible = false;
        }
        if (FileBadge != null)
        {
            FileBadge.IsVisible = false;
            FileBadge.Opacity = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PLAYER CONTROLS
    // ═══════════════════════════════════════════════════════════════

    private void OnVideoAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Single click on video = play/pause toggle
        _isPlaying = !_isPlaying;
        SetPlayingState(_isPlaying);
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        ResetHoverTimer();
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        _isPlaying = !_isPlaying;
        SetPlayingState(_isPlaying);
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

    // ═══════════════════════════════════════════════════════════════
    // CUSTOM SEEK BAR
    // ═══════════════════════════════════════════════════════════════

    private double GetNormalizedFromPointer(PointerEventArgs e)
    {
        if (PipSeekArea == null) return 0;
        var pos = e.GetCurrentPoint(PipSeekArea).Position.X;
        double w = PipSeekArea.Bounds.Width;
        if (w <= 0) return 0;
        return Math.Clamp(pos / w, 0, 1);
    }

    private void OnPipSeekPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isSeeking = true;
        var n = GetNormalizedFromPointer(e);
        _seekNormalized = n;
        UpdateSeekVisuals(n);
        if (PipSeekPreviewDot != null) PipSeekPreviewDot.IsVisible = false;
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

        if (_isSeeking)
        {
            _seekNormalized = n;
            UpdateSeekVisuals(n);
        }
        else
        {
            // Show preview dot on hover
            if (PipSeekPreviewDot != null && PipSeekArea != null)
            {
                PipSeekPreviewDot.IsVisible = true;
                double aw = PipSeekArea.Bounds.Width;
                Canvas.SetLeft(PipSeekPreviewDot, n * (aw - 10));
            }
        }
    }

    private void OnPipSeekPointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isSeeking && PipSeekPreviewDot != null)
            PipSeekPreviewDot.IsVisible = false;
    }

    private void OnBottomBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Prevent drag from bottom bar
    }

    // ═══════════════════════════════════════════════════════════════
    // UI HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnTopBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
        {
            await Task.Delay(150);
            SnapToEdge();
        });
    }

    private void OnPinToggle(object? sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        UpdatePinIcon();
        SaveState();
        ResetHoverTimer();
    }

    private void UpdatePinIcon()
    {
        if (PinIcon != null)
            PinIcon.Opacity = _isPinned ? 1.0 : 0.4;
    }

    /// <summary>Snap window to nearest screen edge.</summary>
    public void SnapToEdge()
    {
        var screens = Screens?.All;
        if (screens == null || screens.Count == 0) return;

        var currentScreen = screens.FirstOrDefault(s =>
            Position.X >= s.WorkingArea.X &&
            Position.X <= s.WorkingArea.X + s.WorkingArea.Width &&
            Position.Y >= s.WorkingArea.Y &&
            Position.Y <= s.WorkingArea.Y + s.WorkingArea.Height)
            ?? screens[0];

        var work = currentScreen.WorkingArea;
        var x = Position.X;
        var y = Position.Y;
        const int snapThreshold = 50;

        if (Math.Abs(x - work.X) < snapThreshold) x = work.X;
        else if (Math.Abs((x + Width) - (work.X + work.Width)) < snapThreshold)
            x = (int)(work.X + work.Width - Width);

        if (Math.Abs(y - work.Y) < snapThreshold) y = work.Y;
        else if (Math.Abs((y + Height) - (work.Y + work.Height)) < snapThreshold)
            y = (int)(work.Y + work.Height - Height);

        Position = new PixelPoint(x, y);
        SaveState();
    }

    private void OnExpandClick(object? sender, RoutedEventArgs e) => OnCloseClick(sender, e);

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _isClosing = true;
        _hoverTimer?.Stop();
        _hoverTimer = null;
        DisableDwmMirror();
        SaveState();
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            OnCloseClick(sender, e);
        else if (e.Key == Key.Space)
            OnPlayPauseClick(sender, e);
    }

    public new void Close()
    {
        if (!_isClosing)
        {
            _isClosing = true;
            _hoverTimer?.Stop();
            _hoverTimer = null;
            StopMirrorRetry();
            DisableDwmMirror();
            SaveState();
        }
        base.Close();
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE PERSISTENCE
    // ═══════════════════════════════════════════════════════════════

    private void SaveState()
    {
        try
        {
            var state = new PipState(Position.X, Position.Y, (int)Width, (int)Height, _isPinned);
            var json = JsonSerializer.Serialize(state);
            File.WriteAllText(PipStatePath, json);
        }
        catch { }
    }

    private void RestoreState()
    {
        try
        {
            if (!File.Exists(PipStatePath)) return;
            var json = File.ReadAllText(PipStatePath);
            var state = JsonSerializer.Deserialize<PipState>(json);
            if (state != null)
            {
                var screens = Screens?.All;
                if (screens != null && screens.Count > 0)
                {
                    var primary = screens[0];
                    bool offScreen = state.X + state.W < 100 ||
                                     state.X > primary.WorkingArea.Width - 100 ||
                                     state.Y + state.H < 50 ||
                                     state.Y > primary.WorkingArea.Height - 50;
                    if (offScreen)
                    {
                        state = new PipState(
                            primary.WorkingArea.Width - state.W - 20,
                            20, state.W, state.H, state.Pinned);
                    }
                }

                Position = new PixelPoint(state.X, state.Y);
                Width = state.W;
                Height = state.H;
                _isPinned = state.Pinned;
                Topmost = _isPinned;
                UpdatePinIcon();
            }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════
    // RESIZE HANDLERS (transparent 8px strips on edges + corner zones)
    // ═══════════════════════════════════════════════════════════════

    private bool _resizing;
    private double _resizeStartX, _resizeStartY;
    private double _resizeStartW, _resizeStartH;
    private int _resizeEdge; // bits: 1=left, 2=right, 4=top, 8=bottom

    private void BeginResize(PointerPressedEventArgs e, int edge)
    {
        _resizing = true;
        _resizeEdge = edge;
        _resizeStartX = e.GetCurrentPoint(this).Position.X;
        _resizeStartY = e.GetCurrentPoint(this).Position.Y;
        _resizeStartW = Width;
        _resizeStartH = Height;
        this.PointerMoved += OnResizePointerMoved;
        this.PointerReleased += OnResizePointerReleased;
        e.Pointer.Capture(this);
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_resizing) return;

        double dx = e.GetCurrentPoint(this).Position.X - _resizeStartX;
        double dy = e.GetCurrentPoint(this).Position.Y - _resizeStartY;

        double newW = _resizeStartW;
        double newH = _resizeStartH;
        double newX = Position.X;
        double newY = Position.Y;

        bool widthAffected = (_resizeEdge & 3) != 0; // left(1) or right(2)
        bool heightAffected = (_resizeEdge & 12) != 0; // top(4) or bottom(8)

        if ((_resizeEdge & 2) != 0) newW = Math.Max(MinWidth, _resizeStartW + dx);
        if ((_resizeEdge & 1) != 0) { newW = Math.Max(MinWidth, _resizeStartW - dx); newX = Position.X + (_resizeStartW - newW); }
        if ((_resizeEdge & 8) != 0) newH = Math.Max(MinHeight, _resizeStartH + dy);
        if ((_resizeEdge & 4) != 0) { newH = Math.Max(MinHeight, _resizeStartH - dy); newY = Position.Y + (_resizeStartH - newH); }

        // Always-locked aspect ratio
        if (_aspectRatio > 0 && widthAffected && heightAffected)
        {
            // Corner resize: lock to aspect ratio
            double ar = _aspectRatio;
            if ((_resizeEdge & 2) != 0 || (_resizeEdge & 1) != 0)
                newH = newW / ar;
            else
                newW = newH * ar;
        }
        else if (_aspectRatio > 0)
        {
            // Edge resize: still lock (determined by aspect ratio)
            if (widthAffected)
                newH = newW / _aspectRatio;
            else
                newW = newH * _aspectRatio;
        }

        Width = Math.Min(MaxWidth, newW);
        Height = Math.Min(MaxHeight, newH);
        Position = new PixelPoint((int)newX, (int)newY);
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _resizing = false;
        this.PointerMoved -= OnResizePointerMoved;
        this.PointerReleased -= OnResizePointerReleased;
        e.Pointer.Capture(null);
        SnapToEdge();
    }

    private void OnTopEdgePointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 4);
    private void OnBottomEdgePointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 8);
    private void OnLeftEdgePointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 1);
    private void OnRightEdgePointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 2);
    private void OnTopLeftCornerPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 1 | 4);
    private void OnTopRightCornerPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 2 | 4);
    private void OnBottomLeftCornerPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 1 | 8);
    private void OnBottomRightCornerPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(e, 2 | 8);
}
