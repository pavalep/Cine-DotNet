using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
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
    private bool _isRecoveringFromEof;

    // Aspect ratio override (maps to mpv's video-aspect-override)
    private double _aspectOverride = -1; // -1 = auto/default

    // OpenGL render API (ANGLE)
    private IntPtr _renderContext;
    private AngleGlContext? _angleContext;
    private MpvRenderNative.MpvRenderUpdateFn? _renderUpdateCallback;
    private int _renderFrameCount;
    private readonly ManualResetEventSlim _renderWakeup = new(false);

    /// <summary>
    /// Fired when a new video frame is available. Data is BGRA byte array.
    /// </summary>
    public event Action<byte[], int, int>? FrameRendered;

    // ANGLE — loaded once, shared across instances
    private static IntPtr _eglHandle;
    private static IntPtr _glesHandle;
    private static readonly object _angleLock = new();

    // Static get_proc_address callback — must be static so GC never collects it.
    // mpv holds the function pointer for the entire lifetime of the render context.
    // Uses MpvRenderNative.MpvGetProcAddressDelegate (Cdecl, void* ctx, string name)
    // which exactly matches the C callback signature.
    private static readonly MpvRenderNative.MpvGetProcAddressDelegate _glGetProcCbStatic = GlGetProcAddressStatic;

    private static unsafe void* GlGetProcAddressStatic(void* ctx, [MarshalAs(UnmanagedType.LPStr)] string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        // Ensure ANGLE DLL handles are loaded
        if (_glesHandle == IntPtr.Zero)
        {
            lock (_angleLock)
            {
                if (_glesHandle == IntPtr.Zero) _glesHandle = LoadLibrary("libGLESv2.dll");
                if (_eglHandle == IntPtr.Zero)  _eglHandle  = LoadLibrary("libEGL.dll");
            }
        }

        // 1. libGLESv2.dll
        if (_glesHandle != IntPtr.Zero)
        {
            var addr = GetProcAddress(_glesHandle, name);
            if (addr != IntPtr.Zero) return (void*)addr;
        }
        // 2. libEGL.dll
        if (_eglHandle != IntPtr.Zero)
        {
            var addr = GetProcAddress(_eglHandle, name);
            if (addr != IntPtr.Zero) return (void*)addr;
        }
        // 3. eglGetProcAddress (extension functions not exported directly)
        var egl = AngleInterop.eglGetProcAddress(name);
        return (void*)egl;
    }

    // Old GlGetProcAddressCallback removed — replaced by GlGetProcAddressStatic above.

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    private static readonly string DebugLogFile = CreateLogFilePath();

    private static string CreateLogFilePath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "MpvPlayer.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "MpvPlayer.log");
        }
    }

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
        SetPlaybackState(PlaybackState.Playing);
    }

    public void Pause()
    {
        EnsureInitializedOrError("Pause");
        SetFlag("pause", true);
        SetPlaybackState(PlaybackState.Paused);
    }

    public void Stop()
    {
        if (!_initialized)
        {
            _state = PlaybackState.Stopped;
            return;
        }

        CommandInternal("stop");
        SetPlaybackState(PlaybackState.Stopped);
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
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(isMuted ? 0 : _volume));
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
    public void AddSubtitle(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Subtitle path cannot be empty", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Subtitle file not found: {path}", path);

        // Try to detect encoding for text-based subtitle formats
        string? encodingArg = null;
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        if (ext == ".srt" || ext == ".vtt" || ext == ".sub" || ext == ".txt")
        {
            try
            {
                encodingArg = DetectSubtitleEncoding(path);
            }
            catch
            {
                // Fallback to default UTF-8
            }
        }

        if (encodingArg != null)
            CommandInternal("sub-add", path, "select", "auto", "--sub-codepage=" + encodingArg);
        else
            CommandInternal("sub-add", path, "select");
    }

    /// <summary>Detect subtitle file encoding by checking BOM and falling back to heuristics.</summary>
    private static string DetectSubtitleEncoding(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        if (fs.Length < 4)
            return "utf-8";

        var bom = reader.ReadBytes(4);

        // UTF-32 LE
        if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
            return "utf-32le";
        // UTF-32 BE
        if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
            return "utf-32be";
        // UTF-16 LE
        if (bom[0] == 0xFF && bom[1] == 0xFE)
            return "utf-16le";
        // UTF-16 BE
        if (bom[0] == 0xFE && bom[1] == 0xFF)
            return "utf-16be";
        // UTF-8 BOM
        if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return "utf-8";

        // No BOM - check for common Windows codepages by sampling
        // Default to UTF-8 with fallback encoding detection
        return "utf-8:cp1252";
    }
    public void AddAudio(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Audio path cannot be empty", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Audio file not found: {path}", path);

        CommandInternal("audio-add", path, "select");
    }
    public void SelectSubtitleTrack(int trackIndex)
    {
        if (trackIndex > 0)
        {
            SetFlag("sub-visibility", true);
            SetInt64("sid", trackIndex);
        }
        else
        {
            SetFlag("sub-visibility", false);
            SetInt64("sid", -1);
        }
    }
    public void SelectAudioTrack(int trackIndex) => SetInt64("aid", trackIndex);
    public void SelectVideoTrack(int trackIndex) => SetInt64("vid", trackIndex);
    public void CycleSubtitleTrack()
    {
        CommandInternal("cycle", "sid");
        // Ensure subtitles become visible when cycling (in case they were off)
        SetFlag("sub-visibility", true);
    }

    public AudioTrackInfo[] AudioSources
    {
        get
        {
            if (!_initialized)
                return Array.Empty<AudioTrackInfo>();

            var json = GetString("track-list");
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return Array.Empty<AudioTrackInfo>();

            try
            {
                return ParseAudioTrackList(json);
            }
            catch
            {
                return Array.Empty<AudioTrackInfo>();
            }
        }
    }

    public VideoTrackInfo[] VideoSources
    {
        get
        {
            if (!_initialized)
                return Array.Empty<VideoTrackInfo>();

            var json = GetString("track-list");
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return Array.Empty<VideoTrackInfo>();

            try
            {
                return ParseVideoTrackList(json);
            }
            catch
            {
                return Array.Empty<VideoTrackInfo>();
            }
        }
    }

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

    private int _subtitlePosition = 100;

    public int SubtitlePosition
    {
        get => _subtitlePosition;
        set
        {
            _subtitlePosition = Math.Clamp(value, 0, 200);
            if (_initialized)
                SetInt64("sub-pos", _subtitlePosition);
        }
    }
    public void SetSubtitlePosition(int position) => SubtitlePosition = position;

    public void SetSubtitleFontSize(double size)
    {
        if (_initialized)
            SetDouble("sub-font-size", size);
    }

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

    public byte[]? ScreenshotRaw(out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!_initialized || _mpv == IntPtr.Zero) return null;

        try
        {
            // Build argv: ["screenshot-raw"]
            var args = new[] { "screenshot-raw" };
            var argv = BuildUtf8Argv(args);

            try
            {
                var err = MpvNative.mpv_command_node(_mpv, argv, out var result);
                if (err < 0)
                    return null;

                try
                {
                    return ParseScreenshotNode(result, out width, out height);
                }
                finally
                {
                    MpvNative.mpv_free_node_contents(ref result);
                }
            }
            finally
            {
                FreeUtf8Argv(argv, args.Length);
            }
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ParseScreenshotNode(MpvNative.mpv_node node, out int width, out int height)
    {
        width = 0;
        height = 0;
        int stride = 0;
        byte[]? pixelData = null;

        if (node.format != MpvNative.mpv_format_node.MPV_FORMAT_NODE_MAP)
            return null;

        var list = Marshal.PtrToStructure<MpvNative.mpv_node_list>(node.u.list);
        if (list.num <= 0 || list.keys == IntPtr.Zero || list.values == IntPtr.Zero)
            return null;

        for (int i = 0; i < list.num; i++)
        {
            // Read key
            var keyPtr = Marshal.ReadIntPtr(list.keys, i * IntPtr.Size);
            var key = Marshal.PtrToStringUTF8(keyPtr) ?? "";

            // Read value node
            var valPtr = IntPtr.Add(list.values, i * Marshal.SizeOf<MpvNative.mpv_node>());
            var val = Marshal.PtrToStructure<MpvNative.mpv_node>(valPtr);

            switch (key)
            {
                case "w":
                    if (val.format == MpvNative.mpv_format_node.MPV_FORMAT_INT64)
                        width = (int)val.u.int64;
                    break;
                case "h":
                    if (val.format == MpvNative.mpv_format_node.MPV_FORMAT_INT64)
                        height = (int)val.u.int64;
                    break;
                case "stride":
                    if (val.format == MpvNative.mpv_format_node.MPV_FORMAT_INT64)
                        stride = (int)val.u.int64;
                    break;
                case "data":
                    if (val.format == MpvNative.mpv_format_node.MPV_FORMAT_BYTE_ARRAY)
                    {
                        var ba = Marshal.PtrToStructure<MpvNative.mpv_byte_array>(val.u.byte_array);
                        if (ba.data != IntPtr.Zero && ba.size > 0)
                        {
                            pixelData = new byte[ba.size];
                            Marshal.Copy(ba.data, pixelData, 0, (int)ba.size);
                        }
                    }
                    break;
            }
        }

        if (pixelData == null || width <= 0 || height <= 0)
            return null;

        return pixelData;
    }


    private int GetIntProperty(string name)
    {
        if (_mpv == IntPtr.Zero) return 0;
        long val = 0;
        var err = MpvNative.mpv_get_property(_mpv, name, MpvNative.mpv_format.MPV_FORMAT_INT64, ref val);
        return err >= 0 ? (int)val : 0;
    }

    /// <summary>
    /// Gets or sets whether to use high-quality rendering options (default).
    /// Set to <c>false</c> for PiP / low-quality secondary player instances.
    /// Must be set before calling <see cref="InitializeRenderer"/>.
    /// </summary>
    public bool HighQualityRendering { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to disable hardware acceleration and use software
    /// rendering. Default is <c>false</c> (hardware acceleration enabled).
    /// Must be set before calling <see cref="InitializeRenderer"/>.
    /// </summary>
    public bool UseSoftwareRendering { get; set; }

    /// <summary>
    /// Initialize mpv using native HWND-based rendering (used by PiP player).
    /// mpv creates its own D3D11 swap chain targeting the given HWND.
    /// </summary>
    public void InitializeRenderer(IntPtr hwnd)
    {
        DebugLog($"InitializeRenderer called with hwnd={hwnd} (PiP path)");
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

            var options = MpvConfig.GetFullOptions(HighQualityRendering, hwnd);
            if (UseSoftwareRendering)
            {
                options["hwdec"] = "no";
                options["gpu-context"] = "d3d11";
            }
            foreach (var kv in options)
                SetOptionString(kv.Key, kv.Value);

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

        MpvNative.mpv_observe_property(_mpv, 0, "track-list", MpvNative.mpv_format.MPV_FORMAT_NODE);
        MpvNative.mpv_observe_property(_mpv, 0, "chapter-list", MpvNative.mpv_format.MPV_FORMAT_NODE);

        SetDouble("volume", _volume);
        SetFlag("mute", _isMuted);
        SetDouble("speed", _speed);
        SetDouble("audio-delay", _audioDelay);
        SetDouble("sub-delay", _subtitleDelay);
        ApplyLoopMode();

        DebugLog("calling StartEventLoop");
        StartEventLoop();
        DebugLog("StartEventLoop completed");

        var pending = _pendingOpenPath;
        _pendingOpenPath = null;
        if (!string.IsNullOrWhiteSpace(pending))
            LoadFile(pending, replace: true);
    }

    /// <summary>
    /// Initialize mpv using the OpenGL render API via ANGLE (OpenGL ES over D3D11).
    /// We create our own ANGLE/EGL context, mpv renders into it, and we read pixels
    /// back for display in Avalonia. Controls and UI elements render on top naturally.
    /// Requires libmpv built with --enable-libmpv-render.
    /// Requires libEGL.dll + libGLESv2.dll (ANGLE from Chrome) deployed with the app.
    /// </summary>
    public void InitializeRendererOpenGL()
    {
        DebugLog($"InitializeRendererOpenGL called (render API via ANGLE)");
        DebugLog($"  → Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        if (_disposed || _initialized)
        {
            DebugLog("InitializeRendererOpenGL: disposed or already initialized");
            return;
        }

        if (!MpvInterop.IsAvailable)
        {
            DebugLog("InitializeRendererOpenGL: MpvInterop.IsAvailable=false");
            Error?.Invoke(this, "libmpv not available");
            _state = PlaybackState.Stopped;
            return;
        }

        // Step 1-5: Create mpv instance and initialize it on the calling thread.
        // GL context creation and mpv_render_context_create MUST happen on the
        // event loop thread (the same thread that calls mpv_render_context_render),
        // because EGL/ANGLE contexts are thread-affine. We'll do that in EventLoop.
        lock (_gate)
        {
            DebugLog("InitializeRendererOpenGL: [Step 1] Creating mpv instance...");
            _mpv = MpvNative.mpv_create();
            DebugLog($"InitializeRendererOpenGL:   → mpv_create={_mpv}");
            if (_mpv == IntPtr.Zero)
            {
                Error?.Invoke(this, "mpv_create failed");
                return;
            }

            DebugLog("InitializeRendererOpenGL: [Step 2] Setting options...");
            var options = MpvConfig.GetRenderApiOptions();
            foreach (var kv in options)
            {
                SetOptionString(kv.Key, kv.Value);
                DebugLog($"  → {kv.Key}={kv.Value}");
            }

            DebugLog("InitializeRendererOpenGL: [Step 3] mpv_initialize...");
            var initErr = MpvNative.mpv_initialize(_mpv);
            DebugLog($"  → mpv_initialize={initErr}");
            if (initErr < 0)
            {
                Error?.Invoke(this, $"mpv_initialize failed: {MpvNative.GetError(initErr)}");
                MpvNative.mpv_terminate_destroy(_mpv);
                _mpv = IntPtr.Zero;
                return;
            }

            // Mark _initialized = true so the event loop starts; ANGLE+render context
            // init completes on the event loop thread (see EventLoop → InitGlOnEventThread).
            _initialized = true;
        }

        DebugLog("InitializeRendererOpenGL: [Step 4] Observing properties...");
        MpvNative.mpv_observe_property(_mpv, 0, "track-list", MpvNative.mpv_format.MPV_FORMAT_NODE);
        MpvNative.mpv_observe_property(_mpv, 0, "chapter-list", MpvNative.mpv_format.MPV_FORMAT_NODE);

        SetDouble("volume", _volume);
        SetFlag("mute", _isMuted);
        SetDouble("speed", _speed);
        SetDouble("audio-delay", _audioDelay);
        SetDouble("sub-delay", _subtitleDelay);
        ApplyLoopMode();

        DebugLog("InitializeRendererOpenGL: [Step 5] Starting event loop (GL init happens there)...");
        StartEventLoop();
        DebugLog("InitializeRendererOpenGL: === Complete (GL init pending on event loop thread) ===");

        var pending = _pendingOpenPath;
        _pendingOpenPath = null;
        if (!string.IsNullOrWhiteSpace(pending))
            LoadFile(pending, replace: true);
    }

    /// <summary>
    /// Called once at the start of EventLoop to create the ANGLE GL context and
    /// mpv_render_context on the event loop thread. This is required because
    /// EGL/ANGLE contexts are thread-affine — the context must be current on the
    /// same thread that calls mpv_render_context_render.
    /// Uses unsafe fixed pointers (like the reference LibMpv-OpenGL implementation)
    /// to guarantee correct C ABI struct layout with no padding ambiguity.
    /// </summary>
    private unsafe bool InitGlOnEventThread()
    {
        DebugLog($"InitGlOnEventThread: → Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        try
        {
            _angleContext = new AngleGlContext(1920, 1080);
        }
        catch (Exception ex)
        {
            DebugLog($"InitGlOnEventThread: ANGLE context creation FAILED: {ex.Message}");
            Error?.Invoke(this, $"Failed to create ANGLE GL context: {ex.Message}");
            return false;
        }
        DebugLog("InitGlOnEventThread: ANGLE context created and current on event loop thread");

        // Pre-load ANGLE DLL handles for the proc address callback
        lock (_angleLock)
        {
            if (_glesHandle == IntPtr.Zero) _glesHandle = LoadLibrary("libGLESv2.dll");
            if (_eglHandle == IntPtr.Zero)  _eglHandle  = LoadLibrary("libEGL.dll");
        }
        DebugLog($"InitGlOnEventThread: GLES handle=0x{_glesHandle:X} EGL handle=0x{_eglHandle:X}");

        // Use the static get_proc_address callback — static field keeps it alive permanently.
        // Implicit conversion from delegate → MpvGetProcAddressFunc stores the function pointer.
        var glInitParams = new MpvRenderNative.MpvOpenglInitParams
        {
            GetProcAddress = _glGetProcCbStatic,   // implicit delegate → MpvGetProcAddressFunc
            GetProcAddressCtx = null
        };

        var apiTypePtr = Marshal.StringToHGlobalAnsi(MpvRenderNative.MPV_RENDER_API_TYPE_OPENGL);
        var advancedControl = 1; // enable advanced control

        try
        {
            var initParams = new MpvRenderNative.MpvRenderParam[]
            {
                new() { Type = MpvRenderNative.MPV_RENDER_PARAM_API_TYPE, Data = (void*)apiTypePtr },
                new() { Type = MpvRenderNative.MPV_RENDER_PARAM_OPENGL_INIT_PARAMS, Data = &glInitParams },
                new() { Type = MpvRenderNative.MPV_RENDER_PARAM_ADVANCED_CONTROL, Data = &advancedControl },
                new() { Type = MpvRenderNative.MPV_RENDER_PARAM_INVALID, Data = null }
            };

            fixed (MpvRenderNative.MpvRenderParam* paramsPtr = initParams)
            {
                var renderErr = MpvRenderNative.mpv_render_context_create(out _renderContext, _mpv, paramsPtr);
                DebugLog($"InitGlOnEventThread: render_context_create={renderErr} ctx=0x{_renderContext:X}");
                if (renderErr < 0 || _renderContext == IntPtr.Zero)
                {
                    DebugLog($"InitGlOnEventThread: FAILED: {MpvNative.GetError(renderErr)} (code={renderErr})");
                    Error?.Invoke(this, $"mpv_render_context_create failed: {MpvNative.GetError(renderErr)} (code={renderErr})");
                    _angleContext.Dispose();
                    _angleContext = null;
                    return false;
                }
            }

            _renderUpdateCallback = OnRenderUpdate;
            MpvRenderNative.mpv_render_context_set_update_callback(
                _renderContext, _renderUpdateCallback, IntPtr.Zero);

            DebugLog("InitGlOnEventThread: SUCCESS — render context ready");
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(apiTypePtr);
        }
    }

    // Keep delegate alive — prevent GC from collecting it after InitGlOnEventThread returns
    // (now using _glGetProcCbStatic above — nothing needed here)

    /// <summary>
    /// Called by mpv's render thread when a new frame is available.
    /// We signal via wakeup — TryRenderFrame is called on the event loop thread.
    /// </summary>
    private void OnRenderUpdate(IntPtr ctx)
    {
        // This runs on mpv's internal thread — just signal wakeup
        _renderWakeup?.Set();
    }

    /// <summary>
    /// Called from the event loop when a render update is pending.
    /// Renders mpv's current frame into our ANGLE FBO, reads pixels back,
    /// and fires FrameRendered for Avalonia to display.
    /// Uses unsafe fixed pointers (same pattern as LibMpv-OpenGL reference)
    /// to guarantee correct C ABI struct layout.
    /// </summary>
    private unsafe void TryRenderFrame()
    {
        if (_renderContext == IntPtr.Zero || _angleContext == null)
        {
            if (_renderFrameCount < 5)
                DebugLog($"TryRenderFrame: ctx=0x{_renderContext:X} angle={_angleContext != null} (skipping)");
            return;
        }

        var flags = MpvRenderNative.mpv_render_context_update(_renderContext);
        if ((flags & MpvRenderNative.MPV_RENDER_UPDATE_FRAME) == 0)
            return;

        // Get actual video dimensions from mpv properties
        int w = (int)GetDouble("dwidth");
        int h = (int)GetDouble("dheight");
        if (w <= 0 || h <= 0) { w = 1920; h = 1080; }

        // Ensure FBO is sized correctly and bind it before render
        try
        {
            _angleContext.EnsureFboSize(w, h);
            _angleContext.BindFbo();
        }
        catch (Exception ex)
        {
            DebugLog($"TryRenderFrame: EnsureFboSize/BindFbo failed: {ex.Message}");
            return;
        }

        int fboHandle = _angleContext.FboHandle;
        if (_renderFrameCount < 3)
            DebugLog($"TryRenderFrame: fboHandle={fboHandle} w={w} h={h}");

        // Use unsafe fixed pointers — same pattern as LibMpv-OpenGL reference library.
        // This guarantees correct C ABI layout (no padding ambiguity) for MpvRenderParam.
        var fbo = new MpvRenderNative.MpvOpenglFbo { Fbo = fboHandle, W = w, H = h, InternalFormat = 0 };
        var flipY = 1;

        var renderParams = new MpvRenderNative.MpvRenderParam[]
        {
            new() { Type = MpvRenderNative.MPV_RENDER_PARAM_OPENGL_FBO, Data = &fbo },
            new() { Type = MpvRenderNative.MPV_RENDER_PARAM_FLIP_Y,     Data = &flipY },
            new() { Type = MpvRenderNative.MPV_RENDER_PARAM_INVALID,    Data = null },
        };

        fixed (MpvRenderNative.MpvRenderParam* paramsPtr = renderParams)
        {
            try
            {
                var err = MpvRenderNative.mpv_render_context_render(_renderContext, paramsPtr);
                if (err == 0)
                {
                    MpvRenderNative.mpv_render_context_report_swap(_renderContext);

                    var pixels = _angleContext.ReadPixels(w, h);
                    _angleContext.UnbindFbo();

                    _renderFrameCount++;
                    if (_renderFrameCount <= 3)
                        DebugLog($"render frame #{_renderFrameCount} OK ({w}x{h})");

                    FrameRendered?.Invoke(pixels, w, h);
                }
                else
                {
                    if (_renderFrameCount < 20)
                        DebugLog($"render failed: err={err} (fbo={fboHandle} w={w} h={h})");
                }
            }
            catch (Exception ex)
            {
                if (_renderFrameCount < 5)
                    DebugLog($"render exception: {ex.Message}");
            }
        }
    }

    public void NotifyResize(int width, int height)
    {
        // For the render API path, resize is handled by adjusting the FBO dimensions
        // in TryRenderFrame.
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
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChangedEvent;
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;
    public event EventHandler<ChapterListChangedEventArgs>? ChapterListChanged;
    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<TrackListChangedEventArgs>? TrackListChanged;
    public event EventHandler<FullscreenChangedEventArgs>? FullscreenChangedEvent;
    public event EventHandler<LoopChangedEventArgs>? LoopChangedEvent;
    public event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;
    public event EventHandler<string>? Error;

    public bool UseNativeRendering { get; set; } = true;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();
        try { _eventLoop?.Wait(250); } catch { }
        _cts?.Dispose();
        _cts = null;

        // Free mpv render context (must happen before mpv_terminate_destroy)
        if (_renderContext != IntPtr.Zero)
        {
            try { MpvRenderNative.mpv_render_context_free(_renderContext); } catch { }
            _renderContext = IntPtr.Zero;
        }

        // Dispose ANGLE GL context
        if (_angleContext != null)
        {
            try { _angleContext.Dispose(); } catch { }
            _angleContext = null;
        }

        _renderWakeup?.Dispose();

        if (_mpv != IntPtr.Zero)
        {
            try { MpvNative.mpv_terminate_destroy(_mpv); } catch { }
            _mpv = IntPtr.Zero;
        }
        _initialized = false;
    }

    private void StartEventLoop()
    {
        DebugLog("StartEventLoop called");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _eventLoop = Task.Run(() => EventLoop(token), token);
    }

    private void SetPlaybackState(PlaybackState state)
    {
        if (_state == state)
            return;

        _state = state;
        PlaybackStateChangedEvent?.Invoke(this, new PlaybackStateChangedEventArgs(state));
    }

    private void EventLoop(CancellationToken token)
    {
        int loopCount = 0;

        // If this is the OpenGL render API path, initialize ANGLE + mpv render context
        // HERE on the event loop thread. EGL/ANGLE contexts are thread-affine: the context
        // that calls mpv_render_context_create MUST be on the same thread that calls
        // mpv_render_context_render. Doing it on the UI thread and then transferring
        // causes mpv to return -4 (MPV_ERROR_INVALID_PARAMETER) on every render call.
        bool isGlPath = (_renderContext == IntPtr.Zero && _angleContext == null && _mpv != IntPtr.Zero);
        if (isGlPath)
        {
            if (!InitGlOnEventThread())
            {
                DebugLog("EventLoop: GL init failed — exiting event loop");
                return;
            }
        }

        while (!token.IsCancellationRequested && !_disposed)
        {
            try
            {
                if (!_initialized || _mpv == IntPtr.Zero)
                {
                    Thread.Sleep(25);
                    continue;
                }

                loopCount++;
                if (loopCount % 100 == 0)
                    DebugLog($"EventLoop heartbeat: iter={loopCount} ctx=0x{_renderContext:X} angle={_angleContext != null}");

                var evPtr = MpvNative.mpv_wait_event(_mpv, 0.03);
                if (evPtr != IntPtr.Zero)
                {
                    var ev = Marshal.PtrToStructure<MpvNative.mpv_event>(evPtr);
                    switch ((MpvNative.mpv_event_id)ev.event_id)
                    {
                        case MpvNative.mpv_event_id.MPV_EVENT_FILE_LOADED:
                            // mpv may briefly report pause=true after loading.
                            // Force unpause so playback starts immediately.
                            SetPlaybackState(PlaybackState.Playing);
                            if (GetFlag("pause"))
                            {
                                Play();
                            }
                            Opened?.Invoke(this, EventArgs.Empty);
                            break;
                        case MpvNative.mpv_event_id.MPV_EVENT_START_FILE:
                            break;
                        case MpvNative.mpv_event_id.MPV_EVENT_END_FILE:
                            SetPlaybackState(PlaybackState.Stopped);
                            break;
                        case MpvNative.mpv_event_id.MPV_EVENT_PAUSE:
                            SetPlaybackState(PlaybackState.Paused);
                            break;
                        case MpvNative.mpv_event_id.MPV_EVENT_UNPAUSE:
                            SetPlaybackState(PlaybackState.Playing);
                            break;
                        case MpvNative.mpv_event_id.MPV_EVENT_SHUTDOWN:
                            SetPlaybackState(PlaybackState.Stopped);
                            return;
                        case MpvNative.mpv_event_id.MPV_EVENT_PROPERTY_CHANGE:
                            HandlePropertyChange(ev);
                            break;
                    }
                }

                // Poll time-pos every loop iteration (~30ms).
            // The pos >= 0 guard naturally filters out invalid positions (e.g. before
            // any file is loaded or while the file is still buffering). No additional
            // _isFileLoaded guard is needed — FILE_LOADED fires before time-pos is valid
            // (playback hasn't actually started), and that would block our first updates.
            double pos, dur;            lock (_gate)
            {
                pos = GetDouble("time-pos");
                dur = GetDouble("duration");
            }
            if (pos >= 0 && !double.IsNaN(pos))
            {
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(
                    TimeSpan.FromSeconds(pos), TimeSpan.FromSeconds(dur)));
            }

                // DXGI render API: process frame
                if (_renderContext != IntPtr.Zero)
                    TryRenderFrame();
            }
            catch (Exception ex)
            {
                DebugLog($"Exception in EventLoop: {ex}");
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
        SetPlaybackState(PlaybackState.Playing);
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
                {
                    var json = GetString("track-list");
                    if (string.IsNullOrWhiteSpace(json) || json == "null")
                        break;

                    try
                    {
                        var tracks = JsonSerializer.Deserialize<JsonElement>(json);
                        if (tracks.ValueKind != JsonValueKind.Array)
                            break;

                        var audioTracks = new List<SubtitleSource>();
                        var videoTracks = new List<SubtitleSource>();
                        var subtitleTracks = new List<SubtitleSource>();

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
                                Language = !string.IsNullOrWhiteSpace(lang) ? lang : title,
                                Type = kind,
                                IsEnabled = selected
                            };

                            switch (kind)
                            {
                                case "audio": audioTracks.Add(src); break;
                                case "video": videoTracks.Add(src); break;
                                default: subtitleTracks.Add(src); break;
                            }
                        }

                        TrackListChanged?.Invoke(this, new TrackListChangedEventArgs(
                            audioTracks.ToArray(),
                            videoTracks.ToArray(),
                            subtitleTracks.ToArray()));
                    }
                    catch { /* JSON parse failed — skip */ }
                }
                break;
            case "chapter-list":
                var ch = ChapterList;
                ChapterListChanged?.Invoke(this, new ChapterListChangedEventArgs(ch));
                break;
            case "pause":
                var isPaused = GetFlag("pause");
                SetPlaybackState(isPaused ? PlaybackState.Paused : PlaybackState.Playing);
                break;
            case "core-idle":
                // Bypass core-idle pause changes to prevent play/pause state mismatch
                break;
            case "eof-reached":
                if (GetFlag("eof-reached"))
                {
                    SetPlaybackState(PlaybackState.Stopped);
                    // Replay at EOF for continuous playback with keep-open.
                    // Use a guard to prevent re-entrancy if the seek triggers
                    // another eof-reached event.
                    if (GetFlag("keep-open") && !_isRecoveringFromEof)
                    {
                        _isRecoveringFromEof = true;
                        Seek(TimeSpan.Zero);
                        Play();
                        _isRecoveringFromEof = false;
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

    public void GetVideoSize(out int width, out int height)
    {
        width = (int)GetInt64("dwidth");
        height = (int)GetInt64("dheight");
        if (width <= 0 || height <= 0)
        { width = 1920; height = 1080; }
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
            // Only include subtitle tracks (exclude audio and video)
            if (kind == "audio" || kind == "video")
                continue;
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

    private static AudioTrackInfo[] ParseAudioTrackList(string json)
    {
        var tracks = JsonSerializer.Deserialize<JsonElement>(json);
        if (tracks.ValueKind != JsonValueKind.Array)
            return Array.Empty<AudioTrackInfo>();

        var result = new List<AudioTrackInfo>();
        foreach (var t in tracks.EnumerateArray())
        {
            var kind = t.TryGetProperty("type", out var kindProp) ? kindProp.GetString() ?? "" : "";
            if (kind != "audio") continue;

            var id = t.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1;
            var lang = t.TryGetProperty("lang", out var langProp) ? langProp.GetString() ?? "" : "";
            var title = t.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var codec = t.TryGetProperty("codec", out var codecProp) ? codecProp.GetString() ?? "" : "";
            var selected = t.TryGetProperty("selected", out var selProp) && selProp.GetBoolean();
            var isDefault = t.TryGetProperty("default", out var defProp) && defProp.GetBoolean();

            // Extract channel count from mpv's audio-channels or demux-channel-count
            int channels = 0;
            if (t.TryGetProperty("demux-channel-count", out var chProp) && chProp.ValueKind == JsonValueKind.Number)
                channels = chProp.GetInt32();
            else if (t.TryGetProperty("audio-channels", out var acProp) && acProp.ValueKind == JsonValueKind.Number)
                channels = acProp.GetInt32();

            // Extract sample rate
            int sampleRate = 0;
            if (t.TryGetProperty("demux-samplerate", out var srProp) && srProp.ValueKind == JsonValueKind.Number)
                sampleRate = srProp.GetInt32();

            var info = new AudioTrackInfo
            {
                Id = id,
                Language = lang,
                Title = !string.IsNullOrWhiteSpace(title) ? title : lang,
                Codec = codec,
                Channels = channels,
                SampleRate = sampleRate,
                IsSelected = selected,
                IsDefault = isDefault
            };

            result.Add(info);
        }
        return result.ToArray();
    }

    private static VideoTrackInfo[] ParseVideoTrackList(string json)
    {
        var tracks = JsonSerializer.Deserialize<JsonElement>(json);
        if (tracks.ValueKind != JsonValueKind.Array)
            return Array.Empty<VideoTrackInfo>();

        var result = new List<VideoTrackInfo>();
        foreach (var t in tracks.EnumerateArray())
        {
            var kind = t.TryGetProperty("type", out var kindProp) ? kindProp.GetString() ?? "" : "";
            if (kind != "video") continue;

            var id = t.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1;
            var title = t.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var codec = t.TryGetProperty("codec", out var codecProp) ? codecProp.GetString() ?? "" : "";
            var selected = t.TryGetProperty("selected", out var selProp) && selProp.GetBoolean();
            var isDefault = t.TryGetProperty("default", out var defProp) && defProp.GetBoolean();

            int width = 0, height = 0;
            if (t.TryGetProperty("demux-w", out var wProp) && wProp.ValueKind == JsonValueKind.Number)
                width = wProp.GetInt32();
            if (t.TryGetProperty("demux-h", out var hProp) && hProp.ValueKind == JsonValueKind.Number)
                height = hProp.GetInt32();

            double fps = 0;
            if (t.TryGetProperty("demux-fps", out var fpsProp) && fpsProp.ValueKind == JsonValueKind.Number)
                fps = fpsProp.GetDouble();

            var info = new VideoTrackInfo
            {
                Id = id,
                Title = title,
                Codec = codec,
                Width = width,
                Height = height,
                Fps = fps,
                IsSelected = selected,
                IsDefault = isDefault
            };

            result.Add(info);
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

        try
        {
            var value = Marshal.PtrToStringUTF8(ptr);
            return value ?? string.Empty;
        }
        finally
        {
            MpvNative.mpv_free(ptr);
        }
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
            public int error;
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
        internal static extern void mpv_free(IntPtr data);

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

        // ── mpv_node structures for screenshot-raw ──

        internal enum mpv_format_node
        {
            MPV_FORMAT_NONE = 0,
            MPV_FORMAT_STRING = 1,
            MPV_FORMAT_FLAG = 3,
            MPV_FORMAT_INT64 = 4,
            MPV_FORMAT_DOUBLE = 5,
            MPV_FORMAT_NODE_MAP = 15,
            MPV_FORMAT_BYTE_ARRAY = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct mpv_byte_array
        {
            public IntPtr data;
            public long size; // size_t on x64 = 8 bytes
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct mpv_node
        {
            public mpv_node_union u;
            public mpv_format_node format;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct mpv_node_union
        {
            [FieldOffset(0)] public IntPtr string_ptr;
            [FieldOffset(0)] public long int64;
            [FieldOffset(0)] public double double_;
            [FieldOffset(0)] public int flag;
            [FieldOffset(0)] public IntPtr list;       // mpv_node_list*
            [FieldOffset(0)] public IntPtr byte_array; // mpv_byte_array*
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct mpv_node_list
        {
            public int num;
            public IntPtr keys;   // char**
            public IntPtr values; // mpv_node*
        }

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_command_node(IntPtr ctx, IntPtr args, out mpv_node result);

        [DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_free_node_contents(ref mpv_node node);
    }
}
