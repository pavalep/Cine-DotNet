using System;
using System.Threading.Tasks;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Consistent error boundary for async event handlers.
/// P8.6: One-line guard replaces per-handler try-catch. Logs + notifies.
/// </summary>
public static class ErrorBoundary
{
    /// <summary>
    /// Wrap an async void handler (event handler) with error handling.
    /// Usage: ErrorBoundary.Run(() => OnMediaOpened(sender, e));
    /// </summary>
    public static async void Run(Func<Task> action, Action<Exception>? onError = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            System.Diagnostics.Debug.WriteLine($"[ErrorBoundary] {ex.Message}");
        }
    }

    /// <summary>
    /// Wrap a synchronous action with error handling.
    /// </summary>
    public static void Run(Action action, Action<Exception>? onError = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            System.Diagnostics.Debug.WriteLine($"[ErrorBoundary] {ex.Message}");
        }
    }

    /// <summary>
    /// Async version that returns a Result.
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ErrorBoundary] {ex.Message}");
            return Result.Fail(ex.Message);
        }
    }
}
