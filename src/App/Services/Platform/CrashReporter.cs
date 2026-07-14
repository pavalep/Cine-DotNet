using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;

namespace Simba.Avalonia.Services;

/// <summary>
/// Robust crash reporting — writes crash dumps to %LOCALAPPDATA%\Simba\crash\.
/// P10.6: Replaces silent catch with durable file logging + context capture.
/// </summary>
public static class CrashReporter
{
    private static readonly string CrashDir;

    static CrashReporter()
    {
        CrashDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simba", "crash");
        try { Directory.CreateDirectory(CrashDir); }
        catch { /* best-effort — crash dir creation can fail in low-disk scenarios */ }
    }

    // Re-entry guard — prevents crash-loop when the crash reporter itself throws
    private static int _inCrash;

    /// <summary>
    /// Write a crash dump with exception details, timestamp, and app version.
    /// P5B.2: Re-entry guard prevents crash-loop on crash-writer failure.
    /// P5B.2: Structured dump with OS, framework, thread, memory info.
    /// </summary>
    public static void Dump(Exception ex, string context = "")
    {
        // Re-entry guard: if we're already writing a crash dump, don't recurse
        if (Interlocked.Exchange(ref _inCrash, 1) == 1)
        {
            Debug.WriteLine($"[CrashReporter] Re-entry blocked: {ex.GetType().Name} in {context}");
            return;
        }

        try
        {
            var fileName = $"crash_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt";
            var path = Path.Combine(CrashDir, fileName);

            using var sw = new StreamWriter(path, append: false);
            sw.WriteLine("=== Simba Crash Report ===");
            sw.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sw.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName}");
            sw.WriteLine($"PID: {Environment.ProcessId}");
            sw.WriteLine($"Thread: {Thread.CurrentThread.Name ?? "unnamed"} (#{Environment.CurrentManagedThreadId})");
            sw.WriteLine($"Context: {context}");
            sw.WriteLine($"Version: {typeof(CrashReporter).Assembly.GetName().Version ?? new Version(0,0,0,0)}");
            sw.WriteLine($"Framework: {Environment.Version}");
            sw.WriteLine($"OS: {Environment.OSVersion}");
            sw.WriteLine($"64-bit: {Environment.Is64BitProcess}");
            sw.WriteLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
            sw.WriteLine($"TickCount: {Environment.TickCount64} ms");
            sw.WriteLine();
            sw.WriteLine("--- Exception ---");
            sw.WriteLine(ex.ToString());
            sw.WriteLine();
            if (ex.StackTrace != null)
            {
                sw.WriteLine("--- Stack Trace ---");
                sw.WriteLine(ex.StackTrace);
            }

            // Unwrap inner exceptions
            var inner = ex.InnerException;
            int depth = 1;
            while (inner != null)
            {
                sw.WriteLine();
                sw.WriteLine($"--- Inner Exception #{depth} ---");
                sw.WriteLine(inner.ToString());
                inner = inner.InnerException;
                depth++;
            }

            sw.Flush();

            // Keep only last 20 crash dumps
            CleanupOldDumps(20);
        }
        catch (Exception dumpEx)
        {
            // Last resort — log to Debug output if file write fails
            Debug.WriteLine($"[CrashReporter] Failed to write crash dump: {dumpEx.Message}");
            Debug.WriteLine($"[CrashReporter] Original error: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _inCrash, 0);
        }
    }

    /// <summary>
    /// Non-fatal error/warning log. Writes to simba_errors.log.
    /// </summary>
    public static void Log(Exception ex, bool isWarning = false)
    {
        LogError(isWarning ? $"[WARN] {ex.GetType().Name}: {ex.Message}" : ex.ToString());
    }

    /// <summary>
    /// Non-fatal error log — writes to simba_errors.log.
    /// </summary>
    public static void LogError(string message, Exception? ex = null)
    {
        try
        {
            var path = Path.Combine(
                Path.GetDirectoryName(CrashDir) ?? CrashDir,
                "simba_errors.log");
            using var sw = new StreamWriter(path, append: true);
            sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            if (ex != null) sw.WriteLine($"  Exception: {ex.Message}");
            sw.Flush();
        }
        catch
        {
            /* best-effort — this IS the crash writer, can't log downstream */
        }
    }

    /// <summary>
    /// Initialize global exception handlers.
    /// </summary>
    public static void InstallGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Dump(ex, "AppDomain.UnhandledException");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Dump(e.Exception, "TaskScheduler.UnobservedTaskException");
            try { e.SetObserved(); }
            catch { /* SetObserved throws if already observed — safe to ignore */ }
        };
    }

    private static void CleanupOldDumps(int maxFiles)
    {
        try
        {
            var dir = new DirectoryInfo(CrashDir);
            var files = dir.GetFiles("crash_*.txt");
            if (files.Length <= maxFiles) return;
            Array.Sort(files, (a, b) => a.CreationTime.CompareTo(b.CreationTime));
            for (int i = 0; i < files.Length - maxFiles; i++)
                files[i].Delete();
        }
        catch { /* best-effort cleanup — not critical */ }
    }
}
