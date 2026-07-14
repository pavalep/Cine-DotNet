using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Simba.Core;

namespace Simba.Avalonia.Services;

/// <summary>
/// Manages screenshot capture with clipboard copy and file save options.
/// Supports configurable output directory and format.
/// Media metadata can be embedded in filenames (e.g. "MediaName_16x9_SDR_1920x1080").
/// </summary>
public class ScreenshotService
{
    private readonly string _outputDir;
    private int _shotCounter;

    private static readonly string[] SupportedFormats = { ".png", ".jpg", ".jpeg", ".bmp" };

    /// <summary>
    /// Fired after each screenshot is saved, with the file path.
    /// Wired by shell to show OSD notification.
    /// </summary>
    public Action<string>? ScreenshotSaved { get; set; }

    /// <summary>The format to use for saved screenshots (default .png).</summary>
    public string Format { get; set; } = ".png";

    /// <summary>
    /// Optional media name to embed in screenshot filenames.
    /// Set when a file is opened (derived from the filename without extension).
    /// </summary>
    public string? MediaName { get; set; }

    /// <summary>
    /// Create the screenshot service.
    /// </summary>
    /// <param name="outputDir">Directory to save screenshots. Defaults to %Pictures%\Simba Screenshots.</param>
    public ScreenshotService(string? outputDir = null)
    {
        _outputDir = outputDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Simba Screenshots");
        Directory.CreateDirectory(_outputDir);
    }

    /// <summary>
    /// Takes a screenshot via the player callback and saves to the configured directory.
    /// Returns the file path of the saved screenshot.
    /// </summary>
    public string SaveScreenshot(Func<string> takeScreenshot, string? format = null)
    {
        var fmt = NormalizeFormat(format ?? Format);
        _shotCounter++;

        var timestamp = DateTime.Now;
        var mediaTag = !string.IsNullOrWhiteSpace(MediaName)
            ? $"{Sanitize(MediaName)}_"
            : "";
        var filename = $"Simba_{mediaTag}{timestamp:yyyy-MM-dd_HHmmss}_{_shotCounter}{fmt}";
        var path = Path.Combine(_outputDir, filename);

        Log.ForContext<ScreenshotService>().Info("Saving screenshot to {Path}", path);
        takeScreenshot();

        // Notify shell (OSD feedback)
        ScreenshotSaved?.Invoke(path);

        return path;
    }

    /// <summary>Takes screenshot and returns raw bytes for clipboard copy.</summary>
    public byte[] CaptureToBytes(global::Avalonia.Media.Imaging.Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Sanitize a string for safe filename use.</summary>
    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
            name = name.Replace(c, '_');
        return name.Trim(' ', '_');
    }

    private string NormalizeFormat(string format)
    {
        var f = format.ToLowerInvariant();
        if (!f.StartsWith(".")) f = "." + f;
        return Array.Exists(SupportedFormats, s => s == f) ? f : ".png";
    }
}
