using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Cine.Avalonia.Models;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Centralized manager for all audio-related state: Volume, Mute, Equalizer,
/// Audio tracks, Dialogue Boost, Audio Normalization, and Audio Delay.
///
/// Subscribes to IMediaPlayer events once and exposes unified properties + events.
/// All UI code reads from / subscribes to this manager.
/// </summary>
public sealed class AudioManager : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private bool _disposed;

    // ── Volume / Mute ──
    private double _volume = 50;
    private bool _isMuted;

    // ── Equalizer ──
    public static readonly double[] EqualizerFrequencies = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
    private double[] _equalizerBands = new double[10];
    private string _equalizerPresetName = "Flat";
    private bool _isAudioNormalizationEnabled;
    private bool _isDialogueBoostEnabled;

    // ── Audio Delay ──
    private float _audioDelay;

    // ── Audio Tracks ──
    private int _currentAudioTrackId = -1;
    private int? _pendingAudioTrackId;

    // ── File dialog callback for "Add Audio Track…" ──
    public Func<Task<string?>>? RequestAudioFileAsync { get; set; }

    // ── Persistence ──
    private readonly AudioSettingsStore _audioStore = new();
    private string? _currentMediaPath;
    private System.Threading.Timer? _debounceTimer;
    private bool _dirty;

    public AudioManager(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));

        // Sync initial state from player
        _volume = Math.Clamp(_player.Volume, 0, VolumeMax);
        _isMuted = _player.IsMuted;
        _audioDelay = _player.AudioDelay;

        // Subscribe to player events
        _player.VolumeChanged += OnPlayerVolumeChanged;
        _player.TrackListChanged += OnPlayerTrackListChanged;

        BuildEmptyTrackMenus();
    }

    // ── Observable Properties ──

    #region Volume / Mute

    public double Volume
    {
        get => _volume;
        set => VolumeValue = value;
    }

    public double VolumeMax => _player.VolumeMax;

    public string VolumeText => $"{_volume:F0}%";

    public double VolumeValue
    {
        get => _volume;
        set
        {
            var clamped = Math.Clamp(value, 0, VolumeMax);
            if (Math.Abs(_volume - clamped) < 0.001) return;
            _volume = clamped;
            _player.Volume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumeText));
            VolumeChanged?.Invoke(this, EventArgs.Empty);
            MarkDirty();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            _player.Mute(value);
            OnPropertyChanged();
            VolumeChanged?.Invoke(this, EventArgs.Empty);
            MarkDirty();
        }
    }

    public void IncreaseVolume() => VolumeValue = Math.Min(VolumeMax, VolumeValue + 5);
    public void DecreaseVolume() => VolumeValue = Math.Max(0, VolumeValue - 5);
    public void ToggleMute() => IsMuted = !IsMuted;

    #endregion

    #region Equalizer

    public double[] EqualizerBands
    {
        get => _equalizerBands;
        set { _equalizerBands = value; OnPropertyChanged(); }
    }

    public string EqualizerPresetName
    {
        get => _equalizerPresetName;
        set { _equalizerPresetName = value; OnPropertyChanged(); }
    }

    public bool IsAudioNormalizationEnabled
    {
        get => _isAudioNormalizationEnabled;
        set
        {
            if (_isAudioNormalizationEnabled == value) return;
            _isAudioNormalizationEnabled = value;
            OnPropertyChanged();
            ApplyEqualizer();
            MarkDirty();
        }
    }

    public bool IsDialogueBoostEnabled
    {
        get => _isDialogueBoostEnabled;
        set
        {
            if (_isDialogueBoostEnabled == value) return;
            _isDialogueBoostEnabled = value;
            ApplyEqualizer();
            MarkDirty();
            OnPropertyChanged();
        }
    }

    public void SetEqualizerBand(int bandIndex, double gain)
    {
        if (bandIndex < 0 || bandIndex >= 10) return;
        _equalizerBands[bandIndex] = Math.Clamp(gain, -20, 20);
        ApplyEqualizer();
        MarkDirty();
    }

    public void ApplyEqualizerPreset(string presetName)
    {
        var preset = GetPreset(presetName);
        for (int i = 0; i < 10 && i < preset.Length; i++)
            _equalizerBands[i] = preset[i];
        EqualizerPresetName = presetName;
        OnPropertyChanged(nameof(EqualizerBands));
        ApplyEqualizer();
        MarkDirty();
    }

    public void ToggleAudioNormalization()
    {
        IsAudioNormalizationEnabled = !IsAudioNormalizationEnabled;
    }

    private void ApplyEqualizer()
    {
        try
        {
            var filters = new List<string>();

            for (int i = 0; i < 10; i++)
            {
                if (Math.Abs(_equalizerBands[i]) > 0.5)
                    filters.Add($"equalizer=f={EqualizerFrequencies[i]}:t=q:w=1:g={_equalizerBands[i]:F1}");
            }

            if (_isAudioNormalizationEnabled)
                filters.Add("lavfi=[acompressor=threshold=-20dB:ratio=4:makeup=8dB]");

            if (_isDialogueBoostEnabled)
                filters.Add("lavfi=[dialoguenhancer]");

            if (filters.Count > 0)
                _player.Command("set", "af", string.Join(",", filters));
        }
        catch { /* player not ready */ }
    }

    private static double[] GetPreset(string name) => name switch
    {
        "Classical" => new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -4.0, -4.0, -4.0, -6.0 },
        "Rock" => new[] { 4.0, 3.0, 2.0, 1.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 },
        "Pop" => new[] { -1.0, 0.0, 2.0, 3.0, 4.0, 3.0, 2.0, 0.0, -1.0, -1.0 },
        "Jazz" => new[] { 3.0, 2.0, 1.0, 2.0, 3.0, 3.0, 2.0, 1.0, 1.0, 2.0 },
        "Bass Boost" => new[] { 6.0, 5.0, 4.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
        _ => new double[10] // Flat
    };

    #endregion

    #region Audio Delay

    public float AudioDelay
    {
        get => _audioDelay;
        set
        {
            _audioDelay = value;
            _player.AudioDelay = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public void ResetAudioDelay() => AudioDelay = 0;

    #endregion

    #region Audio Tracks

    public ObservableCollection<TrackMenuItem> AudioTracks { get; } = new();

    /// <summary>True if the current media has at least one audio track.</summary>
    public bool IsAudioEnabled => AudioTracks.Any(t => t.IsSelected && !t.IsPseudoEntry);

    public void RestorePendingTrack()
    {
        if (!_pendingAudioTrackId.HasValue) return;
        var track = AudioTracks.FirstOrDefault(t =>
            t.TrackIndex == _pendingAudioTrackId.Value && !t.IsPseudoEntry);
        if (track?.SelectCommand.CanExecute(track) == true)
            track.SelectCommand.Execute(track);
        _pendingAudioTrackId = null;
    }

    public void SetPendingTrackId(int trackId)
    {
        _pendingAudioTrackId = trackId;
    }

    private void BuildEmptyTrackMenus()
    {
        AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
        AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));
    }

    private void OnSelectAudio(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Audio Track…")
        {
            _ = OnAddAudioAsync();
            return;
        }

        if (item.DisplayName == "None")
        {
            _player.SelectAudioTrack(-1);
            _currentAudioTrackId = -1;
            foreach (var t in AudioTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectAudioTrack(item.TrackIndex);
            _currentAudioTrackId = item.TrackIndex;
            foreach (var t in AudioTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            MarkDirty();
        }
    }

    private async Task OnAddAudioAsync()
    {
        if (RequestAudioFileAsync == null) return;
        try
        {
            var path = await RequestAudioFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
                _player.AddAudio(path);
        }
        catch { /* user cancelled or error */ }
    }

    /// <summary>
    /// Refresh audio tracks from a TrackListChanged event.
    /// Called by the owner (MainWindow or MainViewModel) when track list changes.
    /// </summary>
    public void RefreshAudioTracks(IEnumerable<SubtitleSource> audioSources)
    {
        AudioTracks.Clear();
        AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
        AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));

        if (audioSources != null)
        {
            int idx = 0;
            foreach (var track in audioSources)
            {
                var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                var item = new TrackMenuItem(
                    FormatTrack("Audio", track),
                    TrackType.Audio,
                    trackId,
                    OnSelectAudio,
                    track
                );
                item.IsSelected = track.IsEnabled;
                AudioTracks.Add(item);
                idx++;
            }
        }

        OnPropertyChanged(nameof(IsAudioEnabled));
    }

    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        var state = track.IsEnabled ? "on" : "off";
        return $"{prefix}: {lang} ({state})";
    }

    #endregion

    // ═══════════════════════════════════════════════
    //  Persistence Lifecycle
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Called by MainViewModel when a media file opens.
    /// Loads per-file audio settings + global defaults.
    /// </summary>
    public void NotifyMediaOpened(string mediaPath)
    {
        _currentMediaPath = mediaPath;

        // Load global defaults
        var defaults = _audioStore.LoadDefaults();
        VolumeValue = Math.Clamp(defaults.Volume, 0, VolumeMax);
        IsMuted = defaults.IsMuted;
        ApplyEqualizerPreset(defaults.EqualizerPreset);
        IsAudioNormalizationEnabled = defaults.IsNormalizationEnabled;
        IsDialogueBoostEnabled = defaults.IsDialogueBoostEnabled;

        // Load per-file settings
        var perFile = _audioStore.LoadPerFile(mediaPath);
        if (perFile != null)
        {
            AudioDelay = Math.Clamp(perFile.AudioDelay, -10, 10);
            if (perFile.EqualizerBands != null && perFile.EqualizerBands.Length == 10)
            {
                for (int i = 0; i < 10; i++)
                    _equalizerBands[i] = Math.Clamp(perFile.EqualizerBands[i], -20, 20);
                OnPropertyChanged(nameof(EqualizerBands));
                ApplyEqualizer();
            }
            if (!string.IsNullOrWhiteSpace(perFile.EqualizerPreset))
                EqualizerPresetName = perFile.EqualizerPreset;
            if (perFile.SelectedTrackId >= 0)
                SetPendingTrackId(perFile.SelectedTrackId);
        }
        else
        {
            // Use global last-selected track as fallback
            if (defaults.LastSelectedTrackId >= 0)
                SetPendingTrackId(defaults.LastSelectedTrackId);
        }
    }

    /// <summary>Called by MainViewModel when a media file closes. Saves per-file state.</summary>
    public void OnFileClosing()
    {
        if (!string.IsNullOrWhiteSpace(_currentMediaPath))
        {
            SavePerFileSettings();
            _currentMediaPath = null;
        }
    }

    /// <summary>Force-save current state to store.</summary>
    public void SaveSettings()
    {
        // Save global defaults
        _audioStore.SaveDefaults(new AudioSettingsStore.AudioGlobalDefaults
        {
            Volume = _volume,
            IsMuted = _isMuted,
            EqualizerPreset = _equalizerPresetName,
            IsNormalizationEnabled = _isAudioNormalizationEnabled,
            IsDialogueBoostEnabled = _isDialogueBoostEnabled,
            LastSelectedTrackId = _currentAudioTrackId >= 0 ? _currentAudioTrackId : -1
        });

        // Save per-file
        SavePerFileSettings();
        _dirty = false;
    }

    private void SavePerFileSettings()
    {
        if (string.IsNullOrWhiteSpace(_currentMediaPath)) return;
        _audioStore.SavePerFile(_currentMediaPath, new AudioSettingsStore.AudioPerFileSettings
        {
            SelectedTrackId = _currentAudioTrackId >= 0 ? _currentAudioTrackId : -1,
            AudioDelay = _audioDelay,
            EqualizerBands = _equalizerBands,
            EqualizerPreset = _equalizerPresetName
        });
    }

    /// <summary>Mark state as dirty and schedule a debounced save (2s).</summary>
    private void MarkDirty()
    {
        if (_disposed) return;
        _dirty = true;
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            if (_dirty)
            {
                try { SaveSettings(); }
                catch { /* best-effort */ }
            }
        }, null, 2000, Timeout.Infinite);
    }

    #region Reset

    public void ResetAllAudio()
    {
        VolumeValue = 50;
        IsMuted = false;
        ResetAudioDelay();
        EqualizerPresetName = "Flat";
        for (int i = 0; i < 10; i++) _equalizerBands[i] = 0;
        OnPropertyChanged(nameof(EqualizerBands));
        ApplyEqualizer();
        IsDialogueBoostEnabled = false;
        IsAudioNormalizationEnabled = false;

        // Clear per-file stored settings
        if (!string.IsNullOrWhiteSpace(_currentMediaPath))
        {
            _audioStore.DeletePerFile(_currentMediaPath);
            _dirty = false;
        }
    }

    #endregion

    // ── Player Event Handlers ──

    private void OnPlayerVolumeChanged(object? sender, VolumeChangedEventArgs e)
    {
        if (e.IsMuted)
        {
            _isMuted = true;
            OnPropertyChanged(nameof(IsMuted));
            VolumeChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var playerVolume = Math.Clamp(_player.Volume, 0, VolumeMax);
        if (Math.Abs(_volume - playerVolume) >= 0.001)
        {
            _volume = playerVolume;
            _isMuted = _player.IsMuted;
            OnPropertyChanged(nameof(VolumeValue));
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumeText));
            OnPropertyChanged(nameof(IsMuted));
            VolumeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPlayerTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        if (e.AudioTracks != null)
            RefreshAudioTracks(e.AudioTracks);
    }

    // ── Events ──

    /// <summary>Fires when volume or mute state changes.</summary>
    public event EventHandler? VolumeChanged;

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

        // Force-save before shutdown
        try
        {
            if (_dirty)
            {
                SaveSettings();
                _dirty = false;
            }
            if (!string.IsNullOrWhiteSpace(_currentMediaPath))
                SavePerFileSettings();
        }
        catch { /* best-effort */ }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _player.VolumeChanged -= OnPlayerVolumeChanged;
        _player.TrackListChanged -= OnPlayerTrackListChanged;
    }
}
