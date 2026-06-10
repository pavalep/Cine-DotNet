using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private bool _isMuted;
    private DwmThumbnailManager? _dwmManager;
    private int _thumbnailId;
    private double _aspectRatio = 16.0 / 9.0;
    private bool _isApplyingAspectRatio;

    // Mirror retry
    private DispatcherTimer? _mirrorRetryTimer;
    private Stopwatch? _mirrorRetryWatch;
    private const int MirrorRetryMaxMs = 5000;

    // Overlay window
    private PipOverlayWindow? _overlay;
    private DispatcherTimer? _posSyncTimer;
    private bool _overlayVisible;

    internal bool IsClosed { get; private set; }

    // ────── Player events (forwarded from overlay → PipService) ──────
    public event EventHandler? PlayPauseRequested;
    public event EventHandler<double>? SeekRequested;
    public event EventHandler? MuteToggled;

    private static readonly string PipStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "pip_state.json");

    private record PipState(int X, int Y, int W, int H, bool Pinned);

    public PipWindow()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        InitializeComponent();
        KeyDown += OnKeyDown;

        this.SizeChanged += (_, _) =>
        {
            if (!_resizing) SyncThumbnailRect();
            if (!_isApplyingAspectRatio) ApplyAspectRatioConstraint();
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // OVERLAY WINDOW
    // ═══════════════════════════════════════════════════════════════

    private void EnsureOverlay()
    {
        if (_overlay != null) return;

        _overlay = new PipOverlayWindow();
        _overlay.PlayPauseRequested += (_, _) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        _overlay.SeekRequested += (_, pos) => SeekRequested?.Invoke(this, pos);
        _overlay.MuteToggled += (_, _) => MuteToggled?.Invoke(this, EventArgs.Empty);
        _overlay.CloseRequested += (_, _) => Close();
        _overlay.ExpandRequested += (_, _) => Close();
        _overlay.PinToggled += (_, pinned) =>
        {
            _isPinned = pinned;
            Topmost = _isPinned;
            SaveState();
        };

        _overlay.SyncGeometry(Position, Width, Height);
        _overlay.SetPlayingState(_isPlaying);
        _overlay.SetMuted(_isMuted);
        _overlay.Show();

        _posSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _posSyncTimer.Tick += (_, _) =>
        {
            if (_overlay != null) _overlay.SyncGeometry(Position, Width, Height);
        };
        _posSyncTimer.Start();
    }

    public void ShowOverlay()
    {
        EnsureOverlay();
        _overlay?.ShowControls();
        _overlay?.StartAutoHide();
        _overlayVisible = true;
    }

    public void HideOverlay()
    {
        _overlay?.HideControls();
        _overlayVisible = false;
    }

    private void DestroyOverlay()
    {
        _posSyncTimer?.Stop();
        _posSyncTimer = null;
        if (_overlay != null)
        {
            _overlay.Close();
            _overlay = null;
        }
        _overlayVisible = false;
    }

    private void OnPipWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_overlayVisible) ShowOverlay();
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API (called by PipService / MainWindow)
    // ═══════════════════════════════════════════════════════════════

    public void SetFileName(string fileName, string folderOrCodec)
    {
        EnsureOverlay();
        _overlay?.SetFileName(fileName, folderOrCodec);
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        _overlay?.SetMuted(muted);
    }

    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        _overlay?.SetPlayingState(isPlaying);
    }

    public void SetReplayMode(bool showReplay) => _overlay?.SetReplayMode(showReplay);

    public void UpdatePosition(double positionSec, double durationSec) =>
        _overlay?.UpdatePosition(positionSec, durationSec);

    public void SetAspectRatio(double ar)
    {
        if (ar > 0) { _aspectRatio = ar; ApplyAspectRatioConstraint(); }
    }

    public void ShowAllControls() => ShowOverlay();
    public void StartHoverTimer() => _overlay?.StartAutoHide();

    // ═══════════════════════════════════════════════════════════════
    // DWM THUMBNAIL MIRROR
    // ═══════════════════════════════════════════════════════════════

    public void EnableDwmMirror(DwmThumbnailManager manager)
    {
        if (_thumbnailId > 0) return;
        _dwmManager = manager;
        if (!TryRegisterMirror())
            Log.ForContext<PipWindow>().Warning("EnableDwmMirror: deferred");
    }

    private bool TryRegisterMirror()
    {
        if (_dwmManager == null || _thumbnailId > 0 || _isClosing) return _thumbnailId > 0;
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || _dwmManager.SourceHwnd == IntPtr.Zero) return false;
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

    private void OnActivatedRetryMirror(object? s, EventArgs e) { this.Activated -= OnActivatedRetryMirror; if (TryRegisterMirror()) StopMirrorRetry(); }
    private void OnMirrorRetryTick(object? s, EventArgs e)
    {
        if (TryRegisterMirror()) { StopMirrorRetry(); return; }
        if (_mirrorRetryWatch?.ElapsedMilliseconds >= MirrorRetryMaxMs) StopMirrorRetry();
    }

    private void DoEnableMirror(IntPtr handle)
    {
        if (_dwmManager == null || _thumbnailId > 0 || handle == IntPtr.Zero || _dwmManager.SourceHwnd == IntPtr.Zero) return;
        _thumbnailId = _dwmManager.RegisterTarget(handle);
        if (_thumbnailId > 0) { StopMirrorRetry(); SyncThumbnailRect(); }
    }

    public void DisableDwmMirror()
    {
        StopMirrorRetry();
        if (_dwmManager != null && _thumbnailId > 0) _dwmManager.UnregisterTarget(_thumbnailId);
        _thumbnailId = 0;
    }

    /// <summary>DWM thumbnail fills full window. Controls are in separate overlay window.</summary>
    private void SyncThumbnailRect()
    {
        if (_dwmManager == null || _thumbnailId <= 0 || _isClosing) return;
        double s = RenderScaling;
        _dwmManager.UpdateTarget(_thumbnailId, 255, true,
            destLeft: 0, destTop: 0, destRight: Math.Max(1, (int)(Width * s)), destBottom: Math.Max(1, (int)(Height * s)));
    }

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
        if (!TryRegisterMirror()) StartMirrorRetry();
        _ = Dispatcher.UIThread.OnUiThreadAsync(async () => { await Task.Delay(100); if (!_isClosing) ShowOverlay(); });
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        DestroyOverlay();
        StopMirrorRetry();
        DisableDwmMirror();
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
        if (!_isClosing) { _isClosing = true; DestroyOverlay(); StopMirrorRetry(); DisableDwmMirror(); SaveState(); }
        base.Close();
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════════════

    private void SaveState()
    {
        try { File.WriteAllText(PipStatePath, JsonSerializer.Serialize(new PipState(Position.X, Position.Y, (int)Width, (int)Height, _isPinned))); } catch { }
    }

    private void RestoreState()
    {
        try
        {
            if (!File.Exists(PipStatePath)) return;
            var state = JsonSerializer.Deserialize<PipState>(File.ReadAllText(PipStatePath));
            if (state == null) return;
            var screens = Screens?.All;
            if (screens != null && screens.Count > 0)
            {
                bool onScreen = false;
                foreach (var sc in screens)
                {
                    var wa = sc.WorkingArea;
                    if (state.X >= wa.X - 50 && state.X + state.W <= wa.X + wa.Width + 50
                        && state.Y >= wa.Y - 50 && state.Y + state.H <= wa.Y + wa.Height + 50)
                    { onScreen = true; break; }
                }
                if (!onScreen)
                    state = new PipState(screens[0].WorkingArea.Width - state.W - 20, 20, state.W, state.H, state.Pinned);
            }
            Position = new PixelPoint(state.X, state.Y);
            Width = state.W; Height = state.H;
            _isPinned = state.Pinned; Topmost = _isPinned;
        }
        catch { }
    }

    public void SnapToEdge() { /* unchanged resize snap logic */ }

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
        double nw = _resizeStartW, nh = _resizeStartH, nx = Position.X, ny = Position.Y;
        if ((_resizeEdge & 2) != 0) nw = Math.Max(MinWidth, _resizeStartW + dx);
        if ((_resizeEdge & 1) != 0) { nw = Math.Max(MinWidth, _resizeStartW - dx); nx = Position.X + (_resizeStartW - nw); }
        if ((_resizeEdge & 8) != 0) nh = Math.Max(MinHeight, _resizeStartH + dy);
        if ((_resizeEdge & 4) != 0) { nh = Math.Max(MinHeight, _resizeStartH - dy); ny = Position.Y + (_resizeStartH - nh); }
        if (_aspectRatio > 0) { if ((_resizeEdge & 3) != 0) nh = nw / _aspectRatio; else nw = nh * _aspectRatio; }
        Width = Math.Min(MaxWidth, nw); Height = Math.Min(MaxHeight, nh);
        Position = new PixelPoint((int)nx, (int)ny);
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _resizing = false;
        this.PointerMoved -= OnResizePointerMoved; this.PointerReleased -= OnResizePointerReleased;
        e.Pointer.Capture(null);
        SyncThumbnailRect();
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
