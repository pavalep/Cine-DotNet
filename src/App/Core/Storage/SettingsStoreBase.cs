using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Simba.Core.Services;

namespace Simba.Avalonia.Storage;

/// <summary>
/// Base class for JSON‑based settings stores under %LOCALAPPDATA%\Simba.
/// Provides common file I/O, error handling, and hashing helpers.
/// </summary>
public abstract class SettingsStoreBase
{
    private readonly string _subfolder;

    /// <summary>Root store directory (%LOCALAPPDATA%\Simba).</summary>
    protected string StoreRoot { get; }

    /// <summary>Fully-qualified store directory (StoreRoot + subfolder, if any).</summary>
    protected string StoreDirectory { get; }

    protected SettingsStoreBase(string subfolder = "")
    {
        _subfolder = subfolder;
        StoreRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simba");
        StoreDirectory = string.IsNullOrEmpty(subfolder)
            ? StoreRoot
            : Path.Combine(StoreRoot, subfolder);
        Directory.CreateDirectory(StoreDirectory);
    }

    /// <summary>Resolve an absolute path inside the store directory.</summary>
    protected string StorePath(string relativePath) => Path.Combine(StoreDirectory, relativePath);

    /// <summary>Load and deserialize a JSON file. Returns null if missing or corrupt.</summary>
    protected T? LoadJson<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            ForContext().Warning("Corrupt store file {Path}, deleting: {Error}", path, ex.Message);
            TryDelete(path);
            return null;
        }
    }

    /// <summary>Serialize and write a JSON file.</summary>
    protected void SaveJson<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, options ?? DefaultJsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            ForContext().Error(ex, "SaveJson failed for {Path}", path);
        }
    }

    /// <summary>Delete a file, swallowing IO errors.</summary>
    protected static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) { global::Simba.Core.Log.ForContext(nameof(SettingsStoreBase)).Error(ex, "Failed to delete {Path}", path); }
    }

    /// <summary>Compute a stable short hash from a file path (first 16 hex chars of SHA‑256).</summary>
    protected static string ComputeHash(string filePath)
    {
        var normalized = Path.GetFullPath(filePath).ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes, 0, 16).ToLowerInvariant();
    }

    /// <summary>Get a contextual logger for the concrete type.</summary>
    protected static ILogger ForContext<TStore>() where TStore : SettingsStoreBase
        => global::Simba.Core.Log.ForContext<TStore>();

    /// <summary>Get a contextual logger for the concrete type.</summary>
    protected ILogger ForContext() => global::Simba.Core.Log.ForContext(GetType().Name);

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true
    };
}
