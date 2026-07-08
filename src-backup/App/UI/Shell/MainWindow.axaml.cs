using System;
using System.IO;
using Cine.Avalonia.Components;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Services;

namespace Cine.Avalonia;

public partial class MainWindow : global::Avalonia.Controls.Window
{
    /// <summary>XAML runtime constructor (design-time preview).</summary>
    public MainWindow() : this(null, null) { }

    /// <summary>DI constructor — injects optional services.</summary>
    public MainWindow(InputRoutingService? inputRouter, PlayerService? playerService)
    {
        InitializeComponent();
        LoadIcon();
        if (inputRouter != null) _inputRouter = inputRouter;
        if (playerService != null) _playerService = playerService;
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
