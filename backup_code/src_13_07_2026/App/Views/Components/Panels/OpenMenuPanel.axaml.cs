using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class OpenMenuPanel : UserControl
{
    public ItemsControl RecentFilesControl => RecentFilesList;

    public event EventHandler<RoutedEventArgs>? OpenFileClicked;
    public event EventHandler<RoutedEventArgs>? OpenFolderClicked;
    public event EventHandler<string>? RecentFileClicked;

    public OpenMenuPanel()
    {
        InitializeComponent();
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        OpenFileClicked?.Invoke(sender, e);
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        OpenFolderClicked?.Invoke(sender, e);
    }

    private void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is string filePath)
        {
            RecentFileClicked?.Invoke(this, filePath);
        }
    }
}
