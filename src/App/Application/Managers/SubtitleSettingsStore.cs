using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cine.Core.Services;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Per-file subtitle settings persistence.
/// Stores global defaults + per-file overrides as JSON in %LOCALAPPDATA%\Cine\subtitles\.
/// </summary>
public sealed class SubtitleSettingsStore
{
    private readonly string _storeDir;
    private readonly ILogger _log;

    public SubtitleSettingsStore()
    {
        _storeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "subtitles");
        _log = global::Cine.Core.Log.ForContext<SubtitleSettingsStore>();
        Directory.CreateDirectory(_storeDir);
        _log.Debug("Constructor: store dir={StoreDir}", _storeDir);
    }

    // ═══════════════════════════════════════════════
    //  Models
    // ═══════════════════════════════════════════════

    public sealed record SubtitleDefaults
    {
        public int Version { get; init; } = 3;
        public bool AutoEnabled { get; init; } = true;
        public string[] PreferredLanguages { get; init; } = new[] { "eng", "jpn", "und" };
        public bool FallbackToExternal { get; init; } = true;
        public string[] ExternalSubDirectories { get; init; } = new[] { "./subs", "./subtitles" };
        public SubtitleStyle Style { get; init; } = new();
    }

    public sealed record SubtitleStyle
    {
        /// <summary>Font scale factor (1.0 = base size). Default 1.1 for premium readability.</summary>
        public double FontScale { get; init; } = 1.1;
        /// <summary>Vertical position (0=top, 100=default bottom).</summary>
        public int Position { get; init; } = 100;
        public double Delay { get; init; } = 0.0;
        /// <summary>Border/outline thickness. Default 2.5 for clearer separation from video.</summary>
        public double BorderSize { get; init; } = 2.5;
        /// <summary>Drop shadow offset. Default 1.5 for subtle depth.</summary>
        public double ShadowOffset { get; init; } = 1.5;
        public double Opacity { get; init; } = 1.0;
        public double Blur { get; init; } = 0.0;
        public bool Bold { get; init; }
        /// <summary>Default system font with broad readability. Fallback-safe.</summary>
        public string Font { get; init; } = "Segoe UI";
        public string Color { get; init; } = "#FFFFFF";
    }

    public sealed record PerFileSettings
    {
        public int Version { get; init; } = 2;
        public string MediaPath { get; init; } = "";
        public string MediaHash { get; init; } = "";
        public int? SelectedTrackId { get; init; }
        public bool? SubtitleVisible { get; init; }
        public SubtitleStyle? StyleOverrides { get; init; }
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    }

    // ═══════════════════════════════════════════════
    //  Defaults
    // ═══════════════════════════════════════════════

    private string DefaultsPath => Path.Combine(_storeDir, "defaults.json");

    private static readonly SubtitleDefaults BuiltInDefaults = new();

    public SubtitleDefaults LoadDefaults()
    {
        try
        {
            var json = File.ReadAllText(DefaultsPath);
            var result = JsonSerializer.Deserialize<SubtitleDefaults>(json);
            if (result != null)
            {
                _log.Trace("LoadDefaults: loaded defaults (version={Version})", result.Version);
                return result;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _log.Warning("LoadDefaults: corrupt defaults file, regenerating — {Error}", ex.Message);
            try { File.Delete(DefaultsPath); }
            catch (Exception innerEx) { _log.Error(innerEx, "LoadDefaults: failed to delete corrupted defaults"); }
        }

        _log.Debug("LoadDefaults: using built-in defaults");
        SaveDefaults(BuiltInDefaults);
        return BuiltInDefaults;
    }

    /// <summary>Load defaults and return preferred languages. Convenience shortcut.</summary>
    public string[] GetPreferredLanguages()
    {
        return LoadDefaults().PreferredLanguages;
    }

    public void SaveDefaults(SubtitleDefaults defaults)
    {
        try
        {
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DefaultsPath, json);
            _log.Debug("SaveDefaults: saved (version={Version})", defaults.Version);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SaveDefaults failed");
        }
    }

    // ═══════════════════════════════════════════════
    //  Per-file
    // ═══════════════════════════════════════════════

    public static string ComputeHash(string filePath)
    {
        var normalized = Path.GetFullPath(filePath).ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string PerFilePath(string hash) => Path.Combine(_storeDir, $"{hash}.json");

    public PerFileSettings? LoadPerFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _log.Trace("LoadPerFile: empty path, returning null");
            return null;
        }

        try
        {
            var hash = ComputeHash(filePath);
            var path = PerFilePath(hash);
            if (!File.Exists(path))
            {
                _log.Trace("LoadPerFile: no settings file for hash={Hash}", hash);
                return null;
            }

            var json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<PerFileSettings>(json);
            _log.Debug("LoadPerFile: loaded for {MediaPath} (hash={Hash}, trackId={TrackId})",
                filePath, hash, result?.SelectedTrackId);
            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _log.Warning("LoadPerFile: corrupt file for {MediaPath}, deleting — {Error}", filePath, ex.Message);
            try { File.Delete(PerFilePath(ComputeHash(filePath))); }
            catch (Exception innerEx) { _log.Error(innerEx, "LoadPerFile: failed to delete corrupted per-file"); }
            return null;
        }
    }

    public void SavePerFile(string filePath, int? trackId, bool? visible, SubtitleStyle? overrides)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            var hash = ComputeHash(filePath);
            var settings = new PerFileSettings
            {
                MediaPath = filePath,
                MediaHash = hash,
                SelectedTrackId = trackId,
                SubtitleVisible = visible,
                StyleOverrides = overrides,
                UpdatedAt = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PerFilePath(hash), json);
            _log.Debug("SavePerFile: saved for {MediaPath} — trackId={TrackId}, visible={Visible}",
                filePath, trackId, visible);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SavePerFile failed for {MediaPath}", filePath);
        }
    }

    public void DeletePerFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            var hash = ComputeHash(filePath);
            var path = PerFilePath(hash);
            if (File.Exists(path))
            {
                File.Delete(path);
                _log.Trace("DeletePerFile: deleted per-file settings for hash={Hash}", hash);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "DeletePerFile failed for {MediaPath}", filePath);
        }
    }

    /// <summary>
    /// Remove orphaned per-file settings entries whose original media files no longer exist.
    /// Returns the number of entries cleaned up. Call periodically or on app startup.
    /// </summary>
    public int CleanupOrphaned()
    {
        int cleaned = 0;
        try
        {
            if (!Directory.Exists(_storeDir))
            {
                _log.Trace("CleanupOrphaned: store directory does not exist");
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(_storeDir, "*.json"))
            {
                // Skip defaults.json — that's always valid
                var fileName = Path.GetFileName(file);
                if (string.Equals(fileName, "defaults.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var json = File.ReadAllText(file);
                    var perFile = JsonSerializer.Deserialize<PerFileSettings>(json);
                    if (perFile == null || string.IsNullOrWhiteSpace(perFile.MediaPath))
                    {
                        // Corrupted — delete
                        File.Delete(file);
                        cleaned++;
                        _log.Debug("CleanupOrphaned: deleted corrupted file {FileName}", fileName);
                        continue;
                    }

                    // Check if the original media file still exists
                    if (!File.Exists(perFile.MediaPath))
                    {
                        File.Delete(file);
                        cleaned++;
                    }
                }
                catch
                {
                    // Unreadable — delete orphan
                    try { File.Delete(file); cleaned++; }
                    catch { /* best-effort */ }
                }
            }
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleSettingsStore>().Error(ex, "CleanupOrphaned failed");
        }
        return cleaned;
    }
}
