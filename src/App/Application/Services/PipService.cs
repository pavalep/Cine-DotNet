using System;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Views.Dialogs;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Manages PiP (Picture-in-Picture) lifecycle.
/// Shares video frames from the main MpvVideoView — no second mpv instance needed.
/// </summary>
public class PipService : IDisposable
{
    private readonly MpvVideoView _videoView;
    private PipWindow? _pipWindow;
    private bool _isActive;
    private bool _disposed;

    public PipService(MpvVideoView videoView)
    {
        _videoView = videoView ?? throw new ArgumentNullException(nameof(videoView));
    }

    public bool IsActive => _isActive;
    public PipWindow? PipWindow => _pipWindow;

    /// <summary>Fires when the user clicks play/pause in the PiP window.</summary>
    public event EventHandler? PlayPauseRequested;
    /// <summary>Fires when the user seeks in the PiP window (normalized 0..1).</summary>
    public event EventHandler<double>? SeekRequested;
    /// <summary>Fires when the user toggles mute in the PiP window.</summary>
    public event EventHandler? MuteToggled;
    /// <summary>Fires when the PiP window is closed.</summary>
    public event EventHandler? PipClosed;

    public PipWindow? EnterPip()
    {
        if (_disposed) return null;

        if (_isActive)
        {
            if (_pipWindow == null || _pipWindow.IsClosed)
            {
                // Stale state — reset
                _isActive = false;
                _pipWindow = null;
            }
            else
            {
                return _pipWindow; // Already active
            }
        }

        try
        {
            _pipWindow = new PipWindow();
            _pipWindow.Closed += OnPipWindowClosed;

            // Forward PiP window control events
            _pipWindow.PlayPauseRequested += (_, _) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
            _pipWindow.SeekRequested += (_, pos) => SeekRequested?.Invoke(this, pos);
            _pipWindow.MuteToggled += (_, _) => MuteToggled?.Invoke(this, EventArgs.Empty);

            _pipWindow.Show();

            // Subscribe to main window's video frames — no second player needed
            _videoView.FrameRendered += OnFrameRendered;

            _pipWindow.ShowAllControls();
            _pipWindow.StartHoverTimer();

            _isActive = true;
            return _pipWindow;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PipService] EnterPip failed: {ex}");
            CleanupPip();
            return null;
        }
    }

    public void ExitPip()
    {
        if (!_isActive) return;

        _videoView.FrameRendered -= OnFrameRendered;

        if (_pipWindow != null)
        {
            _pipWindow.Closed -= OnPipWindowClosed;
            try { _pipWindow.Close(); } catch { }
            _pipWindow = null;
        }
        _isActive = false;
    }

    private void OnFrameRendered(byte[] pixels, int width, int height)
    {
        // Forward frame to PiP window on UI thread
        _pipWindow?.UpdateFrame(pixels, width, height);
    }

    private void OnPipWindowClosed(object? sender, EventArgs e)
    {
        _videoView.FrameRendered -= OnFrameRendered;
        _pipWindow = null;
        _isActive = false;
        PipClosed?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupPip()
    {
        _videoView.FrameRendered -= OnFrameRendered;

        if (_pipWindow != null)
        {
            _pipWindow.Closed -= OnPipWindowClosed;
            try { _pipWindow.Close(); } catch { }
            _pipWindow = null;
        }
        _isActive = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupPip();
    }
}
