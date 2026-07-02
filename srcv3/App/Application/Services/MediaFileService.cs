using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        ".mp3", ".aac", ".flac", ".ogg", ".wav", ".wma", ".m4a", ".opus"
    };

    private static readonly HashSet<string> SupportedExtensions = new(
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
    };

    /// <inheritdoc/>
    public bool IsValidMediaFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
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
}
