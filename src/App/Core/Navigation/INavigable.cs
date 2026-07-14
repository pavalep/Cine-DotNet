namespace Simba.Avalonia.Core.Navigation;

/// <summary>
/// Lifecycle hooks for navigable pages managed by <see cref="Simba.Avalonia.Navigation.INavigationService"/>.
/// Pages implement this interface to react to navigation events.
/// </summary>
public interface INavigable
{
    /// <summary>Called when the page becomes the active navigation target.</summary>
    /// <param name="parameter">Optional data passed from the navigation request.</param>
    void OnNavigatedTo(object? parameter);

    /// <summary>Called when the page is no longer the active navigation target.</summary>
    void OnNavigatedFrom();
}
