namespace Cine.Avalonia.Models;

/// <summary>
/// Persistent data for a single recent-file entry.
/// Stored as JSON in the recent-files store.
/// </summary>
public sealed record RecentFileEntry
{
    public string FilePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string LastOpened { get; init; } = string.Empty;
    public string? ThumbnailPath { get; init; }
    public long PositionTicks { get; init; }
    public long DurationTicks { get; init; }

    // Display helpers used by OpenMenuPanel
    public string LastOpenedFormatted => FormatRelativeTime(LastOpened);

    private static string FormatRelativeTime(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return string.Empty;
        if (!DateTime.TryParse(isoDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return string.Empty;

        var span = DateTime.Now - dt;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM d");
    }
}
