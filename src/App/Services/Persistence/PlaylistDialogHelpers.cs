using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Simba.Avalonia.ViewModels;

namespace Simba.Avalonia.Services;

/// <summary>
/// Helpers for PlaylistDialog logic — search filtering and M3U export.
/// Extracted from code-behind for testability.
/// </summary>
public static class PlaylistDialogHelpers
{
    /// <summary>
    /// Apply search filter to playlist items. Returns true if any match.
    /// Items without a match are set IsVisible = false.
    /// </summary>
    public static bool ApplySearchFilter(IEnumerable<PlaylistItemViewModel> items, string filter)
    {
        filter = filter.Trim().ToLowerInvariant();
        var anyVisible = false;

        foreach (var item in items)
        {
            var matches = string.IsNullOrEmpty(filter) || item.Title.ToLowerInvariant().Contains(filter);
            item.IsVisible = matches;
            if (matches) anyVisible = true;
        }

        return anyVisible;
    }

    /// <summary>
    /// Export playlist items as M3U format to a file.
    /// </summary>
    public static async Task ExportToM3UAsync(IEnumerable<PlaylistItemViewModel> items, string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenWrite(filePath);
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync("#EXTM3U".AsMemory(), ct);
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"#EXTINF:0,{item.Title}".AsMemory(), ct);
            await writer.WriteLineAsync(item.FilePath.AsMemory(), ct);
        }
    }
}
