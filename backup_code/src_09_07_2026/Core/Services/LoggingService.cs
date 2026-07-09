using System;
using System.Collections.Concurrent;
using Cine.Core.Services;

namespace Cine.Core;

/// <summary>
/// Central logging service - singleton factory for ILogger instances.
/// All application code should use Log.ForContext< T >() to get a named logger.
/// </summary>
public static class Log
{
    private static readonly Lazy<FileLogger> _root = new(() => new FileLogger("Cine"));
    private static readonly ConcurrentDictionary<string, ILogger> _cache = new();

    public static ILogger Default => _root.Value;

    /// <summary>Get a named logger (e.g. Log.ForContext("PlayerService")).</summary>
    public static ILogger ForContext(string name)
    {
        return _cache.GetOrAdd(name, n => _root.Value.WithContext("source", n));
    }

    /// <summary>Get a logger typed to a class (e.g. Log.ForContext<PlayerService>()).</summary>
    public static ILogger ForContext<T>() => ForContext(typeof(T).Name);

    // Convenience methods that delegate to the root logger
    public static void Trace(string message, params object?[] args) => _root.Value.Trace(message, args);
    public static void Debug(string message, params object?[] args) => _root.Value.Debug(message, args);
    public static void Info(string message, params object?[] args) => _root.Value.Info(message, args);
    public static void Warning(string message, params object?[] args) => _root.Value.Warning(message, args);
    public static void Error(Exception ex, string message, params object?[] args) => _root.Value.Error(ex, message, args);
    public static void Critical(Exception ex, string message, params object?[] args) => _root.Value.Critical(ex, message, args);
}
