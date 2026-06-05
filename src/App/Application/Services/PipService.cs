using System;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Views.Dialogs;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Manages PIP (Picture-in-Picture) lifecycle.
/// Creates PipWindow and wires DWM thumbnail mirroring.
/// Exposes player control events from the PipWindow.
/// </summary>
public class PipService : IDisposable
{
    private PipWindow? _pipWindow;
    private bool _isActive;
    private bool _disposed;
    private readonly DwmThumbnailManager _dwmManager;

    public PipService(DwmThumbnailManager dwmManager)
    {
        _dwmManager = dwmManager ?? throw new ArgumentNullException(nameof(dwmManager));
    }

    /// <summary>Whether PIP mode is currently active.</summary>
    public bool IsActive => _isActive;

    /// <summary>The active PipWindow, if any.</summary>
    public PipWindow? PipWindow => _pipWindow;

    /// <summary>Fires when the user clicks play/pause in the PIP window.</summary>
    public event EventHandler? PlayPauseRequested;

    /// <summary>Fires when the user seeks in the PIP window (normalized 0..1).</summary>
    public event EventHandler<double>? SeekRequested;

    public PipWindow? EnterPip()
    {
        if (_disposed) return null;
        if (_isActive) return _pipWindow;

        try
        {
            _pipWindow = new PipWindow();
            _pipWindow.Closed += OnPipWindowClosed;

            // Forward player control events
            _pipWindow.PlayPauseRequested += (s, e) => PlayPauseRequested?.Invoke(s, e);
            _pipWindow.SeekRequested += (s, pos) => SeekRequested?.Invoke(s, pos);

            _pipWindow.Show();

            // Wire DWM thumbnail mirroring
            _pipWindow.EnableDwmMirror(_dwmManager);

            _isActive = true;
            return _pipWindow;
        }
        catch
        {
            CleanupPip();
            return null;
        }
    }

    public void ExitPip()
    {
        if (!_isActive || _pipWindow == null) return;
        _pipWindow.Close();
        _isActive = false;
    }

    private void OnPipWindowClosed(object? sender, EventArgs e)
    {
        _isActive = false;
        _pipWindow = null;
    }

    private void CleanupPip()
    {
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
