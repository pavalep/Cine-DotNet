// ============================================================================
// MfHelper  -  Media Foundation Source Reader Pipeline
// ============================================================================
//
// WHAT THIS IS:
//   A C# wrapper around Media Foundation's IMFSourceReader API.
//   Opens media files, enumerates streams, decodes video + audio samples,
//   and dispatches them to D3D11Renderer (video) and AudioRenderer (audio).
//
// ARCHITECTURE ROLE:
//   File (disk)  →  MF Source Reader (decodes)  →  IMFSample  →  Renderer
//                     ↑
//                 MfHelper manages this entire pipeline:
//                   - OpenFile:  creates IMFSourceReader, discovers streams
//                   - Duration:  queried from IMFPresentationDescriptor
//                   - Seek:      creates new reader+media source at target pos
//
// THREADING MODEL:
//   - Main thread: UI + control calls (Open, Play, Stop, Seek)
//   - Background thread: Reading loop pulls decoded frames
//     and raises events consumed by the player/renderer
//
// LIFECYCLE:
//   1. Initialize()     → CoInitializeEx + MFStartup
//   2. OpenFile(path)   → Create IMFSourceReader, enumerate streams
//   3. StartPlayback()  → Begin background reading loop
//   4. StopPlayback()   → Signal loop to stop
//   5. CloseFile()      → Release source reader
//   6. Shutdown()       → MFShutdown + CoUninitialize
//   7. Dispose()        → Full cleanup (idempotent)
//
// DEV NOTE:
//   Seeking is implemented by creating a new IMFMediaSource from the
//   stored file URL and wrapping it in a fresh IMFSourceReader.  This
//   matches the pattern used by MfCreateSourceReaderFromURL under the
//   hood but gives us explicit control over the start position via
//   IMFPresentationDescriptor::SetUINT64(MF_PD_START_TIME, ...).
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cine.Media.Implementations;

internal sealed class MfHelper : IDisposable
{
    #region debug-point V0:runtime-reporter
    private static readonly HttpClient DebugHttpClient = new();
    private static readonly object DebugEnvLock = new();
    private static string? _debugServerUrl;
    private static string? _debugSessionId;

