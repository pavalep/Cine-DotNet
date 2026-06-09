using System;
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
    private DwmThumbnailManager? _dwmManager;
    private int _thumbnailId;
    private DispatcherTimer? _hoverTimer;
    private bool _isUpdatingSeekFromExternal;
    private double _aspectRatio = 16.0 / 9.0;
    private DispatcherTimer? _mirrorRetryTimer;
    private int _mirrorRetryAttempts;
    private const int MirrorRetryMaxAttempts = 50;

    // ────── Player control events ──────
    public event EventHandler? PlayPauseRequested;
    public event EventHandler<double>? SeekRequested;

    // ────── State persistence ──────
    private static readonly string PipStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "pip_state.json");

    private record PipState(int X, int Y, int W, int H, bool Pinned);

    public PipWindow()
    {
        InitializeComponent();
        TitleBar.PointerPressed += OnTitleBarPointerPressed;
        KeyDown += OnKeyDown;

        PipSeekSlider.PropertyChanged += OnSeekSliderChanged;
    }

    // ═══════════════════════════════════════════════════════════════
    // DWM THUMBNAIL MIRROR
    // ═══════════════════════════════════════════════════════════════

    public void EnableDwmMirror(DwmThumbnailManager manager)
    {
        if (_thumbnailId > 0)
        {
            Log.ForContext<PipWindow>().Info("EnableDwmMirror: already enabled, id={Id}", _thumbnailId);
            return;
        }
        _dwmManager = manager;
        if (TryEnableMirrorNow())
        {
            StopMirrorRetry();
            return;
        }

        Log.ForContext<PipWindow>().Warning(
            "EnableDwmMirror: waiting for valid source/handle (source=0x{Source:X}, handle=0x{Handle:X})",
            _dwmManager.SourceHwnd, TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        this.Opened -= OnOpenedRetryMirror;
        this.Opened += OnOpenedRetryMirror;
        StartMirrorRetry();
    }

    private void OnOpenedRetryMirror(object? sender, EventArgs e)
    {
        this.Opened -= OnOpenedRetryMirror;
        if (TryEnableMirrorNow())
        {
            StopMirrorRetry();
            return;
        }
        StartMirrorRetry();
    }

    private bool TryEnableMirrorNow()
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

        _mirrorRetryAttempts = 0;
        _mirrorRetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _mirrorRetryTimer.Tick += OnMirrorRetryTick;
        _mirrorRetryTimer.Start();
    }

    private void StopMirrorRetry()
    {
        if (_mirrorRetryTimer == null) return;
        _mirrorRetryTimer.Stop();
        _mirrorRetryTimer.Tick -= OnMirrorRetryTick;
        _mirrorRetryTimer = null;
        _mirrorRetryAttempts = 0;
    }

    private void OnMirrorRetryTick(object? sender, EventArgs e)
    {
        if (TryEnableMirrorNow())
        {
            StopMirrorRetry();
            return;
        }

        _mirrorRetryAttempts++;
        if (_mirrorRetryAttempts >= MirrorRetryMaxAttempts)
        {
            Log.ForContext<PipWindow>().Warning(
                "Mirror retry exhausted after {Attempts} attempts (source=0x{Source:X}, handle=0x{Handle:X})",
                _mirrorRetryAttempts,
                _dwmManager?.SourceHwnd ?? IntPtr.Zero,
                TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
            StopMirrorRetry();
        }
    }

    private void DoEnableMirror(IntPtr handle)
    {
        if (_dwmManager == null || _thumbnailId > 0 || handle == IntPtr.Zero || _dwmManager.SourceHwnd == IntPtr.Zero)
        {
            Log.ForContext<PipWindow>().Info("DoEnableMirror: skipped, mgr={Mgr} id={Id}", _dwmManager != null, _thumbnailId);
            return;
        }

        Log.ForContext<PipWindow>().Info("DoEnableMirror: Registering target, dest=0x{Dest:X} source=0x{Source:X}", handle, _dwmManager.SourceHwnd);
        _thumbnailId = _dwmManager.RegisterTarget(handle);
        Log.ForContext<PipWindow>().Info("DoEnableMirror: registered id={Id}", _thumbnailId);
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
        {
            _dwmManager.UnregisterTarget(_thumbnailId);
        }
        _thumbnailId = 0;
        _dwmManager = null;
    }

    /// <summary>Constrains DWM thumbnail to video area (below titlebar).</summary>
    private void SyncThumbnailRect()
    {
        if (_dwmManager == null || _thumbnailId <= 0 || _isClosing) return;

        double scale = RenderScaling;
        int w = (int)(Width * scale);
        int h = (int)(Height * scale);

        int top = (int)((TitleBar?.Bounds.Height ?? 28) * scale);
        // SeekContainer is inside HoverOverlay which overlays the video; no bottom offset needed
        int bottom = 0;

        _dwmManager.UpdateTarget(_thumbnailId, opacity: 255, visible: true,
            destLeft: 0, destTop: top,
            destRight: w, destBottom: h - bottom);
    }

    /// <summary>Sets the target aspect ratio for resize locking.</summary>
    public void SetAspectRatio(double ar)
    {
        if (ar > 0)
        {
            _aspectRatio = ar;
            // Apply immediately if already sized
            if (Width > 0 && Height > 0)
                ApplyAspectRatioConstraint();
        }
    }

    /// <summary>Applies aspect ratio constraint after resize completes</summary>
    private void ApplyAspectRatioConstraint()
    {
        if (_aspectRatio <= 0 || Width <= 0 || Height <= 0) return;

        var currentRatio = Width / Height;
        const double tolerance = 0.01; // 1% tolerance

        if (Math.Abs(currentRatio - _aspectRatio) > tolerance)
        {
            var newWidth = Height * _aspectRatio;
            Width = newWidth;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PLAYBACK SYNC (called externally)
    // ═══════════════════════════════════════════════════════════════

    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        if (PlayPauseIcon != null)
            PlayPauseIcon.Data = isPlaying
                ? (Geometry)this.FindResource("PauseIcon")!
                : (Geometry)this.FindResource("PlayIcon")!;
    }

    public void UpdatePosition(double positionSec, double durationSec)
    {
        _isUpdatingSeekFromExternal = true;
        try
        {
            if (PipSeekSlider != null && durationSec > 0)
            {
                var normalized = Math.Clamp(positionSec / durationSec, 0, 1);
                PipSeekSlider.Value = normalized * 1000;
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

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        RestoreState();
        SetupHoverTimer();

        // Apply aspect ratio constraint if set
        if (_aspectRatio > 0 && Width > 0 && Height > 0)
        {
            ApplyAspectRatioConstraint();
        }

        if (!TryEnableMirrorNow())
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

    // ═══════════════════════════════════════════════════════════════
    // HOVER AUTO-HIDE (2s for all controls + file badge)
    // ═══════════════════════════════════════════════════════════════

    private void SetupHoverTimer()
    {
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _hoverTimer.Tick += (_, _) =>
        {
            HoverOverlay.Opacity = 0;
            FileBadge.Opacity = 0;
            TitleBar.Opacity = 0;
            _hoverTimer?.Stop();
        };
    }

    public void ShowAllControls()
    {
        HoverOverlay.Opacity = 1;
        TitleBar.Opacity = 1;
        FileBadge.Opacity = 1;
    }

    /// <summary>Starts the hover auto-hide timer externally (e.g., from PipService after EnterPip).</summary>
    public void StartHoverTimer()
    {
        if (_hoverTimer == null)
            SetupHoverTimer();
        _hoverTimer?.Start();
    }

    private void ResetHoverTimer()
    {
        ShowAllControls();
        _hoverTimer?.Stop();
        _hoverTimer?.Start();
    }

    private void OnOverlayPointerEntered(object? sender, PointerEventArgs e) => ResetHoverTimer();
    private void OnOverlayPointerExited(object? sender, PointerEventArgs e) => _hoverTimer?.Start();

    // ═══════════════════════════════════════════════════════════════
    // PLAYER CONTROLS
    // ═══════════════════════════════════════════════════════════════

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        _isPlaying = !_isPlaying;
        SetPlayingState(_isPlaying);
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        ResetHoverTimer();
    }

    private void OnSeekSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_isUpdatingSeekFromExternal) return;
        if (e.Property == Slider.ValueProperty && PipSeekSlider != null)
        {
            var normalized = PipSeekSlider.Value / 1000.0;
            SeekRequested?.Invoke(this, normalized);
        }
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
        catch { /* Best-effort PiP state save */ }
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
        catch { /* Best-effort PiP state restore */ }
    }

    // ═══════════════════════════════════════════════════════════════
    // UI HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // Snap to edge after drag finishes (with a short delay to avoid flicker)
        _ = Dispatcher.UIThread.OnUiThreadAsync(async () =>
        {
            await Task.Delay(150);
            SnapToEdge();
        });
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnPinToggle(object? sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        UpdatePinIcon();
        SaveState();
    }

    private void UpdatePinIcon()
    {
        if (PinIcon != null)
            PinIcon.Opacity = _isPinned ? 1.0 : 0.4;
    }

    /// <summary>Snap window to nearest screen edge ( international PiP standard behavior)</summary>
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

        // Snap left edge
        if (Math.Abs(x - work.X) < snapThreshold)
            x = work.X;
        // Snap right edge
        else if (Math.Abs((x + Width) - (work.X + work.Width)) < snapThreshold)
            x = (int)(work.X + work.Width - Width);
        // Snap top edge
        if (Math.Abs(y - work.Y) < snapThreshold)
            y = work.Y;
        // Snap bottom edge
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
        if (e.Key == Key.Space)
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
    // EDGE RESIZE HANDLERS (transparent 8px strips on all edges)
    // ═══════════════════════════════════════════════════════════════

    private void OnTopEdgePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.North, e);
    }

    private void OnBottomEdgePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.South, e);
    }

    private void OnLeftEdgePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.West, e);
    }

    private void OnRightEdgePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.East, e);
    }

    private void OnTopLeftCornerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.NorthWest, e);
    }

    private void OnTopRightCornerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.NorthEast, e);
    }

    private void OnBottomLeftCornerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthWest, e);
    }

    private void OnBottomRightCornerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthEast, e);
    }
}
