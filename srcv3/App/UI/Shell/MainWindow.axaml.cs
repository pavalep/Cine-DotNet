using System;
using System.IO;
using Cine.Avalonia.Components;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Services;

namespace Cine.Avalonia;

public partial class MainWindow : global::Avalonia.Controls.Window
{
    /// <summary>XAML designer constructor only. Does not run initialization.</summary>
    public MainWindow() { InitializeComponent(); LoadIcon(); }

    /// <summary>DI constructor — injects service provider for deferred resolution.</summary>
    public MainWindow(IServiceProvider serviceProvider) : this()
    {
        _serviceProvider = serviceProvider;
        OnWindowInitialized();
    }

    private void LoadIcon()
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "UI\\Resources\\AppIcon.ico");
            if (File.Exists(icoPath))
                Icon = new global::Avalonia.Controls.WindowIcon(icoPath);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Finds the window-level FlyoutOverlay from any control in the tree.
    /// Returns null if not inside a MainWindow.
    /// </summary>
    public static FlyoutOverlay? GetOverlay(global::Avalonia.Visual from)
    {
        if (global::Avalonia.Controls.TopLevel.GetTopLevel(from) is MainWindow mw)
            return mw.FlyoutOverlay;
        return null;
    }
}
