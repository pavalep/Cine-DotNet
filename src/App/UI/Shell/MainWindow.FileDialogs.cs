using System.Threading.Tasks;
using Cine.Avalonia.Services;

namespace Cine.Avalonia;

public partial class MainWindow
{
    // All file-dialog operations are delegated to FileDialogHandler
    // so the Avalonia #21433 workaround (Task.Delay) is applied in one place.

    private FileDialogHandler? _dialogHandler;

    private Task<string[]?> OpenFileDialogAsync() =>
        _dialogHandler!.OpenFilesAsync()!;

    private Task<string?> OpenFolderDialogAsync() =>
        _dialogHandler!.OpenFolderAsync()!;

    private Task<string[]?> OpenAddFilesDialogAsync() =>
        _dialogHandler!.AddFilesAsync()!;

    private Task<string?> OpenSubtitleDialogAsync() =>
        _dialogHandler!.OpenSubtitleAsync()!;

    private Task<string?> OpenAudioDialogAsync() =>
        _dialogHandler!.OpenAudioAsync()!;
}
