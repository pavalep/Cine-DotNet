using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Cine.Avalonia.ViewModels;
using PointerEventArgs = Avalonia.Input.PointerEventArgs;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using PointerReleasedEventArgs = Avalonia.Input.PointerReleasedEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using SizeChangedEventArgs = Avalonia.Controls.SizeChangedEventArgs;

namespace Cine.Avalonia.Controls;

public partial class SeekBarControl : AvaloniaUserControl
{
    public void InitializeSeekBar()
    {
        if (SeekArea != null)
        {
            SeekArea.SizeChanged += OnSeekAreaSizeChanged;
            SeekArea.LayoutUpdated += OnSeekAreaLayoutUpdated;
        }
    }

    /// <summary>
    /// Called from MainWindow keyboard shortcut (T key) to toggle elapsed/remaining.
    /// </summary>
    public void ToggleTimeDisplay()
    {
        _showRemaining = !_showRemaining;
        UpdatePositionLabel();
    }

    public event EventHandler? SeekStarted;
    public event EventHandler? SeekEnded;
    public event EventHandler<double>? SeekWheelChanged;

    private MainViewModel? _viewModel;
    private bool _isSeeking;
    private double _lastSeekNormalized;
    private TimeSpan _lastPosition;
    private TimeSpan _lastDuration;
    private DateTime _lastSeekWheel = DateTime.MinValue;
    private DateTime _lastSeekMove = DateTime.MinValue;
    private const int SeekMoveDebounceMs = 16; // ~60fps

    private bool _showRemaining;
    private string _lastPositionText = "00:00:00";
    private string _lastDurationText = "00:00:00";
    private DateTime _lastSeekVisualUpdate = DateTime.MinValue;
    private bool _awaitingSeekSettle;
    private double _pendingSeekNormalized;
    private DateTime _pendingSeekStarted = DateTime.MinValue;
    private const int SeekSettleWindowMs = 400;
    private const double SeekSettleTolerance = 0.03;

    public SeekBarControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    public void UpdatePosition(TimeSpan position)
    {
        _lastPosition = position;
        if (_awaitingSeekSettle && _lastDuration.TotalSeconds > 0)
        {
            var elapsed = (DateTime.UtcNow - _pendingSeekStarted).TotalMilliseconds;
            var normalized = Math.Clamp(_lastPosition.TotalSeconds / _lastDuration.TotalSeconds, 0.0, 1.0);
            if (elapsed < SeekSettleWindowMs &&
                Math.Abs(normalized - _pendingSeekNormalized) > SeekSettleTolerance)
            {
                // Ignore stale pre-seek position events for a short window after release.
                return;
            }

            _awaitingSeekSettle = false;
        }
        if (!_isSeeking)
        {
            // Throttle seek bar visual updates to ~30fps
            var now = DateTime.UtcNow;
            if ((now - _lastSeekVisualUpdate).TotalMilliseconds >= 33)
            {
                _lastSeekVisualUpdate = now;
                UpdateSeekBar();
            }
        }
    }

    public void UpdateDuration(TimeSpan duration)
    {
        _lastDuration = duration;
        if (duration.TotalSeconds > 0)
        {
            UpdateSeekBar();
            UpdateChapterMarkers();
        }
    }

    public void UpdateTimeLabels(string positionText, string durationText)
    {
        _lastPositionText = positionText;
        _lastDurationText = durationText;
        DurationTimeLabel.Text = durationText;
        UpdatePositionLabel();
    }

    private void UpdatePositionLabel()
    {
        if (_showRemaining && _lastDuration.TotalSeconds > 0)
        {
            var remaining = _lastDuration - _lastPosition;
            if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;
            PositionTimeLabel.Text = "-" + FormatTimeSpan(remaining);
        }
        else
        {
            PositionTimeLabel.Text = _lastPositionText;
        }
    }

    public void SetPositionText(string text) => PositionTimeLabel.Text = text;
    public void SetDurationText(string text) => DurationTimeLabel.Text = text;

    public void ForceSeekUpdate(double normalized)
    {
        _lastSeekNormalized = normalized;
        UpdateSeekBar();
    }

    public void SetFontSize(double size)
    {
        PositionTimeLabel.FontSize = size;
        DurationTimeLabel.FontSize = size;
    }

    // --- Seek bar rendering ---

