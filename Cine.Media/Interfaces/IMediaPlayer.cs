using System;

namespace Cine.Media.Interfaces;
public interface IMediaPlayer
{
    void Open(string path);
    void Play();
    void Pause();
    void Stop();
    double Volume { get; set; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    event EventHandler? PositionChanged;
    event EventHandler? StateChanged;
    bool IsPlaying { get; }
}