namespace Cine.Avalonia.Services;

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
}
