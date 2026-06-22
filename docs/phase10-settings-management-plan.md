# Phase 10.7 — Settings Management Consolidation

> **Goal**: Unify all fragmented settings stores into a single, versioned, centralized settings service. **Estimated effort: 2 hours**.

---

## Current State

Settings are scattered across **5+ independent stores**, each with its own save/load logic, error handling, and file path construction:

| Store | File Format | Location | Lines of Code |
|-------|-------------|----------|---------------|
| `PlaylistSettingsStore` | JSON | `%LOCALAPPDATA%\Cine\...` | ~50 |
| `AudioSettingsStore` | JSON | `%LOCALAPPDATA%\Cine\...` | ~50 |
| `SubtitleSettingsStore` | JSON | `%LOCALAPPDATA%\Cine\...` | ~50 |
| `MainViewModel` (recent files) | JSON | `%LOCALAPPDATA%\Cine\...` | ~40 |
| `ResumeService` | JSON | `%LOCALAPPDATA%\Cine\...` | ~30 |

Each duplicates:
- Directory creation logic
- JSON serialization/deserialization
- Error handling for file I/O
- File path construction

---

## Implementation Steps

### Step 1: Create `ISettingsService` Interface

```csharp
public interface ISettingsService
{
    T? Load<T>(string key) where T : class;
    void Save<T>(string key, T value) where T : class;
    bool Exists(string key);
    string SettingsDirectory { get; }
}
```

### Step 2: Create `JsonSettingsService` Implementation

```csharp
public class JsonSettingsService : ISettingsService
{
    private readonly string _basePath;
    private readonly string _fileExtension = ".json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public JsonSettingsService(string appName = "Cine")
    {
        _basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName, "Settings");
        Directory.CreateDirectory(_basePath);
    }

    public string SettingsDirectory => _basePath;

    public T? Load<T>(string key) where T : class
    {
        try
        {
            var path = GetFilePath(key);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (Exception ex)
        {
            Log.ForContext<JsonSettingsService>()
                .Warning(ex, "Failed to load settings: {Key}", key);
            return null;
        }
    }

    public void Save<T>(string key, T value) where T : class
    {
        try
        {
            var path = GetFilePath(key);
            var json = JsonSerializer.Serialize(value, Options);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Log.ForContext<JsonSettingsService>()
                .Warning(ex, "Failed to save settings: {Key}", key);
        }
    }

    public bool Exists(string key) => File.Exists(GetFilePath(key));

    private string GetFilePath(string key) =>
        Path.Combine(_basePath, SanitizeKey(key) + _fileExtension);

    private static string SanitizeKey(string key)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(key.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
```

### Step 3: Add Schema Versioning

```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsManifest))]
internal partial class SettingsContext : JsonSerializerContext { }

public class SettingsManifest
{
    public int Version { get; set; } = 1;
    public DateTime LastSaved { get; set; }
}

// On every Load/Save, check version and migrate if needed
public class VersionedSettingsService : JsonSettingsService
{
    private const int CurrentVersion = 1;

    public void MigrateIfNeeded()
    {
        var manifest = Load<SettingsManifest>("__manifest__");
        if (manifest == null || manifest.Version < CurrentVersion)
        {
            RunMigration(manifest?.Version ?? 0, CurrentVersion);
            Save("__manifest__", new SettingsManifest
            {
                Version = CurrentVersion,
                LastSaved = DateTime.Now
            });
        }
    }

    private void RunMigration(int fromVersion, int toVersion)
    {
        // Future: add migration steps between versions
        Log.ForContext<VersionedSettingsService>()
            .Info("Migrating settings from v{From} to v{To}", fromVersion, toVersion);
    }
}
```

### Step 4: Migrate Existing Stores

```csharp
// ❌ Before — each store has its own file I/O
public class PlaylistSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "playlist.json");
    // ... save/load logic
}

// ✅ After — delegate to ISettingsService
public class PlaylistSettingsStore
{
    private readonly ISettingsService _settings;

    public PlaylistSettingsStore(ISettingsService settings)
    {
        _settings = settings;
    }

    public PlaylistData? Load() => _settings.Load<PlaylistData>("Playlist");
    public void Save(PlaylistData data) => _settings.Save("Playlist", data);
}
```

---

## Migration Table

| Current Store | New Key | Data Type |
|---------------|---------|-----------|
| `PlaylistSettingsStore` | `"Playlist"` | `PlaylistData` |
| `AudioSettingsStore` | `"Audio"` | `AudioSettings` |
| `SubtitleSettingsStore` | `"Subtitle"` | `SubtitleSettings` |
| `MainViewModel.RecentFiles` | `"RecentFiles"` | `List<string>` |
| `ResumeService` | `"Resume"` | `SessionData` |

---

## Success Criteria

- [ ] `ISettingsService` interface defined in Core project
- [ ] `JsonSettingsService` implementation with versioning
- [ ] All 5 existing stores migrated to use `ISettingsService`
- [ ] Schema versioning with migration support
- [ ] Single `Settings/` directory managed by the service
- [ ] All error handling centralized (no per-store try-catch)