    private static void DebugReport(string hypothesisId, string location, string msg, object? data = null, string runId = "pre-fix")
    {
        try
        {
            EnsureDebugEnvLoaded();
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = _debugSessionId ?? "video-open-crash",
                runId,
                hypothesisId,
                location,
                msg = $"[DEBUG] {msg}",
                data,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            _ = DebugHttpClient.PostAsync(
                _debugServerUrl ?? "http://127.0.0.1:7777/event",
                new StringContent(payload, Encoding.UTF8, "application/json"))
                .ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
        catch
        {
        }
    }

    private static void EnsureDebugEnvLoaded()
    {
        if (!string.IsNullOrWhiteSpace(_debugServerUrl) && !string.IsNullOrWhiteSpace(_debugSessionId))
            return;

        lock (DebugEnvLock)
        {
            if (!string.IsNullOrWhiteSpace(_debugServerUrl) && !string.IsNullOrWhiteSpace(_debugSessionId))
                return;

            foreach (var root in EnumerateDebugRoots())
            {
                var dir = new DirectoryInfo(root);
                while (dir != null)
                {
                    var envPath = Path.Combine(dir.FullName, ".dbg", "no-playback.env");
                    if (!File.Exists(envPath))
                        envPath = Path.Combine(dir.FullName, ".dbg", "video-transparent.env");
                    if (!File.Exists(envPath))
                        envPath = Path.Combine(dir.FullName, ".dbg", "video-open-crash.env");
                    if (!File.Exists(envPath))
                        envPath = Path.Combine(dir.FullName, ".dbg", "video-no-playback.env");

                    if (File.Exists(envPath))
                    {
                        foreach (var line in File.ReadAllLines(envPath))
                        {
                            if (line.StartsWith("DEBUG_SERVER_URL=", StringComparison.Ordinal))
                                _debugServerUrl = line["DEBUG_SERVER_URL=".Length..].Trim();
                            else if (line.StartsWith("DEBUG_SESSION_ID=", StringComparison.Ordinal))
                                _debugSessionId = line["DEBUG_SESSION_ID=".Length..].Trim();
                        }
                        return;
                    }

                    dir = dir.Parent;
                }
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateDebugRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
    }

    private static int TryQueryInterface(IntPtr unk, Guid iid, out IntPtr ppv)
    {
        ppv = IntPtr.Zero;
        if (unk == IntPtr.Zero) return unchecked((int)0x80004003);
        return Marshal.QueryInterface(unk, ref iid, out ppv);
    }
    #endregion

    #region debug-point V0:propvariant
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(IntPtr pvar);
    #endregion

    #region Constants

    private const uint COINIT_MULTITHREADED = 0x0;

    // ReadSample result flags
    private const uint MFSOURCE_READERF_ENDOFSTREAM        = 0x00000001;
    private const uint MFSOURCE_READERF_NEWSTREAM          = 0x00000002;
    private const uint MFSOURCE_READERF_NATIVETYPECHANGED  = 0x00000004;
    private const uint MFSOURCE_READERF_CURRENTTYPECHANGED = 0x00000008;

    #endregion

    #region Fields

    private bool    _mfInitialized;
    private IntPtr  _sourceReader;        // IMFSourceReader raw COM pointer
    private int     _videoStreamIndex = -1;
    private int     _audioStreamIndex = -1;
    private long    _duration100ns;        // total media duration in 100-ns units (-1 = unknown)

    private CancellationTokenSource? _cts;
    private Task? _readingTask;
    private volatile bool _isPlaying;
    private volatile bool _stopRequested;
    private int _timingResetRequested;
    private string? _currentFilePath;

    private bool _disposed;

    #endregion

    #region Properties

    public bool IsInitialized => _mfInitialized;
    public bool IsOpen        => _sourceReader != IntPtr.Zero;
    public bool IsPlaying     => _isPlaying;
    public int  VideoStreamIndex => _videoStreamIndex;
    public int  AudioStreamIndex => _audioStreamIndex;

    /// <summary>Total media duration in TimeSpan units.  Zero if unknown.</summary>
    public TimeSpan Duration => _duration100ns > 0
        ? TimeSpan.FromTicks(_duration100ns / 10)  // 100-ns → TimeSpan ticks (both are 100-ns units)
        : TimeSpan.Zero;

    #endregion

    #region Events

    /// <summary>Fired when the file is opened and stream info is ready.</summary>
    public event EventHandler<MediaOpenedEventArgs>?   MediaOpened;
    /// <summary>Fired when a decoded video sample is ready for rendering.</summary>
    public event EventHandler<SampleReadyEventArgs>?    SampleReady;
    /// <summary>Fired when a decoded audio sample is ready for playback.</summary>
    public event EventHandler<AudioSampleReadyEventArgs>? AudioSampleReady;
    /// <summary>Fired when playback reaches the end of the stream.</summary>
    public event EventHandler?                          PlaybackEnded;
    /// <summary>Fired on a playback error.</summary>
    public event EventHandler<ErrorEventArgs>?          Error;

    #endregion

    // ============================================================
    //  STARTUP / SHUTDOWN
    // ============================================================

    /// <summary>
    /// Initializes COM (multithreaded apartment) and Media Foundation.
    /// Safe to call multiple times  -  only initializes once.
    /// </summary>
    public void Initialize()
    {
        if (_mfInitialized) return;

        // COM must be initialized before any MF calls.
        // MTA is required since our reading loop uses background threads.
        int hr = NativeMethods.CoInitializeEx(
            pvReserved: IntPtr.Zero,
            dwCoInit: COINIT_MULTITHREADED);

        DebugReport("V0", "MfHelper.Initialize", "CoInitializeEx result.", new { hr = hr, hrHex = $"0x{hr:X8}" });

        // RPC_E_CHANGED_MODE (0x80010106) means someone else already initialized
        // COM in a different apartment type. That's OK  -  we can still proceed.
        if (hr < 0 && hr != unchecked((int)0x80010106))
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        // Start Media Foundation  -  version 1.0
        hr = NativeMethods.MFStartup(MfGuids.MF_VERSION_1_0, dwFlags: 0);
        DebugReport("V0", "MfHelper.Initialize", "MFStartup result.", new { hr = hr, hrHex = $"0x{hr:X8}", version = MfGuids.MF_VERSION_1_0 });
        Marshal.ThrowExceptionForHR(hr);

        _mfInitialized = true;
    }

    /// <summary>
    /// Shuts everything down in reverse order:
    ///   Stop → CloseFile → MFShutdown → CoUninitialize
    /// </summary>
    public void Shutdown()
    {
        StopPlayback();
        CloseFile();

        if (_mfInitialized)
        {
            NativeMethods.MFShutdown();
            _mfInitialized = false;
        }

        NativeMethods.CoUninitialize();
    }

    // ============================================================
    //  FILE OPERATIONS
    // ============================================================

    /// <summary>
    /// Opens a media file. Creates the IMFSourceReader, discovers streams,
    /// queries the duration, and configures video/audio output.
    /// </summary>
    public void OpenFile(string path)
    {
        if (!_mfInitialized)
            Initialize();

        CloseFile();  // clean up any previous file
        _currentFilePath = path;
        _duration100ns = -1;

        IntPtr sourceReaderAttributes = IntPtr.Zero;
        try
        {
            NativeMethods.MFCreateAttributes(out sourceReaderAttributes, 4);
            var attrs = (IMFAttributes)Marshal.GetObjectForIUnknown(sourceReaderAttributes);
            try
            {
                attrs.SetUINT32(MfGuids.MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, 1);
                attrs.SetUINT32(MfGuids.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);
            }
            finally
            {
                Marshal.ReleaseComObject(attrs);
            }

            // Create the source reader from a file URL.
            int hr = NativeMethods.MFCreateSourceReaderFromURL(
                pwszURL: path,
                pAttributes: sourceReaderAttributes,
                out IntPtr ppSourceReader);

            DebugReport("V1", "MfHelper.OpenFile", "MFCreateSourceReaderFromURL result.", new
            {
                path,
                hr,
                hrHex = $"0x{hr:X8}",
                ppSourceReader = ppSourceReader.ToInt64()
            });

            Marshal.ThrowExceptionForHR(hr);
            _sourceReader = ppSourceReader;
        }
        finally
        {
            if (sourceReaderAttributes != IntPtr.Zero)
                Marshal.Release(sourceReaderAttributes);
        }

        try
        {
            var iidFromInterop = MfGuids.IID_IMFSourceReader;
            int q1 = TryQueryInterface(_sourceReader, iidFromInterop, out IntPtr p1);
            if (p1 != IntPtr.Zero) Marshal.Release(p1);

            var iidKnown = new Guid("70AE66F2-C809-4E4F-8915-BDCB406B7993");
            int q2 = TryQueryInterface(_sourceReader, iidKnown, out IntPtr p2);
            if (p2 != IntPtr.Zero) Marshal.Release(p2);

            DebugReport("V1", "MfHelper.OpenFile", "QueryInterface probes for IMFSourceReader.", new
            {
                iidFromInterop,
                qiFromInteropHr = q1,
                qiFromInteropHrHex = $"0x{q1:X8}",
                iidKnown,
                qiKnownHr = q2,
                qiKnownHrHex = $"0x{q2:X8}"
            });
        }
        catch
        {
        }

        // Discover which streams contain video and audio
        EnumerateStreams();

        // Query the media duration
        QueryDuration();

        // Select video + audio streams and configure output
        ConfigureAndSelectStreams();

        // Tell the UI we're ready
        OnMediaOpened();
    }

    /// <summary>
    /// Releases the source reader and resets all stream state.
    /// Does not call MFShutdown (that's Shutdown()'s job).
    /// </summary>
    public void CloseFile()
    {
        StopPlayback();

        if (_sourceReader != IntPtr.Zero)
        {
            Marshal.Release(_sourceReader);
            _sourceReader = IntPtr.Zero;
        }

        _videoStreamIndex = -1;
        _audioStreamIndex = -1;
        _duration100ns = -1;
    }

    // ============================================================
    //  DURATION QUERY (Phase 2)
    // ============================================================

    /// <summary>
    /// Queries the IMFPresentationDescriptor for total media duration.
    /// Stores result in _duration100ns (in 100-nanosecond units).
    /// -1 means "unknown duration."
    /// </summary>
    private unsafe void QueryDuration()
    {
        if (_sourceReader == IntPtr.Zero) return;

        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        try
        {
            // Get the underlying IMFMediaSource via GetServiceForStream
            Guid guidService = MfGuids.MF_MEDIASOURCE_SERVICE;
            Guid iidMediaSource = typeof(IMFMediaSource).GUID;

            int hr = reader.GetServiceForStream(
                0xFFFFFFFF,
                ref guidService,
                ref iidMediaSource,
                out IntPtr ppMediaSourceObj);

            DebugReport("VDUR", "MfHelper.QueryDuration", "GetServiceForStream for media source.", new
            {
                hr,
                hrHex = $"0x{hr:X8}",
                streamIndex = 0xFFFFFFFF,
                service = guidService.ToString("B").ToUpper(),
                iid = iidMediaSource.ToString("B").ToUpper(),
                ptr = ppMediaSourceObj.ToInt64()
            });

            if (hr >= 0 && ppMediaSourceObj != IntPtr.Zero)
            {
                var mediaSource = (IMFMediaSource)Marshal.GetObjectForIUnknown(ppMediaSourceObj);
                Marshal.Release(ppMediaSourceObj);

                try
                {
                    hr = mediaSource.CreatePresentationDescriptor(out IMFPresentationDescriptor? ppd);
                    Marshal.ThrowExceptionForHR(hr);

                    if (ppd != null)
                    {
                        Guid pdDuration = MfGuids.MF_PD_DURATION;
                        hr = ppd.GetUINT64(ref pdDuration, out ulong duration);
                        DebugReport("VDUR", "MfHelper.QueryDuration", "IMFPresentationDescriptor.GetUINT64(MF_PD_DURATION).", new
                        {
                            hr,
                            hrHex = $"0x{hr:X8}",
                            duration,
                            duration100ns = (long)duration
                        });
                        if (hr >= 0 && duration > 0)
                        {
                            _duration100ns = (long)duration;
                        }

                        Marshal.ReleaseComObject(ppd);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(mediaSource);
                }
            }

            if (_duration100ns <= 0)
            {
                uint[] streamCandidates = _videoStreamIndex >= 0
                    ? new[] { (uint)_videoStreamIndex, (uint)MfGuids.MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX, 0xFFFFFFFF, 0xFFFFFFFC, 0xFFFFFFFD }
                    : new[] { (uint)MfGuids.MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX, 0xFFFFFFFF, 0xFFFFFFFC, 0xFFFFFFFD };

                int size = Marshal.SizeOf<PropVariant>();
                IntPtr pvPtr = Marshal.AllocHGlobal(size);
                try
                {
                    const ushort VT_I8 = 20;
                    const ushort VT_UI8 = 21;

                    foreach (var streamIndex in streamCandidates)
                    {
                        Marshal.StructureToPtr(new PropVariant(), pvPtr, fDeleteOld: false);
                        hr = reader.GetPresentationAttribute(streamIndex, MfGuids.MF_PD_DURATION, pvPtr);
                        var pv0 = Marshal.PtrToStructure<PropVariant>(pvPtr);
                        DebugReport("VDUR", "MfHelper.QueryDuration", "IMFSourceReader.GetPresentationAttribute(MF_PD_DURATION).", new
                        {
                            streamIndex,
                            hr,
                            hrHex = $"0x{hr:X8}",
                            vt = pv0.vt,
                            valueI64 = pv0.p.ToInt64()
                        });

                        if (hr >= 0 && (pv0.vt == VT_I8 || pv0.vt == VT_UI8))
                        {
                            long d = pv0.p.ToInt64();
                            if (d > 0)
                            {
                                _duration100ns = d;
                                break;
                            }
                        }

                        try { PropVariantClear(pvPtr); } catch { }
                    }
                }
                finally
                {
                    try { PropVariantClear(pvPtr); } catch { }
                    Marshal.FreeHGlobal(pvPtr);
                }
            }
        }
        catch
        {
            _duration100ns = -1;
        }
        finally
        {
            Marshal.ReleaseComObject(reader);
        }
    }

    // ============================================================
    //  STREAM ENUMERATION
    // ============================================================

    /// <summary>
    /// Iterates through all streams (up to 16) and identifies which ones
    /// are video vs. audio by checking their major media type GUID.
    /// </summary>
    private unsafe void EnumerateStreams()
    {
        DebugReport("V2", "MfHelper.EnumerateStreams", "Enter EnumerateStreams.", new
        {
            sourceReaderPtr = _sourceReader.ToInt64(),
            iidFromInterop = MfGuids.IID_IMFSourceReader
        });

        IMFSourceReader reader;
        try
        {
            reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);
        }
        catch (Exception ex)
        {
            DebugReport("V2", "MfHelper.EnumerateStreams", "Marshal.GetObjectForIUnknown cast failed.", new
            {
                exception = ex.ToString(),
                iidFromInterop = MfGuids.IID_IMFSourceReader
            });
            throw;
        }

        for (uint streamIdx = 0; streamIdx < 16; streamIdx++)
        {
            int hr = reader.GetNativeMediaType(streamIdx, 0, out IntPtr mediaTypePtr);

            if (streamIdx == 0 || streamIdx == 1)
            {
                DebugReport("V2", "MfHelper.EnumerateStreams", "GetNativeMediaType probe.", new
                {
                    streamIdx,
                    hr,
                    hrHex = $"0x{hr:X8}",
                    mediaTypePtr = mediaTypePtr.ToInt64()
                });
            }

            if (hr < 0) break;
            if (mediaTypePtr == IntPtr.Zero) continue;

            IMFMediaType mediaType = (IMFMediaType)Marshal.GetObjectForIUnknown(mediaTypePtr);
            Marshal.Release(mediaTypePtr);

            try
            {
                hr = mediaType.GetMajorType(out Guid majorType);
                if (hr < 0) continue;

                if (majorType == MfGuids.MFMediaType_VIDEO && _videoStreamIndex < 0)
                {
                    _videoStreamIndex = (int)streamIdx;
                }
                else if (majorType == MfGuids.MFMediaType_AUDIO && _audioStreamIndex < 0)
                {
                    _audioStreamIndex = (int)streamIdx;
                }

                if (_videoStreamIndex >= 0 && _audioStreamIndex >= 0)
                    break;
            }
            finally
            {
                Marshal.ReleaseComObject(mediaType);
            }
        }
    }

    /// <summary>
    /// Configures stream selections so both video and audio deliver samples.
    /// Attempts to set the video output format to RGB32 so the D3D11Renderer
    /// receives BGRA frames directly (no shader needed for most decoders).
    /// Falls back silently to the native format if conversion isn't supported.
    /// </summary>
    private unsafe void ConfigureAndSelectStreams()
    {
        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        // Attempt RGB32 output for D3D11Renderer
        if (_videoStreamIndex >= 0)
        {
            SetVideoOutputType(reader, MfGuids.MFVideoFormat_RGB32);
        }

        // Enable video AND audio streams
        for (uint i = 0; i < 16; i++)
        {
            bool enable = (i == _videoStreamIndex || i == _audioStreamIndex);
            reader.SetStreamSelection(i, enable);
        }
    }

    /// <summary>
    /// Tries to reconfigure a stream's output to the desired pixel format.
    /// Falls back silently to the native format if the conversion isn't supported.
    /// </summary>
    private unsafe void SetVideoOutputType(IMFSourceReader reader, Guid desiredSubtype)
    {
        int hr = NativeMethods.MFCreateMediaType(out IntPtr newTypePtr);
        if (hr < 0 || newTypePtr == IntPtr.Zero) return;

        var newType = (IMFMediaType)Marshal.GetObjectForIUnknown(newTypePtr);
        try
        {
            newType.SetGUID(MfGuids.MF_MT_MAJOR_TYPE, MfGuids.MFMediaType_VIDEO);
            newType.SetGUID(MfGuids.MF_MT_SUBTYPE, desiredSubtype);

            hr = reader.SetCurrentMediaType((uint)_videoStreamIndex, IntPtr.Zero, newType);
        }
        finally
        {
            Marshal.ReleaseComObject(newType);
        }
    }

    // ============================================================
    //  SEEKING (Phase 2)
    // ============================================================

    /// <summary>
    /// Seeks to the specified position (in 100-nanosecond units).
    ///
    /// Strategy: get the underlying IMFMediaSource, create a new
    /// presentation descriptor, set the start time, and tell the
    /// IMFSourceReader about the seek via ProcessMessage.
    /// </summary>
    public unsafe void Seek(long position)
    {
        if (_sourceReader == IntPtr.Zero || string.IsNullOrEmpty(_currentFilePath))
            return;

        try
        {
            Interlocked.Exchange(ref _timingResetRequested, 1);
            var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);
            var pv = new PropVariant();
            int size = Marshal.SizeOf<PropVariant>();
            IntPtr pvPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(pv, pvPtr, fDeleteOld: false);
                int hr = NativeMethods.InitPropVariantFromInt64(position, pvPtr);
                Marshal.ThrowExceptionForHR(hr);

                hr = reader.SetCurrentPosition(MfGuids.MF_TIME_FORMAT_MEDIA_TIME_GUID, pvPtr);
                Marshal.ThrowExceptionForHR(hr);

                reader.Flush(MfGuids.MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX);
            }
            finally
            {
                try { PropVariantClear(pvPtr); } catch { }
                Marshal.FreeHGlobal(pvPtr);
            }
        }
        catch (Exception ex)
        {
            OnError(new InvalidOperationException($"Seek to position {position} failed: {ex.Message}", ex));
        }
    }

    // ============================================================
    //  PLAYBACK CONTROL
    // ============================================================

    public void StartPlayback()
    {
        if (_sourceReader == IntPtr.Zero || _isPlaying) return;
        if (_videoStreamIndex < 0)
        {
            OnError(new InvalidOperationException("No video stream found in the media file."));
            return;
        }

        _stopRequested = false;
        _isPlaying = true;
        if (_readingTask != null && !_readingTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _readingTask = Task.Run(() => ReadingLoop(_cts.Token), _cts.Token);
    }

    public void StopPlayback()
    {
        if (!_isPlaying) return;

        _stopRequested = true;
        _cts?.Cancel();

        try { _readingTask?.Wait(2000); }
        catch (AggregateException) { }

        _cts?.Dispose();
        _cts = null;
        _isPlaying = false;
    }

    public void Pause() => _isPlaying = false;

    public void Resume()
    {
        if (_sourceReader == IntPtr.Zero || _isPlaying) return;

        _isPlaying = true;
        if (_readingTask == null || _readingTask.IsCompleted)
        {
            _cts = new CancellationTokenSource();
            _readingTask = Task.Run(() => ReadingLoop(_cts.Token), _cts.Token);
        }
    }

    // ============================================================
    //  FORMAT INFO QUERIES
    // ============================================================

    internal unsafe VideoStreamInfo? GetVideoStreamInfo()
    {
        if (_videoStreamIndex < 0 || _sourceReader == IntPtr.Zero)
            return null;

        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        int hr = reader.GetCurrentMediaType((uint)_videoStreamIndex, out IntPtr mediaTypePtr);
        if (hr < 0 || mediaTypePtr == IntPtr.Zero)
            return null;

        var mediaType = (IMFMediaType)Marshal.GetObjectForIUnknown(mediaTypePtr);
        Marshal.Release(mediaTypePtr);

        try
        {
            Guid subtype = Guid.Empty;
            ulong frameSize = 0;
            ulong frameRate = 0;

            try { mediaType.GetGUID(MfGuids.MF_MT_SUBTYPE, out subtype); } catch { }
            try { mediaType.GetUINT64(MfGuids.MF_MT_FRAME_SIZE, out frameSize); } catch { }
            try { mediaType.GetUINT64(MfGuids.MF_MT_FRAME_RATE, out frameRate); } catch { }

            uint width = (uint)(frameSize >> 32);
            uint height = (uint)(frameSize & 0xFFFFFFFF);

            uint fpsNum = (uint)(frameRate >> 32);
            uint fpsDen = (uint)(frameRate & 0xFFFFFFFF);
            double frameRateValue = fpsDen > 0 ? (double)fpsNum / fpsDen : 0;

            if (width == 0 || height == 0)
            {
                int hr2 = reader.GetNativeMediaType((uint)_videoStreamIndex, 0, out IntPtr nativeTypePtr);
                if (hr2 >= 0 && nativeTypePtr != IntPtr.Zero)
                {
                    var nativeType = (IMFMediaType)Marshal.GetObjectForIUnknown(nativeTypePtr);
                    Marshal.Release(nativeTypePtr);
                    try
                    {
                        Guid nativeSubtype = Guid.Empty;
                        ulong nativeFrameSize = 0;
                        ulong nativeFrameRate = 0;
                        try { nativeType.GetGUID(MfGuids.MF_MT_SUBTYPE, out nativeSubtype); } catch { }
                        try { nativeType.GetUINT64(MfGuids.MF_MT_FRAME_SIZE, out nativeFrameSize); } catch { }
                        try { nativeType.GetUINT64(MfGuids.MF_MT_FRAME_RATE, out nativeFrameRate); } catch { }

                        uint nativeW = (uint)(nativeFrameSize >> 32);
                        uint nativeH = (uint)(nativeFrameSize & 0xFFFFFFFF);
                        uint nFpsNum = (uint)(nativeFrameRate >> 32);
                        uint nFpsDen = (uint)(nativeFrameRate & 0xFFFFFFFF);
                        double nativeFps = nFpsDen > 0 ? (double)nFpsNum / nFpsDen : 0;

                        if (nativeW > 0 && nativeH > 0)
                        {
                            DebugReport("V2", "MfHelper.GetVideoStreamInfo", "Frame size recovered from native media type.", new
                            {
                                stream = _videoStreamIndex,
                                recovered = $"{nativeW}x{nativeH}",
                                fps = nativeFps,
                                subtype = nativeSubtype == Guid.Empty ? "Unknown" : nativeSubtype.ToString("B").ToUpper()
                            });
                            width = nativeW;
                            height = nativeH;
                            frameRateValue = nativeFps;
                            if (subtype == Guid.Empty && nativeSubtype != Guid.Empty)
                                subtype = nativeSubtype;
                        }
                        else
                        {
                            DebugReport("V2", "MfHelper.GetVideoStreamInfo", "Frame size still unknown after native media type probe.", new
                            {
                                stream = _videoStreamIndex,
                                currentFrameSize = frameSize,
                                nativeFrameSize,
                                currentSubtype = subtype == Guid.Empty ? "Unknown" : subtype.ToString("B").ToUpper(),
                                nativeSubtype = nativeSubtype == Guid.Empty ? "Unknown" : nativeSubtype.ToString("B").ToUpper()
                            });
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(nativeType);
                    }
                }
            }

            return new VideoStreamInfo
            {
                StreamIndex = _videoStreamIndex,
                Width       = (int)width,
                Height      = (int)height,
                FrameRate   = frameRateValue,
                Subtype     = subtype == Guid.Empty ? "Unknown" : subtype.ToString("B").ToUpper()
            };
        }
        finally
        {
            Marshal.ReleaseComObject(mediaType);
        }
    }

    internal unsafe AudioStreamInfo? GetAudioStreamInfo()
    {
        if (_audioStreamIndex < 0 || _sourceReader == IntPtr.Zero)
            return null;

        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        int hr = reader.GetCurrentMediaType((uint)_audioStreamIndex, out IntPtr mediaTypePtr);
        if (hr < 0 || mediaTypePtr == IntPtr.Zero)
            return null;

        var mediaType = (IMFMediaType)Marshal.GetObjectForIUnknown(mediaTypePtr);
        Marshal.Release(mediaTypePtr);

        try
        {
            mediaType.GetGUID(MfGuids.MF_MT_AUDIO_SUBTYPE, out Guid subtype);

            uint bitsPerSample = 0;
            uint numChannels   = 0;
            uint samplesPerSec = 0;
            uint blockAlign    = 0;

            try { mediaType.GetUINT32(MfGuids.MF_MT_AUDIO_BITS_PER_SAMPLE, out bitsPerSample); }   catch { }
            try { mediaType.GetUINT32(MfGuids.MF_MT_AUDIO_NUM_CHANNELS,            out numChannels); }  catch { }
            try { mediaType.GetUINT32(MfGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND,      out samplesPerSec);}  catch { }
            try { mediaType.GetUINT32(MfGuids.MF_MT_AUDIO_BLOCK_ALIGNMENT,         out blockAlign);    }  catch { }

            return new AudioStreamInfo
            {
                StreamIndex    = _audioStreamIndex,
                Channels       = numChannels,
                SamplesPerSec  = samplesPerSec,
                BitsPerSample  = bitsPerSample,
                BlockAlign     = blockAlign,
                Subtype        = subtype.ToString("B").ToUpper()
            };
        }
        finally
        {
            Marshal.ReleaseComObject(mediaType);
        }
    }

    // ============================================================
    //  READING LOOP  (background thread)
    // ============================================================

    private unsafe void ReadingLoop(CancellationToken token)
    {
        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);
        long baseTimestamp = -1;
        long baseTicks = 0;
        long pauseStartTicks = 0;
        bool wasPaused = false;

        while (!token.IsCancellationRequested && !_stopRequested)
        {
            while (!_isPlaying && !_stopRequested && !token.IsCancellationRequested)
            {
                if (!wasPaused)
                {
                    pauseStartTicks = Stopwatch.GetTimestamp();
                    wasPaused = true;
                }
                Thread.Sleep(10);
            }
            if (_stopRequested || token.IsCancellationRequested) break;
            if (wasPaused)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                baseTicks += (nowTicks - pauseStartTicks);
                wasPaused = false;
            }
            if (Interlocked.Exchange(ref _timingResetRequested, 0) == 1)
            {
                baseTimestamp = -1;
                baseTicks = 0;
            }

            // ── Read video sample ──
            int hr = reader.ReadSample(
                (uint)_videoStreamIndex,
                0,
                out uint actualStream,
                out uint flags,
                out long timestamp,
                out IntPtr videoSamplePtr);

            if ((flags & MFSOURCE_READERF_ENDOFSTREAM) != 0)
            {
                OnPlaybackEnded();
                break;
            }

            if ((flags & MFSOURCE_READERF_NATIVETYPECHANGED) != 0)
            {
                OnPlaybackEnded();
                break;
            }

            if (hr < 0)
            {
                OnError(new COMException($"ReadSample (video) failed 0x{hr:X8}", hr));
                break;
            }

            if (videoSamplePtr != IntPtr.Zero)
            {
                var videoSample = (IMFSample)Marshal.GetObjectForIUnknown(videoSamplePtr);
                Marshal.Release(videoSamplePtr);
                if (baseTimestamp < 0)
                {
                    baseTimestamp = timestamp;
                    baseTicks = Stopwatch.GetTimestamp();
                }
                else
                {
                    long targetTicks = baseTicks + (long)((timestamp - baseTimestamp) * (Stopwatch.Frequency / 10_000_000.0));
                    while (true)
                    {
                        long nowTicks = Stopwatch.GetTimestamp();
                        long remaining = targetTicks - nowTicks;
                        if (remaining <= 0)
                            break;
                        double remainingMs = remaining * 1000.0 / Stopwatch.Frequency;
                        if (remainingMs > 2)
                            Thread.Sleep((int)Math.Min(50, remainingMs - 1));
                        else
                            Thread.SpinWait(200);
                    }
                }
                try { OnSampleReady(videoSample, timestamp); }
                finally { Marshal.ReleaseComObject(videoSample); }
            }

            // ── Read audio sample (non-blocking) ──
            if (_audioStreamIndex >= 0)
            {
                hr = reader.ReadSample(
                    (uint)_audioStreamIndex,
                    0,
                    out uint actualAudioStream,
                    out uint audioFlags,
                    out long audioTimestamp,
                    out IntPtr audioSamplePtr);

                if (hr == 0 && audioSamplePtr != IntPtr.Zero)
                {
                    var audioSample = (IMFSample)Marshal.GetObjectForIUnknown(audioSamplePtr);
                    Marshal.Release(audioSamplePtr);
                    try { OnAudioSampleReady(audioSample); }
                    finally { Marshal.ReleaseComObject(audioSample); }
                }
            }
        }
    }

    // ============================================================
    //  EVENT INVOKERS
    // ============================================================

    private void OnMediaOpened()
    {
        var info = GetVideoStreamInfo();

        var args = new MediaOpenedEventArgs
        {
            VideoStreamIndex = _videoStreamIndex,
            AudioStreamIndex = _audioStreamIndex,
            VideoWidth       = info?.Width ?? 0,
            VideoHeight      = info?.Height ?? 0,
            FrameRate        = info?.FrameRate ?? 0,
            VideoFormat      = info?.Subtype ?? "Unknown",
            Duration         = this.Duration
        };
        MediaOpened?.Invoke(this, args);
    }

    private void OnSampleReady(IMFSample sample, long timestamp)
        => SampleReady?.Invoke(this, new SampleReadyEventArgs(sample, timestamp));

    private void OnAudioSampleReady(IMFSample sample)
        => AudioSampleReady?.Invoke(this, new AudioSampleReadyEventArgs(sample));

    private void OnPlaybackEnded()
    {
        _isPlaying = false;
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnError(Exception error)
    {
        _isPlaying = false;
        Error?.Invoke(this, new ErrorEventArgs(error));
    }

    // ============================================================
    //  DISPOSE
    // ============================================================

    public void Dispose()
    {
        if (!_disposed)
        {
            Shutdown();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~MfHelper() => Dispose();
}

// ============================================================================
//  SUPPORTING TYPES
// ============================================================================

/// <summary>Metadata about a video stream.</summary>
public struct VideoStreamInfo
{
    public int    StreamIndex;
    public int    Width;
    public int    Height;
    public double FrameRate;
    public string Subtype;

    public override string ToString() =>
        $"{Width}x{Height} @ {FrameRate:F2}fps [{Subtype}]";
}

/// <summary>Metadata about an audio stream.</summary>
public struct AudioStreamInfo
{
    public int StreamIndex;
    public uint Channels;
    public uint SamplesPerSec;
    public uint BitsPerSample;
    public uint BlockAlign;
    public string Subtype;

    public uint AverageBytesPerSec => SamplesPerSec * BlockAlign;

    public override string ToString() =>
        $"{Channels}ch {SamplesPerSec}Hz {BitsPerSample}-bit [{Subtype}]";
}

internal class MediaOpenedEventArgs : EventArgs
{
    public int VideoStreamIndex = -1;
    public int AudioStreamIndex = -1;
    public int VideoWidth;
    public int VideoHeight;
    public double FrameRate;
    public string? VideoFormat;
    public TimeSpan Duration;
}

internal class SampleReadyEventArgs : EventArgs
{
    public IMFSample? Sample;
    public long Timestamp;
    public SampleReadyEventArgs(IMFSample sample, long timestamp)
    {
        Sample = sample;
        Timestamp = timestamp;
    }
}

internal class AudioSampleReadyEventArgs : EventArgs
{
    public IMFSample? Sample;
    public AudioSampleReadyEventArgs(IMFSample sample) => Sample = sample;
}

internal class ErrorEventArgs : EventArgs
{
    public Exception? Error;
    public ErrorEventArgs(Exception error) => Error = error;
}
