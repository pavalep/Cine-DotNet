using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Cine.Avalonia.State;

/// <summary>
/// Audio settings persistence — per-file + global defaults.
/// Stores in %LOCALAPPDATA%\Cine\audio-settings.json as a single compound file.
/// </summary>
public sealed class AudioSettingsStore : SettingsStoreBase
{
    private const string FileName = "audio-settings.json";
    private static readonly AudioGlobalDefaults BuiltInDefaults = new();

    public AudioSettingsStore() : base() { }

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

    internal sealed record CompoundStore
    {
        public int Version { get; init; } = 1;
        public AudioGlobalDefaults Global { get; init; } = new();
        public Dictionary<string, AudioPerFileSettings>? PerFile { get; init; }
    }

    // ═══════════════════════════════════════════════
    //  Load / Save Compound
    // ═══════════════════════════════════════════════

    private CompoundStore LoadCompound()
        => LoadJson<CompoundStore>(StorePath(FileName)) ?? new CompoundStore();

    private void SaveCompound(CompoundStore store)
        => SaveJson(StorePath(FileName), store);

    // ═══════════════════════════════════════════════
    //  Global Defaults
    // ═══════════════════════════════════════════════

    public AudioGlobalDefaults LoadDefaults()
        => LoadCompound().Global ?? BuiltInDefaults;

    public void SaveDefaults(AudioGlobalDefaults defaults)
        => SaveCompound(LoadCompound() with { Global = defaults });

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
            return compound.PerFile != null && compound.PerFile.TryGetValue(hash, out var settings)
                ? settings
                : null;
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
                ? new Dictionary<string, AudioPerFileSettings>(compound.PerFile)
                : new Dictionary<string, AudioPerFileSettings>();
            perFile[hash] = settings;
            SaveCompound(compound with { PerFile = perFile });
        }
        catch (Exception ex)
        {
            ForContext().Error(ex, "SavePerFile failed");
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
            var perFile = new Dictionary<string, AudioPerFileSettings>(compound.PerFile);
            perFile.Remove(hash);
            SaveCompound(compound with { PerFile = perFile });
        }
        catch (Exception ex)
        {
            ForContext().Error(ex, "DeletePerFile failed");
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
            ForContext().Error(ex, "ClearAllPerFile failed");
        }
    }
}
