using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Cine.Avalonia.ViewModels;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using TappedEventArgs = Avalonia.Input.TappedEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;
using ListBox = Avalonia.Controls.ListBox;
using Button = Avalonia.Controls.Button;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PlaylistDialog : Window
{
    private DispatcherTimer? _searchDebounceTimer;
    private string _searchFilter = string.Empty;

    // P3.5: Drag-reorder state
    private int _dragSourceIndex = -1;
    private bool _isDragging;
    private global::Avalonia.Point _dragStartPoint;

    public PlaylistDialog()
    {
        InitializeComponent();
        AddHandler(global::Avalonia.Input.DragDrop.DropEvent, OnWindowFileDrop);
        AddHandler(global::Avalonia.Input.DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(global::Avalonia.Input.DragDrop.DragLeaveEvent, OnWindowDragLeave);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Playlist.CollectionChanged += (_, _) =>
            {
                UpdateEmptyState();
                ApplySearchFilter();
            };
            UpdateEmptyState();
        }
    }

    // =========================================================================
    // SEARCH (P3.3) — 100ms debounce
    // =========================================================================

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        SearchClearButton.IsVisible = !string.IsNullOrEmpty(SearchTextBox.Text);

        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _searchDebounceTimer.Tick += (s, args) =>
        {
            _searchDebounceTimer?.Stop();
            _searchFilter = SearchTextBox.Text ?? string.Empty;
            ApplySearchFilter();
        };
        _searchDebounceTimer.Start();
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
        if (DataContext is not MainViewModel vm) return;

        if (string.IsNullOrWhiteSpace(_searchFilter))
        {
            foreach (var item in vm.PlaylistItems)
                item.IsVisible = true;
            NoResultsOverlay.IsVisible = false;
        }
        else
        {
            var filter = _searchFilter.Trim().ToLowerInvariant();
            var anyVisible = false;
            foreach (var item in vm.PlaylistItems)
            {
                var matches = item.Title.ToLowerInvariant().Contains(filter);
                item.IsVisible = matches;
                if (matches) anyVisible = true;
            }
            NoResultsOverlay.IsVisible = !anyVisible && vm.PlaylistItems.Count > 0;
        }

        UpdateEmptyState();
    }

    // =========================================================================
    // EMPTY STATE + NO RESULTS (P3.6)
    // =========================================================================

    private void UpdateEmptyState()
    {
        if (EmptyStateOverlay == null || DataContext is not MainViewModel vm) return;
        var hasItems = vm.PlaylistItems.Count > 0;
        EmptyStateOverlay.IsVisible = !hasItems;
        if (!hasItems)
            NoResultsOverlay.IsVisible = false;
    }

    // =========================================================================
    // SAVE PLAYLIST (P3.4)
    // =========================================================================

    private async void OnSavePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.PlaylistItems.Count == 0) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Playlist",
            DefaultExtension = ".m3u8",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Playlist Files")
                {
                    Patterns = new[] { "*.m3u8", "*.m3u" }
                }
            }
        });

        if (file == null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync("#EXTM3U");
            foreach (var item in vm.PlaylistItems)
            {
                await writer.WriteLineAsync($"#EXTINF:0,{item.Title}");
                await writer.WriteLineAsync(item.FilePath);
            }
        }
        catch (Exception ex)
        {
            var notification = new TextBlock
            {
                Text = $"Failed to save: {ex.Message}",
                FontSize = 13,
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
            await Task.Delay(3000);
            popup.Close();
        }
    }

    // =========================================================================
    // DRAG-REORDER (P3.5) — pointer-based
    // =========================================================================

    private void OnPlaylistListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Don't start drag if the user pressed a button inside the item
        if (e.Source is Button) return;

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

        // Calculate target index based on pointer position
        var itemHeight = PlaylistListBox.Bounds.Height / Math.Max(1, PlaylistListBox.ItemCount);
        var targetIndex = Math.Clamp((int)(pos.Y / itemHeight), 0, PlaylistListBox.ItemCount - 1);

        if (_dragSourceIndex != targetIndex && DataContext is MainViewModel vm)
        {
            vm.PlaylistItems.Move(_dragSourceIndex, targetIndex);
            vm.Playlist.Move(_dragSourceIndex, targetIndex);
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
        var vm = DataContext as MainViewModel;
        vm?.AddFilesCommand.Execute(null);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnItemPlayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PlaylistItemViewModel item)
            item.Play();
    }

    private void OnItemRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PlaylistItemViewModel item)
            item.Remove();
    }

    private void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PlaylistListBox?.SelectedItem is PlaylistItemViewModel item)
            item.Play();
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && PlaylistListBox?.SelectedItem is PlaylistItemViewModel item)
            item.Play();
        else if (e.Key == Key.Delete && PlaylistListBox?.SelectedItem is PlaylistItemViewModel deleteItem)
            deleteItem.Remove();
        else if (e.Key == Key.Escape)
            Close();
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
        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files == null) return;
            var vm = DataContext as MainViewModel;
            if (vm == null) return;
            foreach (var file in files)
                vm.Playlist.Add(file.Path.LocalPath);
        }
    }
}
