using System;
using System.IO;
using Avalonia.Controls;
using Cine.Avalonia.Services;

namespace Cine.Avalonia;

public partial class MainWindow : Window
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
                Icon = new WindowIcon(icoPath);
        }
        catch
        {
        }
    }
}
