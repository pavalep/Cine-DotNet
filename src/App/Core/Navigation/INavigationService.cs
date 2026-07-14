using System;

namespace Simba.Avalonia.Core.Navigation;

/// <summary>Abstraction over application-level navigation between pages.</summary>
public interface INavigationService
{
    /// <summary>Raised when a navigation request has been processed.</summary>
    event EventHandler<NavigationRequest>? Navigated;

    /// <summary>The currently active navigable page.</summary>
    INavigable? CurrentPage { get; set; }

    /// <summary>Navigate to the specified route, passing optional data.</summary>
    void Navigate(AppRoute route, object? parameter = null);
}
