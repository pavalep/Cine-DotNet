using System;

namespace Simba.Avalonia.Services;

/// <summary>
/// Parses human-readable time strings into <see cref="TimeSpan"/>.
/// Supports HH:MM:SS, MM:SS, bare seconds, and trimmed input.
/// Returns null for invalid/negative/overflow input.
/// </summary>
public static class TimeParsingUtility
{
    /// <summary>
    /// Try to parse a time string. Examples:
    ///   "90"     → 00:01:30
    ///   "5:30"   → 00:05:30
    ///   "1:23:45" → 01:23:45
    ///   "abc"    → null
    ///   " 1:30 " → 00:01:30 (trimmed)
    ///   null/empty/whitespace → null
    /// </summary>
    public static TimeSpan? TryParseTime(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        var parts = input.Split(':');

        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out var h) &&
                int.TryParse(parts[1], out var m) &&
                int.TryParse(parts[2], out var s))
            {
                if (h < 0 || m < 0 || s < 0) return null;
                if (m >= 60 || s >= 60) return null;
                try { return new TimeSpan(h, m, s); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
        }
        else if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var m) &&
                int.TryParse(parts[1], out var s))
            {
                if (m < 0 || s < 0) return null;
                if (s >= 60) return null;
                try { return new TimeSpan(0, m, s); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
        }
        else if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], out var sec))
            {
                if (sec < 0) return null;
                try { return TimeSpan.FromSeconds(sec); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
        }

        return null;
    }
}
