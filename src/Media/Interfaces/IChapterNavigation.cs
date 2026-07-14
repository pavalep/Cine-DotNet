using System;
using Simba.Media.Events;
using Simba.Media.Models;

namespace Simba.Media.Interfaces;

/// <summary>
/// Chapter list query and navigation.
/// </summary>
public interface IChapterNavigation
{
    int CurrentChapter { get; }
    ChapterInfo[] ChapterList { get; }
    void NextChapter();
    void PreviousChapter();

    event EventHandler<ChapterListChangedEventArgs>? ChapterListChanged;
}
