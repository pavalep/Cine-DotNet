namespace Simba.Media.Events;

/// <summary>
/// Event args for file operations - matches Python's @mpv.event("start-file"), "file-loaded", "end-file"
/// </summary>
public class MediaEventArgs : EventArgs
{
    /// <summary>
    /// File path being loaded or changed
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Error message if file operation failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Creates MediaEventArgs with file path
    /// </summary>
    /// <param name="filePath">The file path</param>
    public MediaEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}