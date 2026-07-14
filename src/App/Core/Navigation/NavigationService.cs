using System;

namespace Simba.Avalonia.Core.Navigation;

/// <summary>Default implementation of <see cref="INavigationService"/>.</summary>
internal sealed class NavigationService : INavigationService
{
    public event EventHandler<NavigationRequest>? Navigated;

    public INavigable? CurrentPage { get; set; }

    public void Navigate(AppRoute route, object? parameter = null)
    {
        var request = new NavigationRequest(route, parameter);
        Navigated?.Invoke(this, request);
    }
}
