using System;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Media.Interfaces;

/// <summary>
/// Subtitle track selection, delay, positioning, font styling, and visibility.
/// </summary>
public interface ISubtitleControl
{
    int CurrentSubtitleTrack { get; set; }
    SubtitleSource[] SubtitleSources { get; }
    void AddSubtitle(string path);
    void SelectSubtitleTrack(int trackIndex);
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

    event EventHandler<SubtitlePropertyChangedEventArgs>? SubtitlePropertyChanged;
}
