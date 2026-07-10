using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Services;
using Cine.Avalonia.Views.Resources;
using Cine.Core;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;
using ListBox = Avalonia.Controls.ListBox;
using Button = Avalonia.Controls.Button;
using Point = Avalonia.Point;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class PlaylistPanel : UserControl
{
    private MainViewModel? _viewModel;
    private DispatcherTimer? _searchDebounceTimer;
    private string _searchFilter = string.Empty;

    // P3.5: Drag-reorder state
    private int _dragSourceIndex = -1;
    private bool _isDragging;
    private Point _dragStartPoint;

    // P3.19: Auto-scroll — prevent scroll loop
    private bool _isScrolling;

    // Queue mode toggle
    private bool _queueMode;

    private const string QueueModeSettingKey = "PlaylistPanel_QueueMode";

    // Centralized file-dialog handler
    private FileDialogHandler? _dialogHandler;
    private CancellationTokenSource? _exportCts;

    // Event handlers stored for unsubscribe
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _listChanged;
    private System.ComponentModel.PropertyChangedEventHandler? _vmPropChanged;

    public event EventHandler? HideRequested;

    public PlaylistPanel()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnWindowFileDrop);
        AddHandler(DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave);
        DataContextChanged += OnDataContextChanged;

        // Wire buttons
        BtnAddFiles.Click += OnAddFilesClick;
        SortBtn.Click += OnSortClick;
        QueueBtn.Click += OnQueueModeClick;
        BtnClearPlaylist.Click += OnClearPlaylistClick;
        BtnSavePlaylist.Click += OnSavePlaylistClick;
        BtnClosePlaylist.Click += OnCloseClick;
        SearchTextBox.TextChanged += OnSearchTextChanged;
        SearchClearButton.Click += OnSearchClearClick;
        PlaylistListBox.DoubleTapped += OnListBoxDoubleTapped;
        PlaylistListBox.KeyDown += OnListBoxKeyDown;
        PlaylistListBox.PointerPressed += OnPlaylistListBoxPointerPressed;
        PlaylistListBox.PointerMoved += OnPlaylistListBoxPointerMoved;
        PlaylistListBox.PointerReleased += OnPlaylistListBoxPointerReleased;
        MenuItemReveal.Click += OnItemRevealClick;
        MenuItemProperties.Click += OnItemPropertiesClick;
    }

    private Window? GetParentWindow() => TopLevel.GetTopLevel(this) as Window;

    // =========================================================================
    // DATA CONTEXT
    // =========================================================================

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm != null && _listChanged != null)
            _subscribedVm.Playlist.CollectionChanged -= _listChanged;
        if (_subscribedVm != null && _vmPropChanged != null)
            _subscribedVm.PropertyChanged -= _vmPropChanged;

        if (DataContext is not MainViewModel vm)
        {
            _viewModel = null;
            _subscribedVm = null;
            return;
        }

        _viewModel = vm;
        _subscribedVm = vm;
        _listChanged = (_, _) =>
        {
            UpdateEmptyState();
            ApplySearchFilter();
        };
        _vmPropChanged = OnVmPropertyChanged;

        vm.Playlist.CollectionChanged += _listChanged;
        vm.PropertyChanged += _vmPropChanged;
        UpdateEmptyState();

        // Load persisted queue mode
        _queueMode = LoadQueueMode();
        if (QueueBtn != null)
            QueueBtn.Opacity = _queueMode ? 1.0 : 0.5;
    }

    private MainViewModel? _subscribedVm;

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PlaylistPosition))
            AutoScrollToCurrentItem();
    }

    // =========================================================================
    // SEARCH (P3.3) — 100ms debounce
    // =========================================================================

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        SearchClearButton.IsVisible = !string.IsNullOrEmpty(SearchTextBox.Text);

        _searchDebounceTimer ??= new DispatcherTimer();
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(100);
        _searchDebounceTimer.Tick -= OnSearchDebounceTick;
        _searchDebounceTimer.Tick += OnSearchDebounceTick;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnSearchDebounceTick(object? sender, EventArgs args)
    {
        _searchDebounceTimer?.Stop();
        _searchFilter = SearchTextBox.Text ?? string.Empty;
        ApplySearchFilter();
    }

    private void OnSearchClearClick(object? sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        _searchFilter = string.Empty;
        SearchClearButton.IsVisible = false;
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        if (_viewModel == null) return;
        var anyVisible = PlaylistDialogHelpers.ApplySearchFilter(_viewModel.PlaylistItems, _searchFilter);
        NoResultsOverlay.IsVisible = !anyVisible && _viewModel.PlaylistItems.Count > 0;
        UpdateEmptyState();
    }

    // =========================================================================
    // AUTO-SCROLL (P3.19)
    // =========================================================================

    private void AutoScrollToCurrentItem()
    {
        if (_isScrolling || _viewModel == null) return;
        _isScrolling = true;
        try
        {
            var idx = _viewModel.PlaylistPosition;
            if (idx >= 0 && idx < PlaylistListBox.ItemCount)
                PlaylistListBox.ScrollIntoView(idx);
        }
        finally
        {
            _isScrolling = false;
        }
    }

    // =========================================================================
    // EMPTY STATE + NO RESULTS (P3.6)
    // =========================================================================

    private void UpdateEmptyState()
    {
        if (EmptyStateOverlay == null || _viewModel == null) return;
        var hasItems = _viewModel.PlaylistItems.Count > 0;
        EmptyStateOverlay.IsVisible = !hasItems;
        if (!hasItems)
            NoResultsOverlay.IsVisible = false;
    }

    // =========================================================================
    // SAVE PLAYLIST (P3.4)
    // =========================================================================

    private async void OnSavePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.PlaylistItems.Count == 0) return;

        var parentWindow = GetParentWindow();
        if (parentWindow == null) return;

        _dialogHandler ??= new FileDialogHandler(parentWindow);
        var path = await _dialogHandler.SavePlaylistAsync();
        if (string.IsNullOrEmpty(path)) return;

        _exportCts?.Cancel();
        _exportCts = new CancellationTokenSource();

        try
        {
            await PlaylistDialogHelpers.ExportToM3UAsync(_viewModel.PlaylistItems, path, _exportCts.Token);
        }
        catch (OperationCanceledException)
        {
            ShowToast("Export cancelled.");
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to save: {ex.Message}");
        }
    }

    // =========================================================================
    // SORT (P3.21)
    // =========================================================================

    private void OnSortClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SortPlaylistByTitle();
    }

    // =========================================================================
    // QUEUE MODE (P3.17)
    // =========================================================================

    private bool LoadQueueMode()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine", "Settings", "playlist-queue-mode.json");
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                return text.Trim() == "true";
            }
        }
        catch (Exception) { global::Cine.Core.Log.ForContext<PlaylistPanel>().Warning("LoadQueueMode failed"); }
        return false;
    }

    private void SaveQueueMode()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine", "Settings");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "playlist-queue-mode.json");
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, _queueMode.ToString().ToLower());
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception) { global::Cine.Core.Log.ForContext<PlaylistPanel>().Warning("SaveQueueMode failed"); }
    }

    private void OnQueueModeClick(object? sender, RoutedEventArgs e)
    {
        _queueMode = !_queueMode;
        SaveQueueMode();
        if (QueueBtn != null)
            QueueBtn.Opacity = _queueMode ? 1.0 : 0.5;
    }

    // =========================================================================
    // CONTEXT MENU (P3.18)
    // =========================================================================

    private void OnItemRevealClick(object? sender, RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.MenuItem mi && mi.DataContext is PlaylistItemViewModel item)
        {
            try
            {
                var dir = Path.GetDirectoryName(item.FilePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{item.FilePath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                global::Cine.Core.Log.ForContext<PlaylistPanel>()
                    .Warning("Failed to open file in explorer: {Error}", ex.Message);
            }
        }
    }

    private void OnItemPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.MenuItem mi && mi.DataContext is PlaylistItemViewModel item)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{item.FilePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                global::Cine.Core.Log.ForContext<PlaylistPanel>()
                    .Warning("Failed to open properties in explorer: {Error}", ex.Message);
            }
        }
    }

    // =========================================================================
    // MULTI-SELECT (P3.20) — Delete removes all selected
    // =========================================================================

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (PlaylistListBox?.SelectedItem is PlaylistItemViewModel item)
                item.Play();
        }
        else if (e.Key == Key.Delete && PlaylistListBox != null)
        {
            var toRemove = PlaylistListBox.SelectedItems!
                .OfType<PlaylistItemViewModel>()
                .OrderByDescending(x => x.Index)
                .ToList();
            if (_viewModel != null)
            {
                foreach (var item in toRemove)
                    _viewModel.RemovePlaylistItem(item.Index);
            }
        }
        else if (e.Key == Key.Escape)
        {
            HideRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // =========================================================================
    // DRAG-REORDER (P3.5) — pointer-based, disabled during multi-select
    // =========================================================================

    private void OnPlaylistListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null) return;

        if (e.Source is Button) return;

        var modifiers = e.KeyModifiers;
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift))
        {
            _dragSourceIndex = -1;
            return;
        }

        var pos = e.GetPosition(PlaylistListBox);
        _dragSourceIndex = PlaylistListBox.SelectedIndex;
        _dragStartPoint = pos;
        _isDragging = false;
    }

    private void OnPlaylistListBoxPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragSourceIndex < 0) return;
        if (!e.GetCurrentPoint(PlaylistListBox).Properties.IsLeftButtonPressed)
        {
            _dragSourceIndex = -1;
            _isDragging = false;
            return;
        }

        var pos = e.GetPosition(PlaylistListBox);
        if (!_isDragging)
        {
            var delta = pos - _dragStartPoint;
            if (Math.Abs(delta.Y) < 10) return;
            _isDragging = true;
        }

        var itemHeight = PlaylistListBox.Bounds.Height / Math.Max(1, PlaylistListBox.ItemCount);
        var targetIndex = Math.Clamp((int)(pos.Y / itemHeight), 0, PlaylistListBox.ItemCount - 1);

        if (_dragSourceIndex != targetIndex && _viewModel != null)
        {
            _viewModel.PlaylistItems.Move(_dragSourceIndex, targetIndex);
            _viewModel.Playlist.Move(_dragSourceIndex, targetIndex);
            _dragSourceIndex = targetIndex;
        }
    }

    private void OnPlaylistListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragSourceIndex = -1;
        _isDragging = false;
    }

    // =========================================================================
    // EXISTING HANDLERS
    // =========================================================================

    private void OnAddFilesClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        if (_queueMode && _viewModel.PlaylistPosition >= 0)
        {
            AddFilesToQueue(_viewModel);
            return;
        }

        _viewModel.AddFilesCommand.Execute(null);
    }

    private void AddFilesToQueue(MainViewModel vm)
    {
        ErrorBoundary.Run(async () =>
        {
            var files = await GetOpenFilePathsAsync();
            if (files != null && files.Length > 0)
                vm.InsertAfterCurrent(files);
        });
    }

    private async Task<string[]?> GetOpenFilePathsAsync()
    {
        var parentWindow = GetParentWindow();
        if (parentWindow == null) return null;
        _dialogHandler ??= new FileDialogHandler(parentWindow);
        return await _dialogHandler.OpenPlaylistFilesAsync();
    }

    private void OnClearPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.PlaylistItems.Count == 0) return;
        _viewModel.ClearPlaylist();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PlaylistListBox?.SelectedItem is PlaylistItemViewModel item)
            item.Play();
    }

    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
    }

    private void OnWindowDragLeave(object? sender, RoutedEventArgs e) { }

    private void OnWindowFileDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File)) return;
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;
        if (_viewModel == null) return;

        var paths = files.Select(f => f.Path.LocalPath).ToArray();

        if (_queueMode && _viewModel.PlaylistPosition >= 0)
            _viewModel.InsertAfterCurrent(paths);
        else
            _ = _viewModel.OpenFiles(paths);
    }

    private void ShowToast(string message)
    {
        var notification = new TextBlock
        {
            Text = message,
            FontSize = Token.Size("font-size-subtitle1"),
            Foreground = global::Avalonia.Media.Brush.Parse("#FFE5E5E5"),
            Padding = new Thickness(16, 8)
        };
        var border = new Border
        {
            Background = global::Avalonia.Media.Brush.Parse("#CC1E1E2E"),
            CornerRadius = new CornerRadius(8),
            Child = notification
        };
        var popup = new Popup
        {
            Placement = PlacementMode.Center,
            Child = border
        };
        popup.Open();
        Task.Delay(3000).ContinueWith(_ => Dispatcher.UIThread.Post(() => popup.Close()));
    }
}
