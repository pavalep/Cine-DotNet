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

    /// <summary>
    /// Gets the directory of the currently playing video to use as the suggested
    /// starting folder for external track file dialogs.
    /// </summary>
    private IStorageFolder? GetVideoFolder()
    {
        var videoPath = _viewModel?.FilePath;
        if (string.IsNullOrWhiteSpace(videoPath))
            return null;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(videoPath);
            if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                return StorageProvider.TryGetFolderFromPathAsync(dir).GetAwaiter().GetResult();
        }
        catch { /* best-effort */ }
        return null;
    }

    private async Task<string?> OpenSubtitleDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Subtitle Track",
            AllowMultiple = false,
            FileTypeFilter = new[] { SubtitleFilesFilter },
            SuggestedStartLocation = GetVideoFolder()
        });
        return result?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> OpenAudioDialogAsync()
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Audio Track",
            AllowMultiple = false,
            FileTypeFilter = new[] { AudioFilesFilter },
            SuggestedStartLocation = GetVideoFolder()
        });
        return result?.FirstOrDefault()?.Path.LocalPath;
    }
}
