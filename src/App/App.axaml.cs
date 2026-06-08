using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;
using Cine.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cine.Avalonia;

public class App : global::Avalonia.Application
{
    private IServiceProvider? _serviceProvider;

    #region debug-point A:runtime-reporter
    private static readonly HttpClient DebugHttpClient = new();
    private static readonly object DebugEnvLock = new();
    private static string? _debugServerUrl;
    private static string? _debugSessionId;

    internal static void DebugReport(string hypothesisId, string location, string msg, object? data = null, string runId = "pre-fix")
    {
#if DEBUG
        try
        {
            EnsureDebugEnvLoaded();
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = _debugSessionId ?? "transparent-window",
                runId,
                hypothesisId,
                location,
                msg = $"[DEBUG] {msg}",
                data,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            _ = DebugHttpClient.PostAsync(
                _debugServerUrl ?? "http://127.0.0.1:7777/event",
                new StringContent(payload, Encoding.UTF8, "application/json"))
                .ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
        catch
        {
        }
#endif
    }

    private static void EnsureDebugEnvLoaded()
    {
        if (!string.IsNullOrWhiteSpace(_debugServerUrl) && !string.IsNullOrWhiteSpace(_debugSessionId))
            return;

        lock (DebugEnvLock)
        {
            if (!string.IsNullOrWhiteSpace(_debugServerUrl) && !string.IsNullOrWhiteSpace(_debugSessionId))
                return;

            foreach (var root in EnumerateDebugRoots())
            {
                var dir = new DirectoryInfo(root);
                while (dir != null)
                {
                    var envPath = Path.Combine(dir.FullName, ".dbg", "no-playback.env");
                    if (!File.Exists(envPath))
                        envPath = Path.Combine(dir.FullName, ".dbg", "video-transparent.env");
                    if (!File.Exists(envPath))
                        envPath = Path.Combine(dir.FullName, ".dbg", "transparent-window.env");

                    if (File.Exists(envPath))
                    {
                        foreach (var line in File.ReadAllLines(envPath))
                        {
                            if (line.StartsWith("DEBUG_SERVER_URL=", StringComparison.Ordinal))
                                _debugServerUrl = line["DEBUG_SERVER_URL=".Length..].Trim();
                            else if (line.StartsWith("DEBUG_SESSION_ID=", StringComparison.Ordinal))
                                _debugSessionId = line["DEBUG_SESSION_ID=".Length..].Trim();
                        }
                        return;
                    }

                    dir = dir.Parent;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateDebugRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
    }
    #endregion

    private static void Log(string msg)
    {
        CrashReporter.LogError(msg);
        Console.WriteLine(msg);
    }

    public static void Main(string[] args)
    {
        CrashReporter.InstallGlobalHandlers();

        try
        {
            Log("=== Cine.Avalonia starting ===");
            var sw = Stopwatch.StartNew();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            sw.Stop();
            Log($"App exited gracefully after {sw.Elapsed.TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            CrashReporter.Dump(ex, "FATAL: Main entry point");
            Log($"FATAL: {ex.Message}");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                CompositionMode = new[] { Win32CompositionMode.RedirectionSurface }
            });
    }

    public override void Initialize()
    {
        Log("App.Initialize() - before base");
        DebugReport("A", "App.Initialize", "Entering application initialize.", new
        {
            currentDirectory = Environment.CurrentDirectory,
            baseDirectory = AppContext.BaseDirectory
        });
        try
        {
            AvaloniaXamlLoader.Load(this);
            Log("App.Initialize() - after base");
            DebugReport("A", "App.Initialize", "Application XAML loaded.", new
            {
                styleCount = Styles.Count
            });
        }
        catch (Exception ex)
        {
            Log($"Initialize FAILED: {ex}");
            DebugReport("A", "App.Initialize", "Application initialize failed.", new
            {
                exception = ex.ToString()
            });
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Log("OnFrameworkInitializationCompleted - start");

        try
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log($"UIThread.UnhandledException: {e.Exception}");
                e.Handled = true;
            };

            _serviceProvider = ConfigureServices();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    DebugReport("A", "App.OnFrameworkInitializationCompleted", "MainWindow resolved from DI.", new
                    {
                        type = mainWindow.GetType().FullName,
                        background = mainWindow.Background?.ToString(),
                        extendClientArea = mainWindow.ExtendClientAreaToDecorationsHint,
                        windowState = mainWindow.WindowState.ToString()
                    });
                    desktop.MainWindow = mainWindow;
                    Log("MainWindow created and assigned successfully.");

                    // Register file associations for double-click support
                    try { Cine.Avalonia.Services.FileAssociationService.Register(); } catch { }
                }
                catch (Exception ex)
                {
                    Log($"MainWindow creation FAILED: {ex}");
                    DebugReport("A", "App.OnFrameworkInitializationCompleted", "MainWindow creation failed.", new
                    {
                        exception = ex.ToString()
                    });
                    throw;
                }
            }
            else
            {
                Log($"NOT IClassicDesktopStyleApplicationLifetime: {ApplicationLifetime?.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            Log($"OnFrameworkInitializationCompleted FAILED: {ex}");
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<PlayerService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider();
    }
}
