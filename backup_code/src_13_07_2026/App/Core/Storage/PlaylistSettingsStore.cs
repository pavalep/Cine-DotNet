using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cine.Avalonia.Serialization;

namespace Cine.Avalonia.Storage;

/// <summary>
/// Playlist persistence — saves/loads the current playlist to/from %LOCALAPPDATA%\Cine\playlist.json.
/// Corruption-safe with automatic recovery.
/// </summary>
public sealed class PlaylistSettingsStore : SettingsStoreBase
{
    private const string FileName = "playlist.json";

    private static readonly PlaylistData EmptyData = new(
        Version: 1,
        Items: new List<string>(),
        CurrentPosition: -1,
        LastPlayed: null
    );

    public PlaylistSettingsStore(string? storePath = null)
        : base()
    {
        if (storePath != null)
        {
            var dir = Path.GetDirectoryName(storePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _storePath = storePath;
        }
    }

    private readonly string? _storePath;
    private string StoreFilePath => _storePath ?? StorePath(FileName);

    /// <summary>Load playlist from disk. Returns null if no saved playlist exists.</summary>
    public List<string>? LoadPlaylist(out int currentPosition)
    {
        currentPosition = -1;
        try
        {
            var path = StoreFilePath;
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize(json, CineJsonContext.Default.PlaylistData);
            if (data == null || data.Version < 1 || data.Items == null || data.Items.Count == 0)
            {
                TryDelete(path);
                return null;
            }

            currentPosition = Math.Clamp(data.CurrentPosition, 0, data.Items.Count - 1);
            return data.Items;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            ForContext().Warning("Playlist file corrupted, recovering: {Error}", ex.Message);
            TryDelete(StoreFilePath);
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
            File.WriteAllText(StoreFilePath, json);
        }
        catch (Exception ex)
        {
            ForContext().Error(ex, "SavePlaylist failed");
        }
    }

    /// <summary>Delete the saved playlist entirely.</summary>
    public void ClearPlaylist() => TryDelete(StoreFilePath);
}
