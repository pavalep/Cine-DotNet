using System;
using Avalonia.Threading;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Synchronizes a secondary (PiP) IMediaPlayer with the primary player.
    /// Mirrors play/pause, file opens, and corrects position drift.
    /// </summary>
    public class PipSyncCoordinator : IDisposable
    {
        private readonly IMediaPlayer _primary;
        private readonly IMediaPlayer _secondary;
        private readonly DispatcherTimer _driftTimer;
        private bool _disposed;
        private bool _isSyncing;  // guard against recursive sync

        private const double DriftThresholdSeconds = 0.5;
        private static readonly TimeSpan DriftCheckInterval = TimeSpan.FromSeconds(1);

        public PipSyncCoordinator(IMediaPlayer primary, IMediaPlayer secondary)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));

            // Mirror playback state (play/pause)
            _primary.PlaybackStateChangedEvent += OnPrimaryPlaybackStateChanged;

            // Mirror file opens
            _primary.Opened += OnPrimaryOpened;

            // Periodic drift check
            _driftTimer = new DispatcherTimer { Interval = DriftCheckInterval };
            _driftTimer.Tick += OnDriftCheck;
            _driftTimer.Start();
        }

        private void OnPrimaryPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
        {
            if (_disposed || _isSyncing) return;

            _isSyncing = true;
            try
            {
                switch (e.State)
                {
                    case PlaybackState.Playing:
                        _secondary.Play();
                        break;
                    case PlaybackState.Paused:
                    case PlaybackState.Stopped:
                        _secondary.Pause();
                        break;
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void OnPrimaryOpened(object? sender, EventArgs e)
        {
            if (_disposed) return;

            var path = _primary.CurrentPath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            _isSyncing = true;
            try
            {
                _secondary.Open(path);
                // Give secondary time to open, then seek to primary's position
                var primaryPos = _primary.Position;
                if (primaryPos.TotalSeconds > 0)
                    _secondary.Seek(primaryPos);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void OnDriftCheck(object? sender, EventArgs? e)
        {
            if (_disposed || _isSyncing) return;

            try
            {
                var primaryPos = _primary.Position.TotalSeconds;
                var secondaryPos = _secondary.Position.TotalSeconds;

                // Skip drift check if either position is invalid
                if (primaryPos < 0 || secondaryPos < 0)
                    return;

                var drift = Math.Abs(primaryPos - secondaryPos);
                if (drift > DriftThresholdSeconds)
                {
                    _isSyncing = true;
                    try
                    {
                        _secondary.Seek(_primary.Position);
                    }
                    finally
                    {
                        _isSyncing = false;
                    }
                }
            }
            catch
            {
                // Ignore sync errors during disposal
            }
        }

        /// <summary>
        /// Manually triggers a seek-sync (e.g. after the user seeks on the primary).
        /// </summary>
        public void SyncNow()
        {
            if (_disposed) return;

            _isSyncing = true;
            try
            {
                var path = _primary.CurrentPath;
                if (!string.IsNullOrWhiteSpace(path) && _secondary.CurrentPath != path)
                    _secondary.Open(path);

                _secondary.Seek(_primary.Position);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _driftTimer.Tick -= OnDriftCheck;
            _driftTimer.Stop();
            (_driftTimer as IDisposable)?.Dispose();

            _primary.PlaybackStateChangedEvent -= OnPrimaryPlaybackStateChanged;
            _primary.Opened -= OnPrimaryOpened;
        }
    }
}
