using System.Collections.ObjectModel;

namespace Cine.Avalonia.Services;

/// <summary>
/// Manages the recent files list with persistence.
/// Singleton — shared across MainViewModel, StartPageViewModel, and HeaderBar.
/// </summary>
public interface IRecentFilesService
{
    ObservableCollection<string> RecentFiles { get; }
    bool HasRecentFiles { get; }
    void AddRecentFile(string path);
    void LoadRecentFiles();
}
