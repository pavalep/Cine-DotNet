using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Downloads native runtime dependencies (libmpv, ANGLE) on first launch.
/// These are excluded from the MSIX/MSI to keep the package small (~15 MB).
/// Downloaded once to %LOCALAPPDATA%\Cine\runtime\ and cached permanently.
/// 
/// Resolution order:
/// 1. AppContext.BaseDirectory (DLLs bundled next to EXE)
/// 2. %LOCALAPPDATA%\Cine\runtime\ (previously downloaded)
/// 3. Download from release archives (extracted automatically when needed)
/// 4. Clear instructions if all automated methods fail
/// </summary>
public class RuntimeDownloader
{
    private static readonly string RuntimeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "runtime");

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(15),
        MaxResponseContentBufferSize = 200_000_000 // 200 MB
    };

    /// <summary>Assets needed for mpv playback.</summary>
    private static readonly (string File, long ExpectedSize)[] Assets =
    [
        ("libmpv-2.dll",  117_440_512),
        ("libEGL.dll",       524_288),
        ("libGLESv2.dll",  7_864_320),
    ];

    /// <summary>
    /// Checks if all runtime files are available (bundled or cached).
    /// </summary>
    public static bool IsRuntimeReady()
    {
        // Check bundled first
        if (CheckBundled())
            return true;

        // Check cached
        foreach (var (file, _) in Assets)
        {
            if (!File.Exists(Path.Combine(RuntimeDir, file)))
                return false;
        }
        return true;
    }

    /// <summary>Returns the runtime directory path.</summary>
    public static string GetRuntimeDirectory() => RuntimeDir;

    /// <summary>
    /// Ensures all required native DLLs exist. Checks bundled location first,
    /// then cached download directory, finally downloads from release archives.
    /// </summary>
    public static async Task<string> EnsureRuntimeAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // Phase 1: Check if DLLs exist next to the executable (bundled build)
        if (CheckBundled())
        {
            progress?.Report("Runtime ready (bundled).");
            return AppContext.BaseDirectory;
        }

        Directory.CreateDirectory(RuntimeDir);

        // Phase 2: Check cached runtime directory
        if (AllCached())
        {
            progress?.Report("Runtime ready (cached).");
            return RuntimeDir;
        }

        // Phase 3: Download missing files
        // libmpv-2.dll comes from zhongfly/mpv-winbuild (daily 7z archives)
        // libEGL.dll and libGLESv2.dll come from ANGLE (bundled with Windows SDK)
        await DownloadLibMpvAsync(progress, ct);

        if (AllCached())
        {
            progress?.Report("Runtime ready.");
            return RuntimeDir;
        }

        // Phase 4: If automated download failed, give clear manual instructions
        var missing = new System.Collections.Generic.List<string>();
        foreach (var (file, _) in Assets)
        {
            if (!File.Exists(Path.Combine(RuntimeDir, file)))
                missing.Add(file);
        }

        var msg = $"Missing native DLLs: {string.Join(", ", missing)}\n\n" +
                  $"1. Download mpv-dev-x86_64-*.7z from:\n" +
                  $"   https://github.com/zhongfly/mpv-winbuild/releases/latest\n\n" +
                  $"2. Extract libmpv-2.dll from the archive (it's inside the 7z)\n\n" +
                  $"3. Place it in: {RuntimeDir}\n\n" +
                  $"libEGL.dll and libGLESv2.dll are included with the Windows SDK.\n" +
                  $"They should be placed next to the executable or in {RuntimeDir}.";

        throw new InvalidOperationException(msg);
    }

    /// <summary>
    /// Download libmpv-2.dll from zhongfly/mpv-winbuild release archives.
    /// The daily builds are 7z archives — we download and extract just the DLL.
    /// </summary>
    private static async Task DownloadLibMpvAsync(
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var dest = Path.Combine(RuntimeDir, "libmpv-2.dll");
        if (File.Exists(dest))
            return;

        try
        {
            // Fetch the latest release info from GitHub API to get the exact archive URL
            progress?.Report("Fetching latest mpv release info...");

            using var apiResponse = await _http.GetAsync(
                "https://api.github.com/repos/zhongfly/mpv-winbuild/releases/latest",
                ct);

            apiResponse.EnsureSuccessStatusCode();
            var json = await apiResponse.Content.ReadAsStringAsync(ct);

            // Extract the first mpv-dev-x86_64 asset URL from the JSON response
            var searchKey = "\"browser_download_url\":\"";
            var searchName = "mpv-dev-x86_64-";
            var nameIdx = json.IndexOf(searchName, StringComparison.Ordinal);
            if (nameIdx < 0)
                throw new InvalidOperationException("Could not find mpv-dev-x86_64 asset in latest release.");

            // Find the download_url key before this asset name
            var urlStart = json.LastIndexOf(searchKey, nameIdx, StringComparison.Ordinal);
            if (urlStart < 0)
                throw new InvalidOperationException("Could not parse download URL from release data.");
            urlStart += searchKey.Length;
            var urlEnd = json.IndexOf('"', urlStart);
            var archiveUrl = json[urlStart..urlEnd];

            progress?.Report($"Downloading mpv archive (31 MB)...");

            var tempDir = Path.Combine(RuntimeDir, ".tmp_extract");
            Directory.CreateDirectory(tempDir);
            var archivePath = Path.Combine(tempDir, "mpv-dev.7z");

            // Download the archive
            using (var resp = await _http.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1;

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var file = File.Create(archivePath);

                var buffer = new byte[81920]; // 80 KB buffer
                long read = 0;
                int bytes;

                while ((bytes = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, bytes), ct);
                    read += bytes;

                    if (total > 0)
                    {
                        var pct = (int)(read * 100 / total);
                        if (pct % 10 == 0)
                            progress?.Report($"  Downloading: {pct}% ({read / 1024 / 1024} / {total / 1024 / 1024} MB)");
                    }
                }
            }

            progress?.Report("  Extracting libmpv-2.dll...");

            // Extract the DLL from the 7z archive using 7-Zip (must be installed or bundled)
            var sevenZipPath = FindSevenZip();
            if (sevenZipPath != null)
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = $"e \"{archivePath}\" -o\"{RuntimeDir}\" libmpv-2.dll -y -bso0 -bsp0",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                using var proc = System.Diagnostics.Process.Start(psi)!;
                var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
                var stderr = await proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"7z extraction failed (exit code {proc.ExitCode}).\n" +
                        $"StdErr: {stderr}\nStdOut: {stdout}");
                }
            }
            else
            {
                // No 7z available — inform user
                throw new InvalidOperationException(
                    "7-Zip not found. Please install 7-Zip (https://7-zip.org) " +
                    "or manually extract libmpv-2.dll from:\n" +
                    $"{archiveUrl}\n" +
                    $"and place it in: {RuntimeDir}");
            }

            // Clean up
            try { File.Delete(archivePath); Directory.Delete(tempDir, true); } catch { }

            if (File.Exists(dest))
                progress?.Report("  libmpv-2.dll: done.");
            else
                throw new InvalidOperationException("Extraction completed but libmpv-2.dll not found in archive.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to download libmpv-2.dll: {ex.Message}\n\n" +
                $"Manual download: https://github.com/zhongfly/mpv-winbuild/releases/latest\n" +
                $"Extract libmpv-2.dll and place it in: {RuntimeDir}", ex);
        }
    }

    /// <summary>Find 7-Zip executable on the system.</summary>
    private static string? FindSevenZip()
    {
        // Check common install paths
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe",
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        // Check PATH
        try
        {
            var which = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "where",
                Arguments = "7z.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });

            if (which != null)
            {
                var output = which.StandardOutput.ReadLine();
                which.WaitForExit(2000);
                if (which.ExitCode == 0 && output != null && File.Exists(output.Trim()))
                    return output.Trim();
            }
        }
        catch { }

        return null;
    }

    private static bool CheckBundled()
    {
        foreach (var (file, _) in Assets)
        {
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, file)))
                return false;
        }
        return true;
    }

    private static bool AllCached()
    {
        foreach (var (file, _) in Assets)
        {
            if (!File.Exists(Path.Combine(RuntimeDir, file)))
                return false;
        }
        return true;
    }
}
