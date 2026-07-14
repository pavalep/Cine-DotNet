using System;

namespace Simba.Avalonia.Core.Navigation;

/// <summary>Represents a navigation request with route and optional parameter.</summary>
public class NavigationRequest : EventArgs
{
    /// <summary>The target route.</summary>
    public AppRoute Route { get; }
    
    /// <summary>Optional data to pass to the navigable page.</summary>
    public object? Parameter { get; }

    public NavigationRequest(AppRoute route, object? parameter = null)
    {
        Route = route;
        Parameter = parameter;
    }
}
