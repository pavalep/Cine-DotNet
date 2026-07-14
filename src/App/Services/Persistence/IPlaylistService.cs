using System.Collections.Generic;

namespace Simba.Avalonia.Services;

/// <summary>
/// Manages playlist state: items, navigation, shuffle, loop, and persistence.
/// </summary>
public interface IPlaylistService
{
    IReadOnlyList<string> Items { get; }
    int Count { get; }
    int CurrentIndex { get; set; }
    string? CurrentPath { get; }
    bool IsShuffleEnabled { get; set; }
    bool IsLoopPlaylistEnabled { get; set; }
    bool IsLoopFileEnabled { get; set; }

    void Add(string path);
    void AddRange(IEnumerable<string> paths);
    void RemoveAt(int index);
    void Clear();
    void Move(int fromIndex, int toIndex);
    void Shuffle();
    void SortByTitle();

    int? GetNextIndex();
    int? GetPreviousIndex();

    void Save();
    bool Load();
    void ClearPersistence();
}
