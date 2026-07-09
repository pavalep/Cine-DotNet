using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Cine.Avalonia.Views.Components;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Services;

namespace Cine.Avalonia.Views.Shell;

public partial class MainWindow : global::Avalonia.Controls.Window
{
    /// <summary>XAML designer constructor only. Does not run initialization.</summary>
    public MainWindow() { InitializeComponent(); LoadIcon(); }

    /// <summary>DI constructor — injects service provider for deferred resolution.</summary>
    public MainWindow(IServiceProvider serviceProvider) : this()
    {
        _serviceProvider = serviceProvider;
        OnWindowInitialized();
        InitializeWindowBorder();
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

    // ─────────────────────────────────────────────────────────────
    //  Window Frame (focus-aware border + rounded corners)
    // ─────────────────────────────────────────────────────────────

    private void InitializeWindowBorder()
    {
        // Wire window state changes (maximized ↔ normal) to corner radius
        PropertyChanged += OnWindowPropertyChanged;

        // DWM rounded corners require the native HWND, which is only
        // available after the window is opened.
        Opened += OnWindowOpened;

        // Set initial state
        UpdateCornerRadius();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        UpdateDwmCornerPreference();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            UpdateCornerRadius();
            UpdateDwmCornerPreference();
        }
    }

    internal void UpdateCornerRadius()
    {
        bool isMaximized = WindowState == WindowState.Maximized
                        || WindowState == WindowState.FullScreen;
        var radius = isMaximized ? new CornerRadius(0) : new CornerRadius(8);

        if (ContentClip != null)
            ContentClip.CornerRadius = radius;
        if (WindowFrame != null)
            WindowFrame.CornerRadius = radius;
    }

    internal void UpdateFocusBorder(bool focused)
    {
        if (WindowFrame == null) return;

        if (focused)
        {
            try
            {
                // Try to get the Windows system accent color via PlatformSettings
                var accentColor = Application.Current?.PlatformSettings?.GetColorValues()?.AccentColor1;
                if (accentColor.HasValue)
                    WindowFrame.BorderBrush = new global::Avalonia.Media.SolidColorBrush(accentColor.Value);
                else
                    WindowFrame.BorderBrush =
                        (IBrush?)Application.Current?.FindResource("AppAccent")
                        ?? new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Colors.White);
            }
            catch
            {
                // Fallback: use the app's accent color resource
                WindowFrame.BorderBrush =
                    (IBrush?)Application.Current?.FindResource("AppAccent")
                    ?? new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Colors.White);
            }
        }
        else
        {
            WindowFrame.BorderBrush = global::Avalonia.Media.Brushes.Transparent;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  DWM Native Rounded Corners (clips MPV video surface too)
    // ─────────────────────────────────────────────────────────────

    private void UpdateDwmCornerPreference()
    {
        try
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (hwnd == nint.Zero)
                return;

            bool round = WindowState != WindowState.Maximized
                      && WindowState != WindowState.FullScreen;
            int preference = round ? 2 : 1; // DWMWCP_ROUND=2, DWMWCP_DONOTROUND=1
            DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
        }
        catch
        {
            // DWM API unavailable (pre-Win11 / non-Windows)
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);
}
