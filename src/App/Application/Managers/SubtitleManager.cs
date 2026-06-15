using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using Cine.Avalonia.Models;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Centralized manager for all subtitle-related state: Subtitle tracks,
/// Delay, Position, Font Size, and file loading.
/// </summary>
public sealed class SubtitleManager : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private bool _disposed;

    // ── Subtitle Tracks ──
    private int _currentSubtitleTrackId = -1;
    private int? _pendingSubtitleTrackId;
    private bool _isSubtitleEnabled;

    // ── Subtitle Delay ──
    private float _subtitleDelay;

    // ── Subtitle Position ──
    private int _subtitlePosition = 100;

    // ── Subtitle Font Size ──
    private double _subtitleFontSize = 24;

    // ── File dialog callback ──
    public Func<Task<string?>>? RequestSubtitleFileAsync { get; set; }

    public SubtitleManager(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));

        // Sync initial state from player
        _subtitleDelay = _player.SubtitleDelay;
        _subtitleFontSize = 24;

        BuildEmptyTrackMenus();
    }

    // ── Observable Properties ──

    #region Subtitle Tracks

    public ObservableCollection<TrackMenuItem> SubtitleTracks { get; } = new();

    public bool IsSubtitleEnabled
    {
        get => _isSubtitleEnabled;
        private set
        {
            if (_isSubtitleEnabled == value) return;
            _isSubtitleEnabled = value;
            OnPropertyChanged();
        }
    }

    public int? PendingSubtitleTrackId
    {
        get => _pendingSubtitleTrackId;
        set => _pendingSubtitleTrackId = value;
    }

    public void RestorePendingTrack()
    {
        if (!_pendingSubtitleTrackId.HasValue) return;
        var track = SubtitleTracks.FirstOrDefault(t =>
            t.TrackIndex == _pendingSubtitleTrackId.Value && !t.IsPseudoEntry);
        if (track?.SelectCommand.CanExecute(track) == true)
            track.SelectCommand.Execute(track);
        _pendingSubtitleTrackId = null;
    }

    private void BuildEmptyTrackMenus()
    {
        SubtitleTracks.Clear();
        SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));
    }

    private void OnSelectSubtitle(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Subtitle Track…")
        {
            _ = OnAddSubtitleAsync();
            return;
        }

        if (item.DisplayName == "None")
        {
            _player.SelectSubtitleTrack(-1);
            _currentSubtitleTrackId = -1;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            IsSubtitleEnabled = false;
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectSubtitleTrack(item.TrackIndex);
            _currentSubtitleTrackId = item.TrackIndex;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            IsSubtitleEnabled = true;
        }
    }

    private async Task OnAddSubtitleAsync()
    {
        if (RequestSubtitleFileAsync == null) return;
        try
        {
            var path = await RequestSubtitleFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
                _player.AddSubtitle(path);
        }
        catch { /* user cancelled or error */ }
    }

    /// <summary>
    /// Load an external subtitle file directly (bypasses file dialog).
    /// Used by drag-drop and automation.
    /// </summary>
    public void LoadExternalSubtitle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        _player.AddSubtitle(filePath);
    }

    /// <summary>
    /// Refresh subtitle tracks from a track list update.
    /// Called by the owner when track list changes.
    /// </summary>
    public void RefreshSubtitleTracks(IEnumerable<SubtitleSource> subtitleSources)
    {
        SubtitleTracks.Clear();
        SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));

        if (subtitleSources != null)
        {
            int idx = 0;
            foreach (var track in subtitleSources)
            {
                var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                var item = new TrackMenuItem(
                    FormatTrack("Sub", track),
                    TrackType.Subtitle,
                    trackId,
                    OnSelectSubtitle,
                    track
                );
                item.IsSelected = track.IsEnabled;
                SubtitleTracks.Add(item);
                idx++;
            }
        }

        IsSubtitleEnabled = subtitleSources?.Any(t => t.IsEnabled) ?? false;

        // Auto-restore pending track if set (from session restore)
        RestorePendingTrack();
    }

    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        var state = track.IsEnabled ? "on" : "off";
        return $"{prefix}: {lang} ({state})";
    }

    #endregion

    #region Subtitle Delay

    public float SubtitleDelay
    {
        get => _subtitleDelay;
        set
        {
            _subtitleDelay = value;
            _player.SubtitleDelay = value;
            OnPropertyChanged();
        }
    }

    public void ResetSubtitleDelay() => SubtitleDelay = 0;

    #endregion

    #region Subtitle Position

    public int SubtitlePosition
    {
        get => _subtitlePosition;
        set
        {
            _subtitlePosition = Math.Clamp(value, 0, 200);
            _player.SetSubtitlePosition(_subtitlePosition);
            OnPropertyChanged();
        }
    }

    #endregion

    #region Subtitle Font Size

    public double SubtitleFontSize
    {
        get => _subtitleFontSize;
        set
        {
            _subtitleFontSize = value;
            _player.SetSubtitleFontSize(value);
            OnPropertyChanged();
        }
    }

    #endregion

    #region Reset

    public void ResetAllSubtitles()
    {
        ResetSubtitleDelay();
        SubtitlePosition = 100;
        SubtitleFontSize = 24;
    }

    #endregion

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
    }
}
