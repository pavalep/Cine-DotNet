using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private static readonly FilePickerFileType VideoFilesFilter = new("Video Files")
    {
        Patterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.flv", "*.webm",
                           "*.m4v", "*.mpg", "*.mpeg", "*.3gp", "*.ts", "*.mts", "*.m2ts",
                           "*.vob", "*.ogv", "*.asf", "*.divx", "*.f4v", "*.rm", "*.rmvb" }
    };

    private static readonly FilePickerFileType SubtitleFilesFilter = new("Subtitle Files")
    {
        Patterns = new[] { "*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub", "*.idx" }
    };

    private static readonly FilePickerFileType AudioFilesFilter = new("Audio Files")
    {
        Patterns = new[] { "*.mp3", "*.aac", "*.flac", "*.ogg", "*.wav", "*.wma", "*.m4a", "*.opus" }
    };

    private async Task<string[]?> OpenFileDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Files",
            AllowMultiple = true,
            FileTypeFilter = new[] { VideoFilesFilter }
        });
        return result?.Select(f => f.Path.LocalPath).ToArray();
    }

    private async Task<string?> OpenFolderDialogAsync()
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder"
        });
        return result?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string[]?> OpenAddFilesDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Files",
            AllowMultiple = true,
            FileTypeFilter = new[] { VideoFilesFilter }
        });
        return result?.Select(f => f.Path.LocalPath).ToArray();
    }

    private async Task<string?> OpenSubtitleDialogAsync()
    {
        try
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Subtitle Track",
                AllowMultiple = false,
                FileTypeFilter = new[] { SubtitleFilesFilter }
            });
            return result?.FirstOrDefault()?.Path.LocalPath;
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainWindow>().Warning("Open subtitle dialog failed: {Error}", ex.Message);
            return null;
        }
    }

    private async Task<string?> OpenAudioDialogAsync()
    {
        try
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Audio Track",
                AllowMultiple = false,
                FileTypeFilter = new[] { AudioFilesFilter }
            });
            return result?.FirstOrDefault()?.Path.LocalPath;
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainWindow>().Warning("Open audio dialog failed: {Error}", ex.Message);
            return null;
        }
    }
}
