using System.Threading.Tasks;

namespace Simba.Avalonia.Services;

/// <summary>
/// Abstracts file-dialog operations so ViewModels can request file selection
/// without depending on Avalonia's UI-layer StorageProvider.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Open one or more media files.</summary>
    Task<string[]?> OpenFilesAsync();

    /// <summary>Select a folder.</summary>
    Task<string?> OpenFolderAsync();

    /// <summary>Add media files to the current playlist.</summary>
    Task<string[]?> AddFilesAsync();

    /// <summary>Select an external subtitle file.</summary>
    Task<string?> OpenSubtitleAsync();

    /// <summary>Select an external audio track file.</summary>
    Task<string?> OpenAudioAsync();
}
