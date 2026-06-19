using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;

namespace Cine.Avalonia.Services;

/// <summary>
/// Robust crash reporting — writes crash dumps to %LOCALAPPDATA%\Cine\crash\.
/// P10.6: Replaces silent catch with durable file logging + context capture.
/// </summary>
public static class CrashReporter
{
    private static readonly string CrashDir;

    static CrashReporter()
    {
        CrashDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "crash");
        try { Directory.CreateDirectory(CrashDir); }
        catch { /* best-effort — crash dir creation can fail in low-disk scenarios */ }
    }

    /// <summary>
    /// Write a crash dump with exception details, timestamp, and app version.
    /// </summary>
    public static void Dump(Exception ex, string context = "")
    {
        try
        {
            var fileName = $"crash_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt";
            var path = Path.Combine(CrashDir, fileName);

            using var sw = new StreamWriter(path, append: false);
            sw.WriteLine("=== Cine Crash Report ===");
            sw.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sw.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName}");
            sw.WriteLine($"PID: {Environment.ProcessId}");
            sw.WriteLine($"Context: {context}");
            sw.WriteLine($"Version: {typeof(CrashReporter).Assembly.GetName().Version ?? new Version(0,0,0,0)}");
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
            sw.Flush();

            // Keep only last 20 crash dumps
            CleanupOldDumps(20);
        }
        catch
        {
            // Last resort — nothing we can do
        }
    }

    /// <summary>
    /// Non-fatal error log — writes to cine_errors.log.
    /// </summary>
    public static void LogError(string message, Exception? ex = null)
    {
        try
        {
            var path = Path.Combine(
                Path.GetDirectoryName(CrashDir) ?? CrashDir,
                "cine_errors.log");
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
