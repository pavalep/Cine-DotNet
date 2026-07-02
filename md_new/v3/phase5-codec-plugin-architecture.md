# Phase 5: Codec Plugin Architecture Specification

> **Version:** 1.0  
> **Date:** 2026-07-02  
> **Depends on:** [phase5-architecture-design.md](./phase5-architecture-design.md) §9

---

## Table of Contents

1. [Motivation](#1-motivation)
2. [Architecture Overview](#2-architecture-overview)
3. [Core Interfaces](#3-core-interfaces)
4. [Built-in Providers](#4-built-in-providers)
5. [CodecManager: Selection & Composition](#5-codecmanager-selection--composition)
6. [External Plugin System (MEF)](#6-external-plugin-system-mef)
7. [Provider Implementation Guide](#7-provider-implementation-guide)
8. [Capability Discovery](#8-capability-discovery)
9. [Error Handling & Fallback](#9-error-handling--fallback)
10. [Performance Considerations](#10-performance-considerations)
11. [Testing Strategy](#11-testing-strategy)

---

## 1. Motivation

The current codebase has `MpvPlayer` as the primary media backend with `MediaFoundationPlayer` as an alternative. Adding support for new codecs (AV1, HDR10+, Dolby Vision) or new backends (VAAPI, VDPAU, CUDA, Vulkan Video) currently requires modifying the monolithic `MpvPlayer`.

**Goals:**

1. Add new codecs without modifying existing code (Open/Closed Principle)
2. Feature-gate codecs by license tier (HDR10 = Pro, H.265 = Full)
3. Graceful fallback — hardware decoder failure → software decode
4. Plugin marketplace — third-party codec providers via MEF (future)
5. Runtime capability detection — no hardcoded codec lists

### Key Design Tenets

| Tenet | Implication |
|-------|-------------|
| **Separation of concerns** | Each provider owns format detection + decoding |
| **Feature-gated** | Codec availability controlled by `IFeatureService` |
| **Fail-fast with fallback** | Preferred provider fails → try next available |
| **Provider ordering** | Priority-based: HW > software > fallback |

---

## 2. Architecture Overview

```
┌────────────────────────────────────────────────────────────────────┐
│                      APPLICATION LAYER                             │
│  ┌────────────────────────────────────────────────────────────┐    │
│  │                    CodecManager                             │    │
│  │  - Selects best provider for media URL                     │    │
│  │  - Integrates with FeatureService for licensing            │    │
│  │  - Provides fallback chain on failure                      │    │
│  └────────────────────┬────────────────────────────────────────┘    │
│                       │                                            │
│  ┌────────────────────▼───────────────────────────────────────┐    │
│  │                  ICodecProvider                             │    │
│  │  CanHandle() | GetCapabilities() | CreateSessionAsync()    │    │
│  └─────────────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────────┤
│  MEDIA LAYER                                                        │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  MpvCodecProvider  |  MFCodecProvider  |  SWFallbackProvider│    │
│  ├─────────────────────────────────────────────────────────────┤    │
│  │  (Future) External Plugins via MEF: VLC, FFmpeg, Custom    │    │
│  └─────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Core Interfaces

### 3.1 ICodecProvider

```csharp
namespace Cine.Media.Codecs;

public interface ICodecProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    int Priority { get; }
    bool IsAvailable { get; }
    bool CanHandle(string mediaUrl);
    IReadOnlyCollection<CodecCapability> GetCapabilities();
    Task<IDecodingSession> CreateSessionAsync(
        string mediaUrl, DecodingOptions options, CancellationToken ct);
}
```

### 3.2 CodecCapability

```csharp
public enum HdrSupportLevel { None, Hdr10, Hdr10Plus, DolbyVision }
public enum CodecType { Video, Audio, Subtitle }

public record CodecCapability(
    string CodecName,               // "h264", "hevc", "vp9"
    string ContainerFormat,         // "mp4", "mkv", "avi"
    CodecType Type,
    bool IsHardwareAccelerated,
    int MaximumResolutionInPixels,
    HdrSupportLevel HdrSupport,
    string? RequiredSystemFeature,  // "d3d11", "vulkan", "cuda"
    string Description);
```

### 3.3 DecodingOptions & IDecodingSession

```csharp
public enum HwdecMode { Auto, Safe, Yes, No, Copy }

public class DecodingOptions
{
    public bool EnableHardwareDecoding { get; init; } = true;
    public HwdecMode HardwareDecoderMode { get; init; } = HwdecMode.Auto;
    public int? TargetWidth { get; init; }
    public int? TargetHeight { get; init; }
    public bool Enable10BitOutput { get; init; }
    public bool EnableHdrPassthrough { get; init; }
    public TimeSpan? StartPosition { get; init; }
    public Dictionary<string, string> ProviderSpecificOptions { get; init; } = [];
}

public interface IDecodingSession : IDisposable
{
    string ProviderId { get; }
    string MediaUrl { get; }
    IPlaybackControl Playback { get; }
    IAudioControl Audio { get; }
    IVideoControl Video { get; }
    ISubtitleControl Subtitles { get; }
    IChapterNavigation Chapters { get; }
    IPlaylistManagement? Playlist { get; }
    SessionDiagnostics Diagnostics { get; }
    event EventHandler<SessionErrorEventArgs>? Error;
    event EventHandler<SessionDiagnostics>? DiagnosticsUpdated;
}

public record SessionDiagnostics(
    string ActiveCodec,
    bool IsHardwareDecoding,
    double AverageDecodeTimeMs,
    int DroppedFrames,
    string RendererInfo);

public class SessionErrorEventArgs : EventArgs
{
    public string Message { get; init; }
    public Exception? Exception { get; init; }
    public bool IsFatal { get; init; }
    public ICodecProvider? FallbackProvider { get; set; }
}
```

---

## 4. Built-in Providers

### 4.1 MpvCodecProvider (Priority: 100)

Key points:
- Wraps `MpvPlayer` with hardware acceleration
- Supports: H.264, H.265/HEVC (HDR10/HDR10+), VP9, AV1 (software), all common audio formats
- Container support: .mp4, .mkv, .avi, .mov, .wmv, .webm, .ts, .mts, .m2ts, .vob, .ogv, .ogg, .3gp, .divx
- Availability: Checks `libmpv-2.dll` via `NativeLibrary.TryLoad()`

### 4.2 MediaFoundationCodecProvider (Priority: 80)

Key points:
- Wraps `MediaFoundationPlayer`
- Supports: H.264, H.265 (via system codecs), AAC, WMA
- Container support: .mp4, .wmv, .asf, .mov
- Availability: Windows 10 1809+

### 4.3 SoftwareFallbackCodecProvider (Priority: 10)

Key points:
- Forces `hwdec=no` on MpvPlayer
- Accepts any format (universal fallback)
- Always available

---

## 5. CodecManager: Selection & Composition

### 5.1 Selection Algorithm

```csharp
public class CodecManager
{
    private readonly IEnumerable<ICodecProvider> _providers;
    private readonly IFeatureService _featureService;
    private readonly ILogger _logger;

    public CodecManager(
        IEnumerable<ICodecProvider> providers,
        IFeatureService featureService,
        ILogger<CodecManager> logger)
    {
        _providers = providers.OrderByDescending(p => p.Priority).ToList();
        _featureService = featureService;
        _logger = logger;
    }

    public ICodecProvider? SelectProvider(string mediaUrl)
    {
        foreach (var provider in _providers)
        {
            if (!provider.IsAvailable) continue;
            if (!provider.CanHandle(mediaUrl)) continue;

            // Check feature gates for video codecs
            foreach (var cap in provider.GetCapabilities())
            {
                if (cap.Type != CodecType.Video) continue;
                var featureKey = $"codecs.{cap.CodecName}";
                if (!_featureService.IsEnabled(featureKey)) continue;

                _logger.Info("Selected {Id} for {Url} (codec: {Codec})",
                    provider.ProviderId, mediaUrl, cap.CodecName);
                return provider;
            }
        }
        return null;
    }

    public async Task<IDecodingSession?> OpenMediaAsync(
        string mediaUrl, DecodingOptions? options = null, CancellationToken ct = default)
    {
        options ??= new DecodingOptions();
        var provider = SelectProvider(mediaUrl);
        if (provider == null) return null;

        try
        {
            return await provider.CreateSessionAsync(mediaUrl, options, ct);
        }
        catch (Exception ex) when (options.EnableHardwareDecoding)
        {
            _logger.Warn(ex, "HW decode failed for {Url}, retrying software", mediaUrl);
            var fallbackOpts = options with { EnableHardwareDecoding = false };
            var fallback = FindFallbackProvider();
            return fallback != null
                ? await fallback.CreateSessionAsync(mediaUrl, fallbackOpts, ct)
                : throw;
        }
    }

    private ICodecProvider? FindFallbackProvider() =>
        _providers.FirstOrDefault(p => p.ProviderId == "software-fallback");
}
```

### 5.2 DI Registration

```csharp
services.AddSingleton<ICodecProvider, MpvCodecProvider>();
services.AddSingleton<ICodecProvider, MediaFoundationCodecProvider>();
services.AddSingleton<ICodecProvider, SoftwareFallbackCodecProvider>();
services.AddSingleton<CodecManager>();
```

---

## 6. External Plugin System (MEF)

### 6.1 Plugin Interface

```csharp
[InheritedExport(typeof(IExternalCodecProvider))]
public interface IExternalCodecProvider : ICodecProvider
{
    string PluginVersion { get; }
    string Author { get; }
    string? LicenseKey { get; }
    string PluginDescription { get; }
}
```

### 6.2 Plugin Loader

```csharp
public class CodecPluginLoader
{
    private readonly string _pluginDirectory;
    private readonly ILogger _logger;

    public CodecPluginLoader(ILogger<CodecPluginLoader> logger)
    {
        _pluginDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        _logger = logger;
    }

    public IReadOnlyList<IExternalCodecProvider> LoadPlugins()
    {
        if (!Directory.Exists(_pluginDirectory)) return [];

        var results = new List<IExternalCodecProvider>();
        foreach (var file in Directory.GetFiles(_pluginDirectory, "*.CodecPlugin.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);
                var types = assembly.GetExportedTypes()
                    .Where(t => typeof(IExternalCodecProvider)
                        .IsAssignableFrom(t) && !t.IsAbstract);

                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is IExternalCodecProvider plugin)
                        results.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load plugin: {File}", file);
            }
        }
        return results;
    }
}
```

### 6.3 Plugin Directory

```
Cine.App/
├── Plugins/
│   ├── VlcCodecPlugin.CodecPlugin.dll
│   ├── FFmpegCodecPlugin.CodecPlugin.dll
│   └── CustomFilterPlugin.CodecPlugin.dll
```

---

## 7. Provider Implementation Guide

### 7.1 Minimum Viable Provider

```csharp
public class MyCustomProvider : ICodecProvider
{
    public string ProviderId => "mycustom";
    public string DisplayName => "My Custom Codec";
    public int Priority => 50;
    public bool IsAvailable => CheckAvailability();

    public bool CanHandle(string mediaUrl) =>
        mediaUrl.EndsWith(".myformat", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyCollection<CodecCapability> GetCapabilities() =>
    [
        new("mycodec", "myformat", CodecType.Video, true,
            8294400, HdrSupportLevel.None, "d3d11", "My custom codec up to 4K")
    ];

    public async Task<IDecodingSession> CreateSessionAsync(
        string mediaUrl, DecodingOptions options, CancellationToken ct)
    {
        var player = new YourPlayerImplementation();
        if (!options.EnableHardwareDecoding) player.SetSoftwareDecode();
        await Task.Run(() => player.Open(mediaUrl), ct);
        return new DecodingSession(
            ProviderId, mediaUrl,
            player, player, player, player, null, null);
    }

    private static bool CheckAvailability() =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
}
```

---

## 8. Capability Discovery

### 8.1 User-Facing Codec Info (Settings/About)

```csharp
public class CodecInfoItem
{
    public string ProviderName { get; init; }
    public string CodecName { get; init; }
    public string Container { get; init; }
    public bool IsHardwareAccelerated { get; init; }
    public string HdrSupport { get; init; }
    public bool IsLicensed { get; init; }
    public string MaxResolution { get; init; } // "SD", "HD", "4K", "> 4K"
}
```

Displayed in Preferences > About > Codec Information tab.

---

## 9. Error Handling & Fallback

### 9.1 Fallback Chain Visual

```
OpenMediaAsync("4k_hdr10.mkv")
    ├──► MpvCodecProvider (Pri: 100) — HW decode HDR10
    │       └──► FAIL (no GPU support)
    │             └──► Retry software mode on MpvCodecProvider
    │                   └──► FAIL (AV1 software too slow)
    │                         └──► Next provider
    ├──► MFCodecProvider (Pri: 80)
    │       └──► FAIL (not MP4 container)
    ├──► SWFallbackProvider (Pri: 10) → SUCCESS
```

### 9.2 Provider Health Monitoring

```csharp
public class ProviderHealthMonitor
{
    private readonly ConcurrentDictionary<string, ProviderHealth> _health = new();

    public void RecordSessionError(string providerId, SessionErrorEventArgs error)
    {
        var health = _health.GetOrAdd(providerId, _ => new ProviderHealth());
        health.ErrorCount++;
        if (error.IsFatal) health.ConsecutiveFailures++;
        if (health.ConsecutiveFailures >= health.FailureThreshold)
            health.IsDegraded = true;
    }

    public bool IsProviderDegraded(string providerId) =>
        _health.TryGetValue(providerId, out var h) && h.IsDegraded;
}

public class ProviderHealth
{
    public int ErrorCount;
    public int ConsecutiveFailures;
    public int FailureThreshold = 3;
    public bool IsDegraded;
    public DateTime LastFailure;
}
```

---

## 10. Performance Considerations

| Aspect | Design |
|--------|--------|
| **Provider discovery** | Happens once at startup via DI registration |
| **Capability query** | Lazy-loaded, cached per provider |
| **Session creation** | Async, respects CancellationToken |
| **Fallback overhead** | Only on failure; no pre-emptive fallback creation |
| **Plugin loading** | On-demand, not at startup (configurable) |
| **Capability cache** | `Lazy<IReadOnlyCollection<CodecCapability>>` per provider |

---

## 11. Testing Strategy

```csharp
[TestFixture]
public class CodecProviderTests
{
    [Test]
    public void Provider_ShouldReportCorrectCapabilities()
    {
        var provider = new MpvCodecProvider();
        Assert.That(provider.GetCapabilities(), Is.Not.Empty);
        Assert.That(provider.GetCapabilities()
            .Any(c => c.CodecName == "h264"), Is.True);
    }

    [Test]
    public void CodecManager_ShouldSelectHighestPriorityProvider()
    {
        var providers = new ICodecProvider[]
        {
            new SoftwareFallbackCodecProvider(),
            new MpvCodecProvider()
        };
        var manager = new CodecManager(providers,
            Substitute.For<IFeatureService>(),
            Substitute.For<ILogger>());
        Assert.That(manager.SelectProvider("video.mp4")!.ProviderId, Is.EqualTo("mpv"));
    }

    [Test]
    public void Provider_ShouldRejectUnknownExtension()
    {
        var provider = new MediaFoundationCodecProvider();
        Assert.That(provider.CanHandle("audio.mp3"), Is.False);
    }
}
```

---

## Appendix A: Implementation Checklist

- [ ] Split `IMediaPlayer` into ISP interfaces (Playback, Audio, Video, Subtitle, Chapters, Playlist)
- [ ] Create `DecodingSession` default implementation
- [ ] Implement `MpvCodecProvider` wrapping `MpvPlayer`
- [ ] Implement `MediaFoundationCodecProvider` wrapping `MFPlayer`
- [ ] Implement `SoftwareFallbackCodecProvider`
- [ ] Create `CodecManager` with DI registration
- [ ] Wire `CodecManager` into `MainViewModel.OpenMediaCommand`
- [ ] Add codec info display to Preferences > About
- [ ] Create `CodecPluginLoader` for MEF plugins
- [ ] Add integration tests with mock providers

---

> **Previous:** [phase5-feature-toggles.md](./phase5-feature-toggles.md)  
> **Next:** [phase5-implementation-roadmap.md](./phase5-implementation-roadmap.md)
