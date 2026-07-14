using System;
using System.IO;
using System.Text.Json;
using Simba.Core;

namespace Simba.Core.Services;

/// <summary>
/// Thread-safe configuration manager with atomic writes, validation, and backup.
/// Stores settings as JSON in %LOCALAPPDATA%\Simba\settings.json
/// </summary>
public class ConfigService
{
    private readonly string _configDir;
    private readonly string _configFile;
    private readonly string _backupFile;
    private readonly object _lock = new();
    private JsonDocument? _cached;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigService(string? configDir = null)
    {
        _configDir = configDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simba");
        Directory.CreateDirectory(_configDir);
        _configFile = Path.Combine(_configDir, "settings.json");
        _backupFile = _configFile + ".bak";
    }

    public T Get<T>(string key, T defaultValue)
    {
        lock (_lock)
        {
            try
            {
                _cached ??= JsonDocument.Parse(File.ReadAllText(_configFile));
                if (_cached.RootElement.TryGetProperty(key, out var el))
                    return JsonSerializer.Deserialize<T>(el.GetRawText(), JsonOptions) ?? defaultValue;
            }
            catch (Exception ex) when (ex is JsonException or FileNotFoundException or IOException)
            {
                Log.ForContext<ConfigService>().Warning("Config read failed for {Key}, using default", key);
                TryRestoreBackup();
            }
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            try
            {
                var dict = LoadAsDict();
                dict[key] = JsonSerializer.SerializeToElement(value, JsonOptions);
                WriteAtomic(dict);
                _cached = null; // Invalidate cache
            }
            catch (Exception ex)
            {
                Log.ForContext<ConfigService>().Error(ex, "Config write failed for {Key}", key);
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                // Flush any pending writes by re-serializing cache
                if (_cached != null)
                    WriteAtomic(LoadAsDict());
            }
            catch (Exception ex)
            {
                Log.ForContext<ConfigService>().Error(ex, "Config save failed");
            }
        }
    }

    private Dictionary<string, JsonElement> LoadAsDict()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_configFile));
                var dict = new Dictionary<string, JsonElement>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    dict[prop.Name] = prop.Value;
                return dict;
            }
        }
        catch
        {
            TryRestoreBackup();
        }
        return new Dictionary<string, JsonElement>();
    }

    private void WriteAtomic(Dictionary<string, JsonElement> dict)
    {
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        var temp = _configFile + ".tmp";

        // Write to temp, then atomic replace
        File.WriteAllText(temp, json);
        File.Replace(temp, _configFile, _backupFile, ignoreMetadataErrors: true);
    }

    private void TryRestoreBackup()
    {
        try
        {
            if (File.Exists(_backupFile))
            {
                File.Copy(_backupFile, _configFile, true);
                Log.ForContext<ConfigService>().Info("Config restored from backup");
            }
        }
        catch { /* Last resort - silent */ }
    }
}
