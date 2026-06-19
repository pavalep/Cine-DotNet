using System;
using System.Collections.Generic;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Orchestrates playlist navigation, shuffle, and loop logic.
/// Persistence is delegated to <see cref="Managers.PlaylistSettingsStore"/>.
/// </summary>
public class PlaylistCoordinator : IPlaylistService
{
    private readonly List<string> _items = new();
    private readonly Random _rng = new();
    private readonly Managers.PlaylistSettingsStore _store;
    private int _currentIndex = -1;
    private bool _isShuffleEnabled;
    private bool _isLoopPlaylistEnabled;
    private bool _isLoopFileEnabled;

    public PlaylistCoordinator(Managers.PlaylistSettingsStore? store = null)
    {
        _store = store ?? new Managers.PlaylistSettingsStore();
    }

    /// <summary>Read-only view of all playlist item paths.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>Number of items in the playlist.</summary>
    public int Count => _items.Count;

    /// <summary>Current position in the playlist (-1 when empty).</summary>
    public int CurrentIndex
    {
        get => _currentIndex;
        set => _currentIndex = Math.Clamp(value, -1, _items.Count - 1);
    }

    /// <summary>The path at the current position, or null if empty.</summary>
    public string? CurrentPath => _currentIndex >= 0 && _currentIndex < _items.Count ? _items[_currentIndex] : null;

    public bool IsShuffleEnabled { get => _isShuffleEnabled; set => _isShuffleEnabled = value; }
    public bool IsLoopPlaylistEnabled { get => _isLoopPlaylistEnabled; set => _isLoopPlaylistEnabled = value; }
    public bool IsLoopFileEnabled { get => _isLoopFileEnabled; set => _isLoopFileEnabled = value; }

    // ── Mutations ────────────────────────────────────────

    public void Add(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !_items.Contains(path, StringComparer.OrdinalIgnoreCase))
            _items.Add(path);
    }

    public void AddRange(IEnumerable<string> paths)
    {
        foreach (var p in paths)
            Add(p);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        _items.RemoveAt(index);
        if (_currentIndex >= _items.Count)
            _currentIndex = _items.Count - 1;
    }

    public void Clear()
    {
        _items.Clear();
        _currentIndex = -1;
    }

    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _items.Count) return;
        if (toIndex < 0 || toIndex >= _items.Count) return;
        var item = _items[fromIndex];
        _items.RemoveAt(fromIndex);
        _items.Insert(toIndex, item);
        if (_currentIndex == fromIndex)
            _currentIndex = toIndex;
        else if (_currentIndex > fromIndex && _currentIndex <= toIndex)
            _currentIndex--;
        else if (_currentIndex < fromIndex && _currentIndex >= toIndex)
            _currentIndex++;
    }

    /// <summary>Shuffle the playlist items randomly.</summary>
    public void Shuffle()
    {
        for (int i = _items.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (_items[i], _items[j]) = (_items[j], _items[i]);
        }
    }

    /// <summary>Sort playlist items alphabetically (case-insensitive).</summary>
    public void SortByTitle()
    {
        _items.Sort(StringComparer.OrdinalIgnoreCase);
    }

    // ── Navigation ───────────────────────────────────────

    /// <summary>Get the next playlist index, respecting loop and shuffle modes.</summary>
    public int? GetNextIndex()
    {
        if (_items.Count == 0) return null;

        if (_isShuffleEnabled)
        {
            var idx = _rng.Next(_items.Count);
            return idx;
        }

        var next = _currentIndex + 1;
        if (next >= _items.Count)
            return _isLoopPlaylistEnabled ? 0 : null;

        return next;
    }

    /// <summary>Get the previous playlist index, respecting loop mode.</summary>
    public int? GetPreviousIndex()
    {
        if (_items.Count == 0) return null;

        if (_isShuffleEnabled)
        {
            var idx = _rng.Next(_items.Count);
            return idx;
        }

        var prev = _currentIndex - 1;
        if (prev < 0)
            return _isLoopPlaylistEnabled ? _items.Count - 1 : null;

        return prev;
    }

    // ── Persistence ──────────────────────────────────────

    public void Save()
    {
        _store.SavePlaylist(_items, _currentIndex);
    }

    /// <summary>Load playlist from disk. Returns true if items were restored.</summary>
    public bool Load()
    {
        var items = _store.LoadPlaylist(out var savedPosition);
        if (items == null || items.Count == 0) return false;

        _items.Clear();
        _items.AddRange(items);
        _currentIndex = Math.Clamp(savedPosition, 0, items.Count - 1);
        return true;
    }

    public void ClearPersistence()
    {
        _store.ClearPlaylist();
    }
}
