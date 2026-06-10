using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Material.Icons;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PipOverlayWindow : Window
{
    private bool _isPlaying = true;
    private bool _isEnded;
    private bool _isMuted;
    private bool _isSeeking;
    private double _seekNormalized;
    private bool _controlsVisible = true;

    // Auto-hide
    private DispatcherTimer? _hoverTimer;
    private bool _hoverTopBar;
    private bool _hoverCenter;
    private bool _hoverBottomBar;

    // Events forwarded to PipWindow
    public event EventHandler? PlayPauseRequested;
    public event EventHandler<double>? SeekRequested;
    public event EventHandler? MuteToggled;
    public event EventHandler? CloseRequested;
    public event EventHandler? ExpandRequested;
    public event EventHandler<bool>? PinToggled;

    public PipOverlayWindow()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        InitializeComponent();
        SetupHoverTimer();
        ShowControls();
    }

    // ═══ Auto-hide ═══

    private void SetupHoverTimer()
    {
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
        _hoverTimer.Tick += (_, _) =>
        {
            if (_hoverTopBar || _hoverCenter || _hoverBottomBar) return;
            HideControls();
        };
    }

    public void ShowControls()
    {
        _controlsVisible = true;
        _hoverTimer?.Stop();
        Opacity = 1;
        IsHitTestVisible = true;
        if (SeekThumb != null) SeekThumb.IsVisible = true;
        if (FileBadge != null) FileBadge.IsVisible = false;
        _hoverTimer?.Start();
    }

    public void HideControls()
    {
        _controlsVisible = false;
        _hoverTimer?.Stop();
        Opacity = 0;
        IsHitTestVisible = false;
        if (FileBadge != null)
        {
            FileBadge.IsVisible = true;
            FileBadge.Opacity = 1;
        }
    }

    public void StartAutoHide() { _hoverTimer?.Start(); }

    private void ResetHoverTimer() { _hoverTimer?.Stop(); _hoverTimer?.Start(); }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_controlsVisible) ShowControls();
        ResetHoverTimer();
    }

    private void OnTopBarPointerEntered(object? sender, PointerEventArgs e) => _hoverTopBar = true;
    private void OnTopBarPointerExited(object? sender, PointerEventArgs e) => _hoverTopBar = false;
    private void OnCenterPointerEntered(object? sender, PointerEventArgs e) => _hoverCenter = true;
    private void OnCenterPointerExited(object? sender, PointerEventArgs e) => _hoverCenter = false;
    private void OnBottomBarPointerEntered(object? sender, PointerEventArgs e) => _hoverBottomBar = true;
    private void OnBottomBarPointerExited(object? sender, PointerEventArgs e) => _hoverBottomBar = false;

    // ═══ Synced to overlay window position/size ═══

    public void SyncGeometry(PixelPoint pipPosition, double pipWidth, double pipHeight)
    {
        Position = pipPosition;
        Width = pipWidth;
        Height = pipHeight;
    }

    // ═══ State setters ═══

    public void SetPlayingState(bool isPlaying)
    {
        _isPlaying = isPlaying;
        if (PlayPauseIcon == null) return;
        if (_isEnded) PlayPauseIcon.Kind = MaterialIconKind.Replay;
        else PlayPauseIcon.Kind = isPlaying ? MaterialIconKind.Pause : MaterialIconKind.Play;
    }

    public void SetReplayMode(bool showReplay)
    {
        _isEnded = showReplay;
        if (showReplay && PlayPauseIcon != null) PlayPauseIcon.Kind = MaterialIconKind.Replay;
        else if (!showReplay) SetPlayingState(_isPlaying);
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        if (MuteIcon != null)
            MuteIcon.Kind = muted ? MaterialIconKind.VolumeOff : MaterialIconKind.VolumeHigh;
    }

    public void SetPinned(bool pinned)
    {
        if (PinIcon != null) PinIcon.Opacity = pinned ? 1.0 : 0.4;
    }

    public void SetFileName(string fileName, string subtitle)
    {
        if (FileNameLabel != null) FileNameLabel.Text = fileName;
        if (FileSubtitleLabel != null) FileSubtitleLabel.Text = subtitle;
        if (BadgeLabel != null) BadgeLabel.Text = fileName;
    }

    public void UpdatePosition(double positionSec, double durationSec)
    {
        if (durationSec > 0 && !_isSeeking)
        {
            _seekNormalized = Math.Clamp(positionSec / durationSec, 0, 1);
            UpdateSeekVisuals(_seekNormalized);
        }

        if (TimeLabel != null)
        {
            var pos = TimeSpan.FromSeconds(positionSec);
            var dur = TimeSpan.FromSeconds(durationSec);
            TimeLabel.Text = $"{(int)pos.TotalMinutes:D2}:{pos.Seconds:D2} / {(int)dur.TotalMinutes:D2}:{dur.Seconds:D2}";
        }
    }

    // ═══ Control handlers ═══

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
        PinToggled?.Invoke(this,
            PinIcon?.Opacity < 0.7);
        ResetHoverTimer();
    }

    private void OnExpandClick(object? sender, RoutedEventArgs e) => ExpandRequested?.Invoke(this, EventArgs.Empty);
    private void OnCloseClick(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    // ═══ Seek bar ═══

    private double GetNormalized(PointerEventArgs e)
    {
        if (SeekArea == null) return 0;
        double pos = e.GetCurrentPoint(SeekArea).Position.X;
        double w = SeekArea.Bounds.Width > 0 ? SeekArea.Bounds.Width : SeekArea.DesiredSize.Width;
        return w > 0 ? Math.Clamp(pos / w, 0, 1) : 0;
    }

    private void OnSeekPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isSeeking = true;
        _seekNormalized = GetNormalized(e);
        UpdateSeekVisuals(_seekNormalized);
        if (SeekPreviewDot != null) SeekPreviewDot.IsVisible = false;
        e.Pointer.Capture(SeekArea);
    }

    private void OnSeekPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeeking) return;
        _isSeeking = false;
        SeekRequested?.Invoke(this, _seekNormalized);
        e.Pointer.Capture(null);
        ResetHoverTimer();
    }

    private void OnSeekPointerMoved(object? sender, PointerEventArgs e)
    {
        double n = GetNormalized(e);
        if (_isSeeking)
        {
            _seekNormalized = n;
            UpdateSeekVisuals(n);
        }
        else if (SeekPreviewDot != null && SeekArea != null)
        {
            SeekPreviewDot.IsVisible = true;
            double aw = SeekArea.Bounds.Width > 0 ? SeekArea.Bounds.Width : SeekArea.DesiredSize.Width;
            SeekPreviewDot.Margin = new Thickness(n * (aw - 10), 0, 0, 0);
        }
        ResetHoverTimer();
    }

    private void OnSeekPointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isSeeking && SeekPreviewDot != null)
            SeekPreviewDot.IsVisible = false;
    }

    private void UpdateSeekVisuals(double normalized)
    {
        if (SeekArea == null || SeekFill == null || SeekThumb == null) return;
        double aw = SeekArea.Bounds.Width > 0 ? SeekArea.Bounds.Width : SeekArea.DesiredSize.Width;
        if (aw <= 0) return;
        double fill = normalized * (aw - 14);
        SeekFill.Width = Math.Max(0, fill);
        SeekThumb.Margin = new Thickness(fill, 0, 0, 0);
        SeekThumb.IsVisible = _isSeeking || _controlsVisible;
    }

    // ═══ Lifecycle ═══

    protected override void OnClosed(EventArgs e)
    {
        _hoverTimer?.Stop();
        _hoverTimer = null;
        base.OnClosed(e);
    }
}
