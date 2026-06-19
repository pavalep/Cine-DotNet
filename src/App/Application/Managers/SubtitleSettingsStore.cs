using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Per-file subtitle settings persistence.
/// Stores global defaults + per-file overrides as JSON in %LOCALAPPDATA%\Cine\subtitles\.
/// </summary>
public sealed class SubtitleSettingsStore
{
    private readonly string _storeDir;

    public SubtitleSettingsStore()
    {
        _storeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "subtitles");
        Directory.CreateDirectory(_storeDir);
    }

    // ═══════════════════════════════════════════════
    //  Models
    // ═══════════════════════════════════════════════

    public sealed record SubtitleDefaults
    {
        public int Version { get; init; } = 2;
        public bool AutoEnabled { get; init; } = true;
        public string[] PreferredLanguages { get; init; } = new[] { "eng", "jpn", "und" };
        public bool FallbackToExternal { get; init; } = true;
        public string[] ExternalSubDirectories { get; init; } = new[] { "./subs", "./subtitles" };
        public SubtitleStyle Style { get; init; } = new();
    }

    public sealed record SubtitleStyle
    {
        public double FontScale { get; init; } = 1.0;
        public int Position { get; init; } = 100;
        public double Delay { get; init; } = 0.0;
        public double BorderSize { get; init; } = 2.0;
        public double ShadowOffset { get; init; } = 1.0;
        public string Font { get; init; } = "Arial";
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
            if (result != null) return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupted — regenerate
            try { File.Delete(DefaultsPath); }
            catch (Exception innerEx) { global::Cine.Core.Log.ForContext<SubtitleSettingsStore>().Error(innerEx, "Failed to delete corrupted defaults"); }
        }

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
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleSettingsStore>().Error(ex, "SaveDefaults failed");
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
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        try
        {
            var hash = ComputeHash(filePath);
            var path = PerFilePath(hash);
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PerFileSettings>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupted — delete and return null
            try { File.Delete(PerFilePath(ComputeHash(filePath))); }
            catch (Exception innerEx) { global::Cine.Core.Log.ForContext<SubtitleSettingsStore>().Error(innerEx, "Failed to delete corrupted per-file settings"); }
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
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleSettingsStore>().Error(ex, "SavePerFile failed");
        }
    }

    public void DeletePerFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            var path = PerFilePath(ComputeHash(filePath));
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleSettingsStore>().Error(ex, "DeletePerFile failed");
        }
    }
}
