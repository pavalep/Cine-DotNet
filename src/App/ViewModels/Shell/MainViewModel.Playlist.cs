using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cine.Avalonia.Serialization;
using Cine.Avalonia.Services;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Playlist and session management: navigation, persistence, recent files.
/// Pure playlist data is delegated to <see cref="Services.PlaylistCoordinator"/>;
/// this file handles UI-facing wrappers (PlaylistItems, bindings).
/// </summary>
public partial class MainViewModel
{
    // ─────────────────────────────────────────────────────
    //  Playlist Position
    // ─────────────────────────────────────────────────────

    public int PlaylistPosition
    {
        get => _playlistCoordinator.CurrentIndex;
        set
        {
            _playlistCoordinator.CurrentIndex = value;
            _player.PlaylistPosition = value;
            OnPropertyChanged();
            foreach (var item in PlaylistItems) item.NotifyPlayingChanged();
        }
    }

    public void PlayPlaylistItem(int index)
    {
        PlaylistPosition = index;
    }

    public void RemovePlaylistItem(int index)
    {
        if (index < 0 || index >= PlaylistItems.Count) return;
        var removedIsCurrent = PlaylistPosition == index;
        _playlistCoordinator.RemoveAt(index);
        PlaylistItems.RemoveAt(index);
        Playlist.RemoveAt(index);
        for (int i = index; i < PlaylistItems.Count; i++)
            PlaylistItems[i].NotifyPlayingChanged();
        HasMultiplePlaylistItems = PlaylistItems.Count > 1;
        if (removedIsCurrent && PlaylistItems.Count > 0)
        {
            var newIdx = Math.Min(index, PlaylistItems.Count - 1);
            PlayPlaylistItem(newIdx);
        }
        else if (removedIsCurrent)
        {
            PlaylistPosition = -1;
        }
        SavePlaylist();
    }

    // ─────────────────────────────────────────────────────
    //  Navigation
    // ─────────────────────────────────────────────────────

    public void PlayNext()
    {
        var nextIdx = _playlistCoordinator.GetNextIndex();
        if (nextIdx.HasValue)
            PlayPlaylistItem(nextIdx.Value);
    }

    public void PlayPrevious()
    {
        var prevIdx = _playlistCoordinator.GetPreviousIndex();
        if (prevIdx.HasValue)
            PlayPlaylistItem(prevIdx.Value);
    }

    /// <summary>Insert a file after the currently playing item (queue mode).</summary>
    public void InsertAfterCurrent(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        int insertIdx = PlaylistPosition >= 0 ? PlaylistPosition + 1 : PlaylistItems.Count;
        _playlistCoordinator.Add(path);
        Playlist.Insert(insertIdx, path);
        PlaylistItems.Insert(insertIdx, new PlaylistItemViewModel(this, insertIdx, path));
        for (int i = insertIdx + 1; i < PlaylistItems.Count; i++)
            PlaylistItems[i].NotifyPlayingChanged();
        HasMultiplePlaylistItems = PlaylistItems.Count > 1;
        SavePlaylist();
    }

    /// <summary>Insert multiple files after the current item.</summary>
    public void InsertAfterCurrent(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;
        int insertIdx = PlaylistPosition >= 0 ? PlaylistPosition + 1 : PlaylistItems.Count;
        for (int i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(paths[i])) continue;
            _playlistCoordinator.Add(paths[i]);
            Playlist.Insert(insertIdx + i, paths[i]);
            PlaylistItems.Insert(insertIdx + i, new PlaylistItemViewModel(this, insertIdx + i, paths[i]));
        }
        for (int i = insertIdx + paths.Length; i < PlaylistItems.Count; i++)
            PlaylistItems[i].NotifyPlayingChanged();
        HasMultiplePlaylistItems = PlaylistItems.Count > 1;
        SavePlaylist();
    }

    /// <summary>Sort playlist items alphabetically by title.</summary>
    public void SortPlaylistByTitle()
    {
        _playlistCoordinator.SortByTitle();

        Playlist.Clear();
        PlaylistItems.Clear();
        foreach (var path in _playlistCoordinator.Items)
        {
            Playlist.Add(path);
            PlaylistItems.Add(new PlaylistItemViewModel(this, PlaylistItems.Count, path));
        }
        SavePlaylist();
    }

    // ─────────────────────────────────────────────────────
    //  Persistence
    // ─────────────────────────────────────────────────────

    /// <summary>Persist current playlist to disk.</summary>
    private void SavePlaylist()
    {
        _playlistCoordinator.Save();
    }

    /// <summary>
    /// Restore playlist items from disk. Does NOT open a file.
    /// </summary>
    public void LoadPlaylist()
    {
        if (!_playlistCoordinator.Load())
        {
            Playlist.Clear();
            PlaylistItems.Clear();
            HasMultiplePlaylistItems = false;
            return;
        }

        Playlist.Clear();
        PlaylistItems.Clear();
        foreach (var path in _playlistCoordinator.Items)
        {
            Playlist.Add(path);
            PlaylistItems.Add(new PlaylistItemViewModel(this, PlaylistItems.Count, path));
        }
        HasMultiplePlaylistItems = Playlist.Count > 1;
        PlaylistPosition = _playlistCoordinator.CurrentIndex;
    }

    /// <summary>Remove all playlist items, stop playback, clear saved playlist.</summary>
    public void ClearPlaylist()
    {
        if (PlaylistItems.Count == 0) return;
        _player.Stop();
        _playlistCoordinator.Clear();
        _playlistCoordinator.ClearPersistence();
        Playlist.Clear();
        PlaylistItems.Clear();
        PlaylistPosition = -1;
        HasMultiplePlaylistItems = false;
    }

    // ─────────────────────────────────────────────────────
    //  Session Resume
    // ─────────────────────────────────────────────────────

    public Action<string, TimeSpan>? SessionResumeRequested { get; set; }

    public void SaveSession()
    {
        _session.Save(
            _filePath,
            _player.Position,
            Subtitles?.CurrentSubtitleTrackId ?? -1,
            _currentAudioTrackId,
            _player.SubtitleDelay,
            _player.AudioDelay,
            Renderer.RendererMode.ToString());

        // Sync playback position to the recent-files entry.
        // Skip when position is 0 (file hasn't started or playback has ended)
        // to avoid overwriting the saved resume position.
        if (!string.IsNullOrEmpty(_filePath) && _player.Position.Ticks > 0)
            _recentFiles.UpdatePosition(_filePath, _player.Position.Ticks, _player.Duration.Ticks);
    }

    public void LoadSession()
    {
        var data = _session.Load();
        if (data == null) return;

        if (File.Exists(data.FilePath))
            SessionResumeRequested?.Invoke(data.FilePath, TimeSpan.FromTicks(data.PositionTicks));

        if (data.SubtitleTrackId >= 0)
            Subtitles?.SelectSubtitleTrackById(data.SubtitleTrackId);
        if (data.AudioTrackId >= 0)
            _pendingAudioTrackId = data.AudioTrackId;
        if (Math.Abs(data.SubtitleDelay) > 0.001f)
            _player.SubtitleDelay = data.SubtitleDelay;
        if (Math.Abs(data.AudioDelay) > 0.001f)
            _player.AudioDelay = data.AudioDelay;
        if (Enum.TryParse<RendererType>(data.RendererMode, out var rm))
            RendererMode = rm;
    }

    public void ClearSession()
    {
        _session.Clear();
    }

    // Recent Files moved to IRecentFilesService (shared singleton, Phase 4).
    // AddRecentFile calls are forwarded via _recentFiles field in Actions.cs.
}
