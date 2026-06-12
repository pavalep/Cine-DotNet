using System;
using System.IO;
using Avalonia.Threading;
using Cine.Core;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Manages PIP (Picture-in-Picture) lifecycle.
/// Creates a second mpv player and syncs it with the primary player.
/// Both players use the OpenGL render API via ANGLE.
/// </summary>
public class PipService : IDisposable
{
    private PipWindow? _pipWindow;
    private PipPlayerService? _pipPlayerService;
    private PipSyncCoordinator? _syncCoordinator;
    private readonly PlayerService _playerService;
    private bool _isActive;
    private bool _disposed;

    public PipService(PlayerService playerService)
    {
        _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
    }

    /// <summary>Whether PIP mode is currently active.</summary>
    public bool IsActive => _isActive;

    /// <summary>The active PipWindow, if any.</summary>
    public PipWindow? PipWindow => _pipWindow;

    /// <summary>The secondary PiP player service, if active.</summary>
    public PipPlayerService? PipPlayer => _pipPlayerService;

    /// <summary>Fires when the user clicks play/pause in the PIP window.</summary>
    public event EventHandler? PlayPauseRequested;

    /// <summary>Fires when the user seeks in the PIP window (normalized 0..1).</summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>Fires when the user toggles mute in the PIP window.</summary>
    public event EventHandler? MuteToggled;

    /// <summary>Fires when the PIP window is closed (by user or programmatically).</summary>
    public event EventHandler? PipClosed;

    public PipWindow? EnterPip()
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cine", "cine_pip.log");
        try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip start{Environment.NewLine}"); } catch { }

        if (_disposed)
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip: disposed{Environment.NewLine}"); } catch { }
            Log.ForContext<PipService>().Warning("EnterPip: disposed");
            return null;
        }
        if (_isActive)
        {
            if (_pipWindow == null || _pipWindow.IsClosed)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip: stale _isActive=true, resetting{Environment.NewLine}"); } catch { }
                Log.ForContext<PipService>().Warning("EnterPip: stale _isActive=true, resetting");
                _isActive = false;
                _pipWindow = null;
                _pipPlayerService = null;
                _syncCoordinator = null;
            }
            else
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip: already active{Environment.NewLine}"); } catch { }
                Log.ForContext<PipService>().Info("EnterPip: already active");
                return _pipWindow;
            }
        }

        try
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Creating PipWindow...{Environment.NewLine}"); } catch { }
            Log.ForContext<PipService>().Info("EnterPip: creating PipWindow");
            _pipWindow = new PipWindow();
            _pipWindow.Closed += OnPipWindowClosed;

            // Forward player control events
            _pipWindow.PlayPauseRequested += (s, e) => PlayPauseRequested?.Invoke(s, e);
            _pipWindow.SeekRequested += (s, pos) => SeekRequested?.Invoke(s, pos);
            _pipWindow.MuteToggled += (s, e) => MuteToggled?.Invoke(s, e);

            Log.ForContext<PipService>().Info("EnterPip: calling Show()");
            _pipWindow.Show();
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Show() returned{Environment.NewLine}"); } catch { }

            // Create secondary player using the OpenGL render API
            _pipPlayerService = new PipPlayerService();
            _pipPlayerService.Error += (_, msg) =>
            {
                Log.ForContext<PipService>().Warning("PipPlayer: {0}", msg);
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] PipPlayer error: {msg}{Environment.NewLine}"); } catch { }
            };

            if (!_pipPlayerService.Initialize())
            {
                Log.ForContext<PipService>().Warning("EnterPip: failed to initialize PipPlayer");
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip: PipPlayer init failed{Environment.NewLine}"); } catch { }
                CleanupPip();
                return null;
            }

            // Wire frame rendering from secondary player to PipWindow
            _pipPlayerService.FrameRendered += (pixels, w, h) =>
            {
                Dispatcher.UIThread.Post(() => _pipWindow?.UpdateFrame(pixels, w, h), DispatcherPriority.Render);
            };

            // Open same file in secondary player and sync position
            var primary = _playerService.Player;
            var secondary = _pipPlayerService.Player;
            if (primary != null && secondary != null)
            {
                var path = primary.CurrentPath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    // Open file — Seek must wait for file to load (async in mpv)
                    EventHandler? onSecondaryOpened = null;
                    onSecondaryOpened = (_, _) =>
                    {
                        secondary.Opened -= onSecondaryOpened;
                        secondary.Seek(primary.Position);
                    };
                    secondary.Opened += onSecondaryOpened;
                    _pipPlayerService.Open(path);
                }
            }

            // Create sync coordinator (mirrors play/pause/position from primary to secondary)
            if (primary != null && secondary != null)
            {
                _syncCoordinator = new PipSyncCoordinator(primary, secondary);
            }

            // Show controls initially, then auto-hide after 5s
            _pipWindow.ShowAllControls();
            _pipWindow.StartHoverTimer();

            _isActive = true;
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip success{Environment.NewLine}"); } catch { }
            Log.ForContext<PipService>().Info("EnterPip: success");
            return _pipWindow;
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EnterPip EXCEPTION: {ex}{Environment.NewLine}"); } catch { }
            Log.ForContext<PipService>().Error(ex, "EnterPip failed");
            CleanupPip();
            return null;
        }
    }

    public void ExitPip()
    {
        if (!_isActive) return;

        // Stop sync first, then stop player
        _syncCoordinator?.Dispose();
        _syncCoordinator = null;

        _pipPlayerService?.Stop();
        _pipPlayerService?.Dispose();
        _pipPlayerService = null;

        if (_pipWindow != null)
        {
            _pipWindow.Closed -= OnPipWindowClosed;
            try { _pipWindow.Close(); } catch { }
            _pipWindow = null;
        }
        _isActive = false;
    }

    private void OnPipWindowClosed(object? sender, EventArgs e)
    {
        // Cleanup player and sync when user closes PiP window directly
        _syncCoordinator?.Dispose();
        _syncCoordinator = null;

        _pipPlayerService?.Stop();
        _pipPlayerService?.Dispose();
        _pipPlayerService = null;

        _pipWindow = null;
        _isActive = false;
        PipClosed?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupPip()
    {
        if (_pipWindow != null)
        {
            _pipWindow.Closed -= OnPipWindowClosed;
            try { _pipWindow.Close(); } catch { /* Window may already be disposed */ }
            _pipWindow = null;
        }

        _syncCoordinator?.Dispose();
        _syncCoordinator = null;

        _pipPlayerService?.Stop();
        _pipPlayerService?.Dispose();
        _pipPlayerService = null;

        _isActive = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupPip();
    }
}
