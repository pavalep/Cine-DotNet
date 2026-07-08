using System;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Media.Interfaces;

/// <summary>
/// Playlist management — add, navigate, shuffle, loop mode.
/// </summary>
public interface IPlaylistManagement
{
    string[] Playlist { get; }
    int PlaylistPosition { get; set; }
    bool IsShuffled { get; set; }
    LoopMode LoopMode { get; set; }
    void AddToPlaylist(string path);
    void NextPlaylistItem();
    void PreviousPlaylistItem();
    void ToggleLoopFile();
    void ToggleLoopPlaylist();

    event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;
    event EventHandler<LoopChangedEventArgs>? LoopChangedEvent;
}
