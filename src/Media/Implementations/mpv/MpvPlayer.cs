using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Media.Implementations;

public sealed class MpvPlayer : IMediaPlayer, IDisposable
{
    private readonly object _gate = new();
    private IntPtr _mpv;
    private bool _initialized;
    private bool _disposed;
    private string _currentPath = string.Empty;
    private PlaybackState _state = PlaybackState.Stopped;

    private double _volume = 50;
    private bool _isMuted;
    private double _speed = 1.0;

    private float _audioDelay;
    private float _subtitleDelay;

    private LoopMode _loopMode = LoopMode.NoLoop;
    private bool _isShuffled;
    private readonly List<string> _playlist = new();
    private int _playlistPosition;

    private Task? _eventLoop;
    private CancellationTokenSource? _cts;
    private IntPtr _hwnd;
    private string? _pendingOpenPath;

    // Track whether a file is loaded (guards position polling)
    private bool _isFileLoaded;

    // Aspect ratio override (maps to mpv's video-aspect-override)
    private double _aspectOverride = -1; // -1 = auto/default

    private static readonly string DebugLogFile = Path.Combine(AppContext.BaseDirectory, "MpvPlayer.log");

    private static void DebugLog(string message)
    {
        try
        {
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MpvPlayer] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    public MpvPlayer()
    {
        DebugLog("MpvPlayer constructor called");
    }

    public void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        lock (_gate)
        {
            _currentPath = path;
            if (_playlist.Count == 0 || _playlistPosition < 0 || _playlistPosition >= _playlist.Count || _playlist[_playlistPosition] != path)
            {
                _playlist.Clear();
                _playlist.Add(path);
                _playlistPosition = 0;
                PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs(_playlist.ToArray(), _playlistPosition));
            }

            if (!_initialized)
            {
                _pendingOpenPath = path;
                _state = PlaybackState.Stopped;
                return;
            }
        }

