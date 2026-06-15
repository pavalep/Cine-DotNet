using System;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Synchronizes a secondary (PiP) IMediaPlayer with the primary player.
    /// Mirrors play/pause, file opens. Uses PositionChanged event for drift correction
    /// instead of polling timer — eliminates unnecessary timer ticks and stutter.
    /// </summary>
    public class PipSyncCoordinator : IDisposable
    {
        private readonly IMediaPlayer _primary;
        private readonly IMediaPlayer _secondary;
        private bool _disposed;
        private bool _isSyncing;  // guard against recursive sync

        private const double DriftThresholdSeconds = 0.5;
        private DateTime _lastDriftCheck = DateTime.MinValue;
        private static readonly TimeSpan MinDriftCheckInterval = TimeSpan.FromMilliseconds(500); // throttle

        public PipSyncCoordinator(IMediaPlayer primary, IMediaPlayer secondary)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));

            _primary.PlaybackStateChangedEvent += OnPrimaryPlaybackStateChanged;
            _primary.Opened += OnPrimaryOpened;
            _primary.PositionChanged += OnPrimaryPositionChanged;
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

        private void OnPrimaryPositionChanged(object? sender, PositionChangedEventArgs e)
        {
            if (_disposed || _isSyncing) return;

            // Throttle drift checks — PositionChanged fires frequently (~60/sec)
            var now = DateTime.UtcNow;
            if ((now - _lastDriftCheck) < MinDriftCheckInterval)
                return;
            _lastDriftCheck = now;

            var secondaryPos = _secondary.Position.TotalSeconds;
            if (secondaryPos < 0) return;

            var drift = Math.Abs(e.Position.TotalSeconds - secondaryPos);
            if (drift > DriftThresholdSeconds)
            {
                _isSyncing = true;
                try { _secondary.Seek(e.Position); }
                finally { _isSyncing = false; }
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

            _primary.PlaybackStateChangedEvent -= OnPrimaryPlaybackStateChanged;
            _primary.Opened -= OnPrimaryOpened;
            _primary.PositionChanged -= OnPrimaryPositionChanged;
        }
    }
}
