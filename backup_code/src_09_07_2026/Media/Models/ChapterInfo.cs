namespace Cine.Media.Models;

/// <summary>
/// Chapter information - matches Python's chapter list from mpv chapter property
/// </summary>
public class ChapterInfo
{
    /// <summary>
    /// Chapter title/ name
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Chapter index/ number (0-based)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Chapter start time in seconds
    /// </summary>
    public double Time { get; set; }

    /// <summary>
    /// Returns chapter info as formatted string
    /// </summary>
    /// <returns>Formatted chapter string</returns>
    public override string ToString()
    {
        return $"Chapter {Index}: {Title} ( starts at {Time:F2}s)";
    }
}
