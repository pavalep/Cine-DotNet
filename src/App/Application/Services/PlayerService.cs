using System;
using System.IO;
using Cine.Core;
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
        private static readonly string DebugLogFile = CreateLogFilePath();

        private static string CreateLogFilePath()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Cine");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "cine_startup.log");
            }
            catch (Exception ex)
            {
                Log.ForContext<PlayerService>().Error(ex, "Log path creation failed");
                return Path.Combine(Path.GetTempPath(), "cine_startup.log");
            }
        }

        private static void DebugLog(string message)
        {
            try
            {
                File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [PlayerService] {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Log.ForContext<PlayerService>().Error(ex, "DebugLog failed");
            }
        }
        #endregion

        public IMediaPlayer? Player => _player;

    public event EventHandler<string>? Error;

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
                Log.ForContext<PlayerService>().Error(ex, "Player creation FAILED");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    if (_player != null)
                    {
                        _player.Stop();
                        (_player as IDisposable)?.Dispose();
                        _player = null;
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"Dispose error: {ex.Message}");
                    Log.ForContext<PlayerService>().Error(ex, "Dispose error");
                }
            }

            _disposed = true;
        }

        ~PlayerService()
        {
            Dispose(false);
        }

        private void OnError(object? sender, string error)
        {
            DebugLog($"[Error] {error}");
            Log.ForContext<PlayerService>().Error(new Exception(error), "Player error");
            Error?.Invoke(this, error);
        }
    }
}
