using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Processes drag-and-drop file operations by delegating to <see cref="IMediaFileService"/>
/// for folder scanning and file validation.
/// </summary>
public sealed class DragDropService : IDragDropService
{
    private readonly IMediaFileService _mediaFile;

    public DragDropService(IMediaFileService mediaFile)
    {
        _mediaFile = mediaFile ?? throw new ArgumentNullException(nameof(mediaFile));
    }

    /// <inheritdoc/>
    public async Task<string[]> ProcessDroppedFilesAsync(string[]? paths, CancellationToken ct = default)
    {
        if (paths == null || paths.Length == 0) return Array.Empty<string>();

        var allFiles = new List<string>();

        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(path))
                {
                    // Folder — scan recursively on background thread
                    var folderFiles = await _mediaFile.ScanFolderAsync(path, ct);
                    allFiles.AddRange(folderFiles);
                }
                else if (File.Exists(path) && _mediaFile.IsMediaFile(path))
                {
                    allFiles.Add(path);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.ForContext<DragDropService>().Error(ex, "Failed to process dropped path: {Path}", path);
                // Continue with other paths — don't let one bad path block the whole batch
            }
        }

        // Naturally sort the deduplicated results
        return _mediaFile.NaturalSort(allFiles.Distinct());
    }
}
