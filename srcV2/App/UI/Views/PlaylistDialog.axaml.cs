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

    private void OnDragEnter(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.Copy;
            var revealer = this.FindControl<Border>("DropIndicatorRevealer");
            if (revealer != null) revealer.IsVisible = true;
        }
    }

    private void OnDragLeave(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        var revealer = this.FindControl<Border>("DropIndicatorRevealer");
        if (revealer != null) revealer.IsVisible = false;
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
