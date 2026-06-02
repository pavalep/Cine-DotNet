using System;
using System.Linq;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void OnSeekAreaSizeChanged(object? sender, global::Avalonia.Controls.SizeChangedEventArgs e)
    {
        UpdateSeekBar();
        UpdateChapterMarkers();
    }

    private void UpdateSeekBar()
    {
        if (SeekArea == null || SeekFill == null || SeekThumb == null) return;

        if (_lastDuration.TotalSeconds <= 0) return;

        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;

        var seekValue = _isSeeking
            ? _lastSeekNormalized
            : Math.Clamp(_lastPosition.TotalSeconds / _lastDuration.TotalSeconds, 0.0, 1.0);

        var fillWidth = seekValue * w;
        SeekFill.Width = fillWidth;

        var thumbLeft = seekValue * w - SeekThumbHalf;
        SeekThumb.Margin = new global::Avalonia.Thickness(thumbLeft, 0, 0, 0);
    }

    private void UpdateChapterMarkers()
    {
        if (ChapterMarkersControl == null || SeekArea == null ||
            _viewModel == null || _viewModel.Chapters.Count == 0)
            return;

        var w = SeekArea.Bounds.Width;
        if (w <= 0) return;

        var container = ChapterMarkersControl.ItemsPanelRoot as global::Avalonia.Controls.Canvas;
        if (container == null) return;

        for (int i = 0; i < container.Children.Count && i < _viewModel.Chapters.Count; i++)
        {
            var ch = _viewModel.Chapters[i];
            var pos = _viewModel.Duration.TotalSeconds > 0
                ? ch.Time / _viewModel.Duration.TotalSeconds
                : 0.0;
            global::Avalonia.Controls.Canvas.SetLeft(container.Children[i], pos * w);
        }
    }

    private static string FormatTimeSpan(global::System.TimeSpan ts)
    {
        if (ts < global::System.TimeSpan.Zero)
            return "-" + global::System.TimeSpan.FromTicks(-ts.Ticks).ToString("hh\\:mm\\:ss");
        return ts.ToString("hh\\:mm\\:ss");
    }

    private void OnSeekAreaPointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_viewModel == null || SeekArea == null || _viewModel.Duration.TotalSeconds <= 0) return;

        var p = e.GetPosition(SeekArea);
        var trackWidth = Math.Max(1.0, SeekArea.Bounds.Width);
        _lastSeekNormalized = Math.Clamp(p.X / trackWidth, 0, 1);

        _isSeeking = true;
        _viewModel.IsSeeking = true;
        _viewModel.SeekTo(_lastSeekNormalized);
        UpdateSeekBar();
        _lastTapTime = DateTime.MinValue;
        e.Handled = true;
    }

    private void OnSeekAreaPointerReleased(object? sender, global::Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (!_isSeeking) return;
        _isSeeking = false;
        if (_viewModel != null)
        {
            _viewModel.IsSeeking = false;
        }
        UpdateSeekBar();
        e.Handled = true;
    }

    private void OnSeekAreaPointerMoved(object? sender, global::Avalonia.Input.PointerEventArgs e)
    {
        if (_viewModel == null || SeekArea == null) return;

        var p = e.GetPosition(SeekArea);
        var trackWidth = Math.Max(1.0, SeekArea.Bounds.Width);
        var normalized = Math.Clamp(p.X / trackWidth, 0, 1);

        if (_isSeeking)
        {
            _lastSeekNormalized = normalized;
            _viewModel.SeekTo(normalized);
            UpdateSeekBar();
        }

        if (_viewModel.Duration.TotalSeconds > 0 &&
            ChapterPreviewPopover != null && ChapterPreviewText != null)
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
            ChapterPreviewPopover.Measure(new global::Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
            var popoverWidth = ChapterPreviewPopover.DesiredSize.Width;
            var xPos = (normalized * trackWidth) - (popoverWidth / 2);
            xPos = Math.Clamp(xPos, 4, Math.Max(4, SeekArea.Bounds.Width - popoverWidth - 4));
            ChapterPreviewPopover.Margin = new global::Avalonia.Thickness(xPos, -34, 0, 0);
        }
    }

    private void OnSeekAreaPointerExited(object? sender, global::Avalonia.Input.PointerEventArgs e)
    {
        if (ChapterPreviewPopover != null)
            ChapterPreviewPopover.IsVisible = false;
    }

    private void OnSeekAreaPointerWheelChanged(object? sender, global::Avalonia.Input.PointerWheelEventArgs e)
    {
        if (_viewModel == null) return;

        var now = DateTime.UtcNow;
        if ((now - _lastSeekWheel).TotalMilliseconds < 90)
            return;
        _lastSeekWheel = now;

        if (e.Delta.Y > 0)
            _viewModel.SeekForward();
        else if (e.Delta.Y < 0)
            _viewModel.SeekBackward();
        e.Handled = true;
    }

    private static string FormatChapterTime(double seconds)
    {
        var ts = global::System.TimeSpan.FromSeconds(seconds);
        return ts.ToString(ts.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
    }
}
