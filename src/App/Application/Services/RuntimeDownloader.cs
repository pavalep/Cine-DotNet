using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cine.Avalonia.Services;

/// <summary>
/// Downloads native runtime dependencies (libmpv, ANGLE) on first launch.
/// These are excluded from the MSIX/MSI to keep the package small (~15 MB).
/// Downloaded once to %LOCALAPPDATA%\Cine\runtime\ and cached permanently.
/// </summary>
public class RuntimeDownloader
{
    private static readonly string RuntimeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "runtime");

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(10) // 112 MB libmpv over slow connections
    };

    /// <summary>Base URL for runtime downloads (GitHub Releases CDN).</summary>
    /// <remarks>
    /// Override via CINE_RUNTIME_URL environment variable for testing.
    /// Default: GitHub Releases where libmpv DLLs are uploaded alongside the MSIX.
    /// </remarks>
    public static string BaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("CINE_RUNTIME_URL")
        ?? "https://github.com/user/Cine/releases/latest/download";

    /// <summary>
    /// Ensures all required native DLLs exist in the runtime directory.
    /// Downloads missing files with progress reporting.
    /// </summary>
    /// <returns>Directory path where DLLs are located.</returns>
    public static async Task<string> EnsureRuntimeAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(RuntimeDir);

        // Files that may need downloading (name → URL path)
        var assets = new (string File, string UrlPath, long ExpectedSize)[]
        {
            ("libmpv-2.dll",     "libmpv-2.dll",     117_440_512),
            ("libEGL.dll",        "libEGL.dll",          524_288),
            ("libGLESv2.dll",     "libGLESv2.dll",     7_864_320),
        };

        foreach (var (file, urlPath, _) in assets)
        {
            ct.ThrowIfCancellationRequested();

            var dest = Path.Combine(RuntimeDir, file);

            if (File.Exists(dest))
            {
                progress?.Report($"{file} — already installed");
                continue;
            }

            progress?.Report($"Downloading {file}...");
            await DownloadFileAsync(urlPath, dest, progress, ct);
        }

        progress?.Report("Runtime ready.");
        return RuntimeDir;
    }

    private static async Task DownloadFileAsync(
        string urlPath,
        string destPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var url = $"{BaseUrl.TrimEnd('/')}/{urlPath}";
        var tempPath = destPath + ".tmp";

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var file = File.Create(tempPath);

            var buffer = new byte[8192];
            long read = 0;
            int bytes;
            int lastPercent = -1;

            while ((bytes = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, bytes), ct);
                read += bytes;

                if (total > 0)
                {
                    var pct = (int)(read * 100 / total);
                    if (pct != lastPercent && pct % 10 == 0)
                    {
                        progress?.Report($"  {Path.GetFileName(destPath)}: {pct}% ({read / 1024 / 1024} / {total / 1024 / 1024} MB)");
                        lastPercent = pct;
                    }
                }
            }

            // Atomic rename — prevents corrupted files from failed downloads
            File.Move(tempPath, destPath, overwrite: true);
            progress?.Report($"  {Path.GetFileName(destPath)}: done.");
        }
        catch
        {
            // Clean up partial download
            try { File.Delete(tempPath); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>Checks if all runtime files are present.</summary>
    public static bool IsRuntimeReady()
    {
        return File.Exists(Path.Combine(RuntimeDir, "libmpv-2.dll"))
            && File.Exists(Path.Combine(RuntimeDir, "libEGL.dll"))
            && File.Exists(Path.Combine(RuntimeDir, "libGLESv2.dll"));
    }

    /// <summary>Returns the runtime directory path.</summary>
    public static string GetRuntimeDirectory() => RuntimeDir;
}
