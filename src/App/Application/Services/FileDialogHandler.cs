using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Cine.Avalonia.Services;

/// <summary>
/// Centralized file-dialog helper for all Avalonia StorageProvider operations.
/// 
/// Why: Avalonia has a known race-condition with Flyout menus + FilePicker
/// (see <see href="https://github.com/AvaloniaUI/Avalonia/issues/21433"/>).
/// 
/// Solution: defer the actual dialog call to DispatcherPriority.Background so
/// the Flyout close animation / layout pass completes before the native dialog
/// opens. This prevents a Windows message-pump deadlock where the Flyout's
/// COM modality steals the message loop from the still-waiting FilePicker.
/// 
/// All dialogs from menus, toolbars, and keyboard shortcuts must go through
/// this handler so the fix is applied in one place.
/// </summary>
public sealed class FileDialogHandler
{
    // ════════════════════════════════════════════════════════════════════
    //  File-Type Filters  (centralized — change once, apply everywhere)
    // ════════════════════════════════════════════════════════════════════

    public static readonly FilePickerFileType VideoFilter = new("Video Files")
    {
        Patterns = new[]
        {
            "*.mp4", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.flv", "*.webm",
            "*.m4v", "*.mpg", "*.mpeg", "*.3gp", "*.ts", "*.mts", "*.m2ts",
            "*.vob", "*.ogv", "*.asf", "*.divx", "*.f4v", "*.rm", "*.rmvb"
        }
    };

    public static readonly FilePickerFileType SubtitleFilter = new("Subtitle Files")
    {
        Patterns = new[] { "*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub", "*.idx" }
    };

    public static readonly FilePickerFileType AudioFilter = new("Audio Files")
    {
        Patterns = new[] { "*.mp3", "*.aac", "*.flac", "*.ogg", "*.wav", "*.wma", "*.m4a", "*.opus" }
    };

    // ════════════════════════════════════════════════════════════════════
    //  Fields
    // ════════════════════════════════════════════════════════════════════

    private readonly TopLevel _topLevel;

    /// <summary>
    /// Optional callback invoked before any file dialog opens.
    /// Should close any open Flyout to prevent the Avalonia #18969 deadlock
    /// ("Windows Freeze when StorageProvider called while Flyout is open").
    /// Returns an optional action to reopen the Flyout after the dialog closes,
    /// giving the user the illusion the flyout was never closed.
    /// </summary>
    public Func<Action?>? OnBeforeOpen { get; set; }

    public FileDialogHandler(TopLevel topLevel)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
    }

    private IStorageProvider? Storage => _topLevel.StorageProvider;

    // ════════════════════════════════════════════════════════════════════
    //  Internal Helpers
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Closes any open Flyout before a native dialog, per Avalonia #18969.
    /// Returns a reopen action if a flyout was closed (null otherwise).
    /// After the dialog completes, the reopen action is invoked to restore
    /// the flyout — so the user sees only a brief flicker, not a permanent close.
    /// </summary>
    private Action? CloseAnyFlyout()
    {
        return OnBeforeOpen?.Invoke();
    }

    /// <summary>
    /// Wraps a dialog operation with close-flyout → delay → dialog → reopen-flyout.
    /// The 50ms delay lets Win32 release popup hooks before the COM dialog opens.
    /// </summary>
    private async Task<T?> WithFlyoutGuard<T>(Func<Task<T?>> dialogOp)
    {
        var reopen = CloseAnyFlyout();
        // Small delay to let popup's Win32 hooks fully release before COM dialog
        if (reopen != null)
            await Task.Delay(50);
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(dialogOp, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<FileDialogHandler>()
                .Warning("File dialog failed: {Error}", ex.Message);
            return default;
        }
        finally
        {
            // Reopen flyout on UI thread after dialog closes
            if (reopen != null)
                Dispatcher.UIThread.Post(reopen, DispatcherPriority.Normal);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Public API  (each method = one use-case)
    // ════════════════════════════════════════════════════════════════════

    // ── Open Files ──────────────────────────────────────────────────────
    /// <summary>
    /// Menu: Open &gt; Open Files        | Keyboard: Ctrl+O
    /// </summary>
    public async Task<string[]?> OpenFilesAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var result = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Files",
                AllowMultiple = true,
                FileTypeFilter = new[] { VideoFilter }
            });
            return result?.Select(f => f.Path.LocalPath).ToArray();
        });
    }

    // ── Open Folder ─────────────────────────────────────────────────────
    /// <summary>
    /// Menu: Open &gt; Open Folder       | Keyboard: Ctrl+Shift+O
    /// </summary>
    public async Task<string?> OpenFolderAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var result = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Folder"
            });
            return result?.FirstOrDefault()?.Path.LocalPath;
        });
    }

    // ── Add Files ───────────────────────────────────────────────────────
    /// <summary>
    /// Menu: Open &gt; Add Files         | Context: add to playlist without replacing
    /// </summary>
    public async Task<string[]?> AddFilesAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var result = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Files",
                AllowMultiple = true,
                FileTypeFilter = new[] { VideoFilter }
            });
            return result?.Select(f => f.Path.LocalPath).ToArray();
        });
    }

    // ── Add Audio Track ─────────────────────────────────────────────────
    /// <summary>
    /// Menu: Audio &gt; Add External     | Also used programmatically by AudioManager
    /// </summary>
    public async Task<string?> OpenAudioAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var result = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Audio Track",
                AllowMultiple = false,
                FileTypeFilter = new[] { AudioFilter }
            });
            return result?.FirstOrDefault()?.Path.LocalPath;
        });
    }

    // ── Add Subtitle Track ──────────────────────────────────────────────
    /// <summary>
    /// Menu: Subtitles &gt; Add External | Also used programmatically by SubtitleManager
    /// </summary>
    public async Task<string?> OpenSubtitleAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var result = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Subtitle Track",
                AllowMultiple = false,
                FileTypeFilter = new[] { SubtitleFilter }
            });
            return result?.FirstOrDefault()?.Path.LocalPath;
        });
    }

    // ── Playlist: Save ───────────────────────────────────────────────────
    /// <summary>
    /// PlaylistDialog &gt; Save Playlist  | Save current playlist to .m3u8 file
    /// </summary>
    public async Task<string?> SavePlaylistAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Playlist",
                DefaultExtension = ".m3u8",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Playlist Files")
                    {
                        Patterns = new[] { "*.m3u8", "*.m3u" }
                    }
                }
            });
            return file?.Path.LocalPath;
        });
    }

    // ── Playlist: Load Files ─────────────────────────────────────────────
    /// <summary>
    /// PlaylistDialog &gt; Load Files  | Select files to append to queue
    /// </summary>
    public async Task<string[]?> OpenPlaylistFilesAsync()
    {
        if (Storage is null) return null;
        return await WithFlyoutGuard(async () =>
        {
            var result = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select files to add to queue",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Media Files")
                    {
                        Patterns = new[] { "*.mkv", "*.mp4", "*.avi", "*.mov", "*.wmv", "*.flv", "*.webm", "*.m4v" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*" }
                    }
                }
            });
            return result?.Select(f => f.Path.LocalPath).ToArray();
        });
    }
}
