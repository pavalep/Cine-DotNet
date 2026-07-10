using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
            StartPage?.UpdateMaximizeIcon(WindowState == WindowState.Maximized);
        }
    }

    private Geometry? CreateRoundedRectClip(double w, double h, CornerRadius r)
    {
        if (w <= 0 || h <= 0) return null;
        double tl = r.TopLeft, tr = r.TopRight, br = r.BottomRight, bl = r.BottomLeft;

        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(new global::Avalonia.Point(tl, 0), isFilled: true);
        ctx.LineTo(new global::Avalonia.Point(w - tr, 0));
        ctx.ArcTo(new global::Avalonia.Point(w, tr), new global::Avalonia.Size(tr, tr), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new global::Avalonia.Point(w, h - br));
        ctx.ArcTo(new global::Avalonia.Point(w - br, h), new global::Avalonia.Size(br, br), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new global::Avalonia.Point(bl, h));
        ctx.ArcTo(new global::Avalonia.Point(0, h - bl), new global::Avalonia.Size(bl, bl), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new global::Avalonia.Point(0, tl));
        ctx.ArcTo(new global::Avalonia.Point(tl, 0), new global::Avalonia.Size(tl, tl), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: true);
        return geom;
    }

    internal void UpdateCornerRadius()
    {
        bool isMaximized = WindowState == WindowState.Maximized
                        || WindowState == WindowState.FullScreen;
        var radius = isMaximized ? new CornerRadius(0) : new CornerRadius(8);

        if (ContentClip != null)
        {
            ContentClip.CornerRadius = radius;

            // StreamGeometry clip prevents native ANGLE/OpenGL rendering
            // from spilling past the rounded corners (ClipToBounds alone
            // only clips to the rectangular area).
            if (!isMaximized)
                ContentClip.Clip = CreateRoundedRectClip(
                    ContentClip.Bounds.Width, ContentClip.Bounds.Height, radius);
            else
                ContentClip.Clip = null;
        }

        // Apply clip directly to PlayerPage for defense-in-depth.
        // MpvVideoView handles its own internal clip via ArrangeOverride.
        if (PlayerPage != null)
        {
            if (!isMaximized)
                PlayerPage.Clip = CreateRoundedRectClip(
                    PlayerPage.Bounds.Width, PlayerPage.Bounds.Height, radius);
            else
                PlayerPage.Clip = null;
        }
        if (PlayerPage?.MpvVideoView != null)
        {
            // Set CornerRadius — MpvVideoView.ArrangeOverride applies the
            // StreamGeometry clip directly to the internal _videoImage,
            // which is the only reliable way to clip native ANGLE rendering.
            PlayerPage.MpvVideoView.CornerRadius = radius;
        }

        if (WindowFrame != null)
        {
            WindowFrame.CornerRadius = radius;
            // Hide the 2px focus border when maximized/fullscreen — it only
            // makes sense in windowed mode.
            WindowFrame.IsVisible = !isMaximized;
        }

        // Hide resize grips when maximized (fullscreen already handled
        // by RefreshFullscreenUi to preserve gesture parity).
        if (ResizeGripPanel != null)
            ResizeGripPanel.IsVisible = !isMaximized;
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

    // ════════════════════════════════════════════════════════════════
    //  PANEL MANAGEMENT — hide/show all inline panels + light dismiss
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hides ALL inline panels (both header and controls bar panels)
    /// and disables the light-dismiss overlay.
    /// </summary>
    public void HideAllPanels()
    {
        MainVolumePanel.IsVisible = false;
        MainSubtitlePanel.IsVisible = false;
        MainAudioTrackPanel.IsVisible = false;
        MainChaptersPanel.IsVisible = false;
        MainPlaylistPanel.IsVisible = false;
        MainOpenMenuPanel.IsVisible = false;
        MainPrimaryMenuPanel.IsVisible = false;
        PanelDismissBackground.IsHitTestVisible = false;
    }

    /// <summary>
    /// Enables the light-dismiss overlay so clicking anywhere outside
    /// the panels closes them.
    /// </summary>
    public void EnablePanelDismiss()
    {
        PanelDismissBackground.IsHitTestVisible = true;
    }

    /// <summary>
    /// Re-evaluates whether any panel is visible and updates the
    /// light-dismiss overlay accordingly. Call after manually hiding
    /// a panel (e.g. on same-button toggle-off or panel-internal close).
    /// </summary>
    public void UpdatePanelDismissState()
    {
        bool anyVisible = MainVolumePanel.IsVisible
            || MainSubtitlePanel.IsVisible
            || MainAudioTrackPanel.IsVisible
            || MainChaptersPanel.IsVisible
            || MainPlaylistPanel.IsVisible
            || MainOpenMenuPanel.IsVisible
            || MainPrimaryMenuPanel.IsVisible;

        PanelDismissBackground.IsHitTestVisible = anyVisible;
    }

    private void OnPanelDismissBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        HideAllPanels();
    }
}
