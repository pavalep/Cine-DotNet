using Cine.Avalonia.Services;

namespace Cine.Avalonia.Views.Components;

/// <summary>
/// Extension methods for <see cref="IFlyoutService"/> that work with
/// <see cref="IFlyoutSource"/> to reduce boilerplate in flyout controls.
/// </summary>
public static class FlyoutManagerExtensions
{
    /// <summary>
    /// Shows a flyout for an <see cref="IFlyoutSource"/>: dismisses other
    /// open flyouts, wires background-dismiss on the overlay, shows the
    /// content, then cleans up on dismissal (unwires the event, calls
    /// <see cref="IFlyoutSource.OnDismissed"/>, and marks the flyout closed).
    /// </summary>
    public static void ShowFlyoutFor(this IFlyoutService manager, IFlyoutSource source, FlyoutOverlay overlay)
    {
        if (!source.CanOpen) return;

        manager.DismissOthers(source.FlyoutKey);
        overlay.OnBackgroundDismissed -= OnDismissed;
        overlay.OnBackgroundDismissed += OnDismissed;
        overlay.ShowContent(source.Anchor, source.BuildContent(), placeAbove: true);

        void OnDismissed()
        {
            overlay.OnBackgroundDismissed -= OnDismissed;
            source.OnDismissed();
            manager.MarkClosed(source.FlyoutKey);
        }
    }
}
