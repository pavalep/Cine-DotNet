using Microsoft.Extensions.DependencyInjection;
using Cine.Avalonia.Services;
using Cine.Avalonia.State;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Features;
using Cine.Media.Codecs;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.Infrastructure;

/// <summary>
/// Application composition root — single location for all dependency registration.
/// No service should be instantiated with 'new' outside this class.
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        // ── Core infrastructure (stateless singletons) ──
        services.AddSingleton<InputRoutingService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IFlyoutService, FlyoutManager>();

        // ── Settings stores (shared state singletons) ──
        services.AddSingleton<SubtitleSettingsStore>();
        services.AddSingleton<AudioSettingsStore>();
        services.AddSingleton<PlaylistSettingsStore>();

        // ── Codec providers (registered for DI resolution by CodecManager) ──
        services.AddSingleton<ICodecProvider, MpvCodecProvider>();
        services.AddSingleton<ICodecProvider, MFCodecProvider>();
        services.AddSingleton<ICodecProvider, SoftwareFallbackCodecProvider>();
        services.AddSingleton<CodecManager>();
        services.AddSingleton<CodecPluginLoader>();

        // ── Feature management (licensing, toggles, evaluation) ──
        services.AddSingleton<IFeatureStore, FeatureStore>();
        services.AddSingleton<ILicensingService, LicensingService>();
        services.AddSingleton<IFeatureService, FeatureService>();

        // ── Player service (singleton, initialized once at startup) ──
        services.AddSingleton<PlayerService>();

        // ── Domain managers (resolved after player is initialized) ──
        services.AddSingleton<ISubtitleManager>(sp =>
        {
            var player = sp.GetRequiredService<PlayerService>().Player
                ?? throw new InvalidOperationException("PlayerService must be initialized before ISubtitleManager is resolved.");
            var store = sp.GetRequiredService<SubtitleSettingsStore>();
            return new SubtitleManager(player, store);
        });

        // ── Infrastructure (EventBus shared across managers & shell) ──
        services.AddSingleton<IEventBus, EventBus>();

        // ── Playback state manager (singleton — unified state) ──
        services.AddSingleton<PlaybackStateManager>();

        // ── Application services (singleton, stateless or shared state) ──
        services.AddSingleton<IRendererService, RendererCoordinator>();
        services.AddSingleton<IPlaylistService, PlaylistCoordinator>();
        services.AddSingleton<ISessionService, SessionManager>();
        services.AddSingleton<IMediaFileService, MediaFileService>();
        services.AddSingleton<IDragDropService, DragDropService>();

        // ── ViewModels (transient — new instance per resolve) ──
        services.AddTransient<MainViewModel>();

        // ── Views (transient — Avalonia manages lifetime) ──
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
