using System;
using Simba.Media.Events;
using Simba.Media.Models;

namespace Simba.Media.Interfaces;

/// <summary>
/// Core playback control — open, play, pause, stop, seek, speed, screenshots.
/// </summary>
public interface IPlaybackControl
{
    void Open(string path);
    void Play();
    void Pause();
    void Stop();

    PlaybackState State { get; }
    bool IsPlaying { get; }
    string CurrentPath { get; }

    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    void Seek(TimeSpan position);
    void SeekForward(double seconds);
    void SeekBackward(double seconds);

    double Speed { get; set; }
    void SetSpeed(double speed);
    void ResetSpeed();
    void IncreaseSpeed();
    void DecreaseSpeed();

    void NextFrame();
    void PreviousFrame();

    void TakeScreenshot(string outputPath, bool includeSubtitles = true);
    void ScreenshotWithSubtitles();
    void ScreenshotWithoutSubtitles();
    byte[]? ScreenshotRaw(out int width, out int height);
    void GetVideoSize(out int width, out int height);

    event EventHandler? Opened;
    event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChangedEvent;
    event EventHandler<PositionChangedEventArgs>? PositionChanged;
    event EventHandler<string>? Error;
}
