using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia.Controls;

public partial class StartPage : global::Avalonia.Controls.UserControl
{
    public StartPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Shows the start page overlay.</summary>
    public void Show()
    {
        var root = this.FindControl<Border>("StartPageRoot");
        if (root != null)
            root.IsVisible = true;
    }

    /// <summary>Hides the start page overlay.</summary>
    public void Hide()
    {
        var root = this.FindControl<Border>("StartPageRoot");
        if (root != null)
            root.IsVisible = false;
    }

    /// <summary>Filters file paths to only video extensions matching Python reference.</summary>
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

    /// <summary>Handles open file button click.</summary>
    private async void BtnOpenFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (vm.RequestOpenFilesAsync != null)
            {
                var paths = await vm.RequestOpenFilesAsync();
                if (paths != null && paths.Length > 0)
                {
                    vm.OpenFiles(paths);
                    Hide();
                }
            }
        }
    }

    private async void BtnOpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (vm.RequestOpenFolderAsync != null)
            {
                var path = await vm.RequestOpenFolderAsync();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    vm.OpenFile(path);
                    Hide();
                }
            }
        }
    }
}
