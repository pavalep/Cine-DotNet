using System.Collections.ObjectModel;
using Cine.Avalonia.Models;

namespace Cine.Avalonia.Services;

public interface IRecentFilesService
{
    ObservableCollection<RecentFileEntry> RecentFiles { get; }
    bool HasRecentFiles { get; }
    void AddRecentFile(string path, long positionTicks = 0, string? thumbnailPath = null);
    void UpdatePosition(string filePath, long positionTicks, long durationTicks = 0);
    void UpdateThumbnail(string filePath, string thumbnailPath);
    void LoadRecentFiles();
}
