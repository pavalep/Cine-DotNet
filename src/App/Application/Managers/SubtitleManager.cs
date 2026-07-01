using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Cine.Avalonia.Helpers;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Core.Services;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.Managers;

/// <summary>
/// Centralized manager for all subtitle-related state.
/// Single source of truth — subscribes directly to player events.
/// Handles persistence with debounced auto-save and session override.
/// </summary>
public sealed class SubtitleManager : ISubtitleManager
{
    private readonly IMediaPlayer _player;
    private readonly SubtitleSettingsStore _store;
    private readonly ILogger _log;
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
    private double _subtitleOpacity = 1.0;
    private double _subtitleBlur = 0.0;
    private bool _subtitleBold;
    private string _subtitleFont = "Arial";
    private string _subtitleColor = "#FFFFFF";

    // ── Session Override ──
    private string? _currentMediaPath;
    private bool _sessionOverride;
    private CancellationTokenSource? _mediaOpenCts;
    private bool _settingsDirty;

    // ── Debounced Save ──
    private System.Timers.Timer? _saveTimer;
    private const int SaveDebounceMs = 2000;

    // ── Event coalescing (track list only — property changes bypass to avoid drops) ──
    private readonly object _eventLock = new();
    private bool _pendingSubtitleTrackDispatch;

    // ── Callback ──
    public Func<Task<string?>>? RequestSubtitleFileAsync { get; set; }
    public Func<Task>? DismissFlyoutAsync { get; set; }
    public Action<string>? TrackChangedMessage { get; set; }

    // ── Events from player ──
    private EventHandler<TrackListChangedEventArgs>? _trackListChangedHandler;
    private EventHandler<SubtitlePropertyChangedEventArgs>? _subPropHandler;

    // ── Track menu — populated by RebuildSubtitleTracks() on first subtitle TrackListChanged event ──
    private readonly ObservableCollection<TrackMenuItem> _subtitleTracks = new();

    public SubtitleManager(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _store = new SubtitleSettingsStore();
        _log = global::Cine.Core.Log.Default;

        _log.Debug("Constructor: initializing SubtitleManager");

        // Load global defaults from preferences
        var defaults = _store.LoadDefaults();
        if (defaults.Style != null)
        {
            _subtitleFontScale = defaults.Style.FontScale;
            _subtitlePosition = defaults.Style.Position;
            _subtitleDelay = (float)defaults.Style.Delay;
            _subtitleBorderSize = defaults.Style.BorderSize;
            _subtitleShadowOffset = defaults.Style.ShadowOffset;
            _subtitleOpacity = defaults.Style.Opacity;
            _subtitleBlur = defaults.Style.Blur;
            _subtitleBold = defaults.Style.Bold;
            _subtitleFont = defaults.Style.Font;
            _subtitleColor = defaults.Style.Color;
            _log.Debug("Constructor: loaded defaults — font={Font}, scale={Scale}, delay={Delay}",
                defaults.Style.Font, defaults.Style.FontScale, defaults.Style.Delay);
        }
        else
        {
            _subtitleDelay = player.SubtitleDelay;
            _subtitleFontScale = 1.0;
            _log.Debug("Constructor: no defaults found, using player fallback delay={Delay}", player.SubtitleDelay);
        }

        // Subscribe directly to player events
        _trackListChangedHandler = OnSubtitleTrackListChanged;
        _subPropHandler = OnSubtitlePropertyChanged;
        _player.TrackListChanged += _trackListChangedHandler;
        _player.SubtitlePropertyChanged += _subPropHandler;
        _player.Opened += OnPlayerOpened;
        _player.Error += OnPlayerError;
        _log.Debug("Constructor: subscribed to player events (TrackListChanged, SubtitlePropertyChanged, Opened, Error)");

        // Pre-populate track menu with pseudo entries so "+ Add Subtitles…"
        // and "None" are always available — even before any media is loaded.
        // OnSubtitleTrackListChanged will add real tracks later via RebuildSubtitleTracks.
        SubtitleTracks.Add(new TrackMenuItem("+ Add Subtitles\u2026", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));
        _log.Debug("Constructor: pre-populated track menu with pseudo entries (add/none)");

        // Note: _saveTimer is created lazily on first dirty mark.
    }

    // ═══════════════════════════════════════════════
    //  Observable Properties — single source of truth
    // ═══════════════════════════════════════════════

