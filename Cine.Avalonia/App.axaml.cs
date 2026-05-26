using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cine.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cine.Avalonia;

public class App : global::Avalonia.Application
{
    private IServiceProvider? _serviceProvider;
    private static readonly string LogFile = Path.Combine(
        AppContext.BaseDirectory, "cine_startup.log");

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}"); }
        catch { }
        Console.WriteLine(msg);
    }

    public static void Main(string[] args)
    {
        try
        {
            Log("=== Cine.Avalonia starting ===");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect();
    }

    public override void Initialize()
    {
        Log("App.Initialize() - before base");
        try
        {
            AvaloniaXamlLoader.Load(this);
            Log("App.Initialize() - after base");
        }
        catch (Exception ex)
        {
            Log($"Initialize FAILED: {ex}");
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Log("OnFrameworkInitializationCompleted - start");

        try
        {
            _serviceProvider = ConfigureServices();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    desktop.MainWindow = mainWindow;
                    Log("MainWindow created and assigned successfully.");
                }
                catch (Exception ex)
                {
                    Log($"MainWindow creation FAILED: {ex}");
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
