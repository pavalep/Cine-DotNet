using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
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
            SeekArea.SizeChanged += OnSeekAreaSizeChanged;
    }

    public event EventHandler<double>? SeekRequested;
    public event EventHandler? SeekStarted;
    public event EventHandler? SeekEnded;
    public event EventHandler<double>? SeekWheelChanged;

    private MainViewModel? _viewModel;
    private bool _isSeeking;
    private double _lastSeekNormalized;
    private TimeSpan _lastPosition;
    private TimeSpan _lastDuration;
    private DateTime _lastSeekWheel = DateTime.MinValue;
    private DateTime _lastTapTime = DateTime.MinValue;

    private const double SeekThumbHalf = 8.0;

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
        if (!_isSeeking)
            UpdateSeekBar();
    }

    public void UpdateDuration(TimeSpan duration)
    {
        _lastDuration = duration;
        UpdateSeekBar();
        UpdateChapterMarkers();
    }

    public void UpdateTimeLabels(string positionText, string durationText)
    {
        PositionTimeLabel.Text = positionText;
        DurationTimeLabel.Text = durationText;
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

    private void UpdateSeekBar()
    {
        if (_lastDuration.TotalSeconds <= 0) return;

        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;

        var seekValue = _isSeeking
            ? _lastSeekNormalized
            : Math.Clamp(_lastPosition.TotalSeconds / _lastDuration.TotalSeconds, 0.0, 1.0);

        var fillWidth = seekValue * w;
        SeekFill.Width = fillWidth;

        var thumbLeft = seekValue * w - SeekThumbHalf;
        SeekThumb.Margin = new Thickness(thumbLeft, 0, 0, 0);
    }

    public void UpdateChapterMarkers()
    {
        if (_viewModel == null || _viewModel.Chapters.Count == 0)
            return;

        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;

        var container = ChapterMarkersControl.ItemsPanelRoot as Canvas;
        if (container == null) return;

        for (int i = 0; i < container.Children.Count && i < _viewModel.Chapters.Count; i++)
        {
            var ch = _viewModel.Chapters[i];
            var pos = _viewModel.Duration.TotalSeconds > 0
                ? ch.Time / _viewModel.Duration.TotalSeconds
                : 0.0;
            Canvas.SetLeft(container.Children[i], pos * w);
        }
    }

    // --- Seek event handlers ---

    private void OnSeekAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null || _viewModel.Duration.TotalSeconds <= 0) return;

        var p = e.GetPosition(SeekArea);
        var trackWidth = Math.Max(1.0, SeekArea.Bounds.Width);
        _lastSeekNormalized = Math.Clamp(p.X / trackWidth, 0, 1);

        _isSeeking = true;
        _viewModel.IsSeeking = true;
        SeekStarted?.Invoke(this, EventArgs.Empty);
        _viewModel.SeekTo(_lastSeekNormalized);
        UpdateSeekBar();
        _lastTapTime = DateTime.MinValue;
        e.Handled = true;
    }

    private void OnSeekAreaPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeeking) return;
        _isSeeking = false;
        if (_viewModel != null)
            _viewModel.IsSeeking = false;
        SeekEnded?.Invoke(this, EventArgs.Empty);
        UpdateSeekBar();
        e.Handled = true;
    }

    private void OnSeekAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel == null) return;

        var p = e.GetPosition(SeekArea);
        var trackWidth = Math.Max(1.0, SeekArea.Bounds.Width);
        var normalized = Math.Clamp(p.X / trackWidth, 0, 1);

        if (_isSeeking)
        {
            _lastSeekNormalized = normalized;
            _viewModel.SeekTo(normalized);
            UpdateSeekBar();
        }

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
            var popoverWidth = ChapterPreviewPopover.DesiredSize.Width;
            var xPos = (normalized * trackWidth) - (popoverWidth / 2);
            xPos = Math.Clamp(xPos, 4, Math.Max(4, SeekArea.Bounds.Width - popoverWidth - 4));
            ChapterPreviewPopover.Margin = new Thickness(xPos, -34, 0, 0);
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

    private static string FormatChapterTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(ts.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }

    public static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero)
            return "-" + TimeSpan.FromTicks(-ts.Ticks).ToString("hh\\:mm\\:ss");
        return ts.ToString("hh\\:mm\\:ss");
    }
}
