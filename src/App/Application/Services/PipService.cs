using System;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Manages PIP (Picture-in-Picture) lifecycle by screenshot-polling the main player.
/// No secondary decoder — pulls frames from the existing player at ~30fps.
/// </summary>
public class PipService : IDisposable
{
    private readonly PlayerService _playerService;
    private IMediaPlayer? _mainPlayer;
    private string? _currentFilePath;
    private PipWindow? _pipWindow;
    private bool _isActive;
    private bool _disposed;

    public PipService(PlayerService playerService)
    {
        _playerService = playerService;
    }

    /// <summary>Whether PIP mode is currently active.</summary>
    public bool IsActive => _isActive;

    /// <summary>Whether PIP is feasible (main player + file loaded).</summary>
    public bool CanPip => _mainPlayer != null && !string.IsNullOrEmpty(_currentFilePath);

    /// <summary>Fired when PIP window opens.</summary>
    public event EventHandler? PipOpened;

    /// <summary>Fired when PIP encounters an error.</summary>
    public event EventHandler<string>? PipError;

    /// <summary>Fired when PIP window closes.</summary>
    public event EventHandler? PipClosed;

    public void Initialize(IMediaPlayer mainPlayer)
    {
        _mainPlayer = mainPlayer;
    }

    public void SetCurrentFilePath(string filePath)
    {
        _currentFilePath = filePath;
    }

    public PipWindow? EnterPip()
    {
        if (_disposed) return null;
        if (_isActive) return _pipWindow;
        if (!CanPip) return null;

        try
        {
            _pipWindow = new PipWindow(_mainPlayer!);
            _pipWindow.Closed += OnPipWindowClosed;
            _pipWindow.Show();
            _isActive = true;

            PipOpened?.Invoke(this, EventArgs.Empty);
            return _pipWindow;
        }
        catch (Exception ex)
        {
            PipError?.Invoke(this, $"PIP failed: {ex.Message}");
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
        PipClosed?.Invoke(this, EventArgs.Empty);
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
