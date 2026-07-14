using System;
using System.Globalization;
using System.IO;

namespace Simba.Avalonia.Models;

/// <summary>
/// Lightweight display model for a recent file shown in the StartPage card list.
/// Properties are pre-computed once at construction so the DataTemplate needs no converters.
/// </summary>
public sealed class RecentFileItem
{
    public string FilePath  { get; }
    public string Title     { get; }
    public string Extension { get; }
    public bool   IsVideo   { get; }

    /// <summary>ISO 8601 timestamp of when the file was last opened.</summary>
    public string LastOpened { get; }

    /// <summary>Human-readable relative time (e.g. "2h ago", "3d ago").</summary>
    public string LastOpenedFormatted { get; }

    /// <summary>Path to a saved thumbnail screenshot, or null.</summary>
    public string? ThumbnailPath { get; }

    /// <summary>True if this file has a saved thumbnail.</summary>
    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailPath);

    /// <summary>True if we have a saved playback position for resume.</summary>
    public bool HasResumePosition { get; }

    /// <summary>Saved playback position in ticks for the "Continue" feature.</summary>
    public long ResumePositionTicks { get; }

    /// <summary>Total media duration in ticks, if known.</summary>
    public long DurationTicks { get; }

    public bool HasDuration => DurationTicks > 0;

    /// <summary>Formatted playback info like "01:12 / 24:55".</summary>
    public string PlaybackTimeText { get; }

    public RecentFileItem(string filePath, bool isVideo,
                          string lastOpened = "",
                          string? thumbnailPath = null,
                          long resumePositionTicks = 0,
                          long durationTicks = 0)
    {
        FilePath  = filePath;
        Title     = Path.GetFileNameWithoutExtension(filePath);
        Extension = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        IsVideo   = isVideo;
        LastOpened = lastOpened;
        ThumbnailPath = thumbnailPath;
        ResumePositionTicks = resumePositionTicks;
        DurationTicks = durationTicks;
        HasResumePosition = resumePositionTicks > 0;

        LastOpenedFormatted = FormatRelativeTime(lastOpened);
        PlaybackTimeText = FormatPlaybackTime(resumePositionTicks, durationTicks);
    }

    private static string FormatRelativeTime(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate))
            return string.Empty;

        if (!DateTime.TryParse(isoDate, null, DateTimeStyles.RoundtripKind, out var dt))
            return string.Empty;

        var span = DateTime.Now - dt;

        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24)  return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7)    return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM d");
    }

    private static string FormatPlaybackTime(long positionTicks, long durationTicks)
    {
        if (positionTicks <= 0 && durationTicks <= 0)
            return string.Empty;

        if (durationTicks > 0)
            return $"{FormatClock(positionTicks)} / {FormatClock(durationTicks)}";

        return FormatClock(positionTicks);
    }

    private static string FormatClock(long ticks)
    {
        var time = TimeSpan.FromTicks(Math.Max(0, ticks));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }
}
