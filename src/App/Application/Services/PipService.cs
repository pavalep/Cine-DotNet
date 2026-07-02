using System;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Dialogs;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Manages PiP (Picture-in-Picture) lifecycle.
/// Shares video frames from the main MpvVideoView — no second mpv instance needed.
/// Uses <see cref="IPipWindow"/> for the PiP window, enabling testability.
/// </summary>
public class PipService : IPipService
{
    private readonly MpvVideoView _videoView;
    private IPipWindow? _pipWindow;
    private bool _isActive;
    private bool _disposed;

    public PipService(MpvVideoView? videoView)
    {
        _videoView = videoView ?? null!;
    }

    public bool IsActive => _isActive;
    public IPipWindow? PipWindow => _pipWindow;

    /// <summary>Fires when the user clicks play/pause in the PiP window.</summary>
    public event EventHandler? PlayPauseRequested;
    /// <summary>Fires when the user seeks in the PiP window (normalized 0..1).</summary>
    public event EventHandler<double>? SeekRequested;
    /// <summary>Fires when the user toggles mute in the PiP window.</summary>
    public event EventHandler? MuteToggled;
    /// <summary>Fires when the PiP window is closed.</summary>
    public event EventHandler? PipClosed;

    /// <summary>
    /// Create or toggle PiP window. Overload accepts an IPipWindow for testing.
    /// </summary>
    public IPipWindow? EnterPip(IPipWindow? testWindow = null)
    {
        if (_disposed) return null;

        if (_isActive)
        {
            if (_pipWindow == null || _pipWindow.IsClosed)
            {
                _isActive = false;
                _pipWindow = null;
            }
            else
            {
                return _pipWindow;
            }
        }

        try
        {
            _pipWindow = testWindow ?? new PipWindow();
            _pipWindow.Closed += OnPipWindowClosed;

            _pipWindow.PlayPauseRequested += (_, _) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
            _pipWindow.SeekRequested += (_, pos) => SeekRequested?.Invoke(this, pos);
            _pipWindow.MuteToggled += (_, _) => MuteToggled?.Invoke(this, EventArgs.Empty);

            _pipWindow.Show();

            if (_videoView != null)
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

        if (_videoView != null)
            _videoView.FrameRendered -= OnFrameRendered;

        if (_pipWindow != null)
        {
            _pipWindow.Closed -= OnPipWindowClosed;
            try { _pipWindow.Close(); }
            catch (Exception ex) { Log.ForContext<PipService>().Error(ex, "Failed to close PiP window"); }
            _pipWindow = null;
        }
        _isActive = false;
    }

    private void OnFrameRendered(byte[] pixels, int width, int height)
    {
        _pipWindow?.UpdateFrame(pixels, width, height);
    }

    private void OnPipWindowClosed(object? sender, EventArgs e)
    {
        if (_videoView != null)
            _videoView.FrameRendered -= OnFrameRendered;
        _pipWindow = null;
        _isActive = false;
        PipClosed?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupPip()
    {
        if (_videoView != null)
            _videoView.FrameRendered -= OnFrameRendered;

        if (_pipWindow != null)
        {
            _pipWindow.Closed -= OnPipWindowClosed;
            try { _pipWindow.Close(); }
            catch (Exception ex) { Log.ForContext<PipService>().Error(ex, "Failed to close PiP window"); }
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
