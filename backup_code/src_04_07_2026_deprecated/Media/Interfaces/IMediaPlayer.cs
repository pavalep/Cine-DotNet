using System;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Media.Interfaces;

public interface IMediaPlayer
{
    // === Playback control ===
    void Open(string path);
    void Play();
    void Pause();
    void Stop();

    // === State ===
    PlaybackState State { get; }
    bool IsPlaying { get; }
    string CurrentPath { get; }

    // === Position & Duration ===
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    void Seek(TimeSpan position);
    void SeekForward(double seconds);
    void SeekBackward(double seconds);

    // === Volume & Audio ===
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

    // === Speed ===
    double Speed { get; set; }
    void SetSpeed(double speed);
    void ResetSpeed();
    void IncreaseSpeed();
    void DecreaseSpeed();

    // === Playlist ===
    string[] Playlist { get; }
    int PlaylistPosition { get; set; }
    bool IsShuffled { get; set; }
    LoopMode LoopMode { get; set; }
    void AddToPlaylist(string path);
    void NextPlaylistItem();
    void PreviousPlaylistItem();
    void ToggleLoopFile();
    void ToggleLoopPlaylist();

    // === Subtitles ===
    int CurrentSubtitleTrack { get; set; }
    SubtitleSource[] SubtitleSources { get; }
    void AddSubtitle(string path);
    void AddAudio(string path);
    void SelectSubtitleTrack(int trackIndex);
    void SelectAudioTrack(int trackIndex);
    void CycleSubtitleTrack();
    float SubtitleDelay { get; set; }
    void IncreaseSubtitleDelay();
    void DecreaseSubtitleDelay();
    int SubtitlePosition { get; set; }
    void SetSubtitlePosition(int position);
    void SetSubtitleFontSize(double size);
    void SetSubtitleVisibility(bool visible);
    void SetSubtitleFont(string fontFamily);
    void SetSubtitleBorderSize(double size);
    void SetSubtitleShadowOffset(double offset);
    void SetSubtitleColor(string colorHex);
    void SetSubtitleOpacity(double opacity);
    void SetSubtitleBlur(double blur);
    void SetSubtitleBold(bool bold);

    // === Audio / Video Track Enumeration ===
    AudioTrackInfo[] AudioSources { get; }
    VideoTrackInfo[] VideoSources { get; }
    void SelectVideoTrack(int trackIndex);

    // === Video filters ===
    double Zoom { get; set; }
    double AspectRatio { get; set; }
    double Contrast { get; set; }
    double Brightness { get; set; }
    double Gamma { get; set; }
    double Saturation { get; set; }
    double Hue { get; set; }
    void IncreaseContrast();
    void DecreaseContrast();
    void IncreaseBrightness();
    void DecreaseBrightness();
    void IncreaseGamma();
    void DecreaseGamma();
    void IncreaseSaturation();
    void DecreaseSaturation();
    void IncreaseHue();
    void DecreaseHue();

    // === Chapter support ===
    int CurrentChapter { get; }
    ChapterInfo[] ChapterList { get; }
    void NextChapter();
    void PreviousChapter();

    // === Fullscreen ===
    bool IsFullscreen { get; set; }
    void ToggleFullscreen();
    void SetFullscreen(bool fullscreen);

    // === Navigation ===
    void NextFrame();
    void PreviousFrame();

    // === Screenshot ===
    void TakeScreenshot(string outputPath, bool includeSubtitles = true);
    void ScreenshotWithSubtitles();
    void ScreenshotWithoutSubtitles();
    /// <summary>
    /// Captures the current video frame as a raw BGRA32 byte buffer.
    /// Returns null if no frame is available or on failure.
    /// Width/height are the actual video dimensions.
    /// </summary>
    byte[]? ScreenshotRaw(out int width, out int height);
    void GetVideoSize(out int width, out int height);

    // === Audio device ===
    bool IsAudioExclusive { get; set; }
    string[] AudioDeviceList { get; }
    string CurrentAudioDevice { get; set; }
    event EventHandler? AudioDeviceChanged;

    // === Native rendering ===
    void InitializeRenderer(IntPtr hwnd);
    void NotifyResize(int width, int height);
    void Command(string command, params string[] args);

    // === Events ===
    event EventHandler? Opened;
    event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChangedEvent;
    event EventHandler<PositionChangedEventArgs>? PositionChanged;
    event EventHandler<ChapterListChangedEventArgs>? ChapterListChanged;
    event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    event EventHandler<TrackListChangedEventArgs>? TrackListChanged;
    event EventHandler<FullscreenChangedEventArgs>? FullscreenChangedEvent;
    event EventHandler<LoopChangedEventArgs>? LoopChangedEvent;
    event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;
    /// <summary>Fires when any subtitle property changes (sid, sub-visibility, sub-pos, sub-scale, sub-delay).</summary>
    event EventHandler<SubtitlePropertyChangedEventArgs>? SubtitlePropertyChanged;
    event EventHandler<string>? Error;
}
