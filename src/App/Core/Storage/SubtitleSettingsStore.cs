using System;
using System.IO;
using System.Text.Json;
using Cine.Core.Services;

namespace Cine.Avalonia.Storage;

/// <summary>
/// Per-file subtitle settings persistence.
/// Stores global defaults + per-file overrides as JSON in %LOCALAPPDATA%\Cine\subtitles\.
/// </summary>
public sealed class SubtitleSettingsStore : SettingsStoreBase
{
    private const string DefaultsFile = "defaults.json";
    private static readonly SubtitleDefaults BuiltInDefaults = new();

    public SubtitleSettingsStore() : base("subtitles") { }

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

    private string DefaultsPath => StorePath(DefaultsFile);

    public SubtitleDefaults LoadDefaults()
    {
        var result = LoadJson<SubtitleDefaults>(DefaultsPath);
        if (result != null)
        {
            ForContext().Trace("LoadDefaults: loaded defaults (version={Version})", result.Version);
            return result;
        }

        ForContext().Debug("LoadDefaults: using built-in defaults");
        SaveDefaults(BuiltInDefaults);
        return BuiltInDefaults;
    }

    /// <summary>Load defaults and return preferred languages. Convenience shortcut.</summary>
    public string[] GetPreferredLanguages() => LoadDefaults().PreferredLanguages;

    public void SaveDefaults(SubtitleDefaults defaults)
    {
        SaveJson(DefaultsPath, defaults);
        ForContext().Debug("SaveDefaults: saved (version={Version})", defaults.Version);
    }

    // ═══════════════════════════════════════════════
    //  Per-file
    // ═══════════════════════════════════════════════

    private string PerFilePath(string hash) => StorePath($"{hash}.json");

    public PerFileSettings? LoadPerFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            ForContext().Trace("LoadPerFile: empty path, returning null");
            return null;
        }

        var hash = ComputeHash(filePath);
        var path = PerFilePath(hash);
        if (!File.Exists(path))
        {
            ForContext().Trace("LoadPerFile: no settings file for hash={Hash}", hash);
            return null;
        }

        var result = LoadJson<PerFileSettings>(path);
        ForContext().Debug("LoadPerFile: loaded for {MediaPath} (hash={Hash}, trackId={TrackId})",
            filePath, hash, result?.SelectedTrackId);
        return result;
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
            SaveJson(PerFilePath(hash), settings);
            ForContext().Debug("SavePerFile: saved for {MediaPath} — trackId={TrackId}, visible={Visible}",
                filePath, trackId, visible);
        }
        catch (Exception ex)
        {
            ForContext().Error(ex, "SavePerFile failed for {MediaPath}", filePath);
        }
    }

    public void DeletePerFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        var hash = ComputeHash(filePath);
        TryDelete(PerFilePath(hash));
        ForContext().Trace("DeletePerFile: deleted per-file settings for hash={Hash}", hash);
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
            if (!Directory.Exists(StoreDirectory))
            {
                ForContext().Trace("CleanupOrphaned: store directory does not exist");
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(StoreDirectory, "*.json"))
            {
                var fileName = Path.GetFileName(file);
                if (string.Equals(fileName, DefaultsFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var json = File.ReadAllText(file);
                    var perFile = JsonSerializer.Deserialize<PerFileSettings>(json);
                    if (perFile == null || string.IsNullOrWhiteSpace(perFile.MediaPath))
                    {
                        File.Delete(file);
                        cleaned++;
                        ForContext().Debug("CleanupOrphaned: deleted corrupted file {FileName}", fileName);
                        continue;
                    }

                    if (!File.Exists(perFile.MediaPath))
                    {
                        File.Delete(file);
                        cleaned++;
                    }
                }
                catch
                {
                    try { File.Delete(file); cleaned++; }
                    catch { /* best-effort */ }
                }
            }
        }
        catch (Exception ex)
        {
            ForContext().Error(ex, "CleanupOrphaned failed");
        }
        return cleaned;
    }
}
