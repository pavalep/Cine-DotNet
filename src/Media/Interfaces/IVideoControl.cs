using System;
using Simba.Media.Events;
using Simba.Media.Models;

namespace Simba.Media.Interfaces;

/// <summary>
/// Video filter adjustments (zoom, aspect ratio, contrast, brightness, etc.),
/// video track selection, and fullscreen control.
/// </summary>
public interface IVideoControl
{
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

    VideoTrackInfo[] VideoSources { get; }
    void SelectVideoTrack(int trackIndex);

    bool IsFullscreen { get; set; }
    void ToggleFullscreen();
    void SetFullscreen(bool fullscreen);

    event EventHandler<TrackListChangedEventArgs>? TrackListChanged;
    event EventHandler<FullscreenChangedEventArgs>? FullscreenChangedEvent;
}
