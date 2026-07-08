using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Cine.Avalonia.Models;
using Cine.Media.Models;

namespace Cine.Avalonia.Services;

/// <summary>
/// Centralized manager for audio state: volume, mute, equalizer, tracks, delay.
/// Single source of truth for all audio-related UI bindings.
/// </summary>
public interface IAudioManager : INotifyPropertyChanged, IDisposable
{
    // ── Volume / Mute ──
    double Volume { get; set; }
    double VolumeMax { get; }
    double VolumeValue { get; set; }
    string VolumeText { get; }
    bool IsMuted { get; set; }
    void IncreaseVolume();
    void DecreaseVolume();
    void ToggleMute();

    // ── Events ──
    event EventHandler? VolumeChanged;

    // ── Equalizer ──
    double[] EqualizerBands { get; set; }
    string EqualizerPresetName { get; set; }
    bool IsAudioNormalizationEnabled { get; set; }
    bool IsDialogueBoostEnabled { get; set; }
    void SetEqualizerBand(int bandIndex, double gain);
    void ApplyEqualizerPreset(string presetName);
    void ToggleAudioNormalization();

    // ── Audio Delay ──
    float AudioDelay { get; set; }
    void ResetAudioDelay();

    // ── Audio Tracks ──
    ObservableCollection<TrackMenuItem> AudioTracks { get; }
    bool IsAudioEnabled { get; }
    void RefreshAudioTracks(IEnumerable<SubtitleSource> audioSources);
    void RestorePendingTrack();
    void SetPendingTrackId(int trackId);

    // ── File Dialog ──
    Func<Task<string?>>? RequestAudioFileAsync { get; set; }
    Func<Task>? DismissFlyoutAsync { get; set; }

    // ── Reset ──
    void ResetAllAudio();

    // ── Lifecycle ──
    void OnFileClosing();
    void NotifyMediaOpened(string mediaPath);
}
