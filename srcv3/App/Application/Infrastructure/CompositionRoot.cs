using Microsoft.Extensions.DependencyInjection;
using Cine.Avalonia.Services;
using Cine.Avalonia.State;
using Cine.Avalonia.ViewModels;

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

        // ── Player service (singleton, initialized once at startup) ──
        services.AddSingleton<PlayerService>();

        // ── Infrastructure (EventBus shared across managers & shell) ──
        services.AddSingleton<IEventBus, EventBus>();

        // ── Playback state manager (singleton — unified state) ──
        services.AddSingleton<PlaybackStateManager>();

        // ── Application services (singleton, stateless or shared state) ──
        services.AddSingleton<IRendererService, RendererCoordinator>();
        services.AddSingleton<IPlaylistService, PlaylistCoordinator>();
        services.AddSingleton<ISessionService, SessionManager>();
        services.AddSingleton<IMediaFileService, MediaFileService>();

        // ── ViewModels (transient — new instance per resolve) ──
        services.AddTransient<MainViewModel>();

        // ── Views (transient — Avalonia manages lifetime) ──
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
