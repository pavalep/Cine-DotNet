using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cine.Core.Services;

public class FileLogger : ILogger, IDisposable
{
    private readonly string _logDir;
    private readonly string _logFile;
    private readonly string _name;
    private readonly ConcurrentDictionary<string, object?> _context = new();
    private readonly StreamWriter? _writer;
    private readonly object _lock = new();

    public FileLogger(string name = "Cine", string? logDir = null)
    {
        _name = name;
        _logDir = logDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "logs");
        Directory.CreateDirectory(_logDir);

        _logFile = Path.Combine(_logDir, $"{name}_{DateTime.Now:yyyy-MM-dd}.log");

        try
        {
            _writer = new StreamWriter(_logFile, append: true, Encoding.UTF8)
            {
                AutoFlush = false // Batch writes for performance
            };
        }
        catch
        {
            // Can't log - no file system access; fallback silent
        }
    }

    public void Trace(string message, params object?[] args) => Write("TRACE", null, message, args);
    public void Debug(string message, params object?[] args) => Write("DEBUG", null, message, args);
    public void Info(string message, params object?[] args) => Write("INFO", null, message, args);
    public void Warning(string message, params object?[] args) => Write("WARN", null, message, args);
    public void Error(Exception ex, string message, params object?[] args) => Write("ERROR", ex, message, args);
    public void Critical(Exception ex, string message, params object?[] args) => Write("CRIT", ex, message, args);

    public ILogger WithContext(string key, object? value)
    {
        var child = new FileLogger(_name + "." + key, _logDir);
        foreach (var kv in _context)
            child._context.TryAdd(kv.Key, kv.Value);
        child._context.TryAdd(key, value);
        return child;
    }

    private void Write(string level, Exception? ex, string message, object?[] args)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var formatted = args.Length > 0 ? string.Format(message, args) : message;

        // Build context string
        string ctx = "";
        if (!_context.IsEmpty)
            ctx = " [" + string.Join(", ", _context) + "]";

        var line = $"{timestamp} [{level}] [{_name}]{ctx} {formatted}";
        if (ex != null)
            line += $"\n{timestamp} [{level}] [{_name}] Exception: {ex}";

        // Always write to Debug output
        System.Diagnostics.Debug.WriteLine(line);

        // Write to file
        if (_writer != null)
        {
            lock (_lock)
            {
                _writer.WriteLine(line);
                _writer.Flush(); // Ensure log is written immediately for crash safety
            }
        }

        // Also trace to console for development
        System.Diagnostics.Trace.WriteLine(line);
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
    }
}
