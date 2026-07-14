using System;

namespace Simba.Avalonia.Extensions;

/// <summary>
/// Weak event handler — prevents memory leaks from strong event subscriptions.
/// P10.5: Use for long-lived event sources that outlive subscribers.
/// </summary>
public static class WeakEventHandler
{
    /// <summary>
    /// Wrap a handler in a weak reference so the subscriber can be GC'd independently.
    /// </summary>
    public static EventHandler<T> Wrap<T>(EventHandler<T> handler) where T : EventArgs
    {
        var weakRef = new WeakReference<EventHandler<T>>(handler);
        return (sender, args) =>
        {
            if (weakRef.TryGetTarget(out var target))
                target(sender, args);
        };
    }

    /// <summary>
    /// Wrap a non-generic EventHandler.
    /// </summary>
    public static EventHandler Wrap(EventHandler handler)
    {
        var weakRef = new WeakReference<EventHandler>(handler);
        return (sender, args) =>
        {
            if (weakRef.TryGetTarget(out var target))
                target(sender, args);
        };
    }
}
