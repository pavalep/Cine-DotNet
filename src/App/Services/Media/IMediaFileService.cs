using System.Collections.Generic;

namespace Simba.Avalonia.Services;

/// <summary>
/// Validates media files and provides file-system utilities for media operations.
/// </summary>
public interface IMediaFileService
{
    /// <summary>Check whether a path points to a valid, supported media file.</summary>
    bool IsValidMediaFile(string path);

    /// <summary>Filter an array of paths to only supported media files.</summary>
    string[] FilterMediaFiles(string[] paths);

    /// <summary>Generate a unique screenshot file path in the Pictures folder.</summary>
    string GenerateScreenshotPath();

    /// <summary>Check whether the file has a supported media extension (no I/O).</summary>
    bool IsMediaFile(string path);

    /// <summary>Check whether the file has a supported video extension (no I/O).</summary>
    bool IsVideoFile(string path);

    /// <summary>The set of all supported media extensions (lower-case, including dot).</summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>The set of supported video extensions (lower-case, including dot).</summary>
    IReadOnlySet<string> VideoExtensions { get; }

    /// <summary>Recursively scan a folder for supported media files.</summary>
    List<string> ScanFolderForMedia(string folderPath);

    /// <summary>Recursively scan a folder for supported media files on a background thread.</summary>
    Task<string[]> ScanFolderAsync(string folder, CancellationToken ct = default);

    /// <summary>Sort paths in natural order (numeric segments compared by value).</summary>
    string[] NaturalSort(IEnumerable<string> paths);
}
