using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia.Views;

public partial class PlaylistDialog : Window
{
    public PlaylistDialog()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnAddFilesClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        vm?.AddFilesCommand.Execute(null);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnListBoxDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        var listBox = sender as global::Avalonia.Controls.ListBox;
        if (listBox?.SelectedItem is PlaylistItemViewModel item)
            item.Play();
    }

    private void OnListBoxKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Enter)
        {
            var listBox = sender as global::Avalonia.Controls.ListBox;
            if (listBox?.SelectedItem is PlaylistItemViewModel item)
                item.Play();
        }
    }

    private async void OnDragEnter(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.Copy;
            var revealer = this.FindControl<Border>("DropIndicatorRevealer");
            if (revealer != null)
            {
                revealer.IsVisible = true;
                revealer.Opacity = 1;
            }
        }
    }

    private async void OnDragLeave(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        var revealer = this.FindControl<Border>("DropIndicatorRevealer");
        if (revealer != null)
        {
            revealer.Opacity = 0;
            await Task.Delay(200);
            revealer.IsVisible = false;
        }
    }

    private void OnDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        var revealer = this.FindControl<Border>("DropIndicatorRevealer");
        if (revealer != null) revealer.IsVisible = false;

        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
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
