using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
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
        if (_dwmManager != null || _thumbnailId > 0) return;

        _dwmManager = manager;

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        _thumbnailId = manager.RegisterTarget(handle);
        SyncThumbnailRect();
    }

    public void DisableDwmMirror()
    {
        if (_dwmManager != null && _thumbnailId > 0)
        {
            _dwmManager.UnregisterTarget(_thumbnailId);
            _thumbnailId = 0;
            _dwmManager = null;
        }
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

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SyncThumbnailRect();
    }

    /// <summary>Sets the target aspect ratio for resize locking.</summary>
    public void SetAspectRatio(double ar)
    {
        if (ar > 0) _aspectRatio = ar;
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

        if (_dwmManager != null && _thumbnailId == 0)
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                _thumbnailId = _dwmManager.RegisterTarget(handle);
                SyncThumbnailRect();
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        _hoverTimer?.Stop();
        _hoverTimer = null;
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
