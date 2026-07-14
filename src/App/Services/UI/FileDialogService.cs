using System.Threading.Tasks;

namespace Simba.Avalonia.Services;

/// <summary>
/// Wraps <see cref="FileDialogHandler"/> as an <see cref="IFileDialogService"/>
/// so ViewModels and Managers can request file selection without UI-layer coupling.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly FileDialogHandler _handler;

    public FileDialogService(FileDialogHandler handler)
    {
        _handler = handler ?? throw new System.ArgumentNullException(nameof(handler));
    }

    /// <inheritdoc/>
    public Task<string[]?> OpenFilesAsync() => _handler.OpenFilesAsync();

    /// <inheritdoc/>
    public Task<string?> OpenFolderAsync() => _handler.OpenFolderAsync();

    /// <inheritdoc/>
    public Task<string[]?> AddFilesAsync() => _handler.AddFilesAsync();

    /// <inheritdoc/>
    public Task<string?> OpenSubtitleAsync() => _handler.OpenSubtitleAsync();

    /// <inheritdoc/>
    public Task<string?> OpenAudioAsync() => _handler.OpenAudioAsync();
}
