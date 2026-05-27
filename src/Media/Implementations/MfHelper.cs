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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cine.Media.Implementations;

internal sealed class MfHelper : IDisposable
{
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

        // RPC_E_CHANGED_MODE (0x80010106) means someone else already initialized
        // COM in a different apartment type. That's OK  -  we can still proceed.
        if (hr < 0 && hr != unchecked((int)0x80010106))
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        // Start Media Foundation  -  version 1.0
        hr = NativeMethods.MFStartup(MfGuids.MF_VERSION_1_0, dwFlags: 0);
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

        // Create the source reader from a file URL.
        int hr = NativeMethods.MFCreateSourceReaderFromURL(
            pwszURL: path,
            pAttributes: IntPtr.Zero,
            out IntPtr ppSourceReader);

        Marshal.ThrowExceptionForHR(hr);
        _sourceReader = ppSourceReader;

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
            Guid guidService = MfGuids.MR_STREAM_MEDIASOURCE;
            Guid iidMediaSource = typeof(IMFMediaSource).GUID;

            int hr = reader.GetServiceForStream(
                (uint)MfGuids.MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX,
                ref guidService,
                ref iidMediaSource,
                out IntPtr ppMediaSourceObj);

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
        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        for (uint streamIdx = 0; streamIdx < 16; streamIdx++)
        {
            int hr = reader.GetNativeMediaType(streamIdx, 0, out IMFMediaType? mediaType);

            if (hr < 0) break;
            if (mediaType is null) continue;

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
                if (mediaType != null)
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
            int enable = ((i == _videoStreamIndex || i == _audioStreamIndex) ? 1 : 0);
            reader.SetStreamSelection(i, enable);
        }
    }

    /// <summary>
    /// Tries to reconfigure a stream's output to the desired pixel format.
    /// Falls back silently to the native format if the conversion isn't supported.
    /// </summary>
    private unsafe void SetVideoOutputType(IMFSourceReader reader, Guid desiredSubtype)
    {
        // Get the current media type so we can clone its attributes
        int hr = reader.GetCurrentMediaType((uint)_videoStreamIndex, out IMFMediaType? currentType);
        if (hr < 0 || currentType is null) return;

        try
        {
            // Create a blank media type via MFCreateMediaType
            hr = NativeMethods.MFCreateMediaType(out IntPtr newTypePtr);
            if (hr < 0) return;

            var newType = (IMFMediaType)Marshal.GetObjectForIUnknown(newTypePtr);

            try
            {
                // Clone all attributes from the current type
                currentType.GetCount(out uint itemCount);

                for (uint i = 0; i < itemCount; i++)
                {
                    currentType.GetItemByIndex(i, out Guid key, out IntPtr value);
                    newType.SetItem(ref key, value);
                }

                // Override just the subtype (pixel format) to RGB32/BGRA
                newType.SetGUID(MfGuids.MF_MT_SUBTYPE, desiredSubtype);

                // Apply the new output type to the stream
                hr = reader.SetCurrentMediaType((uint)_videoStreamIndex, 0, newType);
                // If this fails the decoder doesn't support RGB32  -  keep native format
            }
            finally
            {
                Marshal.ReleaseComObject(newType);
            }
        }
        finally
        {
            if (currentType is not null)
                Marshal.ReleaseComObject(currentType);
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
            var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

            // Get the underlying IMFMediaSource
            Guid guidService = MfGuids.MR_STREAM_MEDIASOURCE;
            Guid iidMediaSource = typeof(IMFMediaSource).GUID;

            int hr = reader.GetServiceForStream(
                (uint)MfGuids.MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX,
                ref guidService,
                ref iidMediaSource,
                out IntPtr ppMediaSourceObj);

            if (hr < 0 || ppMediaSourceObj == IntPtr.Zero) return;

            var mediaSource = (IMFMediaSource)Marshal.GetObjectForIUnknown(ppMediaSourceObj);
            Marshal.Release(ppMediaSourceObj);

            try
            {
                // Create a new presentation descriptor
                hr = mediaSource.CreatePresentationDescriptor(out IMFPresentationDescriptor? ppd);
                if (hr < 0 || ppd == null) return;

                try
                {
                    // Set the start time
                    Guid pdStartTime = MfGuids.MF_PD_START_TIME;
                    ppd.SetUINT64(ref pdStartTime, (ulong)position);

                    // Restart the media source at the new position
                    Guid timeFormat = MfGuids.MF_TIME_FORMAT_MEDIA_TIME_GUID;
                    hr = mediaSource.Start(ppd, ref timeFormat, ref position);
                    Marshal.ThrowExceptionForHR(hr);
                }
                finally
                {
                    Marshal.ReleaseComObject(ppd);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(mediaSource);
            }

            // Notify the reader about the seek
            uint mfsParam = (uint)MfGuids.MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX;
            reader.ProcessMessage((uint)MfGuids.MFSessionMessage.NotifySeek, (ulong)mfsParam);
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

        int hr = reader.GetCurrentMediaType((uint)_videoStreamIndex, out IMFMediaType? mediaType);
        if (hr < 0 || mediaType is null)
            return null;

        try
        {
            mediaType.GetGUID(MfGuids.MF_MT_SUBTYPE, out Guid subtype);

            mediaType.GetUINT64(MfGuids.MF_MT_FRAME_SIZE, out ulong frameSize);
            uint width  = (uint)(frameSize & 0xFFFFFFFF);
            uint height = (uint)(frameSize >> 32);

            mediaType.GetUINT64(MfGuids.MF_MT_FRAME_RATE, out ulong frameRate);
            uint fpsNum = (uint)(frameRate & 0xFFFFFFFF);
            uint fpsDen = (uint)(frameRate >> 32);
            double frameRateValue = fpsDen > 0 ? (double)fpsNum / fpsDen : 0;

            return new VideoStreamInfo
            {
                StreamIndex = _videoStreamIndex,
                Width       = (int)width,
                Height      = (int)height,
                FrameRate   = frameRateValue,
                Subtype     = subtype.ToString("B").ToUpper()
            };
        }
        finally
        {
            if (mediaType is not null)
                Marshal.ReleaseComObject(mediaType);
        }
    }

    internal unsafe AudioStreamInfo? GetAudioStreamInfo()
    {
        if (_audioStreamIndex < 0 || _sourceReader == IntPtr.Zero)
            return null;

        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        int hr = reader.GetCurrentMediaType((uint)_audioStreamIndex, out IMFMediaType? mediaType);
        if (hr < 0 || mediaType is null)
            return null;

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
            if (mediaType is not null)
                Marshal.ReleaseComObject(mediaType);
        }
    }

    // ============================================================
    //  READING LOOP  (background thread)
    // ============================================================

    private unsafe void ReadingLoop(CancellationToken token)
    {
        var reader = (IMFSourceReader)Marshal.GetObjectForIUnknown(_sourceReader);

        while (!token.IsCancellationRequested && !_stopRequested)
        {
            while (!_isPlaying && !_stopRequested && !token.IsCancellationRequested)
            {
                Thread.Yield();
            }
            if (_stopRequested || token.IsCancellationRequested) break;

            // ── Read video sample ──
            int hr = reader.ReadSample(
                (uint)_videoStreamIndex,
                0,
                out int actualStream,
                out uint flags,
                out long timestamp,
                out IMFSample? videoSample);

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

            if (videoSample is not null)
                OnSampleReady(videoSample, timestamp);

            // ── Read audio sample (non-blocking) ──
            if (_audioStreamIndex >= 0)
            {
                hr = reader.ReadSample(
                    (uint)_audioStreamIndex,
                    0,
                    out int actualAudioStream,
                    out uint audioFlags,
                    out long audioTimestamp,
                    out IMFSample? audioSample);

                if (hr == 0 && audioSample is not null)
                {
                    OnAudioSampleReady(audioSample);
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
