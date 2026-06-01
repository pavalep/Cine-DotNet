using System;
using System.IO;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Service that wraps the active player backend for use by Avalonia ViewModels.
    /// Provides lifecycle management and exposes platform-agnostic playback functionality.
    /// </summary>
    public class PlayerService : IDisposable
    {
        private IMediaPlayer? _player;
        private bool _disposed;
        #region debug-point player-service-log
        private static readonly string DebugLogFile = Path.Combine(
            AppContext.BaseDirectory,
            "cine_startup.log");

        private static void DebugLog(string message)
        {
            try
            {
                File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [PlayerService] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
        #endregion

        public IMediaPlayer? Player => _player;

    public IMediaPlayer CreateSecondaryPlayer()
    {
        var secondary = new MpvPlayer();
        return secondary;
    }

        public PlayerService()
        {
        }

        public void Initialize()
        {
            try
            {
                #region debug-point player-service-init-start
                DebugLog("Initialize start");
                #endregion
                _player = new MpvPlayer();
                #region debug-point player-service-player-created
                DebugLog($"{_player.GetType().Name} created");
                #endregion
                _player.Error += OnError;
                #region debug-point player-service-init-finish
                DebugLog("Initialize finish");
                #endregion
            }
            catch (Exception ex)
            {
                #region debug-point player-service-init-fail
                DebugLog($"Initialize failed: {ex}");
                #endregion
                System.Diagnostics.Debug.WriteLine($"[PlayerService] Player creation FAILED: {ex}");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_player != null)
            {
                _player.Stop();
                if (_player is IDisposable disposable)
                    disposable.Dispose();
                _player = null;
            }
        }

        private void OnError(object? sender, string error)
        {
            DebugLog($"[Error] {error}");
            System.Diagnostics.Debug.WriteLine($"[PlayerService Error] {error}");
            System.Diagnostics.Trace.WriteLine($"[PlayerService Error] {error}");
        }
    }
}
