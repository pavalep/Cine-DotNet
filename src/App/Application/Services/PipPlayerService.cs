using System;
using System.IO;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Manages the secondary MpvPlayer instance used for PiP (Picture-in-Picture).
    /// Uses the mpv OpenGL render API via ANGLE — same as the main window.
    /// Frames are delivered via FrameRendered for display in the PipWindow.
    /// </summary>
    public class PipPlayerService : IDisposable
    {
        private MpvPlayer? _player;
        private bool _disposed;

        /// <summary>The secondary PiP player instance, if initialized.</summary>
        public IMediaPlayer? Player => _player;

        /// <summary>Fired when a new video frame is available (BGRA byte array).</summary>
        public event Action<byte[], int, int>? FrameRendered;

        /// <summary>Fired when an error occurs on the secondary player.</summary>
        public event EventHandler<string>? Error;

        /// <summary>
        /// Creates the secondary mpv player using the OpenGL render API.
        /// No child HWND needed — frames are read back via ANGLE and delivered
        /// through the <see cref="FrameRendered"/> event.
        /// </summary>
        public bool Initialize()
        {
            if (_disposed)
            {
                Error?.Invoke(this, "PipPlayerService is disposed");
                return false;
            }

            if (_player != null)
                return true;

            try
            {
                var player = new MpvPlayer();
                player.Error += OnSecondaryError;
                player.Mute(true);
                _player = player;

                PipLog("Initialize success (FBO render path)");
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, $"Failed to create secondary player: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        private void OnFrameRendered(byte[] pixels, int width, int height)
        {
            FrameRendered?.Invoke(pixels, width, height);
        }

        /// <summary>
        /// Opens a file in the secondary player (must be called after <see cref="Initialize"/>).
        /// </summary>
        public void Open(string path)
        {
            _player?.Open(path);
        }

        /// <summary>
        /// Seeks the secondary player to the specified position.
        /// </summary>
        public void Seek(TimeSpan position)
        {
            _player?.Seek(position);
        }

        /// <summary>
        /// Sets the secondary player's mute state (should always be muted for PiP).
        /// </summary>
        public void SetMuted(bool muted)
        {
            _player?.Mute(muted);
        }

        /// <summary>
        /// Stops and disposes the secondary player. Safe to call multiple times.
        /// </summary>
        public void Stop()
        {
            Cleanup();
        }

        private void OnSecondaryError(object? sender, string message)
        {
            Error?.Invoke(this, message);
        }

        // ── Cleanup ──

        private void Cleanup()
        {
            if (_player != null)
            {
                _player.Error -= OnSecondaryError;
                _player.Dispose();
                _player = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Cleanup();
        }

        // ── Logging ──

        private static void PipLog(string msg)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Cine", "cine_pip_video.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
