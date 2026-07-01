using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Services;
using Cine.Media.Implementations;

namespace Cine.Avalonia;

/// <summary>
/// Startup, player initialization, and renderer setup.
/// Extracted from MainWindow.Initialization.cs (~444 lines → ~80 lines here).
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Initializes the ANGLE/OpenGL video renderer attached to the mpv player.
    /// MpvVideoView creates its own ANGLE context and runs a dedicated render thread.
    /// </summary>
    private void InitVideoRenderer()
    {
        var player = _playerService?.Player as MpvPlayer;
        if (player == null || _viewModel == null)
        {
            DebugLog("InitVideoRenderer: player or viewModel is null");
            return;
        }

        DebugLog("InitVideoRenderer: initializing MpvVideoView (ANGLE + render API)");

        // Main window uses ANGLE/OpenGL render API by default.
        // MpvVideoView creates its own ANGLE context, initializes mpv render API,
        // and runs a dedicated render thread that updates a WriteableBitmap Image.
        // This bypasses Avalonia's OpenGlControlBase which can fail silently in v12.
        try
        {
            MpvVideoView.Initialize(player);

            // Phase 2 premium: wire performance services
            var perfMonitor = new PerformanceMonitor();
            var renderThrottle = new RenderThrottleService();
            MpvVideoView.SetPerformanceServices(perfMonitor, renderThrottle);
            DebugLog("InitVideoRenderer: performance services wired");
        }
        catch (System.DllNotFoundException dllEx)
        {
            // Missing native ANGLE/GL DLLs — continue without fatal crash and log clear guidance.
            DebugLog($"InitVideoRenderer FAILED: {dllEx}");
            DebugLog("ANGLE/GL not available. Video rendering disabled. To enable, install runtime DLLs (libEGL.dll/libGLESv2.dll) or unset CINE_DEV_MODE.");
            // Detach player so other systems can still operate.
            try { MpvVideoView.DetachPlayer(); } catch (Exception detEx) { DebugLog($"DetachPlayer failed: {detEx}"); }
        }
        catch (Exception ex)
        {
            // Generic fallback — avoid crashing the whole UI if renderer init fails.
            DebugLog($"InitVideoRenderer FAILED: {ex}");
            DebugLog("Video renderer initialization failed; continuing without hardware-backed video.");
            try { MpvVideoView.DetachPlayer(); } catch (Exception detEx) { DebugLog($"DetachPlayer failed: {detEx}"); }
        }
    }

    /// <summary>
    /// Sets up a 15-second interval timer that persists playback session state.
    /// </summary>
    private void InitializeSessionSave()
    {
        _sessionSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _sessionSaveTimer.Tick += (_, _) => _viewModel?.SaveSession();
        _sessionSaveTimer.Start();
    }
}
