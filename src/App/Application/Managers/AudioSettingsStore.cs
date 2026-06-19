using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Audio settings persistence — per-file + global defaults.
/// Stores in %LOCALAPPDATA%\Cine\audio-settings.json as a single compound file.
/// </summary>
public sealed class AudioSettingsStore
{
    private readonly string _storePath;

    public AudioSettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "audio-settings.json");
    }

    // ═══════════════════════════════════════════════
    //  Models
    // ═══════════════════════════════════════════════

    public sealed record AudioGlobalDefaults
    {
        public double Volume { get; init; } = 50.0;
        public bool IsMuted { get; init; } = false;
        public string EqualizerPreset { get; init; } = "Flat";
        public bool IsNormalizationEnabled { get; init; } = false;
        public bool IsDialogueBoostEnabled { get; init; } = false;
        public int LastSelectedTrackId { get; init; } = -1;
    }

    public sealed record AudioPerFileSettings
    {
        public int SelectedTrackId { get; init; } = -1;
        public float AudioDelay { get; init; } = 0.0f;
        public double[]? EqualizerBands { get; init; }
        public string? EqualizerPreset { get; init; }
    }

    private sealed record CompoundStore
    {
        public int Version { get; init; } = 1;
        public AudioGlobalDefaults Global { get; init; } = new();
        public System.Collections.Generic.Dictionary<string, AudioPerFileSettings>? PerFile { get; init; }
    }

    private static readonly AudioGlobalDefaults BuiltInDefaults = new();

    // ═══════════════════════════════════════════════
    //  Hash
    // ═══════════════════════════════════════════════

    private static string ComputeHash(string filePath)
    {
        var normalized = Path.GetFullPath(filePath).ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes, 0, 16).ToLowerInvariant(); // first 16 hex chars
    }

    // ═══════════════════════════════════════════════
    //  Load / Save Compound
    // ═══════════════════════════════════════════════

    private CompoundStore LoadCompound()
    {
        try
        {
            if (!File.Exists(_storePath))
                return new CompoundStore();

            var json = File.ReadAllText(_storePath);
            var result = JsonSerializer.Deserialize<CompoundStore>(json);
            if (result != null && result.Version >= 1)
                return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            try { File.Delete(_storePath); }
            catch (Exception innerEx) { global::Cine.Core.Log.ForContext<AudioSettingsStore>().Error(innerEx, "Failed to delete corrupted audio settings"); }
        }
        return new CompoundStore();
    }

    private void SaveCompound(CompoundStore store)
    {
        try
        {
            var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storePath, json);
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<AudioSettingsStore>().Error(ex, "SaveCompound failed");
        }
    }

    // ═══════════════════════════════════════════════
    //  Global Defaults
    // ═══════════════════════════════════════════════

    public AudioGlobalDefaults LoadDefaults()
    {
        var compound = LoadCompound();
        return compound.Global ?? BuiltInDefaults;
    }

    public void SaveDefaults(AudioGlobalDefaults defaults)
    {
        var compound = LoadCompound();
        SaveCompound(compound with { Global = defaults });
    }

    // ═══════════════════════════════════════════════
    //  Per-file
    // ═══════════════════════════════════════════════

    public AudioPerFileSettings? LoadPerFile(string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath)) return null;
        try
        {
            var compound = LoadCompound();
            var hash = ComputeHash(mediaPath);
            if (compound.PerFile == null || !compound.PerFile.TryGetValue(hash, out var settings))
                return null;
            return settings;
        }
        catch
        {
            return null;
        }
    }

    public void SavePerFile(string mediaPath, AudioPerFileSettings settings)
    {
        if (string.IsNullOrWhiteSpace(mediaPath)) return;
        try
        {
            var compound = LoadCompound();
            var hash = ComputeHash(mediaPath);
            var perFile = compound.PerFile != null
                ? new System.Collections.Generic.Dictionary<string, AudioPerFileSettings>(compound.PerFile)
                : new System.Collections.Generic.Dictionary<string, AudioPerFileSettings>();
            perFile[hash] = settings;
            SaveCompound(compound with { PerFile = perFile });
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<AudioSettingsStore>().Error(ex, "SavePerFile failed");
        }
    }

    public void DeletePerFile(string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath)) return;
        try
        {
            var compound = LoadCompound();
            var hash = ComputeHash(mediaPath);
            if (compound.PerFile == null || !compound.PerFile.ContainsKey(hash)) return;
            var perFile = new System.Collections.Generic.Dictionary<string, AudioPerFileSettings>(compound.PerFile);
            perFile.Remove(hash);
            SaveCompound(compound with { PerFile = perFile });
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<AudioSettingsStore>().Error(ex, "DeletePerFile failed");
        }
    }

    /// <summary>Remove ALL per-file entries (used by Reset to Default).</summary>
    public void ClearAllPerFile()
    {
        try
        {
            var compound = LoadCompound();
            SaveCompound(compound with { PerFile = null });
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<AudioSettingsStore>().Error(ex, "ClearAllPerFile failed");
        }
    }
}
