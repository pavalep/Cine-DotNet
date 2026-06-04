using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Extension methods for Dispatcher to reduce boilerplate.
/// P7.1: Replaces Dispatcher.UIThread.Post/InvokeAsync with one-liner .OnUiThread().
/// </summary>
public static class DispatcherExtensions
{
    /// <summary>
    /// Invoke an action on the UI thread. If already on UI thread, executes synchronously.
    /// </summary>
    public static void OnUiThread(this Dispatcher dispatcher, Action action, DispatcherPriority priority = default)
    {
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Post(action, priority);
    }

    /// <summary>
    /// Invoke a function on the UI thread and await the result.
    /// </summary>
    public static async Task<T> OnUiThreadAsync<T>(this Dispatcher dispatcher, Func<T> func, DispatcherPriority priority = default)
    {
        if (dispatcher.CheckAccess())
            return func();
        return await dispatcher.InvokeAsync(func, priority);
    }

    /// <summary>
    /// Invoke an async function on the UI thread and await its completion.
    /// </summary>
    public static async Task OnUiThreadAsync(this Dispatcher dispatcher, Func<Task> func, DispatcherPriority priority = default)
    {
        if (dispatcher.CheckAccess())
            await func();
        else
            await dispatcher.InvokeAsync(func, priority);
    }

    /// <summary>
    /// Invoke an action on the UI thread and await its completion.
    /// </summary>
    public static async Task OnUiThreadAsync(this Dispatcher dispatcher, Action action, DispatcherPriority priority = default)
    {
        if (dispatcher.CheckAccess())
            action();
        else
            await dispatcher.InvokeAsync(action, priority);
    }
}