        LoadFile(path, replace: true);
    }

    public void Play()
    {
        EnsureInitializedOrError("Play");
        SetFlag("pause", false);
        _state = PlaybackState.Playing;
    }

    public void Pause()
    {
        EnsureInitializedOrError("Pause");
        SetFlag("pause", true);
        _state = PlaybackState.Paused;
    }

    public void Stop()
    {
        if (!_initialized)
        {
            _state = PlaybackState.Stopped;
            return;
        }

        CommandInternal("stop");
        _state = PlaybackState.Stopped;
    }

    public PlaybackState State => _state;
    public bool IsPlaying => _state == PlaybackState.Playing;
    public string CurrentPath => _currentPath;

    public TimeSpan Position
    {
        get
        {
            if (!_initialized)
                return TimeSpan.Zero;

            var seconds = GetDouble("time-pos");
            if (seconds <= 0 || double.IsNaN(seconds))
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(seconds);
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (!_initialized)
                return TimeSpan.Zero;

            var seconds = GetDouble("duration");
            if (seconds <= 0 || double.IsNaN(seconds))
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(seconds);
        }
    }

    public void Seek(TimeSpan position)
    {
        EnsureInitializedOrError("Seek");
        var seconds = position.TotalSeconds;
        if (seconds < 0) seconds = 0;
        CommandInternal("seek", seconds.ToString("0.###", CultureInfo.InvariantCulture), "absolute+exact");
    }

    public void SeekForward(double seconds) => Seek(Position + TimeSpan.FromSeconds(seconds));
    public void SeekBackward(double seconds) => Seek(Position - TimeSpan.FromSeconds(seconds));

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, VolumeMax);
            if (_initialized)
                SetDouble("volume", _volume);
            VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(_volume));
        }
    }

    public double VolumeMax => 150;

    public bool IsMuted
    {
        get => _isMuted;
        set => Mute(value);
    }

    public void Mute(bool isMuted)
    {
        _isMuted = isMuted;
        if (_initialized)
            SetFlag("mute", _isMuted);
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(_isMuted));
    }

    public void IncreaseVolume() => Volume = Math.Min(VolumeMax, Volume + 5);
    public void DecreaseVolume() => Volume = Math.Max(0, Volume - 5);
    public void ToggleMute() => Mute(!_isMuted);

    public float AudioDelay
    {
        get => _audioDelay;
        set
        {
            _audioDelay = value;
            if (_initialized)
                SetDouble("audio-delay", _audioDelay);
        }
    }

    public void IncreaseAudioDelay() => AudioDelay += 0.05f;
    public void DecreaseAudioDelay() => AudioDelay -= 0.05f;

    public double Speed
    {
        get => _speed;
        set => SetSpeed(value);
    }

    public void SetSpeed(double speed)
    {
        _speed = Math.Clamp(speed, 0.1, 8.0);
        if (_initialized)
            SetDouble("speed", _speed);
    }

    public void ResetSpeed() => SetSpeed(1.0);
    public void IncreaseSpeed() => SetSpeed(_speed + 0.1);
    public void DecreaseSpeed() => SetSpeed(_speed - 0.1);

    public string[] Playlist => _playlist.ToArray();

    public int PlaylistPosition
    {
        get => _playlistPosition;
        set
        {
            if (_playlist.Count == 0)
                return;

            var clamped = Math.Clamp(value, 0, _playlist.Count - 1);
            if (_playlistPosition == clamped)
                return;

            _playlistPosition = clamped;
            PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs(_playlist.ToArray(), _playlistPosition));
            Open(_playlist[_playlistPosition]);
        }
    }

    public bool IsShuffled
    {
        get => _isShuffled;
        set
        {
            _isShuffled = value;
            PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs(_playlist.ToArray(), _playlistPosition));
        }
    }

    public LoopMode LoopMode
    {
        get => _loopMode;
        set
        {
            var previous = _loopMode;
            _loopMode = value;
            ApplyLoopMode();
            LoopChangedEvent?.Invoke(this, new LoopChangedEventArgs(_loopMode, previous));
        }
    }

    public void AddToPlaylist(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _playlist.Add(path);
        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs(_playlist.ToArray(), _playlistPosition));

        if (_initialized)
            CommandInternal("loadfile", path, "append-play");
    }

    public void NextPlaylistItem()
    {
        if (_playlist.Count == 0)
            return;

        var next = _playlistPosition + 1;
        if (next >= _playlist.Count)
        {
            if (_loopMode == LoopMode.Playlist)
                next = 0;
            else
                return;
        }

        PlaylistPosition = next;
    }

    public void PreviousPlaylistItem()
    {
        if (_playlist.Count == 0)
            return;

        var prev = _playlistPosition - 1;
        if (prev < 0)
        {
            if (_loopMode == LoopMode.Playlist)
                prev = _playlist.Count - 1;
            else
                return;
        }

        PlaylistPosition = prev;
    }

    public void ToggleLoopFile() => LoopMode = _loopMode == LoopMode.File ? LoopMode.NoLoop : LoopMode.File;
    public void ToggleLoopPlaylist() => LoopMode = _loopMode == LoopMode.Playlist ? LoopMode.NoLoop : LoopMode.Playlist;

    public int CurrentSubtitleTrack
    {
        get => (int)GetInt64("sid");
        set => SetInt64("sid", value);
    }
    public SubtitleSource[] SubtitleSources
    {
        get
        {
            if (!_initialized)
                return Array.Empty<SubtitleSource>();

            var json = GetString("track-list");
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return Array.Empty<SubtitleSource>();

            try
            {
                return ParseTrackList(json);
            }
            catch
            {
                return Array.Empty<SubtitleSource>();
            }
        }
    }
    public void AddSubtitle(string path) => CommandInternal("sub-add", path, "select");
    public void SelectSubtitleTrack(int trackIndex) => SetInt64("sid", trackIndex);
    public void SelectAudioTrack(int trackIndex) => SetInt64("aid", trackIndex);
    public void CycleSubtitleTrack() => CommandInternal("cycle", "sid");

    public float SubtitleDelay
    {
        get => _subtitleDelay;
        set
        {
            _subtitleDelay = value;
            if (_initialized)
                SetDouble("sub-delay", _subtitleDelay);
        }
    }

    public void IncreaseSubtitleDelay() => SubtitleDelay += 0.05f;
    public void DecreaseSubtitleDelay() => SubtitleDelay -= 0.05f;

    public int SubtitlePosition { get; set; }
    public void SetSubtitlePosition(int position) => SubtitlePosition = position;

    public double Zoom
    {
        get => GetDouble("video-zoom");
        set => SetDouble("video-zoom", value);
    }

    public double AspectRatio
    {
        get => _aspectOverride;
        set
        {
            _aspectOverride = value;
            if (_initialized)
                SetDouble("video-aspect-override", value);
        }
    }

    public double Contrast
    {
        get => GetDouble("contrast");
        set => SetDouble("contrast", value);
    }

    public double Brightness
    {
        get => GetDouble("brightness");
        set => SetDouble("brightness", value);
    }

    public double Gamma
    {
        get => GetDouble("gamma");
        set => SetDouble("gamma", value);
    }

    public double Saturation
    {
        get => GetDouble("saturation");
        set => SetDouble("saturation", value);
    }

    public double Hue
    {
        get => GetDouble("hue");
        set => SetDouble("hue", value);
    }

    public void IncreaseContrast() => Contrast += 1;
    public void DecreaseContrast() => Contrast -= 1;
    public void IncreaseBrightness() => Brightness += 1;
    public void DecreaseBrightness() => Brightness -= 1;
    public void IncreaseGamma() => Gamma += 1;
    public void DecreaseGamma() => Gamma -= 1;
    public void IncreaseSaturation() => Saturation += 1;
    public void DecreaseSaturation() => Saturation -= 1;
    public void IncreaseHue() => Hue += 1;
    public void DecreaseHue() => Hue -= 1;

    public int CurrentChapter => (int)GetInt64("chapter");

    public ChapterInfo[] ChapterList
    {
        get
        {
            if (!_initialized)
                return Array.Empty<ChapterInfo>();

            var json = GetString("chapter-list");
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return Array.Empty<ChapterInfo>();

            try
            {
                var chapters = ParseChapterList(json);
                return chapters ?? Array.Empty<ChapterInfo>();
            }
            catch
            {
                return Array.Empty<ChapterInfo>();
            }
        }
    }

    public void NextChapter() => CommandInternal("add", "chapter", "1");
    public void PreviousChapter() => CommandInternal("add", "chapter", "-1");

    public bool IsFullscreen
    {
        get => GetFlag("fullscreen");
        set => SetFullscreen(value);
    }

    public void ToggleFullscreen() => SetFullscreen(!IsFullscreen);

    public void SetFullscreen(bool fullscreen)
    {
        var prev = GetFlag("fullscreen");
        SetFlag("fullscreen", fullscreen);
        FullscreenChangedEvent?.Invoke(this, new FullscreenChangedEventArgs(fullscreen, prev));
    }

    public void NextFrame() => CommandInternal("frame-step");
    public void PreviousFrame() => CommandInternal("frame-back-step");

    public void TakeScreenshot(string outputPath, bool includeSubtitles = true)
    {
        EnsureInitializedOrError("TakeScreenshot");
        CommandInternal("screenshot-to-file", outputPath, includeSubtitles ? "subtitles" : "video");
    }

    public void ScreenshotWithSubtitles() => TakeScreenshot(GetDefaultScreenshotPath(), includeSubtitles: true);
    public void ScreenshotWithoutSubtitles() => TakeScreenshot(GetDefaultScreenshotPath(), includeSubtitles: false);

    public void InitializeRenderer(IntPtr hwnd)
    {
        DebugLog($"InitializeRenderer called with hwnd={hwnd}");
        if (_disposed)
        {
            DebugLog("InitializeRenderer: _disposed=true, returning");
            return;
        }

        if (_initialized)
        {
            DebugLog("InitializeRenderer: already initialized, returning");
            return;
        }

        _hwnd = hwnd;

        if (!MpvInterop.IsAvailable)
        {
            Error?.Invoke(this, "libmpv is not available (expected libmpv-2.dll / mpv-2.dll on PATH or next to the executable).");
            DebugLog("InitializeRenderer: MpvInterop.IsAvailable=false");
            _state = PlaybackState.Stopped;
            return;
        }

        lock (_gate)
        {
            _mpv = MpvNative.mpv_create();
            DebugLog($"InitializeRenderer: mpv_create returned {_mpv}");
            if (_mpv == IntPtr.Zero)
            {
                Error?.Invoke(this, "mpv_create failed.");
                return;
            }

            SetOptionString("terminal", "no");
            SetOptionString("msg-level", "all=warn");
            SetOptionString("keep-open", "yes");
            SetOptionString("keep-open-pause", "no");
            SetOptionString("osc", "no");
            SetOptionString("vo", "gpu");
            SetOptionString("gpu-context", "d3d11");
            SetOptionString("hwdec", "auto-safe");
            SetOptionString("volume-max", "150");

            // HQ scaler defaults (parity with Python reference)
            SetOptionString("scale", "spline36");
            SetOptionString("cscale", "spline36");
            SetOptionString("dscale", "mitchell");
            SetOptionString("correct-downscaling", "yes");
            SetOptionString("deband", "yes");
            SetOptionString("deband-iterations", "1");
            SetOptionString("dither-depth", "auto");

            SetOptionInt64("wid", hwnd.ToInt64());

            var initErr = MpvNative.mpv_initialize(_mpv);
            if (initErr < 0)
            {
                Error?.Invoke(this, $"mpv_initialize failed: {MpvNative.GetError(initErr)}");
                MpvNative.mpv_terminate_destroy(_mpv);
                _mpv = IntPtr.Zero;
                return;
            }

            _initialized = true;
        }

        // Observe track-list and chapter-list via push-based events
        // (time-pos uses polling instead — see event loop for details)
        MpvNative.mpv_observe_property(_mpv, 0, "track-list", MpvNative.mpv_format.MPV_FORMAT_NODE);
        MpvNative.mpv_observe_property(_mpv, 0, "chapter-list", MpvNative.mpv_format.MPV_FORMAT_NODE);

        SetDouble("volume", _volume);
        SetFlag("mute", _isMuted);
        SetDouble("speed", _speed);
        SetDouble("audio-delay", _audioDelay);
        SetDouble("sub-delay", _subtitleDelay);
        ApplyLoopMode();

        StartEventLoop();

        var pending = _pendingOpenPath;
        _pendingOpenPath = null;
        if (!string.IsNullOrWhiteSpace(pending))
            LoadFile(pending, replace: true);
    }

    public bool UseNativeRendering { get; set; } = true;

    public void NotifyResize(int width, int height)
    {
    }

    public void Command(string command, params string[] args)
    {
        EnsureInitializedOrError("Command");
        if (string.IsNullOrWhiteSpace(command))
            return;
        if (args == null || args.Length == 0)
        {
            CommandInternal(command);
            return;
        }

        var all = new string[args.Length + 1];
        all[0] = command;
        Array.Copy(args, 0, all, 1, args.Length);
        CommandInternal(all);
    }

    public event EventHandler? Opened;
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;
    public event EventHandler<ChapterListChangedEventArgs>? ChapterListChanged;
    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<TrackListChangedEventArgs>? TrackListChanged;
    public event EventHandler<FullscreenChangedEventArgs>? FullscreenChangedEvent;
    public event EventHandler<LoopChangedEventArgs>? LoopChangedEvent;
    public event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;
    public event EventHandler<string>? Error;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();
        try { _eventLoop?.Wait(250); } catch { }
        _cts?.Dispose();
        _cts = null;

        lock (_gate)
        {
            if (_mpv != IntPtr.Zero)
            {
                try { MpvNative.mpv_terminate_destroy(_mpv); } catch { }
                _mpv = IntPtr.Zero;
            }
            _initialized = false;
        }
    }

    private void StartEventLoop()
    {
        DebugLog("StartEventLoop called");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _eventLoop = Task.Run(() => EventLoop(token), token);
    }

    private void EventLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_disposed)
        {
            if (!_initialized || _mpv == IntPtr.Zero)
            {
                Thread.Sleep(25);
                continue;
            }

            var evPtr = MpvNative.mpv_wait_event(_mpv, 0.1);
            if (evPtr != IntPtr.Zero)
            {
                var ev = Marshal.PtrToStructure<MpvNative.mpv_event>(evPtr);
                switch ((MpvNative.mpv_event_id)ev.event_id)
                {
                    case MpvNative.mpv_event_id.MPV_EVENT_FILE_LOADED:
                        // mpv may briefly report pause=true after loading.
                        // Force unpause so playback starts immediately.
                        _state = PlaybackState.Playing;
                        if (GetFlag("pause"))
                        {
                            Play();
                        }
                        _isFileLoaded = true;
                        Opened?.Invoke(this, EventArgs.Empty);
                        break;
                    case MpvNative.mpv_event_id.MPV_EVENT_START_FILE:
                        break;
                    case MpvNative.mpv_event_id.MPV_EVENT_END_FILE:
                        _state = PlaybackState.Stopped;
                        _isFileLoaded = false;
                        break;
                    case MpvNative.mpv_event_id.MPV_EVENT_PAUSE:
                        _state = PlaybackState.Paused;
                        break;
                    case MpvNative.mpv_event_id.MPV_EVENT_UNPAUSE:
                        _state = PlaybackState.Playing;
                        break;
                    case MpvNative.mpv_event_id.MPV_EVENT_SHUTDOWN:
                        _state = PlaybackState.Stopped;
                        return;
                    case MpvNative.mpv_event_id.MPV_EVENT_PROPERTY_CHANGE:
                        HandlePropertyChange(ev);
                        break;
                }
            }

            // Poll time-pos every loop iteration (~100ms).
            // mpv_observe_property("time-pos") is unreliable on Windows (known mpv bug #4195
            // — property change coalescing causes frame-based updates to be skipped).
            // Direct polling via GetDouble is the reliable cross-platform approach
            // used by Mpv.NET-lib and other production C# mpv embeddings.
            if (_isFileLoaded)
            {
                var pos = GetDouble("time-pos");
                if (pos >= 0 && !double.IsNaN(pos))
                {
                    PositionChanged?.Invoke(this, new PositionChangedEventArgs(TimeSpan.FromSeconds(pos)));
                }
            }
        }
    }

    private void EnsureInitializedOrError(string action)
    {
        if (_initialized)
            return;

        if (!MpvInterop.IsAvailable)
            Error?.Invoke(this, $"{action} failed: libmpv is not available.");
        else
            Error?.Invoke(this, $"{action} failed: player is not initialized (renderer HWND not set yet).");
    }

    private void LoadFile(string path, bool replace)
    {
        DebugLog($"LoadFile called with path={path}, replace={replace}");
        EnsureInitializedOrError("Open");
        if (!_initialized)
        {
            DebugLog("LoadFile: not initialized, returning");
            return;
        }

        CommandInternal("loadfile", path, replace ? "replace" : "append-play");
        _state = PlaybackState.Playing;
        DebugLog($"LoadFile: state set to Playing");
    }

    private void HandlePropertyChange(MpvNative.mpv_event ev)
    {
        if (ev.data == IntPtr.Zero)
            return;

        var prop = Marshal.PtrToStructure<MpvNative.mpv_event_property>(ev.data);
        var propName = Marshal.PtrToStringUTF8(prop.name) ?? "";

        switch (propName)
        {
            case "track-list":
                TrackListChanged?.Invoke(this, new TrackListChangedEventArgs(
                    Array.Empty<SubtitleSource>(),
                    Array.Empty<SubtitleSource>(),
                    SubtitleSources));
                break;
            case "chapter-list":
                var ch = ChapterList;
                ChapterListChanged?.Invoke(this, new ChapterListChangedEventArgs(ch));
                break;
            case "pause":
                var isPaused = GetFlag("pause");
                _state = isPaused ? PlaybackState.Paused : PlaybackState.Playing;
                break;
            case "core-idle":
                if (GetFlag("core-idle") && _state == PlaybackState.Playing)
                    _state = PlaybackState.Paused;
                break;
            case "eof-reached":
                if (GetFlag("eof-reached"))
                {
                    _state = PlaybackState.Stopped;
                    // Replay at EOF for continuous playback with keep-open
                    if (GetFlag("keep-open"))
                    {
                        Seek(TimeSpan.Zero);
                        Play();
                    }
                }
                break;
        }
    }

    private void ApplyLoopMode()
    {
        if (!_initialized)
            return;

        switch (_loopMode)
        {
            case LoopMode.File:
                SetString("loop-file", "inf");
                SetString("loop-playlist", "no");
                break;
            case LoopMode.Playlist:
                SetString("loop-file", "no");
                SetString("loop-playlist", "inf");
                break;
            default:
                SetString("loop-file", "no");
                SetString("loop-playlist", "no");
                break;
        }
    }

    private void CommandInternal(params string[] args)
    {
        if (!_initialized || _mpv == IntPtr.Zero)
            return;

        var argv = BuildUtf8Argv(args);
        try
        {
            var err = MpvNative.mpv_command(_mpv, argv);
            if (err < 0)
                Error?.Invoke(this, $"mpv_command failed: {MpvNative.GetError(err)} ({string.Join(" ", args)})");
        }
        finally
        {
            FreeUtf8Argv(argv, args.Length);
        }
    }

    private static IntPtr BuildUtf8Argv(string[] args)
    {
        var count = args.Length + 1;
        var argv = Marshal.AllocHGlobal(IntPtr.Size * count);

        for (int i = 0; i < args.Length; i++)
        {
            var strPtr = StringToHGlobalUtf8(args[i]);
            Marshal.WriteIntPtr(argv, i * IntPtr.Size, strPtr);
        }

        Marshal.WriteIntPtr(argv, args.Length * IntPtr.Size, IntPtr.Zero);
        return argv;
    }

    private static void FreeUtf8Argv(IntPtr argv, int argsLength)
    {
        for (int i = 0; i < argsLength; i++)
        {
            var ptr = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
        Marshal.FreeHGlobal(argv);
    }

    private static IntPtr StringToHGlobalUtf8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value + "\0");
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }

    private void SetOptionString(string name, string value)
    {
        if (_mpv == IntPtr.Zero)
            return;

        var err = MpvNative.mpv_set_option_string(_mpv, name, value);
        if (err < 0)
            Error?.Invoke(this, $"mpv_set_option_string failed: {name}={value} ({MpvNative.GetError(err)})");
    }

    private void SetOptionInt64(string name, long value)
    {
        if (_mpv == IntPtr.Zero)
            return;

        var v = value;
        var err = MpvNative.mpv_set_option(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_INT64, ref v);
        if (err < 0)
            Error?.Invoke(this, $"mpv_set_option failed: {name}={value} ({MpvNative.GetError(err)})");
    }

    private void SetString(string name, string value)
    {
        if (!_initialized)
            return;

        var err = MpvNative.mpv_set_property_string(_mpv, name, value);
        if (err < 0)
            Error?.Invoke(this, $"mpv_set_property_string failed: {name}={value} ({MpvNative.GetError(err)})");
    }

    private void SetDouble(string name, double value)
    {
        if (!_initialized)
            return;

        var v = value;
        var err = MpvNative.mpv_set_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_DOUBLE, ref v);
        if (err < 0)
            Error?.Invoke(this, $"mpv_set_property failed: {name}={value} ({MpvNative.GetError(err)})");
    }

    private void SetInt64(string name, long value)
    {
        if (!_initialized)
            return;

        var v = value;
        var err = MpvNative.mpv_set_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_INT64, ref v);
        if (err < 0)
            Error?.Invoke(this, $"mpv_set_property failed: {name}={value} ({MpvNative.GetError(err)})");
    }

    private void SetFlag(string name, bool value)
    {
        if (!_initialized)
            return;

        var v = value ? 1 : 0;
        var err = MpvNative.mpv_set_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_FLAG, ref v);
        if (err < 0)
            Error?.Invoke(this, $"mpv_set_property failed: {name}={value} ({MpvNative.GetError(err)})");
    }

    private double GetDouble(string name)
    {
        if (!_initialized || _mpv == IntPtr.Zero)
            return 0;

        var v = 0.0;
        var err = MpvNative.mpv_get_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_DOUBLE, ref v);
        return err < 0 ? 0 : v;
    }

    private long GetInt64(string name)
    {
        if (!_initialized || _mpv == IntPtr.Zero)
            return 0;

        long v = 0;
        var err = MpvNative.mpv_get_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_INT64, ref v);
        return err < 0 ? 0 : v;
    }

    private bool GetFlag(string name)
    {
        if (!_initialized || _mpv == IntPtr.Zero)
            return false;

        int v = 0;
        var err = MpvNative.mpv_get_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_FLAG, ref v);
        return err >= 0 && v != 0;
    }

    private static string GetDefaultScreenshotPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return System.IO.Path.Combine(dir, $"cine_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

    // ── JSON parsing for mpv property data ──

    private static SubtitleSource[] ParseTrackList(string json)
    {
        var tracks = JsonSerializer.Deserialize<JsonElement>(json);
        if (tracks.ValueKind != JsonValueKind.Array)
            return Array.Empty<SubtitleSource>();

        var result = new List<SubtitleSource>();
        foreach (var t in tracks.EnumerateArray())
        {
            var kind = t.TryGetProperty("type", out var kindProp) ? kindProp.GetString() ?? "" : "";
            var lang = t.TryGetProperty("lang", out var langProp) ? langProp.GetString() ?? "" : "";
            var title = t.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var selected = t.TryGetProperty("selected", out var selProp) && selProp.GetBoolean();
            var id = t.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1;

            var src = new SubtitleSource
            {
                PathOrId = id.ToString(),
                Language = lang,
                Type = kind,
                IsEnabled = selected
            };
            // Store title in Language if no language, so it's displayed
            if (string.IsNullOrWhiteSpace(lang) && !string.IsNullOrWhiteSpace(title))
                src.Language = title;

            result.Add(src);
        }
        return result.ToArray();
    }

    private static ChapterInfo[] ParseChapterList(string json)
    {
        var chapters = JsonSerializer.Deserialize<JsonElement>(json);
        if (chapters.ValueKind != JsonValueKind.Array)
            return Array.Empty<ChapterInfo>();

        var result = new List<ChapterInfo>();
        int idx = 0;
        foreach (var c in chapters.EnumerateArray())
        {
            var title = c.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
            var time = c.TryGetProperty("time", out var timeProp) ? timeProp.GetDouble() : 0;

            result.Add(new ChapterInfo
            {
                Title = string.IsNullOrWhiteSpace(title) ? $"Chapter {idx + 1}" : title,
                Index = idx,
                Time = time
            });
            idx++;
        }
        return result.ToArray();
    }

    // ── Property string getter via native mpv ──

    private string GetString(string name)
    {
        if (!_initialized || _mpv == IntPtr.Zero)
            return string.Empty;

        var err = MpvNative.mpv_get_property_string(_mpv, name, out var ptr);
        if (err < 0 || ptr == IntPtr.Zero)
            return string.Empty;

        var value = Marshal.PtrToStringUTF8(ptr);
        Marshal.FreeHGlobal(ptr);
        return value ?? string.Empty;
    }

    private static class MpvNative
    {
        internal enum mpv_event_id
        {
            MPV_EVENT_NONE = 0,
            MPV_EVENT_SHUTDOWN = 1,
            MPV_EVENT_LOG_MESSAGE = 2,
            MPV_EVENT_START_FILE = 6,
            MPV_EVENT_END_FILE = 7,
            MPV_EVENT_FILE_LOADED = 8,
            MPV_EVENT_PAUSE = 18,
            MPV_EVENT_UNPAUSE = 19,
            MPV_EVENT_PROPERTY_CHANGE = 24,
        }

        internal enum mpv_format
        {
            MPV_FORMAT_NONE = 0,
            MPV_FORMAT_STRING = 1,
            MPV_FORMAT_FLAG = 3,
            MPV_FORMAT_INT64 = 4,
            MPV_FORMAT_DOUBLE = 5,
            MPV_FORMAT_NODE = 6
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct mpv_event
        {
            public int event_id;
            public long error;
            public ulong reply_userdata;
            public IntPtr data;
        }

        // mpv_event_property structure for MPV_EVENT_PROPERTY_CHANGE
        [StructLayout(LayoutKind.Sequential)]
        internal struct mpv_event_property
        {
            public IntPtr name;   // const char*
            public mpv_format format;
            public IntPtr data;   // union of int64/double/string/ba/fb/keypress
        }

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_create();

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_initialize(IntPtr ctx);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_terminate_destroy(IntPtr ctx);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_command(IntPtr ctx, IntPtr args);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_option(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref long data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref double data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref int data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref long data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref double data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out IntPtr data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref int data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format, ref long data);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_observe_property(IntPtr ctx, ulong userdata, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, mpv_format format);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mpv_error_string(int error);

        internal static string GetError(int err)
        {
            if (err >= 0)
                return "success";
            var ptr = mpv_error_string(err);
            return ptr == IntPtr.Zero ? $"err={err}" : Marshal.PtrToStringUTF8(ptr) ?? $"err={err}";
        }
    }
}