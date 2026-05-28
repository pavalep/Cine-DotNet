namespace Cine.Media.Events;

using System.Collections.Generic;
using Cine.Media.Models;

/// <summary>
/// Event args for chapter list changes - matches Python's @mpv.property_observer("chapter")
/// </summary>
public class ChapterListChangedEventArgs : EventArgs
{
    /// <summary>
    /// Collection of available chapters
    /// </summary>
    public IEnumerable<ChapterInfo> Chapters { get; }

    /// <summary>
    /// Creates ChapterListChangedEventArgs with chapter collection
    /// </summary>
    /// <param name="chapters">The list of chapters</param>
    public ChapterListChangedEventArgs(IEnumerable<ChapterInfo> chapters)
    {
        Chapters = chapters;
    }
}
