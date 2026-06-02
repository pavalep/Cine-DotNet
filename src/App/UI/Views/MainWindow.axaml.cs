using System;
using System.IO;
using Avalonia.Controls;

namespace Cine.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadIcon();
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
