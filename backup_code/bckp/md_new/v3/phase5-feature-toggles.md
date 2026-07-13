# Phase 5: Feature Toggle & Licensing System Specification

> **Version:** 1.0  
> **Date:** 2026-07-02  
> **Depends on:** [phase5-architecture-design.md](./phase5-architecture-design.md) §8

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Architecture](#2-architecture)
3. [Feature Definition Schema](#3-feature-definition-schema)
4. [License Tiers](#4-license-tiers)
5. [IFeatureService Interface](#5-ifeatureservice-interface)
6. [IFeatureStore Interface](#6-ifeaturestore-interface)
7. [FeatureGate Attribute](#7-featuregate-attribute)
8. [Caching Strategy](#8-caching-strategy)
9. [UI Integration Patterns](#9-ui-integration-patterns)
10. [Storage & Persistence](#10-storage--persistence)
11. [Security Considerations](#11-security-considerations)
12. [Trial Watermark](#12-trial-watermark)
13. [Feature Dependency Graph](#13-feature-dependency-graph)

---

## 1. System Overview

The Feature Toggle & Licensing system enables **runtime control** over which features are available to a user, based on:

- **Licensing Tier** — Trial, Full, or Pro
- **Runtime Configuration** — JSON-driven toggles for beta/experimental features
- **Percentage Rollout** — Gradual feature release (e.g., 5% of users get Vulkan renderer)
- **Dependency Chain** — Features can depend on other features (e.g., Dolby Vision depends on HDR10)

### Design Goals

| Goal | Approach |
|------|----------|
| **Zero performance overhead for disabled features** | Cached evaluation with invalidation; disabled features skip code paths entirely |
| **Compile-time safety** | Feature keys are constants (not magic strings) |
| **UI-aware gating** | Controls auto-hide/disable via `[FeatureGate]` + binding |
| **Graceful degradation** | Disabled features show upgrade CTA instead of cryptic errors |
| **Auditability** | Every feature check is logged at Debug level with context |

---

## 2. Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        IFeatureService                              │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  IsEnabled(featureKey) → bool                               │    │
│  │  GetFeature(featureKey) → FeatureDefinition?                │    │
│  │  GetAllFeatures() → IReadOnlyCollection<FeatureDefinition>  │    │
│  │  InvalidateCache(featureKey?)                               │    │
│  │                                                             │    │
│  │  ┌─────────────────────┐   ┌─────────────────────────────┐  │    │
│  │  │ ConcurrentDictionary │   │  ConcurrentDictionary       │  │    │
│  │  │ (Resolved Cache)     │   │  (Feature Definitions)      │  │    │
│  │  └─────────────────────┘   └─────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────┘    │
│           │                    │                    │                │
│           ▼                    ▼                    ▼                │
│  ┌──────────────┐   ┌──────────────────┐   ┌──────────────────┐    │
│  │ IFeatureStore │   │ ILicensingService│   │ ITelemetryService│    │
│  │ (JSON file)   │   │ (Hardware-bound)  │   │ (Audit log)      │    │
│  └──────────────┘   └──────────────────┘   └──────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Feature Definition Schema

### 3.1 C# Model

```csharp
namespace Cine.Core.Features;

public enum FeatureToggleType
{
    CompileTime,         // Always on in this build variant
    RuntimeToggle,       // JSON-driven on/off
    LicensingTier,       // Gated by license level
    PercentageRollout,   // Gradual rollout by user hash
    ExperimentGroup      // A/B test group assignment
}

public enum LicensingTier
{
    Trial = 0,
    Full = 1,
    Pro = 2
}

public class FeatureDefinition
{
    /// <summary>Unique key, e.g. "codecs.hdr10".</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable name for UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Detailed description.</summary>
    public string? Description { get; init; }

    /// <summary>How this feature is toggled.</summary>
    public FeatureToggleType ToggleType { get; init; } = FeatureToggleType.LicensingTier;

    /// <summary>Minimum license tier required.</summary>
    public LicensingTier MinimumTier { get; init; } = LicensingTier.Trial;

    /// <summary>Whether this is an experimental/preview feature.</summary>
    public bool IsExperimental { get; init; }

    /// <summary>Percentage of users who have this enabled (0.0–100.0).</summary>
    public double RolloutPercentage { get; init; } = 100.0;

    /// <summary>Feature must be enabled before this one can be.</summary>
    public string? DependsOnFeature { get; init; }

    /// <summary>Required system capabilities (e.g., "native.hdr").</summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = [];
}
```

### 3.2 Embedded JSON Definition File

```json
{
  "$schema": "./feature-definitions-schema.json",
  "version": 1,
  "features": [
    {
      "key": "playback.basic",
      "displayName": "Basic Playback",
      "description": "Play media files (MP4, AVI, MKV)",
      "toggleType": "CompileTime",
      "minimumTier": "Trial"
    },
    {
      "key": "playback.4k",
      "displayName": "4K/UHD Playback",
      "description": "Play 4K resolution media files",
      "toggleType": "LicensingTier",
      "minimumTier": "Full",
      "dependsOnFeature": "playback.basic"
    },
    {
      "key": "codecs.hdr10",
      "displayName": "HDR10 Support",
      "description": "High Dynamic Range (HDR10) video playback",
      "toggleType": "LicensingTier",
      "minimumTier": "Pro",
      "dependsOnFeature": "playback.4k",
      "requiredCapabilities": ["native.hdr"]
    },
    {
      "key": "codecs.dovi",
      "displayName": "Dolby Vision",
      "description": "Dolby Vision HDR playback",
      "toggleType": "LicensingTier",
      "minimumTier": "Pro",
      "dependsOnFeature": "codecs.hdr10",
      "requiredCapabilities": ["native.hdr"]
    },
    {
      "key": "equalizer.basic",
      "displayName": "Basic Equalizer",
      "toggleType": "CompileTime",
      "minimumTier": "Trial"
    },
    {
      "key": "equalizer.advanced",
      "displayName": "10-Band Equalizer",
      "toggleType": "LicensingTier",
      "minimumTier": "Pro",
      "dependsOnFeature": "equalizer.basic"
    },
    {
      "key": "playlist",
      "displayName": "Playlist Management",
      "toggleType": "LicensingTier",
      "minimumTier": "Full",
      "dependsOnFeature": "playback.basic"
    },
    {
      "key": "chapters",
      "displayName": "Chapter Navigation",
      "toggleType": "LicensingTier",
      "minimumTier": "Full",
      "dependsOnFeature": "playback.basic"
    },
    {
      "key": "subtitles.advanced",
      "displayName": "Advanced Subtitle Styling",
      "description": "Custom fonts, colors, borders, shadows for subtitles",
      "toggleType": "LicensingTier",
      "minimumTier": "Full",
      "dependsOnFeature": "playback.basic"
    },
    {
      "key": "codecs.h265",
      "displayName": "H.265/HEVC Hardware Decode",
      "toggleType": "RuntimeToggle",
      "minimumTier": "Full",
      "dependsOnFeature": "playback.4k"
    },
    {
      "key": "codecs.av1",
      "displayName": "AV1 Hardware Decode",
      "toggleType": "LicensingTier",
      "minimumTier": "Pro",
      "dependsOnFeature": "playback.4k"
    },
    {
      "key": "renderer.d3d11",
      "displayName": "D3D11 Video Processing",
      "toggleType": "RuntimeToggle",
      "minimumTier": "Pro"
    },
    {
      "key": "renderer.customshaders",
      "displayName": "Custom Shader Support",
      "toggleType": "LicensingTier",
      "minimumTier": "Pro",
      "dependsOnFeature": "renderer.d3d11"
    },
    {
      "key": "experimental.vulkan",
      "displayName": "Vulkan Render Path (Preview)",
      "description": "Experimental Vulkan-based video rendering",
      "toggleType": "PercentageRollout",
      "minimumTier": "Pro",
      "rolloutPercentage": 5,
      "isExperimental": true
    }
  ]
}
```

### 3.3 Feature Key Constants

```csharp
namespace Cine.Core.Features.Keys;

public static class FeatureKeys
{
    public static class Playback
    {
        public const string Basic = "playback.basic";
        public const string Uhd4K = "playback.4k";
    }

    public static class Codecs
    {
        public const string Hdr10 = "codecs.hdr10";
        public const string DolbyVision = "codecs.dovi";
        public const string H265Hevc = "codecs.h265";
        public const string Av1 = "codecs.av1";
    }

    public static class Equalizer
    {
        public const string Basic = "equalizer.basic";
        public const string Advanced = "equalizer.advanced";
    }

    public static class Subtitles
    {
        public const string Advanced = "subtitles.advanced";
    }

    public static class Playlist
    {
        public const string Management = "playlist";
    }

    public static class Chapters
    {
        public const string Navigation = "chapters";
    }

    public static class Renderer
    {
        public const string D3D11 = "renderer.d3d11";
        public const string CustomShaders = "renderer.customshaders";
        public const string VulkanPreview = "experimental.vulkan";
    }
}
```

---

## 4. License Tiers

### 4.1 Tier Definitions

| Tier | Price Model | Duration | Features | Watermark |
|------|-------------|----------|----------|-----------|
| **Trial** | Free | 14 days | Basic playback, MP4/AVI, volume, seek, basic subtitles, basic equalizer | Yes (OSD) |
| **Full** | One-time purchase | Perpetual | All Trial + 4K, H.265, playlist, chapters, advanced subtitles, full equalizer | No |
| **Pro** | Subscription or purchase | Perpetual | All Full + HDR10, Dolby Vision, AV1, D3D11 processing, custom shaders, Vulkan preview | No |

### 4.2 License Validation

```csharp
public interface ILicensingService
{
    LicensingTier CurrentTier { get; }
    LicenseInfo? CurrentLicense { get; }
    bool IsTrialExpired { get; }
    int TrialDaysRemaining { get; }

    /// <summary>Validate a license key and activate the tier.</summary>
    Task<LicenseActivationResult> ActivateAsync(string licenseKey);

    /// <summary>Deactivate the current license (revert to Trial).</summary>
    Task DeactivateAsync();

    /// <summary>Check if a hardware-bound license is still valid.</summary>
    bool ValidateHardwareBinding();
}

public record LicenseInfo(
    string LicenseId,
    LicensingTier Tier,
    DateTime ActivatedAt,
    DateTime? ExpiresAt,
    string HardwareId,
    string? Email);

public class LicenseActivationResult
{
    public bool Success { get; init; }
    public LicensingTier ActivatedTier { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }
}

public class LicensingService : ILicensingService
{
    private readonly IAppConfigRepository _config;
    private readonly ILogger _logger;
    private LicenseInfo? _cached;

    public LicensingTier CurrentTier
    {
        get
        {
            if (_cached != null) return _cached.Tier;

            // Load from encrypted storage
            var raw = _config.Get("license.data", string.Empty);
            if (string.IsNullOrEmpty(raw)) return LicensingTier.Trial;

            try
            {
                _cached = DecryptAndDeserialize(raw);
                if (_cached.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.Warn("License expired: {LicenseId}", _cached.LicenseId);
                    return LicensingTier.Trial;
                }
                return _cached.Tier;
            }
            catch
            {
                return LicensingTier.Trial; // Corrupt license → Trial
            }
        }
    }

    public int TrialDaysRemaining
    {
        get
        {
            var firstRun = _config.Get("license.firstRun", DateTime.MinValue.ToString("O"));
            if (!DateTime.TryParse(firstRun, out var start)) return 14;
            var remaining = 14 - (DateTime.UtcNow - start).Days;
            return Math.Max(0, remaining);
        }
    }

    public async Task<LicenseActivationResult> ActivateAsync(string licenseKey)
    {
        // 1. Validate format
        if (!LicenseKeyValidator.ValidateFormat(licenseKey))
            return Fail("Invalid license key format.");

        // 2. Call activation API (or offline unlock)
        var result = await CallActivationApiAsync(licenseKey);

        if (result.Success)
        {
            // 3. Store encrypted
            var encrypted = EncryptAndSerialize(result.License!);
            _config.Set("license.data", encrypted);
            _config.Save();
            _cached = result.License;
        }

        return result;
    }

    // ── Encryption: AES-256-GCM with hardware-bound key ──
    private string EncryptAndSerialize(LicenseInfo license) { ... }
    private LicenseInfo? DecryptDeserialize(string encrypted) { ... }
}
```

### 4.3 Trial First-Run Logic

```csharp
// In MainWindow.Initialization.cs or App.axaml.cs
public void OnFirstRun()
{
    var firstRun = _config.Get("license.firstRun", string.Empty);
    if (string.IsNullOrEmpty(firstRun))
    {
        _config.Set("license.firstRun", DateTime.UtcNow.ToString("O"));
        _config.Save();

        // Show welcome dialog with trial info
        _navigationService.NavigateTo(NavigationTarget.FirstLaunch);
    }
}
```

---

## 5. IFeatureService Interface

```csharp
namespace Cine.Core.Features;

public interface IFeatureService
{
    /// <summary>Check if a feature is enabled for the current user/license.</summary>
    bool IsEnabled(string featureKey);

    /// <summary>Get feature definition metadata.</summary>
    FeatureDefinition? GetFeature(string featureKey);

    /// <summary>Get all registered features.</summary>
    IReadOnlyCollection<FeatureDefinition> GetAllFeatures();

    /// <summary>Invalidate cached evaluation for one or all features.</summary>
    void InvalidateCache(string? featureKey = null);

    /// <summary>Event raised when feature availability changes (e.g., license update).</summary>
    event EventHandler<FeatureStateChangedEventArgs>? FeatureStateChanged;
}

public class FeatureStateChangedEventArgs : EventArgs
{
    public string FeatureKey { get; }
    public bool NowEnabled { get; }
}
```

### 5.1 Implementation

```csharp
public class FeatureService : IFeatureService
{
    private readonly IFeatureStore _store;
    private readonly ILicensingService _licensing;
    private readonly ConcurrentDictionary<string, bool> _cache = new();
    private readonly ILogger _logger;

    public FeatureService(
        IFeatureStore store,
        ILicensingService licensing,
        ILogger<FeatureService> logger)
    {
        _store = store;
        _licensing = licensing;
        _logger = logger;
    }

    public bool IsEnabled(string featureKey)
    {
        // Fast path: resolved cache hit
        if (_cache.TryGetValue(featureKey, out var cached))
            return cached;

        // Slow path: resolve and cache
        var result = ResolveFeature(featureKey);
        _cache.TryAdd(featureKey, result);
        return result;
    }

    private bool ResolveFeature(string featureKey)
    {
        var feature = _store.GetFeature(featureKey);
        if (feature == null)
        {
            _logger.Warn("Unknown feature checked: {Key}", featureKey);
            return false; // Unknown feature → disabled
        }

        _logger.Debug("Resolving feature: {Key} (Tier={Tier}, Type={Type})",
            featureKey, feature.MinimumTier, feature.ToggleType);

        // 1. License tier check
        if (_licensing.CurrentTier < feature.MinimumTier)
        {
            _logger.Debug("Feature {Key} denied: tier {Current} < {Required}",
                featureKey, _licensing.CurrentTier, feature.MinimumTier);
            return false;
        }

        // 2. Dependency check (recursive)
        if (feature.DependsOnFeature != null && !IsEnabled(feature.DependsOnFeature))
        {
            _logger.Debug("Feature {Key} denied: dependency {Dep} is disabled",
                featureKey, feature.DependsOnFeature);
            return false;
        }

        // 3. Runtime toggle
        if (feature.ToggleType == FeatureToggleType.RuntimeToggle)
        {
            var enabled = _store.IsRuntimeEnabled(featureKey);
            _logger.Debug("Feature {Key} runtime toggle: {Result}", featureKey, enabled);
            return enabled;
        }

        // 4. Percentage rollout
        if (feature.ToggleType == FeatureToggleType.PercentageRollout)
        {
            var hash = Math.Abs(featureKey.GetDeterministicHashCode()) % 100;
            var enabled = hash < feature.RolloutPercentage;
            _logger.Debug("Feature {Key} rollout ({Hash}% < {Rollout}%): {Result}",
                featureKey, hash, feature.RolloutPercentage, enabled);
            return enabled;
        }

        // 5. CompileTime or LicensingTier without runtime override → enabled
        return true;
    }

    public void InvalidateCache(string? featureKey = null)
    {
        if (featureKey != null)
        {
            _cache.TryRemove(featureKey, out _);
        }
        else
        {
            _cache.Clear();
        }
    }
}
```

---

## 6. IFeatureStore Interface

```csharp
public interface IFeatureStore
{
    /// <summary>Get definition for a feature key.</summary>
    FeatureDefinition? GetFeature(string key);

    /// <summary>Get all feature definitions (loaded from embedded JSON).</summary>
    IReadOnlyCollection<FeatureDefinition> GetAllFeatures();

    /// <summary>Check runtime override state (JSON-driven).</summary>
    bool IsRuntimeEnabled(string featureKey);

    /// <summary>Set runtime override state.</summary>
    void SetRuntimeEnabled(string featureKey, bool enabled);

    /// <summary>Reload definitions from embedded resource.</summary>
    void ReloadDefinitions();
}

public class FeatureStore : IFeatureStore
{
    private readonly ConcurrentDictionary<string, FeatureDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, bool> _runtimeOverrides = new();
    private readonly string _definitionsPath;
    private readonly IAppConfigRepository _config;

    public FeatureStore(IAppConfigRepository config)
    {
        _config = config;
        _definitionsPath = "Cine.Core.Features.feature-definitions.json";
        LoadDefinitions();
        LoadRuntimeOverrides();
    }

    private void LoadDefinitions()
    {
        var assembly = typeof(FeatureStore).Assembly;
        using var stream = assembly.GetManifestResourceStream(_definitionsPath);
        if (stream == null) throw new InvalidOperationException(
            $"Missing embedded resource: {_definitionsPath}");

        var document = JsonDocument.Parse(stream);
        var features = document.RootElement.GetProperty("features");
        foreach (var feature in features.EnumerateArray())
        {
            var def = JsonSerializer.Deserialize<FeatureDefinition>(feature)!;
            _definitions.TryAdd(def.Key, def);
        }
    }

    private void LoadRuntimeOverrides()
    {
        var overrides = _config.Get("features.overrides", "{}");
        var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(overrides);
        if (dict != null)
        {
            foreach (var (key, value) in dict)
                _runtimeOverrides.TryAdd(key, value);
        }
    }

    public bool IsRuntimeEnabled(string featureKey) =>
        _runtimeOverrides.TryGetValue(featureKey, out var enabled) && enabled;

    public void SetRuntimeEnabled(string featureKey, bool enabled)
    {
        _runtimeOverrides[featureKey] = enabled;
        SaveRuntimeOverrides();
    }

    private void SaveRuntimeOverrides()
    {
        var json = JsonSerializer.Serialize(_runtimeOverrides);
        _config.Set("features.overrides", json);
        _config.Save();
    }
}
```

---

## 7. FeatureGate Attribute

### 7.1 Attribute Definition

```csharp
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method |
    AttributeTargets.Property | AttributeTargets.Field,
    Inherited = false, AllowMultiple = true)]
public class FeatureGateAttribute : Attribute
{
    public string FeatureKey { get; }
    public LicensingTier MinimumTier { get; }

    public FeatureGateAttribute(string featureKey)
    {
        FeatureKey = featureKey;
    }

    public FeatureGateAttribute(string featureKey, LicensingTier minimumTier)
    {
        FeatureKey = featureKey;
        MinimumTier = minimumTier;
    }
}
```

### 7.2 Usage Patterns

```csharp
// ── 1. Hide entire control ──
[FeatureGate("renderer.customshaders", LicensingTier.Pro)]
public partial class ShaderSettingsPanel : UserControl
{
    // Entire panel is hidden if feature is disabled
}

// ── 2. Disable specific action ──
public partial class AudioViewModel
{
    [FeatureGate("equalizer.advanced", LicensingTier.Pro)]
    public ICommand OpenAdvancedEqualizerCommand => _openAdvancedCmd;

    [FeatureGate("equalizer.advanced")]
    public bool IsAdvancedEqualizerVisible =>
        _featureService.IsEnabled(FeatureKeys.Equalizer.Advanced);
}

// ── 3. Conditional method behavior ──
public partial class PlaybackService
{
    public bool TryOpen4KMedia(string path)
    {
        if (!_featureService.IsEnabled(FeatureKeys.Playback.Uhd4K))
        {
            _notificationService.Show("Upgrade to Full to play 4K media.");
            return false;
        }
        return OpenInternal(path);
    }
}
```

### 7.3 XAML Binding Helpers

```xml
<!-- Avalonia XAML: Bind visibility to feature gate -->
<UserControl xmlns:features="clr-namespace:Cine.Core.Features;assembly=Cine.Core">
  <StackPanel IsVisible="{Binding Source={x:Static features:FeatureService.Instance},
                                  Path=IsEnabled[equalizer.advanced]}">
    <!-- Advanced equalizer controls -->
  </StackPanel>
</UserControl>
```

---

## 8. Caching Strategy

### 8.1 Cache Invalidation Points

| Event | Action |
|-------|--------|
| License activated/deactivated | Invalidate entire cache + fire `FeatureStateChanged` |
| Runtime toggle changed (dev menu) | Invalidate single feature + fire event |
| Feature definitions reloaded | Invalidate entire cache |
| Application startup | Cache cold — resolve on first access |

### 8.2 Cache Performance

```csharp
// Estimated hit rate: 99.9% (features rarely change mid-session)
// Each IsEnabled() call after cache warm is O(1) dictionary lookup
// Cold path: O(n) where n = dependency chain depth (max 3-4)
```

---

## 9. UI Integration Patterns

### 9.1 Trial Banner

```
┌──────────────────────────────────────────────────────────────┐
│  ⚠ Trial — 12 days remaining.  [Upgrade to Full →]          │
└──────────────────────────────────────────────────────────────┘
```

```csharp
public class TrialBannerViewModel : INotifyPropertyChanged
{
    private readonly ILicensingService _licensing;

    public bool IsVisible => _licensing.CurrentTier == LicensingTier.Trial;
    public int DaysRemaining => _licensing.TrialDaysRemaining;
    public string Message => $"Trial — {DaysRemaining} days remaining.";
    public string UpgradeAction => "Upgrade to Full →";

    public ICommand UpgradeCommand { get; }

    public TrialBannerViewModel(ILicensingService licensing, INavigationService nav)
    {
        _licensing = licensing;
        UpgradeCommand = new RelayCommand(() =>
            nav.NavigateTo(NavigationTarget.UpgradePage));

        // React to license changes
        licensing.LicenseChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(DaysRemaining));
        };
    }
}
```

### 9.2 Feature-Locked Flyout

```csharp
[FeatureGate("equalizer.advanced", LicensingTier.Pro)]
public partial class AudioEqualizerFlyout : UserControl
{
    [ObservableProperty]
    private bool _showUpgradeCta;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // Injected via DI
        ShowUpgradeCta = !_featureService.IsEnabled(FeatureKeys.Equalizer.Advanced);
    }
}
```

```xml
<!-- AudioEqualizerFlyout.axaml -->
<StackPanel>
  <!-- Basic EQ bands (Trial/Full) -->
  <EqualizerBand Band="Bass" />

  <!-- Advanced EQ gated behind Pro -->
  <StackPanel IsVisible="{Binding ShowUpgradeCta}">
    <Border Background="{StaticResource SurfaceVariant}" Padding="16">
      <StackPanel>
        <TextBlock Text="10-Band Equalizer" FontWeight="Bold" />
        <TextBlock Text="Upgrade to Pro for full 10-band equalizer control" />
        <Button Command="{Binding UpgradeToProCommand}"
                Content="Upgrade to Pro" />
      </StackPanel>
    </Border>
  </StackPanel>

  <!-- The actual advanced bands (hidden by binding) -->
  <StackPanel IsVisible="{Binding !ShowUpgradeCta}">
    <EqualizerBand Band="Band1" />
    <EqualizerBand Band="Band2" />
    <!-- ... -->
  </StackPanel>
</StackPanel>
```

### 9.3 Settings Page Gating

```csharp
public class PreferencesViewModel
{
    public IReadOnlyList<SettingsSection> Sections { get; }

    public PreferencesViewModel(IFeatureService features)
    {
        Sections = new List<SettingsSection>
        {
            new("General", IsAvailable: true),
            new("Audio", IsAvailable: true),
            new("Video", IsAvailable: true),
            new("Subtitles", IsAvailable: true),
            new("Advanced Codecs", IsAvailable:
                features.IsEnabled(FeatureKeys.Codecs.Hdr10) ||
                features.IsEnabled(FeatureKeys.Codecs.Av1)),
            new("Renderer", IsAvailable:
                features.IsEnabled(FeatureKeys.Renderer.D3D11)),
        };
    }
}
```

---

## 10. Storage & Persistence

### 10.1 File Locations

| Data | Location | Format |
|------|----------|--------|
| Feature definitions | Embedded resource (`Cine.Core.Features.feature-definitions.json`) | JSON (read-only) |
| Runtime overrides | `%LOCALAPPDATA%\Cine\features.json` | JSON |
| License data | `%LOCALAPPDATA%\Cine\license.dat` | AES-256-GCM encrypted |
| Trial first-run | `%LOCALAPPDATA%\Cine\settings.json` → `license.firstRun` | ISO 8601 string |

### 10.2 Security

- License file is **AES-256-GCM** encrypted with a key derived from the machine's hardware ID (TPM if available, fallback to MAC + volume serial)
- Feature definitions are **embedded** in the assembly (tamper-resistant via strong naming)
- Runtime overrides are **not encrypted** (user can edit to enable experimental features)
- Trial extension prevention: first-run timestamp is stored in both settings.json and a hidden NTFS alternate data stream (detect tampering)

---

## 11. Security Considerations

| Threat | Mitigation |
|--------|------------|
| License file copied to another machine | Hardware-bound encryption key |
| License file edited to extend trial | Trial start stored in alternate data stream + settings.json; cross-validation |
| Feature overrides edited to unlock Pro features | Feature definitions embedded; runtime overrides cannot override licensing tier checks |
| Decompilation to remove feature gates | Obfuscation + license check is in Core (separate assembly) |
| Debugger attached enabling features | `Debugger.IsAttached` check at critical gates (optional) |

---

## 12. Trial Watermark

```csharp
public class TrialWatermarkService
{
    private readonly ILicensingService _licensing;
    private readonly IDispatcherTimer _timer;

    public TrialWatermarkService(ILicensingService licensing)
    {
        _licensing = licensing;
    }

    public void Start()
    {
        if (_licensing.CurrentTier != LicensingTier.Trial) return;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Rotate message
        };
        _timer.Tick += (_, _) =>
        {
            // Show OSD notification: "Cine Trial — 12 days remaining"
            if (_licensing.TrialDaysRemaining <= 3)
            {
                // More prominent warning at 3 days
                ShowPersistentBanner();
            }
        };
        _timer.Start();
    }

    private void ShowPersistentBanner()
    {
        // Rendered in ControlsBox as an overlay
        // "Trial expires in 3 days. [Upgrade]"
    }
}
```

---

## 13. Feature Dependency Graph

```
playback.basic (Trial, CompileTime)
├── playback.4k (Full)
│   ├── codecs.h265 (Full, RuntimeToggle)
│   ├── codecs.av1 (Pro)
│   ├── codecs.hdr10 (Pro)
│   │   └── codecs.dovi (Pro)
│   ├── playlist (Full)
│   └── chapters (Full)
├── equalizer.basic (Trial)
│   └── equalizer.advanced (Pro)
├── subtitles.advanced (Full)
├── renderer.d3d11 (Pro)
│   ├── renderer.customshaders (Pro)
│   └── experimental.vulkan (Pro, 5%)
```

---

## Appendix A: Implementation Checklist

- [ ] Create `FeatureDefinition` model class
- [ ] Create `FeatureKeys` constants class
- [ ] Create `feature-definitions.json` embedded resource
- [ ] Implement `IFeatureStore` with embedded JSON + runtime overrides
- [ ] Implement `IFeatureService` with caching
- [ ] Implement `ILicensingService` with encrypted license storage
- [ ] Create `FeatureGateAttribute`
- [ ] Create `TrialBannerViewModel` + XAML
- [ ] Add trial watermark service
- [ ] Wire `FeatureService` into DI container
- [ ] Gate all protected UI elements (flyouts, menu items, buttons)
- [ ] Gate all protected code paths (codec selection, renderer selection)

---

> **Previous:** [phase5-architecture-design.md](./phase5-architecture-design.md)  
> **Next:** [phase5-codec-plugin-architecture.md](./phase5-codec-plugin-architecture.md)
