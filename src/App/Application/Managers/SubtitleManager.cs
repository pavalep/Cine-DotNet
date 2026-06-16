using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Cine.Avalonia.Models;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Centralized manager for all subtitle-related state.
/// Single source of truth — subscribes directly to player events.
/// Handles persistence with debounced auto-save and session override.
/// </summary>
public sealed class SubtitleManager : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPlayer _player;
    private readonly SubtitleSettingsStore _store;
    private bool _disposed;

    // ── Tracks ──
    private int _currentSubtitleTrackId = -1;
    private bool _isSubtitleEnabled;
    private bool _hasTextSubtitles = true; // True until we detect a bitmap track

    // ── Display Properties ──
    private float _subtitleDelay;
    private int _subtitlePosition = 100;
    private double _subtitleFontScale = 1.0;
    private double _subtitleBorderSize = 2.0;
    private double _subtitleShadowOffset = 1.0;
    private string _subtitleFont = "Arial";
    private string _subtitleColor = "#FFFFFF";

    // ── Session Override ──
    private string? _currentMediaPath;
    private bool _sessionOverride;
    private bool _settingsDirty;

    // ── Debounced Save ──
    private System.Timers.Timer? _saveTimer;
    private const int SaveDebounceMs = 2000;

    // ── Callback ──
    public Func<Task<string?>>? RequestSubtitleFileAsync { get; set; }

    // ── Events from player ──
    private EventHandler<TrackListChangedEventArgs>? _trackListHandler;
    private EventHandler<SubtitlePropertyChangedEventArgs>? _subPropHandler;

    public SubtitleManager(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _store = new SubtitleSettingsStore();

        // Sync initial state from player
        _subtitleDelay = player.SubtitleDelay;
        _subtitleFontScale = 1.0;

        // Subscribe directly to player events
        _trackListHandler = OnTrackListChanged;
        _subPropHandler = OnSubtitlePropertyChanged;
        _player.TrackListChanged += _trackListHandler;
        _player.SubtitlePropertyChanged += _subPropHandler;
        _player.Opened += OnPlayerOpened;

        // Debounced save timer
        _saveTimer = new System.Timers.Timer(SaveDebounceMs) { AutoReset = false };
        _saveTimer.Elapsed += (_, _) => FlushSave();

        BuildEmptyTrackMenus();
    }

    // ═══════════════════════════════════════════════
    //  Observable Properties — single source of truth
    // ═══════════════════════════════════════════════

    public ObservableCollection<TrackMenuItem> SubtitleTracks { get; } = new();

    public bool IsSubtitleEnabled
    {
        get => _isSubtitleEnabled;
        set
        {
            if (_isSubtitleEnabled == value) return;
            _isSubtitleEnabled = value;
            _player.SetSubtitleVisibility(value);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    public int CurrentSubtitleTrackId
    {
        get => _currentSubtitleTrackId;
        private set
        {
            if (_currentSubtitleTrackId == value) return;
            _currentSubtitleTrackId = value;
            OnPropertyChanged();
        }
    }

    public float SubtitleDelay
    {
        get => _subtitleDelay;
        set
        {
            if (Math.Abs(_subtitleDelay - value) < 0.01f) return;
            _subtitleDelay = value;
            _player.SubtitleDelay = value;
            MarkDirty();
            OnPropertyChanged();
        }
    }

    public int SubtitlePosition
    {
        get => _subtitlePosition;
        set
        {
            var clamped = Math.Clamp(value, 0, 200);
            if (_subtitlePosition == clamped) return;
            _subtitlePosition = clamped;
            _player.SetSubtitlePosition(clamped);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>mpv sub-scale — 1.0 = default.</summary>
    public double SubtitleFontScale
    {
        get => _subtitleFontScale;
        set
        {
            if (Math.Abs(_subtitleFontScale - value) < 0.01) return;
            _subtitleFontScale = value;
            _player.SetSubtitleFontSize(value * 24);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Font family for text subtitles (e.g. "Arial", "Segoe UI").</summary>
    public string SubtitleFont
    {
        get => _subtitleFont;
        set
        {
            if (_subtitleFont == value) return;
            _subtitleFont = value;
            _player.SetSubtitleFont(value);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Border/outline width for text subtitles (0-10).</summary>
    public double SubtitleBorderSize
    {
        get => _subtitleBorderSize;
        set
        {
            var clamped = Math.Clamp(value, 0, 10);
            if (Math.Abs(_subtitleBorderSize - clamped) < 0.01) return;
            _subtitleBorderSize = clamped;
            _player.SetSubtitleBorderSize(clamped);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Shadow offset for text subtitles (0-10).</summary>
    public double SubtitleShadowOffset
    {
        get => _subtitleShadowOffset;
        set
        {
            var clamped = Math.Clamp(value, 0, 10);
            if (Math.Abs(_subtitleShadowOffset - clamped) < 0.01) return;
            _subtitleShadowOffset = clamped;
            _player.SetSubtitleShadowOffset(clamped);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Text color in hex format (e.g. "#FFFFFF", "#FFFF00").</summary>
    public string SubtitleColor
    {
        get => _subtitleColor;
        set
        {
            if (_subtitleColor == value) return;
            _subtitleColor = value;
            _player.SetSubtitleColor(value);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// True after the user manually selects a track or adjusts a setting.
    /// Prevents auto-detect from overriding user's choice for the session.
    /// </summary>
    public bool IsSessionOverrideActive => _sessionOverride;

    /// <summary>
    /// True if the current subtitle track is text-based (SRT, ASS, WebVTT) and
    /// supports styling (font size, position, etc.). False for bitmap tracks (PGS, VOBSUB).
    /// </summary>
    public bool HasTextSubtitles
    {
        get => _hasTextSubtitles;
        private set
        {
            if (_hasTextSubtitles == value) return;
            _hasTextSubtitles = value;
            OnPropertyChanged();
        }
    }

    // ── External subtitle auto-detect ──
    private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx" };
    private static readonly string[] SubtitleDirectories = { ".", "./subs", "./subtitles", "./.subtitles" };

    /// <summary>
    /// Scans the media directory and common subdirectories for external subtitle files
    /// matching the media filename and preferred languages.
    /// </summary>
    /// <returns>List of subtitle file paths found, ordered by language preference match.</returns>
    private List<string> AutoDetectExternalSubtitles(string mediaPath)
    {
        var result = new List<string>();
        try
        {
            var mediaDir = Path.GetDirectoryName(Path.GetFullPath(mediaPath));
            var mediaName = Path.GetFileNameWithoutExtension(mediaPath);
            if (string.IsNullOrWhiteSpace(mediaDir) || string.IsNullOrWhiteSpace(mediaName))
                return result;

            var preferredLangs = _store.GetPreferredLanguages();
            var searched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var subDir in SubtitleDirectories)
            {
                var searchDir = subDir == "."
                    ? mediaDir
                    : Path.Combine(mediaDir, subDir);

                if (!Directory.Exists(searchDir)) continue;

                foreach (var file in Directory.EnumerateFiles(searchDir))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!SubtitleExtensions.Contains(ext)) continue;

                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (searched.Contains(file)) continue;
                    searched.Add(file);

                    // Check if this file matches the media name in various ways
                    var match = IsMatchingSubtitle(fileName, mediaName, preferredLangs);
                    if (match.HasValue)
                        result.Insert(match.Value.Index, file);
                    else
                        result.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleManager>().Warning("AutoDetectExternalSubtitles error: {Error}", ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Checks if a subtitle filename matches the media filename.
    /// Returns (score, index) — lower index = better match.
    /// </summary>
    private static (int Index, bool IsExact)? IsMatchingSubtitle(string subFileName, string mediaName, string[] preferredLangs)
    {
        // Exact match: Movie → Movie.srt
        if (string.Equals(subFileName, mediaName, StringComparison.OrdinalIgnoreCase))
            return (0, true);

        // Language match: Movie.en → en is in preferred
        var prefix = mediaName.ToLowerInvariant();
        var subLower = subFileName.ToLowerInvariant();

        if (subLower.StartsWith(prefix + "."))
        {
            var langCode = subLower[(prefix.Length + 1)..];
            // Handle "en.forced" or "en.sdh" variants
            var primaryLang = langCode.Split('.')[0];
            var langIdx = Array.IndexOf(preferredLangs, primaryLang);
            if (langIdx >= 0)
                return (langIdx + 1, false); // Language priority order

            // If it's a known language tag but not preferred, rank after preferred
            if (primaryLang.Length == 2 || primaryLang.Length == 3)
                return (preferredLangs.Length + 5, false);
        }

        // Fuzzy match: Movie contains the media name somewhere
        if (subLower.Contains(mediaName.ToLowerInvariant()))
            return (preferredLangs.Length + 10, false);

        return null;
    }

    // ═══════════════════════════════════════════════
    //  Player Event Handlers
    // ═══════════════════════════════════════════════

    private void OnPlayerOpened(object? sender, EventArgs e)
    {
        _sessionOverride = false; // Fresh session for new file
        // Load persisted settings is done via NotifyMediaOpened(path) from MainViewModel
    }

    /// <summary>
    /// Called by MainViewModel when a media file opens.
    /// Loads per-file settings and restores saved track/state.
    /// </summary>
    public void NotifyMediaOpened(string mediaPath)
    {
        _currentMediaPath = mediaPath;
        _sessionOverride = false;

        var perFile = _store.LoadPerFile(mediaPath);
        var defaults = _store.LoadDefaults();

        if (perFile?.StyleOverrides != null)
        {
            if (perFile.StyleOverrides.FontScale > 0)
                SubtitleFontScale = perFile.StyleOverrides.FontScale;
            if (perFile.StyleOverrides.Position >= 0)
                SubtitlePosition = perFile.StyleOverrides.Position;
            if (Math.Abs(perFile.StyleOverrides.Delay) > 0.01)
                SubtitleDelay = (float)perFile.StyleOverrides.Delay;
            if (perFile.StyleOverrides.BorderSize > 0)
                SubtitleBorderSize = perFile.StyleOverrides.BorderSize;
            if (perFile.StyleOverrides.ShadowOffset > 0)
                SubtitleShadowOffset = perFile.StyleOverrides.ShadowOffset;
            if (!string.IsNullOrWhiteSpace(perFile.StyleOverrides.Font))
                SubtitleFont = perFile.StyleOverrides.Font;
            if (!string.IsNullOrWhiteSpace(perFile.StyleOverrides.Color))
                SubtitleColor = perFile.StyleOverrides.Color;
            if (perFile.SubtitleVisible.HasValue)
                IsSubtitleEnabled = perFile.SubtitleVisible.Value;
        }

        // Track selection via TrackListChanged will happen next
        if (perFile?.SelectedTrackId.HasValue == true && perFile.SelectedTrackId.Value >= 0)
            SelectTrackById(perFile.SelectedTrackId.Value);

        // Auto-detect and load external subtitles (if enabled in defaults)
        if (defaults.AutoEnabled && defaults.FallbackToExternal)
        {
            var externalFiles = AutoDetectExternalSubtitles(mediaPath);
            foreach (var subFile in externalFiles)
            {
                try { _player.AddSubtitle(subFile); }
                catch (Exception ex)
                {
                    global::Cine.Core.Log.ForContext<SubtitleManager>().Warning("Failed to load external sub {Path}: {Error}", subFile, ex.Message);
                }
            }
        }
    }

    private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        RebuildTracks(e.SubtitleTracks);
    }

    private void OnSubtitlePropertyChanged(object? sender, SubtitlePropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "sid":
                var sid = (int)e.Value;
                CurrentSubtitleTrackId = sid;
                IsSubtitleEnabled = sid >= 0;
                UpdateTrackSelection(sid);
                MarkDirty();
                break;
            case "sub-visibility":
                IsSubtitleEnabled = (bool)e.Value;
                MarkDirty();
                break;
            case "sub-pos":
                var pos = (int)e.Value;
                if (pos != _subtitlePosition)
                {
                    _subtitlePosition = pos;
                    OnPropertyChanged(nameof(SubtitlePosition));
                }
                break;
            case "sub-scale":
                var scale = (double)e.Value;
                if (Math.Abs(scale - _subtitleFontScale) > 0.01)
                {
                    _subtitleFontScale = scale;
                    OnPropertyChanged(nameof(SubtitleFontScale));
                }
                break;
            case "sub-delay":
                var delay = (float)(double)e.Value;
                if (Math.Abs(delay - _subtitleDelay) > 0.01f)
                {
                    _subtitleDelay = delay;
                    OnPropertyChanged(nameof(SubtitleDelay));
                }
                break;
        }
    }

    // ═══════════════════════════════════════════════
    //  Track Management
    // ═══════════════════════════════════════════════

    private void BuildEmptyTrackMenus()
    {
        SubtitleTracks.Clear();
        SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));
    }

    private void RebuildTracks(IEnumerable<SubtitleSource>? subtitleSources)
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
    }

    private void UpdateTrackSelection(int selectedId)
    {
        foreach (var item in SubtitleTracks)
            item.RefreshSelection(item.TrackIndex == selectedId);

        // Update bitmap / text detection for the selected track
        var selected = SubtitleTracks.FirstOrDefault(t => t.TrackIndex == selectedId && !t.IsPseudoEntry);
        HasTextSubtitles = selected?.Source == null || !selected.Source.IsBitmap;
    }

    // ═══════════════════════════════════════════════
    //  Selection
    // ═══════════════════════════════════════════════

    private void OnSelectSubtitle(TrackMenuItem item)
    {
        _sessionOverride = true;

        if (item.DisplayName == "Add Subtitle Track…")
        {
            _ = OnAddSubtitleAsync();
            return;
        }

        if (item.DisplayName == "None" || item.TrackIndex == -2)
        {
            _player.SelectSubtitleTrack(-1);
            CurrentSubtitleTrackId = -1;
            IsSubtitleEnabled = false;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            MarkDirty();
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectSubtitleTrack(item.TrackIndex);
            CurrentSubtitleTrackId = item.TrackIndex;
            IsSubtitleEnabled = true;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            MarkDirty();
        }
    }

    /// <summary>Opens a file dialog (via callback) and loads the selected external subtitle.</summary>
    public async Task AddSubtitleTrackAsync()
    {
        await OnAddSubtitleAsync();
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
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleManager>()
                .Error(ex, "AddSubtitleAsync failed");
        }
    }

    /// <summary>Load external subtitle directly (drag-drop, automation).</summary>
    public void LoadExternalSubtitle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        _sessionOverride = true;
        _player.AddSubtitle(filePath);
        MarkDirty();
    }

    /// <summary>Select a subtitle track by its ID. Used for session restore.</summary>
    public void SelectTrackById(int trackId)
    {
        var track = SubtitleTracks.FirstOrDefault(t => t.TrackIndex == trackId && !t.IsPseudoEntry);
        if (track != null && track.SelectCommand?.CanExecute(track) == true)
            track.SelectCommand.Execute(track);
        else
            CurrentSubtitleTrackId = trackId;
    }

    /// <summary>Cycle to the next subtitle track. Wraps around. J key.</summary>
    public void CycleSubtitleTrackForward()
    {
        var tracks = SubtitleTracks.Where(t => !t.IsPseudoEntry && t.TrackIndex >= 0).ToList();
        if (tracks.Count == 0) return;

        int currentIdx = tracks.FindIndex(t => t.TrackIndex == _currentSubtitleTrackId);
        int nextIdx = (currentIdx + 1) % tracks.Count;
        tracks[nextIdx].SelectCommand.Execute(tracks[nextIdx]);
    }

    /// <summary>Cycle to the previous subtitle track. Wraps around. Shift+J key.</summary>
    public void CycleSubtitleTrackBackward()
    {
        var tracks = SubtitleTracks.Where(t => !t.IsPseudoEntry && t.TrackIndex >= 0).ToList();
        if (tracks.Count == 0) return;

        int currentIdx = tracks.FindIndex(t => t.TrackIndex == _currentSubtitleTrackId);
        int prevIdx = currentIdx <= 0 ? tracks.Count - 1 : currentIdx - 1;
        tracks[prevIdx].SelectCommand.Execute(tracks[prevIdx]);
    }

    // ═══════════════════════════════════════════════
    //  Persistence
    // ═══════════════════════════════════════════════

    /// <summary>Mark settings as dirty and start debounce timer.</summary>
    private void MarkDirty()
    {
        _settingsDirty = true;
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    /// <summary>Immediately flush dirty settings to disk. Called on file close / app exit.</summary>
    public void OnFileClosing()
    {
        FlushSave();
    }

    private void FlushSave()
    {
        _saveTimer?.Stop();

        if (!_settingsDirty || string.IsNullOrWhiteSpace(_currentMediaPath))
            return;

        _settingsDirty = false;

        _store.SavePerFile(
            _currentMediaPath,
            _currentSubtitleTrackId >= 0 ? _currentSubtitleTrackId : null,
            _isSubtitleEnabled,
            new SubtitleSettingsStore.SubtitleStyle
            {
                FontScale = _subtitleFontScale,
                Position = _subtitlePosition,
                Delay = _subtitleDelay,
                BorderSize = _subtitleBorderSize,
                ShadowOffset = _subtitleShadowOffset,
                Font = _subtitleFont,
                Color = _subtitleColor
            });
    }

    // ═══════════════════════════════════════════════
    //  Reset
    // ═══════════════════════════════════════════════

    /// <summary>Reset all subtitle settings to defaults, clear session override, delete per-file settings.</summary>
    public void ResetAllSubtitles()
    {
        SubtitleDelay = 0;
        SubtitlePosition = 100;
        SubtitleFontScale = 1.0;
        SubtitleBorderSize = 2.0;
        SubtitleShadowOffset = 1.0;
        SubtitleFont = "Arial";
        SubtitleColor = "#FFFFFF";
        _sessionOverride = false;

        if (!string.IsNullOrWhiteSpace(_currentMediaPath))
        {
            _store.DeletePerFile(_currentMediaPath);
            // Apply global defaults
            var defaults = _store.LoadDefaults();
            if (defaults.Style != null)
            {
                SubtitleFontScale = defaults.Style.FontScale;
                SubtitlePosition = defaults.Style.Position;
                SubtitleDelay = (float)defaults.Style.Delay;
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════

    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        var state = track.IsEnabled ? "on" : "off";
        return $"{prefix}: {lang} ({state})";
    }

    // ═══════════════════════════════════════════════
    //  INotifyPropertyChanged
    // ═══════════════════════════════════════════════

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ═══════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Force-save before disposing
        FlushSave();

        _saveTimer?.Stop();
        _saveTimer?.Dispose();
        _saveTimer = null;

        if (_trackListHandler != null)
            _player.TrackListChanged -= _trackListHandler;
        if (_subPropHandler != null)
            _player.SubtitlePropertyChanged -= _subPropHandler;
        _player.Opened -= OnPlayerOpened;
    }
}