    public ObservableCollection<TrackMenuItem> SubtitleTracks => _subtitleTracks;

    public bool IsSubtitleEnabled
    {
        get => _isSubtitleEnabled;
        set
        {
            if (_isSubtitleEnabled == value) return;
            _isSubtitleEnabled = value;
            _log.Debug("IsSubtitleEnabled = {Enabled}", value);
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
            _log.Debug("SubtitleDelay = {Delay}s", value);
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
            _log.Debug("SubtitlePosition = {Pos}%", clamped);
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
            _log.Debug("SubtitleFontScale = {Scale}× (mpv: {Mpv})", value, value * 24);
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
            _log.Debug("SubtitleFont = {Font}", value);
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
            _log.Debug("SubtitleBorderSize = {Border}", clamped);
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
            _log.Debug("SubtitleShadowOffset = {Shadow}", clamped);
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
            _log.Debug("SubtitleColor = {Color}", value);
            _player.SetSubtitleColor(value);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Subtitle text opacity (0.0 = invisible, 1.0 = fully opaque).</summary>
    public double SubtitleOpacity
    {
        get => _subtitleOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_subtitleOpacity - clamped) < 0.01) return;
            _subtitleOpacity = clamped;
            _log.Debug("SubtitleOpacity = {Opac}", clamped);
            _player.SetSubtitleOpacity(clamped);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Gaussian blur radius for subtitle text (0-20).</summary>
    public double SubtitleBlur
    {
        get => _subtitleBlur;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 20.0);
            if (Math.Abs(_subtitleBlur - clamped) < 0.01) return;
            _subtitleBlur = clamped;
            _log.Debug("SubtitleBlur = {Blur}", clamped);
            _player.SetSubtitleBlur(clamped);
            MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Bold text toggle for subtitle text.</summary>
    public bool SubtitleBold
    {
        get => _subtitleBold;
        set
        {
            if (_subtitleBold == value) return;
            _subtitleBold = value;
            _log.Debug("SubtitleBold = {Bold}", value);
            _player.SetSubtitleBold(value);
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
    private List<string> AutoDetectExternalSubtitles(string mediaPath, CancellationToken ct = default)
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
                ct.ThrowIfCancellationRequested();

                var searchDir = subDir == "."
                    ? mediaDir
                    : Path.Combine(mediaDir, subDir);

                if (!Directory.Exists(searchDir)) continue;

                foreach (var file in Directory.EnumerateFiles(searchDir))
                {
                    ct.ThrowIfCancellationRequested();

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
        catch (OperationCanceledException)
        {
            // Cancelled — new media opened, cleanup is handled by caller
        }
        catch (Exception ex)
        {
            _log.Warning("AutoDetectExternalSubtitles error: {Error}", ex.Message);
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
        _log.Info("NotifyMediaOpened: path={MediaPath}", mediaPath);

        // Cancel any previous auto-detect still running
        _mediaOpenCts?.Cancel();
        _mediaOpenCts?.Dispose();
        _mediaOpenCts = new CancellationTokenSource();
        var ct = _mediaOpenCts.Token;

        _currentMediaPath = mediaPath;
        _sessionOverride = false;

        var perFile = _store.LoadPerFile(mediaPath);
        _log.Debug("NotifyMediaOpened: perFile exists={Exists}, trackId={TrackId}, visible={Visible}",
            perFile != null, perFile?.SelectedTrackId, perFile?.SubtitleVisible);

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
            if (perFile.StyleOverrides.Opacity > 0)
                SubtitleOpacity = perFile.StyleOverrides.Opacity;
            if (perFile.StyleOverrides.Blur > 0)
                SubtitleBlur = perFile.StyleOverrides.Blur;
            // Bold is a toggle — apply whatever was saved
            SubtitleBold = perFile.StyleOverrides.Bold;
            if (perFile.SubtitleVisible.HasValue)
                IsSubtitleEnabled = perFile.SubtitleVisible.Value;
        }

        // Track selection via TrackListChanged will happen next
        if (perFile?.SelectedTrackId.HasValue == true && perFile.SelectedTrackId.Value >= 0)
        {
            _log.Debug("NotifyMediaOpened: scheduling track selection id={TrackId}", perFile.SelectedTrackId.Value);
            SelectSubtitleTrackById(perFile.SelectedTrackId.Value);
        }

        // Auto-detect and load external subtitles (if enabled in defaults)
        // Run on background thread to avoid blocking UI during file I/O
        if (defaults.AutoEnabled && defaults.FallbackToExternal)
        {
            _log.Debug("NotifyMediaOpened: auto-detect enabled, scanning for external subs");
            var capturedMediaPath = mediaPath;
            var capturedCt = ct;
            _ = Task.Run(async () =>
            {
                var externalFiles = AutoDetectExternalSubtitles(capturedMediaPath, capturedCt);
                _log.Debug("NotifyMediaOpened: auto-detect found {Count} external subs", externalFiles.Count);
                if (externalFiles.Count > 0 && !capturedCt.IsCancellationRequested)
                {
                    await DispatchAddExternalSubtitlesAsync(externalFiles);
                }
            }, ct);
        }
        else
        {
            _log.Debug("NotifyMediaOpened: auto-detect skipped (AutoEnabled={AE}, FallbackToExternal={FE})",
                defaults.AutoEnabled, defaults.FallbackToExternal);
        }
    }

    private void OnPlayerError(object? sender, string error)
    {
        _log.Warning("mpv error: {Error}", error);
    }

    private void OnSubtitleTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        var subTracks = e.SubtitleTracks?.ToArray() ?? Array.Empty<SubtitleSource>();
        _log.Debug("OnSubtitleTrackListChanged: {Count} subtitle tracks", subTracks.Length);

        // Log each track's ID and type so we can verify they're really subtitles
        foreach (var st in subTracks)
        {
            _log.Debug("OnSubtitleTrackListChanged:   track path='{Path}' type='{Type}' lang='{Lang}' enabled={Enabled} forced={Forced}",
                st.PathOrId, st.Type, st.Language, st.IsEnabled, st.IsForced);
        }

        // TrackListChanged fires on mpv's background thread.
        // Coalesce rapid-fire events — only the latest track list matters.
        lock (_eventLock)
        {
            if (_pendingSubtitleTrackDispatch)
            {
                _log.Trace("OnSubtitleTrackListChanged: coalescing — pending dispatch exists");
                return;
            }
            _pendingSubtitleTrackDispatch = true;
        }

        _ = global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            lock (_eventLock) _pendingSubtitleTrackDispatch = false;
            RebuildSubtitleTracks(e.SubtitleTracks);
        });
    }

