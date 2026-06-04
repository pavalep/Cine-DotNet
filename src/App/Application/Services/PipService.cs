using System;
using System.IO;
using Avalonia.Threading;
using Cine.Avalonia.Views.Dialogs;
using Cine.Avalonia.Helpers;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Dedicated service managing Picture-in-Picture lifecycle.
/// Owns the second mpv instance, PipWindow, and sync coordination.
/// Replaces ad-hoc PIP logic previously spread across MainWindow.Pip.cs + PipWindow.
/// </summary>
public class PipService : IDisposable
{
    private readonly PlayerService _playerService;
    private IMediaPlayer? _mainPlayer;
    private IMediaPlayer? _pipPlayer;
    private PipWindow? _pipWindow;
    private bool _isActive;
    private bool _disposed;
    private string? _currentFilePath;
    private bool _mainWasMutedBeforePip;

    /// <summary>Raised when PIP mode is activated (window shown).</summary>
    public event EventHandler? PipOpened;

    /// <summary>Raised when PIP mode is deactivated (window closed).</summary>
    public event EventHandler? PipClosed;

    /// <summary>Raised when an error occurs in PIP mode.</summary>
    public event EventHandler<string>? PipError;

    /// <summary>Whether PIP mode is currently active.</summary>
    public bool IsActive => _isActive;

    /// <summary>Whether PIP is feasible (main player + file loaded).</summary>
    public bool CanPip => _mainPlayer != null && !string.IsNullOrEmpty(_currentFilePath);

    /// <summary>The current PIP window (null if not active).</summary>
    public PipWindow? Window => _pipWindow;

    /// <summary>The secondary player instance (null if not active).</summary>
    public IMediaPlayer? PipPlayer => _pipPlayer;

    public PipService(PlayerService playerService)
    {
        _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
    }

    /// <summary>
    /// Initialize the service with the main player reference.
    /// Call once on app startup.
    /// </summary>
    public void Initialize(IMediaPlayer mainPlayer)
    {
        _mainPlayer = mainPlayer ?? throw new ArgumentNullException(nameof(mainPlayer));

        // Subscribe to main player events
        _mainPlayer.Opened += OnMainPlayerOpened;
        _mainPlayer.PositionChanged += OnMainPlayerPositionChanged;
    }

    /// <summary>
    /// Enter PIP mode — creates secondary player + PipWindow.
    /// </summary>
    public PipWindow? EnterPip()
    {
        if (_disposed) return null;
        if (_isActive) return _pipWindow;
        if (!CanPip) return null;

        try
        {
            _pipPlayer = _playerService.CreateSecondaryPlayer();
            _pipPlayer!.Opened += OnPipPlayerOpened;
            _pipPlayer.Error += OnPipPlayerError;

            _pipWindow = new PipWindow(_pipPlayer, _mainPlayer!, _currentFilePath!, this);

            // P2.4: Save main mute state — mute + pause to prevent dual audio overlap
            _mainWasMutedBeforePip = _mainPlayer?.IsMuted ?? false;
            _mainPlayer?.Mute(true);
            _mainPlayer?.Pause();

            _pipWindow.Closed += OnPipWindowClosed;

            _pipWindow.Show();
            _isActive = true;

            PipOpened?.Invoke(this, EventArgs.Empty);
            return _pipWindow;
        }
        catch (Exception ex)
        {
            CleanupPip();
            PipError?.Invoke(this, $"PIP failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Exit PIP mode — closes window and cleans up.
    /// </summary>
    public void ExitPip()
    {
        if (!_isActive) return;

        if (_pipWindow != null && _pipWindow.IsVisible)
        {
            _pipWindow.Close();
        }
        else
        {
            CleanupPip();
        }
    }

    /// <summary>
    /// Force-sync PIP position to main player position.
    /// </summary>
    public void SyncPosition()
    {
        try
        {
            if (!_isActive || _pipPlayer == null || _mainPlayer == null) return;
            var mainPos = _mainPlayer.Position;
            var pipPos = _pipPlayer.Position;
            if (Math.Abs((mainPos - pipPos).TotalSeconds) > 0.3)
                _pipPlayer.Seek(mainPos);
        }
        catch { /* swallow — sync is best-effort */ }
    }

    /// <summary>
    /// Called by PipWindow when its async initialization fails.
    /// P2.1: Ensures clean state even when PIP init fails mid-flight.
    /// </summary>
    public void NotifyInitFailed()
    {
        CleanupPip();
        PipError?.Invoke(this, "PIP initialization failed");
    }

    /// <summary>
    /// Called when the main player opens a new file while PIP is active.
    /// P2.2: Re-opens the new file in the PIP player so PIP stays in sync.
    /// </summary>
    public void NotifyFileChanged(string? newFilePath)
    {
        _currentFilePath = newFilePath;
        if (!_isActive || _pipPlayer == null || string.IsNullOrEmpty(newFilePath)) return;

        try
        {
            _pipPlayer.Open(newFilePath);
            if (_mainPlayer != null)
            {
                var pos = _mainPlayer.Position;
                if (pos.TotalSeconds > 0)
                    _pipPlayer.Seek(pos);
            }
        }
        catch (Exception ex)
        {
            PipError?.Invoke(this, $"PIP file change failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Set the current file path (for CanPip check).
    /// </summary>
    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ExitPip();

        if (_mainPlayer != null)
        {
            _mainPlayer.Opened -= OnMainPlayerOpened;
            _mainPlayer.PositionChanged -= OnMainPlayerPositionChanged;
        }
    }

    private void OnMainPlayerOpened(object? sender, EventArgs e)
    {
        // P2.2: When main player opens a new file while PIP is active, re-open in PIP
        if (_mainPlayer != null)
        {
            _currentFilePath = _mainPlayer.CurrentPath;
            NotifyFileChanged(_currentFilePath);
        }
    }

    private void OnMainPlayerPositionChanged(object? sender, EventArgs e)
    {
        // Event-driven position sync - throttled by PipWindow
    }

    private void OnPipPlayerOpened(object? sender, EventArgs e)
    {
        // PIP player loaded the file successfully
    }

    private void OnPipPlayerError(object? sender, string error)
    {
        PipError?.Invoke(this, $"PIP player error: {error}");
    }

    private void OnPipWindowClosed(object? sender, EventArgs e)
    {
        CleanupPip();
        PipClosed?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupPip()
    {
        try
        {
            if (_pipWindow != null)
            {
                _pipWindow.Closed -= OnPipWindowClosed;
                _pipWindow = null;
            }

            if (_pipPlayer != null)
            {
                _pipPlayer.Opened -= OnPipPlayerOpened;
                _pipPlayer.Error -= OnPipPlayerError;
                try { _pipPlayer.Stop(); } catch { }
                (_pipPlayer as IDisposable)?.Dispose();
                _pipPlayer = null;
            }
        }
        catch { /* cleanup must never throw */ }
        finally
        {
            _isActive = false;

            // P2.4: Restore main player mute state to what it was before PIP
            try
            {
                if (_mainPlayer != null && !_mainWasMutedBeforePip)
                    _mainPlayer.Mute(false);
            }
            catch { }
        }
    }
}
