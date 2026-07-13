using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cine.Avalonia.Models;
using Cine.Avalonia.Serialization;
using Cine.Core;

namespace Cine.Avalonia.Services;

public sealed class RecentFilesService : IRecentFilesService
{
    private const int MaxRecentFiles = 10;
    private readonly string _storePath;
    private readonly ObservableCollection<RecentFileEntry> _recentFiles = new();

    public ObservableCollection<RecentFileEntry> RecentFiles => _recentFiles;
    public bool HasRecentFiles => _recentFiles.Count > 0;

    public RecentFilesService(string? storePath = null)
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
            _storePath = Path.Combine(dir, "recent.json");
        }

        LoadRecentFiles();
    }

    public void AddRecentFile(string path, long positionTicks = 0, string? thumbnailPath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var existing = _recentFiles.FirstOrDefault(r =>
            string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            _recentFiles.Remove(existing);

        var entry = new RecentFileEntry
        {
            FilePath = path,
            Title = Path.GetFileNameWithoutExtension(path),
            LastOpened = DateTime.Now.ToString("o"),
            // Keep existing thumbnail/position unless the caller provides a newer value.
            ThumbnailPath = !string.IsNullOrWhiteSpace(thumbnailPath)
                ? thumbnailPath
                : existing?.ThumbnailPath,
            PositionTicks = positionTicks > 0
                ? positionTicks
                : existing?.PositionTicks ?? 0,
            DurationTicks = existing?.DurationTicks ?? 0
        };

        _recentFiles.Insert(0, entry);

        while (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveAt(_recentFiles.Count - 1);

        SaveRecentFiles();
    }

    public void UpdatePosition(string filePath, long positionTicks, long durationTicks = 0)
    {
        var existing = _recentFiles.FirstOrDefault(r =>
            string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing == null) return;

        var idx = _recentFiles.IndexOf(existing);
        _recentFiles[idx] = existing with
        {
            PositionTicks = positionTicks,
            DurationTicks = durationTicks > 0 ? durationTicks : existing.DurationTicks
        };
        SaveRecentFiles();
    }

    public void UpdateThumbnail(string filePath, string thumbnailPath)
    {
        var existing = _recentFiles.FirstOrDefault(r =>
            string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing == null) return;

        var idx = _recentFiles.IndexOf(existing);
        _recentFiles[idx] = existing with { ThumbnailPath = thumbnailPath };
        SaveRecentFiles();
    }

    public void LoadRecentFiles()
    {
        try
        {
            if (!File.Exists(_storePath))
                return;

            var json = File.ReadAllText(_storePath);
            var list = JsonSerializer.Deserialize(json, CineJsonContext.Default.ListRecentFileEntry);
            if (list == null) return;

            _recentFiles.Clear();

            foreach (var entry in list)
            {
                if (File.Exists(entry.FilePath))
                    _recentFiles.Add(entry);
            }

            // Trim down to max
            while (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
        }
        catch (Exception ex)
        {
            Log.ForContext<RecentFilesService>().Error(ex, "Failed to load recent files");
        }
    }

    private void SaveRecentFiles()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentFiles.ToList(), CineJsonContext.Default.ListRecentFileEntry);
            File.WriteAllText(_storePath, json);
        }
        catch (Exception ex)
        {
            Log.ForContext<RecentFilesService>().Error(ex, "Failed to save recent files");
        }
    }
}
