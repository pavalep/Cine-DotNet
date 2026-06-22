using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Cine.Avalonia.Models;

namespace Cine.Avalonia.Services;

/// <summary>
/// Centralized manager for subtitle state: tracks, delay, position, font, styling.
/// Single source of truth for all subtitle-related UI bindings.
/// </summary>
public interface ISubtitleManager : INotifyPropertyChanged, IDisposable
{
    // ── Tracks ──
    ObservableCollection<TrackMenuItem> SubtitleTracks { get; }
    bool IsSubtitleEnabled { get; set; }
    int CurrentSubtitleTrackId { get; }
    bool HasTextSubtitles { get; }
    void SelectTrackById(int trackId);
    void CycleSubtitleTrackForward();
    void CycleSubtitleTrackBackward();

    // ── Timing ──
    float SubtitleDelay { get; set; }

    // ── Positioning ──
    int SubtitlePosition { get; set; }

    // ── Styling ──
    double SubtitleFontScale { get; set; }
    double SubtitleBorderSize { get; set; }
    double SubtitleShadowOffset { get; set; }
    string SubtitleFont { get; set; }
    string SubtitleColor { get; set; }

    // ── Reset ──
    void ResetAllSubtitles();

    // ── External Files ──
    Func<Task<string?>>? RequestSubtitleFileAsync { get; set; }
    Func<Task>? DismissFlyoutAsync { get; set; }
    void LoadExternalSubtitle(string filePath);
    Task AddSubtitleTrackAsync();

    // ── Lifecycle ──
    void OnFileClosing();
    void NotifyMediaOpened(string mediaPath);
}
