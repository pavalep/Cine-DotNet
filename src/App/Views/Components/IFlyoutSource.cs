namespace Cine.Avalonia.Views.Components;

/// <summary>
/// Interface for flyout sources. Each flyout control implements this to
/// provide the flyout key, anchor element, open guard, and content builder.
/// Used with <see cref="FlyoutManagerExtensions.ShowFlyoutFor"/> to eliminate
/// repetitive overlay-wiring boilerplate.
/// </summary>
public interface IFlyoutSource
{
    /// <summary>Unique key identifying this flyout (e.g. "volume", "chapters").</summary>
    string FlyoutKey { get; }

    /// <summary>The anchor control the flyout appears next to.</summary>
    global::Avalonia.Controls.Control Anchor { get; }

    /// <summary>Whether the flyout can be opened (view-model / state check).</summary>
    bool CanOpen { get; }

    /// <summary>Builds the flyout content border.</summary>
    global::Avalonia.Controls.Border BuildContent();

    /// <summary>
    /// Called when the flyout is dismissed (background click or another
    /// flyout opening). Override to reset control-specific state.
    /// Default implementation is a no-op.
    /// </summary>
    void OnDismissed() { }
}
