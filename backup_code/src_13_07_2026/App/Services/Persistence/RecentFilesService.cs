using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cine.Avalonia.Serialization;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Singleton that owns the RecentFiles collection and handles persistence to disk.
/// Replaces the per-ViewModel RecentFiles management.
/// </summary>
public sealed class RecentFilesService : IRecentFilesService
{
    private static readonly string RecentFilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "recent.json");

    public ObservableCollection<string> RecentFiles { get; } = new();
    public bool HasRecentFiles => RecentFiles.Count > 0;

    public RecentFilesService()
    {
        LoadRecentFiles();
    }

    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > 10)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        SaveRecentFiles();
    }

    public void LoadRecentFiles()
    {
        try
        {
            if (!File.Exists(RecentFilesPath)) return;
            var json = File.ReadAllText(RecentFilesPath);
            var list = JsonSerializer.Deserialize(json, CineJsonContext.Default.ListString);
            if (list != null)
            {
                RecentFiles.Clear();
                foreach (var f in list.Where(File.Exists))
                    RecentFiles.Add(f);
            }
        }
        catch (Exception ex)
        {
            Log.ForContext<RecentFilesService>().Error(ex, "Failed to load recent files");
        }
    }

    public void SaveRecentFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(RecentFilesPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(RecentFilesPath,
                JsonSerializer.Serialize(RecentFiles.ToList()));
        }
        catch (Exception ex)
        {
            Log.ForContext<RecentFilesService>().Error(ex, "Failed to save recent files");
        }
    }
}
