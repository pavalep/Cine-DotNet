using Microsoft.Extensions.DependencyInjection;
using Simba.Avalonia.Services;
using Simba.Avalonia.Managers;
using Simba.Avalonia.Storage;
using Simba.Avalonia.ViewModels;
using Simba.Avalonia.ViewModels.Pages;
using Simba.Avalonia.Features;
using Simba.Avalonia.Views.Shell;
using Simba.Avalonia.Core.Navigation;
using Simba.Avalonia.Services.UI;
using Simba.Media.Codecs;
using Simba.Media.Interfaces;

namespace Simba.Avalonia.Core;

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
        services.AddSingleton<INavigationService, NavigationService>();

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
            var eventBus = sp.GetRequiredService<IEventBus>();
            return new SubtitleManager(player, store, eventBus);
        });

        services.AddSingleton<IAudioManager>(sp =>
        {
            var player = sp.GetRequiredService<PlayerService>().Player
                ?? throw new InvalidOperationException("PlayerService must be initialized before IAudioManager is resolved.");
            var store = sp.GetRequiredService<AudioSettingsStore>();
            var eventBus = sp.GetRequiredService<IEventBus>();
            return new AudioManager(player, store, eventBus);
        });

        services.AddSingleton<VideoManager>(sp =>
        {
            var player = sp.GetRequiredService<PlayerService>().Player
                ?? throw new InvalidOperationException("PlayerService must be initialized before VideoManager is resolved.");
            return new VideoManager(player);
        });

        // ── OSD service (wraps OsdNotification control, set during MainWindow init) ──
        services.AddSingleton<IOsdService, OsdService>();

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
        services.AddSingleton<IRecentFilesService, RecentFilesService>();

        // ── ViewModels (transient — new instance per resolve) ──
        services.AddTransient<MainViewModel>();
        services.AddTransient<StartPageViewModel>();

        // ── Views (transient — Avalonia manages lifetime) ──
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
