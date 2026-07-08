using System;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Media.Interfaces;

/// <summary>
/// Audio volume, mute, delay, track selection, and audio device management.
/// </summary>
public interface IAudioControl
{
    double Volume { get; set; }
    double VolumeMax { get; }
    bool IsMuted { get; set; }
    void Mute(bool isMuted);
    void IncreaseVolume();
    void DecreaseVolume();
    void ToggleMute();

    float AudioDelay { get; set; }
    void IncreaseAudioDelay();
    void DecreaseAudioDelay();

    AudioTrackInfo[] AudioSources { get; }
    void SelectAudioTrack(int trackIndex);
    void AddAudio(string path);

    bool IsAudioExclusive { get; set; }
    string[] AudioDeviceList { get; }
    string CurrentAudioDevice { get; set; }

    event EventHandler? AudioDeviceChanged;
    event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
}
