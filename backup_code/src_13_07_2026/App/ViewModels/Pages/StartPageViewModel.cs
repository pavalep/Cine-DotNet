using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Cine.Avalonia.Core.Navigation;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Avalonia.Utilities;

namespace Cine.Avalonia.ViewModels.Pages;

/// <summary>
/// ViewModel for the StartPage — owns RecentFiles display and open-file commands.
/// </summary>
public sealed class StartPageViewModel : INotifyPropertyChanged
{
    private readonly IMediaFileService _mediaFileService;
    private readonly INavigationService _navigation;
    private readonly IRecentFilesService _recentFiles;
    private readonly IFileDialogService _fileDialog;

    public IMediaFileService MediaFileService => _mediaFileService;

    public ObservableCollection<string> RecentFiles => _recentFiles.RecentFiles;

    /// <summary>Display models for the ItemsRepeater (synced with RecentFiles).</summary>
    public ObservableCollection<RecentFileItem> RecentFileItems { get; } = new();

    /// <summary>Command to open a recent file by path.</summary>
    public ICommand OpenRecentFileCommand { get; }

    public bool HasRecentFiles => _recentFiles.HasRecentFiles;

    public StartPageViewModel(
        IMediaFileService mediaFileService,
        INavigationService navigation,
        IRecentFilesService recentFiles,
        IFileDialogService fileDialog)
    {
        _mediaFileService = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _recentFiles = recentFiles ?? throw new ArgumentNullException(nameof(recentFiles));
        _fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));

        OpenRecentFileCommand = new RelayCommand(path =>
        {
            if (path is string p && !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                _navigation.Navigate(AppRoute.Player, p);
        });

        // Sync RecentFileItems from RecentFiles, and keep in sync
        SyncRecentFileItems();
        RecentFiles.CollectionChanged += OnRecentFilesCollectionChanged;
    }

    /// <summary>Open a recent file — navigates to Player with the file path.</summary>
    public void OpenRecentFile(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            _navigation.Navigate(AppRoute.Player, path);
    }

    /// <summary>Open file(s) via dialog — navigates to Player with the first selected path.</summary>
    public async void OpenFiles()
    {
        var paths = await _fileDialog.OpenFilesAsync();
        if (paths != null && paths.Length > 0)
            _navigation.Navigate(AppRoute.Player, paths[0]);
    }

    /// <summary>Open a folder via dialog — navigates to Player with the folder path.</summary>
    public async void OpenFolder()
    {
        var path = await _fileDialog.OpenFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
            _navigation.Navigate(AppRoute.Player, path);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Loads recent files from persistent storage.</summary>
    public void LoadRecentFiles() => _recentFiles.LoadRecentFiles();

    /// <summary>Adds a file to the recent files list.</summary>
    public void AddRecentFile(string path) => _recentFiles.AddRecentFile(path);

    // ── Collection sync ────────────────────────────────────────────

    private RecentFileItem CreateItem(string filePath)
        => new(filePath, _mediaFileService.IsVideoFile(filePath));

    private void SyncRecentFileItems()
    {
        RecentFileItems.Clear();
        foreach (var path in RecentFiles)
            RecentFileItems.Add(CreateItem(path));
    }

    private void OnRecentFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                for (int i = 0; i < e.NewItems.Count; i++)
                    RecentFileItems.Insert(e.NewStartingIndex + i, CreateItem((string)e.NewItems[i]!));
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                for (int i = 0; i < e.OldItems.Count; i++)
                    RecentFileItems.RemoveAt(e.OldStartingIndex);
                break;

            case NotifyCollectionChangedAction.Replace when e.NewItems is not null:
                for (int i = 0; i < e.NewItems.Count; i++)
                    RecentFileItems[e.NewStartingIndex + i] = CreateItem((string)e.NewItems[i]!);
                break;

            case NotifyCollectionChangedAction.Reset:
                SyncRecentFileItems();
                break;
        }

        OnPropertyChanged(nameof(HasRecentFiles));
    }
}
