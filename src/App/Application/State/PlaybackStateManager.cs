using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.State;

/// <summary>
/// Centralized single source of truth for all playback state.
///
/// Subscribes to IMediaPlayer events and exposes unified properties + events.
/// All UI consumers (MainWindow, ControlsBoxControl, PipWindow, MainViewModel)
/// should read state from and subscribe to this manager — not from the player
/// directly or from each other's property notifications.
///
/// This eliminates the play/pause icon desync caused by 6+ scattered code paths
/// independently setting icon state in unpredictable order.
/// </summary>
public sealed class PlaybackStateManager : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private bool _disposed;

    // ── Backing fields ──
    private PlaybackState _state = PlaybackState.Stopped;
    private TimeSpan _position;
    private TimeSpan _duration;
    private double _normalizedPosition;
    private double _volume = 50;
    private bool _isMuted;
    private double _speed = 1.0;
    private bool _isReplayMode;
    private bool _isMediaLoaded;
    private string _filePath = string.Empty;

    public PlaybackStateManager(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));

        // Subscribe to player events — this is the ONLY place in the app where
        // player events are wired to state management. All other code reads from
        // this manager's properties or subscribes to its events.
        _player.Opened += OnPlayerOpened;
        _player.PlaybackStateChangedEvent += OnPlayerPlaybackStateChanged;
        _player.PositionChanged += OnPlayerPositionChanged;
        _player.VolumeChanged += OnPlayerVolumeChanged;
        _player.TrackListChanged += OnPlayerTrackListChanged;
        _player.ChapterListChanged += OnPlayerChapterListChanged;
        _player.LoopChangedEvent += OnPlayerLoopChanged;
        _player.PlaylistChanged += OnPlayerPlaylistChanged;
        _player.Error += OnPlayerError;

        // Read initial state
        Refresh();
    }

    // ── Observable Properties ──

    /// <summary>Current playback state: Playing, Paused, or Stopped.</summary>
    public PlaybackState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsStopped));
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(value));
        }
    }

    public bool IsPlaying => _state == PlaybackState.Playing;
    public bool IsPaused => _state == PlaybackState.Paused;
    public bool IsStopped => _state == PlaybackState.Stopped;

    /// <summary>Current playback position as TimeSpan.</summary>
    public TimeSpan Position
    {
        get => _position;
        private set
        {
            if (_position == value) return;
            _position = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Total media duration as TimeSpan.</summary>
    public TimeSpan Duration
    {
        get => _duration;
        private set
        {
            if (_duration == value) return;
            _duration = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DurationText));
        }
    }

    /// <summary>Position as 0.0–1.0 fraction of duration.</summary>
    public double NormalizedPosition
    {
        get => _normalizedPosition;
        private set
        {
            if (Math.Abs(_normalizedPosition - value) < 0.0001) return;
            _normalizedPosition = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Current volume level (0 – VolumeMax).</summary>
    public double Volume
    {
        get => _volume;
        private set
        {
            if (Math.Abs(_volume - value) < 0.001) return;
            _volume = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeText));
        }
    }

    public string VolumeText => $"{_volume:F0}%";

    /// <summary>Whether audio is muted.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Current playback speed multiplier.</summary>
    public double Speed
    {
        get => _speed;
        private set
        {
            if (Math.Abs(_speed - value) < 0.001) return;
            _speed = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True when playback has reached end-of-file (replay mode).</summary>
    public bool IsReplayMode
    {
        get => _isReplayMode;
        private set
        {
            if (_isReplayMode == value) return;
            _isReplayMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True when a media file is loaded and playback has started or is ready.</summary>
    public bool IsMediaLoaded
    {
        get => _isMediaLoaded;
        private set
        {
            if (_isMediaLoaded == value) return;
            _isMediaLoaded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Path of the currently loaded media file.</summary>
    public string FilePath
    {
        get => _filePath;
        private set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public string DurationText
    {
        get
        {
            if (_duration <= TimeSpan.Zero) return "00:00:00";
            return _duration.Ticks < 0
                ? "-" + TimeSpan.FromTicks(-_duration.Ticks).ToString(@"hh\:mm\:ss")
                : _duration.ToString(@"hh\:mm\:ss");
        }
    }

    public double VolumeMax => _player.VolumeMax;

    // ── Events ──

    /// <summary>Fires on every play/pause/stop transition. Consumer must marshal to UI thread.</summary>
    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    /// <summary>Fires periodically during playback. Consumer must marshal to UI thread.</summary>
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <summary>Fires when volume or mute changes. Consumer must marshal to UI thread.</summary>
    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;

    /// <summary>Fires when audio/video/subtitle track list changes.</summary>
    public event EventHandler<TrackListChangedEventArgs>? TrackListChanged;

    /// <summary>Fires when chapter list changes.</summary>
    public event EventHandler<ChapterListChangedEventArgs>? ChapterListChanged;

    /// <summary>Fires when loop mode changes.</summary>
    public event EventHandler<LoopChangedEventArgs>? LoopChanged;

    /// <summary>Fires when playlist changes.</summary>
    public event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;

    /// <summary>Fires when a new media file opens and playback begins.</summary>
    public event EventHandler? MediaOpened;

    /// <summary>Fires when playback reaches end-of-file.</summary>
    public event EventHandler? MediaEnded;

    /// <summary>Fires on player errors.</summary>
    public event EventHandler<string>? Error;

    // ── Public API ──

    /// <summary>
    /// Refresh all state by querying the player directly.
    /// Call this after the player is fully initialized or after seeking.
    /// </summary>
    public void Refresh()
    {
        try
        {
            State = _player.State;
            _position = _player.Position;
            _duration = _player.Duration;
            _normalizedPosition = _duration.TotalSeconds > 0
                ? _position.TotalSeconds / _duration.TotalSeconds
                : 0;
            _volume = Math.Clamp(_player.Volume, 0, VolumeMax);
            _isMuted = _player.IsMuted;
            _speed = _player.Speed;
            _filePath = _player.CurrentPath;
            _isMediaLoaded = !string.IsNullOrEmpty(_filePath);

            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(NormalizedPosition));
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(IsMediaLoaded));
        }
        catch
        {
            // Player may not be fully initialized yet
        }
    }

    /// <summary>
    /// Reset replay mode — call when user clicks play after end-of-file.
    /// </summary>
    public void ClearReplayMode()
    {
        IsReplayMode = false;
    }

    /// <summary>
    /// Set replay mode — call when playback reaches end-of-file.
    /// </summary>
    public void SetEnded()
    {
        IsReplayMode = true;
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    // ── Player Event Handlers ──

    private void OnPlayerOpened(object? sender, EventArgs e)
    {
        State = PlaybackState.Playing;
        IsMediaLoaded = true;
        IsReplayMode = false;
        MediaOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlayerPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        if (e.State == PlaybackState.Stopped)
        {
            // Only enter replay mode if media was loaded (not initial stop state)
            if (IsMediaLoaded)
            {
                IsReplayMode = true;
                State = PlaybackState.Stopped;
                MediaEnded?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        // Clear replay mode on any playing/paused transition
        if (e.State == PlaybackState.Playing || e.State == PlaybackState.Paused)
        {
            IsReplayMode = false;
        }

        State = e.State;
    }

    private void OnPlayerPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Position = e.Position;
        Duration = e.Duration;
        NormalizedPosition = e.NormalizedPosition;
        PositionChanged?.Invoke(this, e);
    }

    private void OnPlayerVolumeChanged(object? sender, VolumeChangedEventArgs e)
    {
        Volume = e.Volume;
        IsMuted = e.IsMuted;
        VolumeChanged?.Invoke(this, e);
    }

    private void OnPlayerTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        TrackListChanged?.Invoke(this, e);
    }

    private void OnPlayerChapterListChanged(object? sender, ChapterListChangedEventArgs e)
    {
        ChapterListChanged?.Invoke(this, e);
    }

    private void OnPlayerLoopChanged(object? sender, LoopChangedEventArgs e)
    {
        LoopChanged?.Invoke(this, e);
    }

    private void OnPlayerPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        PlaylistChanged?.Invoke(this, e);
    }

    private void OnPlayerError(object? sender, string error)
    {
        Error?.Invoke(this, error);
    }

    // ── INotifyPropertyChanged ──

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ── Cleanup ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _player.Opened -= OnPlayerOpened;
        _player.PlaybackStateChangedEvent -= OnPlayerPlaybackStateChanged;
        _player.PositionChanged -= OnPlayerPositionChanged;
        _player.VolumeChanged -= OnPlayerVolumeChanged;
        _player.TrackListChanged -= OnPlayerTrackListChanged;
        _player.ChapterListChanged -= OnPlayerChapterListChanged;
        _player.LoopChangedEvent -= OnPlayerLoopChanged;
        _player.PlaylistChanged -= OnPlayerPlaylistChanged;
        _player.Error -= OnPlayerError;
    }
}
