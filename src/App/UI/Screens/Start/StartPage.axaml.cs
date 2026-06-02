using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia.Controls;

public partial class StartPage : global::Avalonia.Controls.UserControl
{
    public StartPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? _previousVm;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_previousVm != null)
            _previousVm.RecentFiles.CollectionChanged -= OnRecentFilesChanged;
        if (DataContext is MainViewModel vm)
        {
            vm.RecentFiles.CollectionChanged += OnRecentFilesChanged;
            RebuildRecentFiles(vm);
        }
        _previousVm = DataContext as MainViewModel;
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            RebuildRecentFiles(vm);
    }

    private void RebuildRecentFiles(MainViewModel vm)
    {
        if (RecentFilesList == null) return;
        RecentFilesList.Children.Clear();
        foreach (var path in vm.RecentFiles)
        {
            var name = Path.GetFileName(path);
            var btn = new global::Avalonia.Controls.Button
            {
                Content = name,
                Tag = path,
                Background = global::Avalonia.Media.Brushes.Transparent,
                Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                FontSize = 12,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new global::Avalonia.Thickness(40, 4),
                BorderThickness = new global::Avalonia.Thickness(0),
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow)
            };
            btn.PointerEntered += (_, _) =>
                btn.Background = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
            btn.PointerExited += (_, _) =>
                btn.Background = global::Avalonia.Media.Brushes.Transparent;
            btn.Click += (s, _) =>
            {
                vm.OpenRecentFile(path);
                IsVisible = false;
            };
            RecentFilesList.Children.Add(btn);
        }
    }

    private void BtnOpenFile_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Parent is null) return;
        var window = TopLevel.GetTopLevel(this);
        if (window is null) return;
        var vm = DataContext as MainViewModel;
        vm?.OpenFilesCommand.Execute(null);
    }

    private void BtnOpenFolder_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Parent is null) return;
        var window = TopLevel.GetTopLevel(this);
        if (window is null) return;
        var vm = DataContext as MainViewModel;
        vm?.OpenFolderCommand.Execute(null);
    }

    public static string[] FilterVideoFiles(string[] files)
    {
        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
            ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".mts", ".m2ts",
            ".vob", ".ogv", ".asf", ".divx", ".f4v", ".rm", ".rmvb"
        };
        return files.Where(f => videoExtensions.Contains(Path.GetExtension(f))).ToArray();
    }
}

