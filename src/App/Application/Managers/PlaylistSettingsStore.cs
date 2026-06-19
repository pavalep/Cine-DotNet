using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cine.Avalonia.Serialization;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Playlist persistence — saves/loads the current playlist to/from %LOCALAPPDATA%\Cine\playlist.json.
/// Corruption-safe with automatic recovery.
/// </summary>
public sealed class PlaylistSettingsStore
{
    private readonly string _storePath;

    private static readonly PlaylistData EmptyData = new(
        Version: 1,
        Items: new List<string>(),
        CurrentPosition: -1,
        LastPlayed: null
    );

    public PlaylistSettingsStore(string? storePath = null)
    {
        if (storePath != null)
        {
            var dir = Path.GetDirectoryName(storePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _storePath = storePath;
        }
        else
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            _storePath = Path.Combine(dir, "playlist.json");
        }
    }

    /// <summary>Load playlist from disk. Returns null if no saved playlist exists.</summary>
    public List<string>? LoadPlaylist(out int currentPosition)
    {
        currentPosition = -1;
        try
        {
            if (!File.Exists(_storePath))
                return null;

            var json = File.ReadAllText(_storePath);
            var data = JsonSerializer.Deserialize(json, CineJsonContext.Default.PlaylistData);
            if (data == null || data.Version < 1 || data.Items == null || data.Items.Count == 0)
            {
                // Corrupted or empty — clean up
                TryDelete();
                return null;
            }

            // Clamp position to valid range
            currentPosition = Math.Clamp(data.CurrentPosition, 0, data.Items.Count - 1);
            return data.Items;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Corrupted — recover silently
            global::Cine.Core.Log.ForContext<PlaylistSettingsStore>()
                .Warning("Playlist file corrupted, recovering: {Error}", ex.Message);
            TryDelete();
            currentPosition = -1;
            return null;
        }
    }

    /// <summary>Save playlist to disk.</summary>
    public void SavePlaylist(IList<string> items, int currentPosition)
    {
        try
        {
            var data = new PlaylistData(
                Version: 1,
                Items: new List<string>(items),
                CurrentPosition: currentPosition,
                LastPlayed: DateTime.UtcNow
            );
            var json = JsonSerializer.Serialize(data, CineJsonContext.Default.PlaylistData);
            File.WriteAllText(_storePath, json);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<PlaylistSettingsStore>()
                .Error(ex, "SavePlaylist failed");
        }
    }

    /// <summary>Delete the saved playlist entirely.</summary>
    public void ClearPlaylist()
    {
        TryDelete();
    }

    private void TryDelete()
    {
        try { if (File.Exists(_storePath)) File.Delete(_storePath); }
        catch (Exception ex) { global::Cine.Core.Log.ForContext<PlaylistSettingsStore>().Error(ex, "Failed to delete playlist file"); }
    }
}
