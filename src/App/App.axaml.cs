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
using Avalonia.Win32;
using Cine.Avalonia.Services;
using Cine.Avalonia.Managers;
using Cine.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cine.Avalonia;

public class App : global::Avalonia.Application
{
    private IServiceProvider? _serviceProvider;

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

    private static void Log(string msg)
    {
        CrashReporter.LogError(msg);
        Console.WriteLine(msg);
    }

    /// <summary>True when running as an MSIX packaged app (installed to WindowsApps).</summary>
    private static bool IsRunningAsPackaged()
    {
        return AppContext.BaseDirectory.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase);
    }

    public static void Main(string[] args)
    {
        // ── Six-Layer Exception Defense ──
        // Layer 4: Thread-pool / non-task exceptions (informational — can't prevent termination)
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            CrashReporter.Dump((Exception)e.ExceptionObject, "AppDomain.UnhandledException");
        };

        // Layer 3: Forgotten task exceptions (fire on finalizer — delayed)
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashReporter.Log(e.Exception, isWarning: true);
            e.SetObserved(); // prevent process teardown
        };

        // Install global handlers for Layer 4+ (handles native AV, etc.)
        CrashReporter.InstallGlobalHandlers();

        // Layer 5: Last line of defense — global try-catch around entire app lifetime
        try
        {
            Log("=== Cine.Avalonia starting ===");

            // ── On-demand runtime download ──
            // When running as MSIX (has package identity), libmpv DLLs are excluded
            // from the package. Download them on first launch.
            // When running locally (dotnet run), DLLs are already in bin/ — skip.
            if (IsRunningAsPackaged())
            {
                try
                {
                    if (!RuntimeDownloader.IsRuntimeReady())
                    {
                        Log("MSIX: Runtime DLLs missing — downloading on demand...");
                        System.Console.WriteLine("Downloading media runtime (first launch, this may take a minute)...");
                        RuntimeDownloader.EnsureRuntimeAsync().GetAwaiter().GetResult();
                        Log("MSIX: Runtime download complete.");
                    }
                }
                catch (Exception dlEx)
                {
                    Log($"MSIX: Runtime download failed: {dlEx.Message}");
                    System.Console.WriteLine($"Warning: Could not download media runtime. ({dlEx.Message})");
                }
            }

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
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

#if WINDOWS
        builder = builder.With(new Win32PlatformOptions
        {
            RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software },
            CompositionMode = new[] { Win32CompositionMode.RedirectionSurface }
        });
#endif

        return builder;
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
            // ── Six-Layer Exception Defense: Layer 1 (UI thread) + Layer 2 (filter) ──
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log($"UIThread.UnhandledException: {e.Exception}");
                CrashReporter.Log(e.Exception, isWarning: true);
                e.Handled = true;
            };

            // Layer 2: Filter benign exceptions — don't trap cancellation
            Dispatcher.UIThread.UnhandledExceptionFilter += (_, e) =>
            {
                if (e.Exception is TaskCanceledException or OperationCanceledException)
                    e.RequestCatch = false;
            };

            _serviceProvider = ConfigureServices();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // ── First-Launch Detection: download native DLLs if missing ──
                if (!RuntimeDownloader.IsRuntimeReady())
                {
                    var downloadVm = new ViewModels.Dialogs.FirstLaunchViewModel();
                    var downloadDialog = new Views.Dialogs.FirstLaunchDialog
                    {
                        DataContext = downloadVm
                    };

                    downloadVm.DownloadComplete += () =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            ShowMainWindow(desktop);
                            downloadDialog.Close();
                        });
                    };

                    downloadDialog.Show();
                }
                else
                {
                    ShowMainWindow(desktop);
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

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
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
            try
            {
                var registry = new Cine.Avalonia.Services.WindowsRegistryService();
                var fileAssoc = new Cine.Avalonia.Services.FileAssociationService(registry);
                fileAssoc.RegisterOnStartup();
            }
            catch (Exception regEx)
            {
                Log($"File association registration failed: {regEx.Message}");
            }
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

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core infrastructure — stateless, safe as singletons
        services.AddSingleton<InputRoutingService>();
        services.AddSingleton<ThemeService>();

        // Settings stores — single instance shared across the app
        services.AddSingleton<SubtitleSettingsStore>();
        services.AddSingleton<AudioSettingsStore>();
        services.AddSingleton<PlaylistSettingsStore>();

        // Player service — singleton, initialized once
        services.AddSingleton<PlayerService>();

        // ViewModels and Windows — transient (Avalonia manages lifetime)
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
