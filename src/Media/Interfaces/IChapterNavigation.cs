using System;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Media.Interfaces;

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
