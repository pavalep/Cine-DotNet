using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.ViewModels;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using TappedEventArgs = Avalonia.Input.TappedEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;
using DataFormat = Avalonia.Input.DataFormat;
using ListBox = Avalonia.Controls.ListBox;
using Button = Avalonia.Controls.Button;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PlaylistDialog : Window
{
    public PlaylistDialog()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Playlist.CollectionChanged += (_, _) => UpdateEmptyState();
            UpdateEmptyState();
        }
    }

    private void UpdateEmptyState()
    {
        if (EmptyStateOverlay == null || DataContext is not MainViewModel vm) return;
        EmptyStateOverlay.IsVisible = vm.Playlist.Count == 0;
    }

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
        var listBox = sender as ListBox;
        if (listBox?.SelectedItem is PlaylistItemViewModel item)
            item.Play();
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is PlaylistItemViewModel item)
                item.Play();
        }
    }

    private async void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            var revealer = this.FindControl<Border>("DropIndicatorRevealer");
            if (revealer != null)
            {
                revealer.IsVisible = true;
                revealer.Opacity = 1;
            }
        }
    }

    private async void OnDragLeave(object? sender, DragEventArgs e)
    {
        var revealer = this.FindControl<Border>("DropIndicatorRevealer");
        if (revealer != null)
        {
            revealer.Opacity = 0;
            await Task.Delay(200);
            revealer.IsVisible = false;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var revealer = this.FindControl<Border>("DropIndicatorRevealer");
        if (revealer != null) revealer.IsVisible = false;

        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files == null) return;

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            foreach (var file in files)
            {
                vm.Playlist.Add(file.Path.LocalPath);
            }
        }
    }
}