    private void OnSeekAreaSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateSeekBar();
        UpdateChapterMarkers();
    }

    private double _lastLayoutWidth;

    /// <summary>
    /// Fallback re-render when layout changes (catches cases where
    /// SizeChanged might not fire or width is temporarily 0).
    /// Only re-renders when width actually changes to avoid redundant work.
    /// </summary>
    private void OnSeekAreaLayoutUpdated(object? sender, EventArgs e)
    {
        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;
        if (Math.Abs(w - _lastLayoutWidth) < 1) return;
        _lastLayoutWidth = w;
        UpdateSeekBar();
        UpdateChapterMarkers();
    }

    /// <summary>
    /// Get the current normalized seek position (0..1).
    /// </summary>
    public double GetNormalizedPosition()
    {
        if (_isSeeking) return _lastSeekNormalized;
        if (_lastDuration.TotalSeconds <= 0) return 0;
        return Math.Clamp(_lastPosition.TotalSeconds / _lastDuration.TotalSeconds, 0.0, 1.0);
    }

    private double GetNormalizedFromPointer(global::Avalonia.Point p)
    {
        var w = SeekArea.Bounds.Width;
        if (w <= 0) return 0;
        var thumbWidth = SeekThumb.Bounds.Width;
        if (thumbWidth <= 0) thumbWidth = 20;
        var thumbHalf = thumbWidth / 2.0;

        var trackActiveWidth = w - thumbWidth;
        if (trackActiveWidth <= 0) return 0;

        return Math.Clamp((p.X - thumbHalf) / trackActiveWidth, 0.0, 1.0);
    }

    private void UpdateSeekBar()
    {
        if (_lastDuration.TotalSeconds <= 0) return;

        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;

        var seekValue = _isSeeking
            ? _lastSeekNormalized
            : Math.Clamp(_lastPosition.TotalSeconds / _lastDuration.TotalSeconds, 0.0, 1.0);

        var thumbWidth = SeekThumb.Bounds.Width;
        if (thumbWidth <= 0) thumbWidth = 20;
        var thumbHalf = thumbWidth / 2.0;

        var thumbLeft = seekValue * (w - thumbWidth);
        SeekThumb.Margin = new Thickness(thumbLeft, 0, 0, 0);

        SeekFill.Width = thumbLeft + thumbHalf;
    }

    public void UpdateChapterMarkers()
    {
        if (_viewModel == null || _viewModel.Chapters.Count == 0)
        {
            ChapterMarkersControl.IsVisible = false;
            return;
        }

        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;

        var container = ChapterMarkersControl.ItemsPanelRoot as Canvas;
        if (container == null) return;

        ChapterMarkersControl.IsVisible = true;

        var max = Math.Min(container.Children.Count, _viewModel.Chapters.Count);
        var duration = _viewModel.Duration.TotalSeconds;
        if (duration <= 0) return;

        for (int i = 0; i < max; i++)
        {
            var ch = _viewModel.Chapters[i];
            var pos = Math.Clamp(ch.Time / duration, 0.0, 1.0);
            Canvas.SetLeft(container.Children[i], pos * w);

            // Tooltip: chapter title + time
            var timeStr = TimeSpan.FromSeconds(ch.Time).ToString(@"hh\:mm\:ss");
            var tip = !string.IsNullOrWhiteSpace(ch.Title)
                ? $"{ch.Title} ({timeStr})"
                : timeStr;
            global::Avalonia.Controls.ToolTip.SetTip(container.Children[i], tip);
        }
    }

    // --- Seek event handlers ---

    private void OnSeekAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null || _viewModel.Duration.TotalSeconds <= 0) return;

        var p = e.GetPosition(SeekArea);
        _lastSeekNormalized = GetNormalizedFromPointer(p);

        _isSeeking = true;
        _awaitingSeekSettle = false;
        _viewModel.IsSeeking = true;
        e.Pointer.Capture(SeekArea);
        SeekStarted?.Invoke(this, EventArgs.Empty);
        // Update visual immediately
        UpdateSeekBar();
        e.Handled = true;
    }

    private void OnSeekAreaPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeeking) return;
        var targetNormalized = _lastSeekNormalized;

        // Force visual update to seek position BEFORE clearing _isSeeking
        // This prevents the thumb from snapping back to the old _lastPosition
        // while waiting for the next PositionChanged event
        UpdateSeekBar();
        _isSeeking = false;
        _awaitingSeekSettle = true;
        _pendingSeekNormalized = targetNormalized;
        _pendingSeekStarted = DateTime.UtcNow;

        // Perform the actual seek only on release (not every mouse move)
        if (_viewModel != null)
        {
            _viewModel.SeekTo(targetNormalized);
            _viewModel.IsSeeking = false;
        }

        e.Pointer.Capture(null);
        SeekEnded?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnSeekAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel == null) return;

        // Debounce to ~60fps to prevent excessive UI updates
        var now = DateTime.UtcNow;
        if ((now - _lastSeekMove).TotalMilliseconds < SeekMoveDebounceMs)
            return;
        _lastSeekMove = now;

        var p = e.GetPosition(SeekArea);
        var normalized = GetNormalizedFromPointer(p);

        if (_isSeeking)
        {
            _lastSeekNormalized = normalized;
            // Visual-only update during drag. Actual seek happens on PointerReleased.
            UpdateSeekBar();
        }

        // Chapter preview popover on hover
        if (_viewModel.Duration.TotalSeconds > 0)
        {
            var seconds = normalized * _viewModel.Duration.TotalSeconds;
            var chapter = _viewModel.Chapters.Count > 0
                ? _viewModel.Chapters
                    .Where(c => c.Time <= seconds)
                    .OrderByDescending(c => c.Time)
                    .FirstOrDefault()
                : null;

            if (chapter != null && Math.Abs(seconds - chapter.Time) < 3.0)
                ChapterPreviewText.Text = $"{chapter.Title}  ({FormatChapterTime(seconds)})";
            else
                ChapterPreviewText.Text = FormatChapterTime(seconds);

            ChapterPreviewPopover.IsVisible = true;
            ChapterPreviewPopover.Measure(new AvaloniaSize(double.PositiveInfinity, double.PositiveInfinity));
            var trackWidth = SeekArea.Bounds.Width;
            var thumbWidth = SeekThumb.Bounds.Width;
            if (thumbWidth <= 0) thumbWidth = 20;
            var thumbHalf = thumbWidth / 2.0;
            var thumbCenter = normalized * (trackWidth - thumbWidth) + thumbHalf;

            var popoverWidth = ChapterPreviewPopover.DesiredSize.Width;
            var minPopoverWidth = ChapterPreviewPopover.MinWidth > 0
                ? ChapterPreviewPopover.MinWidth
                : 80;
            var safeMaxWidth = trackWidth * 0.65;
            var boundedPopoverWidth = Math.Max(minPopoverWidth, Math.Min(popoverWidth, safeMaxWidth));

            // Clamp position to stay within seek bar bounds with a small margin
            var marginPx = 6.0;
            var clampedWidth = Math.Max(marginPx, boundedPopoverWidth);
            var xPos = thumbCenter - (clampedWidth / 2);
            xPos = Math.Clamp(xPos, marginPx, Math.Max(marginPx, trackWidth - clampedWidth - marginPx));

            ChapterPreviewPopover.Width = clampedWidth;
            // Compute Y offset from popover height + gap above seek bar
            var popoverHeight = ChapterPreviewPopover.DesiredSize.Height;
            var yOffset = -(popoverHeight > 0 ? popoverHeight + 4 : 34);
            ChapterPreviewPopover.Margin = new Thickness(xPos, yOffset, 0, 0);
        }
    }

    private void OnSeekAreaPointerExited(object? sender, PointerEventArgs e)
    {
        ChapterPreviewPopover.IsVisible = false;
    }

    private void OnSeekAreaPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSeekWheel).TotalMilliseconds < 90)
            return;
        _lastSeekWheel = now;

        var delta = e.Delta.Y > 0 ? 1.0 : -1.0;
        SeekWheelChanged?.Invoke(this, delta);
        e.Handled = true;
    }

    // --- Time label toggle ---

    private void OnPositionTimeLabelPressed(object? sender, PointerPressedEventArgs e)
    {
        _showRemaining = !_showRemaining;
        UpdatePositionLabel();
        e.Handled = true;
    }

    private static string FormatChapterTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(ts.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }

    public static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero)
            return "-" + FormatTimeSpan(TimeSpan.FromTicks(-ts.Ticks));
        if (ts.TotalHours >= 1)
            return ts.ToString("h\\:mm\\:ss");
        return ts.ToString("mm\\:ss");
    }
}
