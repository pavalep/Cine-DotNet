using System;

namespace Cine.Core.Services;

/// <summary>
/// Structured logger with severity levels, context, and file output.
/// Replaces all Debug.WriteLine() and DebugReport() calls.
/// </summary>
public interface ILogger
{
    void Trace(string message, params object?[] args);
    void Debug(string message, params object?[] args);
    void Info(string message, params object?[] args);
    void Warning(string message, params object?[] args);
    void Error(Exception ex, string message, params object?[] args);
    void Critical(Exception ex, string message, params object?[] args);

    /// <summary>
    /// Creates a scoped child logger with additional context tags.
    /// </summary>
    ILogger WithContext(string key, object? value);
}