    private void OnSubtitlePropertyChanged(object? sender, SubtitlePropertyChangedEventArgs e)
    {
        _log.Trace("OnSubtitlePropertyChanged: prop={Prop}, value={Value}", e.PropertyName, e.Value);

        // This fires on mpv's background thread. Mutating properties that the UI
        // binds to must happen on the UI thread. Dispatch each property change
        // immediately — the UI thread dispatcher naturally serializes them.
        // We do NOT coalesce here: dropping a property change (e.g. "sid") would
        // leave the UI showing stale state.
        _ = global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case "sid":
                    var sid = (int)e.Value;
                    _log.Debug("OnSubtitlePropertyChanged: sid={Sid}", sid);
                    CurrentSubtitleTrackId = sid;
                    IsSubtitleEnabled = sid >= 0;
                    UpdateSubtitleTrackSelection(sid);
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
                        _log.Trace("OnSubtitlePropertyChanged: sub-pos changed to {Pos} (was {OldPos})", pos, _subtitlePosition);
                        _subtitlePosition = pos;
                        OnPropertyChanged(nameof(SubtitlePosition));
                    }
                    break;
                case "sub-scale":
                    var scale = (double)e.Value;
                    if (Math.Abs(scale - _subtitleFontScale) > 0.01)
                    {
                        _log.Trace("OnSubtitlePropertyChanged: sub-scale changed to {Scale} (was {OldScale})", scale, _subtitleFontScale);
                        _subtitleFontScale = scale;
                        OnPropertyChanged(nameof(SubtitleFontScale));
                    }
                    break;
                case "sub-delay":
                    var delay = (float)(double)e.Value;
                    if (Math.Abs(delay - _subtitleDelay) > 0.01f)
                    {
                        _log.Trace("OnSubtitlePropertyChanged: sub-delay changed to {Delay}s (was {OldDelay})", delay, _subtitleDelay);
                        _subtitleDelay = delay;
                        OnPropertyChanged(nameof(SubtitleDelay));
                    }
                    break;
                default:
                    _log.Trace("OnSubtitlePropertyChanged: unhandled prop={Prop}", e.PropertyName);
                    break;
            }
        });
    }

    // ═══════════════════════════════════════════════
    //  Track Management
    // ═══════════════════════════════════════════════

    private void RebuildSubtitleTracks(IEnumerable<SubtitleSource>? subtitleSources)
    {
        // ObservableCollection must only be mutated on the UI thread
        Debug.Assert(
            global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess(),
            "RebuildSubtitleTracks must be called on the UI thread");

        var sourceArray = subtitleSources?.ToArray();
        _log.Debug("RebuildSubtitleTracks: rebuilding with {Count} sources", sourceArray?.Length ?? 0);

        SubtitleTracks.Clear();

        if (sourceArray != null)
        {
            int idx = 0;
            foreach (var track in sourceArray)
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
                _log.Trace("RebuildSubtitleTracks: added track id={Id}, lang={Lang}, enabled={Enabled}, forced={Forced}",
                    trackId, track.Language, track.IsEnabled, track.IsForced);
                idx++;
            }
        }

        // + Add Subtitles… action button (after real tracks, before None)
        SubtitleTracks.Add(new TrackMenuItem("+ Add Subtitles\u2026", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));

        IsSubtitleEnabled = sourceArray?.Any(t => t.IsEnabled) ?? false;

        // Update bitmap/text detection
        var selected = SubtitleTracks.FirstOrDefault(t => t.IsSelected && !t.IsPseudoEntry);
        HasTextSubtitles = selected?.Source == null || !selected.Source.IsBitmap;

        // ── Auto-enable forced subtitle tracks ──
        // If no track is currently enabled and this is a fresh media load
        // (no user overrides yet), look for a forced track and auto-select it.
        if (!_sessionOverride && sourceArray != null && !IsSubtitleEnabled)
        {
            var forcedTrack = sourceArray.FirstOrDefault(s => s.IsForced);
            if (forcedTrack != null && int.TryParse(forcedTrack.PathOrId, out var forcedId))
            {
                _log.Info("RebuildSubtitleTracks: auto-selecting forced track id={Id}, lang={Lang}", forcedId, forcedTrack.Language);
                var forcedItem = SubtitleTracks.FirstOrDefault(t => t.TrackIndex == forcedId);
                if (forcedItem != null)
                {
                    forcedItem.IsSelected = true;
                    _player.SelectSubtitleTrack(forcedId);
                    CurrentSubtitleTrackId = forcedId;
                    IsSubtitleEnabled = true;
                    HasTextSubtitles = !forcedTrack.IsBitmap;
                }
            }
            else
            {
                _log.Trace("RebuildSubtitleTracks: no forced track found — {Count} sources scanned", sourceArray.Length);
            }
        }
        else
        {
            _log.Trace("RebuildSubtitleTracks: forced auto-skip (sessionOverride={SO}, enabled={Enabled})",
                _sessionOverride, IsSubtitleEnabled);
        }

        // Notify listeners (e.g. flyout) that the track list changed
        OnPropertyChanged(nameof(SubtitleTracks));
    }

    private void UpdateSubtitleTrackSelection(int selectedId)
    {
        _log.Trace("UpdateSubtitleTrackSelection: selectedId={SelectedId}", selectedId);
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
        _log.Info("OnSelectSubtitle: track='{DisplayName}', id={TrackIdx}", item.DisplayName, item.TrackIndex);
        _sessionOverride = true;

        if (item.DisplayName.Contains("Add Subtitles"))
        {
            _log.Debug("OnSelectSubtitle: triggering file picker");
            _ = OnAddSubtitleAsync();
            return;
        }

        if (item.DisplayName == "None" || item.TrackIndex == -2)
        {
            _log.Debug("OnSelectSubtitle: disabling subtitles (None)");
            _player.SelectSubtitleTrack(-1);
            CurrentSubtitleTrackId = -1;
            IsSubtitleEnabled = false;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            MarkDirty();
            TrackChangedMessage?.Invoke(item.DisplayName);
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _log.Debug("OnSelectSubtitle: selecting track id={TrackId}", item.TrackIndex);
            _player.SelectSubtitleTrack(item.TrackIndex);
            CurrentSubtitleTrackId = item.TrackIndex;
            IsSubtitleEnabled = true;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            MarkDirty();
            TrackChangedMessage?.Invoke(item.DisplayName);
        }
        else
        {
            _log.Warning("OnSelectSubtitle: unexpected track index {TrackIdx} for '{DisplayName}'", item.TrackIndex, item.DisplayName);
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

        // Avalonia #18969: dismiss flyout BEFORE opening native file dialog,
        // or the Windows message pump deadlocks on the still-open flyout.
        if (DismissFlyoutAsync != null)
            await DismissFlyoutAsync();

        try
        {
            var path = await RequestSubtitleFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                // DispatchAddExternalSubtitlesAsync will add the file to mpv,
                // refresh the track list on the UI thread, and auto-select the
                // newly added track so it appears on screen immediately.
                await DispatchAddExternalSubtitlesAsync(new List<string> { path });
            }
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            _log.Error(ex, "AddSubtitleAsync failed");
        }
    }

    /// <summary>
    /// Handles the full async pipeline for adding external subtitle files:
    /// 1. Add to mpv on a background thread
    /// 2. Wait for mpv's TrackListChanged event (sub-add is async — polling
    ///    SubtitleSources immediately returns 0 because mpv hasn't processed
    ///    the command yet)
    /// 3. Auto-select the first track on UI thread
    ///    (RebuildSubtitleTracks is called naturally by OnSubtitleTrackListChanged)
    /// </summary>
    /// <param name="ct">Cancellation token for the mpv TrackListChanged wait.</param>
    private async Task DispatchAddExternalSubtitlesAsync(List<string> externalFiles, CancellationToken ct = default)
    {
        try
        {
            var knownTrackIds = SubtitleTracks
                .Where(t => !t.IsPseudoEntry && t.TrackIndex >= 0)
                .Select(t => t.TrackIndex)
                .ToHashSet();

            // ═══ IMPORTANT ═══
            // With the mpv render API, ALL mpv calls (including mpv_command and
            // mpv_get_property) must originate from the same thread that drives
            // the event loop (EventLoop).  Calling them from a random threadpool
            // thread silently breaks property-change notifications and can return
            // stale/empty data.
            //
            // Previous attempts used Task.Run + poll, but SubtitleSources always
            // returned 0 because GetString("track-list") was called off the
            // event-loop thread.
            //
            // Fix: keep everything on the caller's thread.  mpv_command is
            // synchronous (blocks until mpv processes the command), and the
            // TrackListChanged event will fire on the event-loop thread via
            // handlePropertyChange → TrackListChanged.
            // ─────────────────

            // Subscribe before calling sub-add so we don't miss the event
            var tcs = new TaskCompletionSource<SubtitleSource[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<TrackListChangedEventArgs>? handler = null;
            handler = (_, args) =>
            {
                _player.TrackListChanged -= handler;
                tcs.TrySetResult(args.SubtitleTracks?.ToArray() ?? Array.Empty<SubtitleSource>());
            };
            _player.TrackListChanged += handler;

            // Add subtitles synchronously (no Task.Run — see note above)
            foreach (var subFile in externalFiles)
            {
                try
                {
                    _player.AddSubtitle(subFile);
                    _log.Debug("DispatchAddExternal: added {SubFile}", subFile);
                }
                catch (Exception ex)
                {
                    _log.Warning("DispatchAddExternal: failed to add {SubFile}: {Error}", subFile, ex.Message);
                }
            }

            // Wait for mpv's event loop to fire TrackListChanged
            var timeout = TimeSpan.FromSeconds(3);
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            delayCts.CancelAfter(timeout);
            bool eventReceived = false;
            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, delayCts.Token));
                eventReceived = tcs.Task.IsCompleted && !tcs.Task.IsFaulted;
            }
            catch (OperationCanceledException)
            {
                // Timed out or cancelled
            }
            var latestTracks = Array.Empty<SubtitleSource>();
            if (eventReceived)
            {
                latestTracks = await tcs.Task;
                _log.Debug("DispatchAddExternal: TrackListChanged received");
            }
            else
            {
                _log.Warning("DispatchAddExternal: TrackListChanged timed out");
                _player.TrackListChanged -= handler;

                latestTracks = _player.SubtitleSources ?? Array.Empty<SubtitleSource>();
                if (latestTracks.Length > 0)
                    _log.Debug("DispatchAddExternal: recovered {Count} subtitle tracks via player snapshot", latestTracks.Length);
            }

            int? preferredTrackId = null;
            foreach (var track in latestTracks)
            {
                if (track.IsEnabled && int.TryParse(track.PathOrId, out var parsedId) && !knownTrackIds.Contains(parsedId))
                {
                    preferredTrackId = parsedId;
                    break;
                }
            }

            if (preferredTrackId == null)
            {
                foreach (var track in latestTracks)
                {
                    if (int.TryParse(track.PathOrId, out var parsedId) && !knownTrackIds.Contains(parsedId))
                    {
                        preferredTrackId = parsedId;
                        break;
                    }
                }
            }

            if (preferredTrackId == null)
            {
                foreach (var track in latestTracks)
                {
                    if (track.IsEnabled && int.TryParse(track.PathOrId, out var parsedId))
                    {
                        preferredTrackId = parsedId;
                        break;
                    }
                }
            }

            if (preferredTrackId == null)
            {
                foreach (var track in latestTracks.Reverse())
                {
                    if (int.TryParse(track.PathOrId, out var parsedId))
                    {
                        preferredTrackId = parsedId;
                        break;
                    }
                }
            }

            // Rebuild from the latest mpv snapshot, then select the actual new track.
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (latestTracks.Length > 0)
                    RebuildSubtitleTracks(latestTracks);

                if (preferredTrackId is int trackId)
                {
                    var preferredItem = SubtitleTracks.FirstOrDefault(t => !t.IsPseudoEntry && t.TrackIndex == trackId);
                    if (preferredItem != null)
                    {
                        _log.Debug("DispatchAddExternal: auto-selecting track id={TrackId} '{Track}'", trackId, preferredItem.DisplayName);
                        preferredItem.SelectCommand.Execute(preferredItem);
                    }
                    else
                    {
                        _log.Warning("DispatchAddExternal: preferred track id={TrackId} not found in menu after rebuild", trackId);
                    }
                }
                else
                {
                    _log.Debug("DispatchAddExternal: no real track to select after add");
                }
            });
        }
        catch (Exception ex)
        {
            _log.Warning("Failed to load external sub(s): {Error}", ex.Message);
        }
    }

    /// <summary>Load external subtitle directly (drag-drop, automation).</summary>
    public async Task LoadExternalSubtitleAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        // ── Dedup: skip if this file is already loaded ──
        var normalizedPath = System.IO.Path.GetFullPath(filePath);
        var alreadyLoaded = SubtitleTracks.Any(t =>
            t.Source != null &&
            t.Source.IsExternal &&
            !string.IsNullOrWhiteSpace(t.Source.ExternalFilename) &&
            System.IO.Path.GetFullPath(t.Source.ExternalFilename).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (alreadyLoaded)
        {
            _log.Info("LoadExternalSubtitleAsync: skipping duplicate {File}", filePath);
            return;
        }

        _sessionOverride = true;
        await DispatchAddExternalSubtitlesAsync(new List<string> { filePath }, ct);
    }

    /// <summary>Legacy synchronous wrapper for tests/automation: starts async load and returns immediately.</summary>
    public void LoadExternalSubtitle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        _sessionOverride = true;
        _ = DispatchAddExternalSubtitlesAsync(new List<string> { filePath });
    }

    /// <summary>Select a subtitle track by its ID. Used for session restore.</summary>
    public void SelectSubtitleTrackById(int trackId)
    {
        // Snapshot to avoid "Collection was modified" from concurrent UI thread modifications
        var track = SubtitleTracks.ToArray().FirstOrDefault(t => t.TrackIndex == trackId && !t.IsPseudoEntry);
        if (track != null && track.SelectCommand?.CanExecute(track) == true)
            track.SelectCommand.Execute(track);
        else
            CurrentSubtitleTrackId = trackId;
    }

    /// <summary>Cycle to the next subtitle track. Wraps around. J key.</summary>
    public void CycleSubtitleTrackForward()
    {
        var tracks = SubtitleTracks.Where(t => !t.IsPseudoEntry && t.TrackIndex >= 0).ToList();
        if (tracks.Count == 0)
        {
            _log.Trace("CycleSubtitleTrackForward: no real tracks to cycle");
            return;
        }

        int currentIdx = tracks.FindIndex(t => t.TrackIndex == _currentSubtitleTrackId);
        int nextIdx = (currentIdx + 1) % tracks.Count;
        _log.Debug("CycleSubtitleTrackForward: current={Current} (idx={Idx}), next={Next} (idx={NIdx})",
            _currentSubtitleTrackId, currentIdx, tracks[nextIdx].TrackIndex, nextIdx);
        tracks[nextIdx].SelectCommand.Execute(tracks[nextIdx]);
    }

    /// <summary>Cycle to the previous subtitle track. Wraps around. Shift+J key.</summary>
    public void CycleSubtitleTrackBackward()
    {
        var tracks = SubtitleTracks.Where(t => !t.IsPseudoEntry && t.TrackIndex >= 0).ToList();
        if (tracks.Count == 0)
        {
            _log.Trace("CycleSubtitleTrackBackward: no real tracks to cycle");
            return;
        }

        int currentIdx = tracks.FindIndex(t => t.TrackIndex == _currentSubtitleTrackId);
        int prevIdx = currentIdx <= 0 ? tracks.Count - 1 : currentIdx - 1;
        _log.Debug("CycleSubtitleTrackBackward: current={Current} (idx={Idx}), prev={Prev} (idx={PIdx})",
            _currentSubtitleTrackId, currentIdx, tracks[prevIdx].TrackIndex, prevIdx);
        tracks[prevIdx].SelectCommand.Execute(tracks[prevIdx]);
    }

    // ═══════════════════════════════════════════════
    //  Persistence
    // ═══════════════════════════════════════════════

    /// <summary>Mark settings as dirty and start debounce timer.</summary>
    private void MarkDirty()
    {
        _settingsDirty = true;

        // Lazy create save timer on first dirty mark
        if (_saveTimer == null)
        {
            _saveTimer = new System.Timers.Timer(SaveDebounceMs) { AutoReset = false };
            _saveTimer.Elapsed += (_, _) => FlushSave();
            _log.Trace("MarkDirty: created save timer ({SaveDebounceMs}ms)", SaveDebounceMs);
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Immediately flush dirty settings to disk. Called on file close / app exit.</summary>
    public void OnFileClosing()
    {
        _log.Info("OnFileClosing: cancelling pending auto-detect and flushing settings");
        _mediaOpenCts?.Cancel();
        _mediaOpenCts?.Dispose();
        _mediaOpenCts = null;

        FlushSave();
    }

    private void FlushSave()
    {
        _saveTimer?.Stop();

        if (!_settingsDirty || string.IsNullOrWhiteSpace(_currentMediaPath))
        {
            _log.Trace("FlushSave: skipped (dirty={Dirty}, path={Path})", _settingsDirty, _currentMediaPath);
            return;
        }

        _settingsDirty = false;
        _log.Debug("FlushSave: saving per-file settings for {MediaPath}", _currentMediaPath);

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
        _log.Info("ResetAllSubtitles: resetting all subtitle settings to defaults");
        var defaults = _store.LoadDefaults();

        SubtitleDelay = (float)defaults.Style.Delay;
        SubtitlePosition = defaults.Style.Position;
        SubtitleFontScale = defaults.Style.FontScale;
        SubtitleBorderSize = defaults.Style.BorderSize;
        SubtitleShadowOffset = defaults.Style.ShadowOffset;
        SubtitleOpacity = defaults.Style.Opacity;
        SubtitleBlur = defaults.Style.Blur;
        SubtitleBold = defaults.Style.Bold;
        SubtitleFont = defaults.Style.Font;
        SubtitleColor = defaults.Style.Color;
        _sessionOverride = false;

        if (!string.IsNullOrWhiteSpace(_currentMediaPath))
        {
            _log.Debug("ResetAllSubtitles: deleting per-file settings for {MediaPath}", _currentMediaPath);
            _store.DeletePerFile(_currentMediaPath);
        }
    }

    // ═══════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════

    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        return Cine.Avalonia.Helpers.TrackDisplayHelper.FormatTrack(TrackType.Subtitle, track);
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

        if (_trackListChangedHandler != null)
            _player.TrackListChanged -= _trackListChangedHandler;
        if (_subPropHandler != null)
            _player.SubtitlePropertyChanged -= _subPropHandler;
        _player.Opened -= OnPlayerOpened;
        _player.Error -= OnPlayerError;
    }
}
