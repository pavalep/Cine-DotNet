using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cine.Avalonia.Services;

/// <summary>
/// Validates media files and provides file-system utilities for media operations.
/// </summary>
public class MediaFileService : IMediaFileService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".mts", ".m2ts",
        ".vob", ".ogv", ".asf", ".divx", ".f4v", ".rm", ".rmvb"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".aac", ".flac", ".ogg", ".wav", ".wma", ".m4a", ".opus",
        ".ac3", ".dts", ".alac", ".ape", ".aiff"
    };

    private static readonly HashSet<string> _supportedExtensions = new(
        VideoExtensions.Concat(AudioExtensions), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Common media file magic-bytes (file signatures) for validation.
    /// First 4-8 bytes of the file are compared against these patterns.
    /// </summary>
    private static readonly Dictionary<string, byte[]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"]  = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], // ftyp box
        [".m4v"]  = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], // ftyp box
        [".mkv"]  = [0x1A, 0x45, 0xDF, 0xA3], // Matroska header
        [".webm"] = [0x1A, 0x45, 0xDF, 0xA3], // Matroska header
        [".avi"]  = [0x52, 0x49, 0x46, 0x46], // RIFF
        [".mov"]  = [0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70], // ftyp box
        [".wmv"]  = [0x30, 0x26, 0xB2, 0x75], // ASF header
        [".flv"]  = [0x46, 0x4C, 0x56],       // FLV
        [".mp3"]  = [0xFF, 0xFB],              // MPEG audio frame sync
        [".flac"] = [0x66, 0x4C, 0x61, 0x43], // fLaC
        [".ogg"]  = [0x4F, 0x67, 0x67, 0x53], // OggS
        [".wav"]  = [0x52, 0x49, 0x46, 0x46], // RIFF/WAVE
        [".ac3"]  = [0x0B, 0x77],             // AC3 sync word
        [".dts"]  = [0x7F, 0xFE, 0x80, 0x01], // DTS sync
        [".ape"]  = [0x4D, 0x41, 0x43, 0x20], // MAC "MAC "
        [".aiff"] = [0x46, 0x4F, 0x52, 0x4D], // FORM
    };

    /// <inheritdoc/>
    public bool IsValidMediaFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || !_supportedExtensions.Contains(ext))
            return false;

        // Magic-byte validation as second layer (best-effort, I/O may fail)
        return IsValidMagicBytes(path, ext);
    }

    /// <summary>
    /// Verify file header signatures to reject renamed non-media files.
    /// </summary>
    private static bool IsValidMagicBytes(string path, string ext)
    {
        try
        {
            if (!MagicBytes.TryGetValue(ext, out var expected))
                return true; // No magic-bytes defined for this extension — trust extension check

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16);
            Span<byte> header = stackalloc byte[expected.Length];
            var read = fs.Read(header);
            return read >= expected.Length && header[..expected.Length].SequenceEqual(expected);
        }
        catch
        {
            // Can't read file header — trust extension check as fallback
            return true;
        }
    }

    /// <inheritdoc/>
    public string[] FilterMediaFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return Array.Empty<string>();

        return paths.Where(IsValidMediaFile).ToArray();
    }

    /// <inheritdoc/>
    public string GenerateScreenshotPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(dir, $"cine_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

    /// <inheritdoc/>
    public bool IsMediaFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && _supportedExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    public bool IsVideoFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && VideoExtensions.Contains(ext);
    }

    /// <inheritdoc/>
    IReadOnlySet<string> IMediaFileService.SupportedExtensions => _supportedExtensions;

    /// <inheritdoc/>
    IReadOnlySet<string> IMediaFileService.VideoExtensions => VideoExtensions;

    /// <inheritdoc/>
    public List<string> ScanFolderForMedia(string folderPath)
    {
        var results = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                if (IsMediaFile(file))
                    results.Add(file);
            }
        }
        catch
        {
            // skip inaccessible folders
        }
        return results;
    }

    /// <inheritdoc/>
    public Task<string[]> ScanFolderAsync(string folder, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var results = new List<string>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsMediaFile(file))
                        results.Add(file);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                // skip inaccessible folders — continue with whatever we found
            }
            catch (IOException)
            {
                // skip folders with I/O errors
            }
            return NaturalSort(results);
        }, ct);
    }

    /// <inheritdoc/>
    public string[] NaturalSort(IEnumerable<string> paths)
    {
        if (paths == null) return Array.Empty<string>();

        // Regex splits on digit/non-digit boundaries for natural comparison
        return paths.OrderBy(p => p, new NaturalStringComparer()).ToArray();
    }

    /// <summary>Compares strings using natural sort order (numeric segments by value).</summary>
    private sealed class NaturalStringComparer : IComparer<string>
    {
        private static readonly Regex SplitRegex = new(@"(\d+)", RegexOptions.Compiled);

        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xParts = SplitRegex.Split(x);
            var yParts = SplitRegex.Split(y);
            int len = Math.Min(xParts.Length, yParts.Length);

            for (int i = 0; i < len; i++)
            {
                if (xParts[i] == yParts[i]) continue;

                // Compare numeric segments as integers
                if (long.TryParse(xParts[i], out var xNum) && long.TryParse(yParts[i], out var yNum))
                {
                    int cmp = xNum.CompareTo(yNum);
                    if (cmp != 0) return cmp;
                }

                return string.Compare(xParts[i], yParts[i], StringComparison.OrdinalIgnoreCase);
            }

            return xParts.Length.CompareTo(yParts.Length);
        }
    }
}
