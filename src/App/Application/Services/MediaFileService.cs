using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cine.Avalonia.Services;

/// <summary>
/// Validates media files and provides file-system utilities for media operations.
/// </summary>
public class MediaFileService : IMediaFileService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".mts", ".m2ts",
        ".vob", ".ogv", ".asf", ".divx", ".f4v", ".rm", ".rmvb"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".aac", ".flac", ".ogg", ".wav", ".wma", ".m4a", ".opus"
    };

    private static readonly HashSet<string> SupportedExtensions = new(
        VideoExtensions.Concat(AudioExtensions), StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsValidMediaFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public string[] FilterMediaFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return Array.Empty<string>();

        return paths.Where(IsValidMediaFile).ToArray();
    }

    /// <inheritdoc/>
    public string GenerateScreenshotPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(dir, $"cine_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }
}
