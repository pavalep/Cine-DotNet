using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Manages screenshot capture with clipboard copy and file save options.
/// Supports configurable output directory and format.
/// </summary>
public class ScreenshotService
{
    private readonly string _outputDir;
    private int _shotCounter;

    private static readonly string[] SupportedFormats = { ".png", ".jpg", ".jpeg", ".bmp" };

    public ScreenshotService(string? outputDir = null)
    {
        _outputDir = outputDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Cine Screenshots");
        Directory.CreateDirectory(_outputDir);
    }

    /// <summary>
    /// Takes a screenshot via the player and saves to the configured directory.
    /// Returns the file path of the saved screenshot.
    /// </summary>
    public string SaveScreenshot(Func<string> takeScreenshot, string? format = null)
    {
        format = NormalizeFormat(format ?? ".png");
        _shotCounter++;

        var timestamp = DateTime.Now;
        var filename = $"screenshot_{timestamp:yyyy-MM-dd_HHmmss}_{_shotCounter}{format}";
        var path = Path.Combine(_outputDir, filename);

        Log.ForContext<ScreenshotService>().Info("Saving screenshot to {Path}", path);
        takeScreenshot();
        return path;
    }

    /// <summary>Takes screenshot and returns raw bytes for clipboard copy.</summary>
    public byte[] CaptureToBytes(global::Avalonia.Media.Imaging.Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }

    private static string NormalizeFormat(string format)
    {
        var f = format.ToLowerInvariant();
        if (!f.StartsWith(".")) f = "." + f;
        return Array.Exists(SupportedFormats, s => s == f) ? f : ".png";
    }
}
