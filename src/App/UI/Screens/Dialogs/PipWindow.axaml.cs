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
        VideoArea.PointerPressed += OnVideoAreaPointerPressed;
        KeyDown += OnKeyDown;

        PipSeekSlider.PropertyChanged += OnSeekSliderChanged;
    }

    // ═══════════════════════════════════════════════════════════════
    // DWM THUMBNAIL MIRROR
    // ═══════════════════════════════════════════════════════════════

    public void EnableDwmMirror(DwmThumbnailManager manager)
    {
        _dwmManager = manager;

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        _thumbnailId = manager.RegisterTarget(handle);
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

    // ═══════════════════════════════════════════════════════════════
    // PLAYBACK SYNC (called externally)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Update the play/pause icon state from the main player.</summary>
    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        if (PlayPauseIcon != null)
            PlayPauseIcon.Data = isPlaying
                ? (Geometry)this.FindResource("PauseIcon")!
                : (Geometry)this.FindResource("PlayIcon")!;
    }

    /// <summary>Update position and duration display.</summary>
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

        // Register DWM thumbnail if manager was set but handle wasn't available yet
        if (_dwmManager != null && _thumbnailId == 0)
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
                _thumbnailId = _dwmManager.RegisterTarget(handle);
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
    // HOVER AUTO-HIDE
    // ═══════════════════════════════════════════════════════════════

    private void SetupHoverTimer()
    {
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hoverTimer.Tick += (_, _) =>
        {
            if (HoverOverlay != null)
                HoverOverlay.Opacity = 0;
            // Auto-hide title bar too — saves vertical space in PIP
            if (TitleBar != null)
                TitleBar.Opacity = 0;
            _hoverTimer?.Stop();
        };
    }

    private void OnHoverOverlayPointerEntered(object? sender, PointerEventArgs e)
    {
        // Show title bar and controls on hover
        if (TitleBar != null)
            TitleBar.Opacity = 1;
        _hoverTimer?.Stop();
    }

    private void OnHoverOverlayPointerExited(object? sender, PointerEventArgs e)
    {
        _hoverTimer?.Start();
    }

    // ═══════════════════════════════════════════════════════════════
    // PLAYER CONTROLS
    // ═══════════════════════════════════════════════════════════════

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        _isPlaying = !_isPlaying;
        SetPlayingState(_isPlaying);
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);

        // Reset hover timer after interaction
        _hoverTimer?.Stop();
        _hoverTimer?.Start();
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
    // UI HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnVideoAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Click on video area toggles play/pause
        OnPlayPauseClick(sender, e);
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
}
