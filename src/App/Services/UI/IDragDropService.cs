using System.Threading;
using System.Threading.Tasks;

namespace Simba.Avalonia.Services;

/// <summary>Service for processing drag-and-drop file operations.</summary>
public interface IDragDropService
{
    /// <summary>
    /// Process dropped files/folders: folders are scanned recursively for media,
    /// files are filtered by the media extension registry. Returns the naturally sorted
    /// list of valid media file paths (empty if none).
    /// </summary>
    Task<string[]> ProcessDroppedFilesAsync(string[]? paths, CancellationToken ct = default);
}
