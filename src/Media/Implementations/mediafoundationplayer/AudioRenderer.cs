// AudioRenderer.cs — WASAPI Shared-Mode Audio Output (Phase 2)
//
// Bridges decoded audio samples from MfHelper to the Windows
// Core Audio API (WASAPI) for low-latency playback.
//
// Pipeline:
//   IMFSourceReader → MfHelper → AudioSampleReady event → AudioRenderer.Write() → WASAPI → speakers
//
// Lifecycle:
//   1. Construct
//   2. Initialize(waveFormat) — open device, configure IAudioClient, start
//   3. Write(byte[], offset, length) — queue audio frames
//   4. Stop() / Start() — pause/resume
//   5. Dispose() — drain, stop, release all COM
//
// Key notes (dev.aside):
// - We store COM objects as raw IntPtr (not RCW) to match the existing
//   codebase pattern used by D3D11Renderer and MfHelper.
// - Temporary typed interfaces are created via Marshal.GetObjectForIUnknown()
//   and released after each use to prevent RCW leaks.
// - Audio format is passed via Marshal.StructureToPtr since IAudioClient.Initialize
//   expects an IntPtr to WAVEFORMATEX.
// ============================================================================

using System;
using System.Runtime.InteropServices;

namespace Simba.Media.Implementations;

internal sealed class AudioRenderer : IDisposable
{
    #region Constants

    private const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    private const uint AUDCLNT_STREAMFLAGS_NOPERSIST     = 0x00080000;
    private const int  AUDCLNT_SHAREMODE_SHARED          = 0;

    // Default buffer duration: 100 ms in 100-nanosecond units
    private const long DEFAULT_BUFFER_DURATION_HNS = 100_0000L;

    #endregion

    #region Fields

    // All COM objects stored as raw IntPtr to avoid RCW overhead
    // and match the manual-lifetime pattern used by D3D11Renderer.
    private IntPtr _device;        // IMMDevice
    private IntPtr _audioClient;   // IAudioClient
    private IntPtr _renderClient;  // IAudioRenderClient
    private IntPtr _eventHandle;   // HANDLE (kernel event for sync)

    private bool _started;
    private bool _disposed;

    // Cached format info for validation / buffer sizing
    private ushort _channels;
    private uint   _sampleRate;
    private ushort _bitsPerSample;
    private uint   _blockAlign;

    #endregion

    #region Properties

    public bool IsInitialized => _audioClient != IntPtr.Zero;
    public bool IsPlaying     => _started;

    #endregion

    // ======================
    //  INITIALIZATION
    // ======================

    /// <summary>
    /// Opens the default audio render device and initializes WASAPI in
    /// shared (non-exclusive) event-driven mode.
    /// </summary>
    /// <param name="waveFormat">Desired audio format (channels, sample rate, bits).</param>
    public unsafe void Initialize(WAVEFORMATEX waveFormat)
    {
        // Cache format fields for later buffer calculations
        _channels      = waveFormat.nChannels;
        _sampleRate    = waveFormat.nSamplesPerSec;
        _bitsPerSample = waveFormat.wBitsPerSample;
        _blockAlign    = waveFormat.nBlockAlign;

        // ── 1. Create the MM device enumerator ──
        // Use local Guid variables because static readonly fields
        // cannot be passed as 'ref' parameters in C#.
        Guid clsidEnum = MfGuids.CLSID_MMDeviceEnumerator;
        Guid iidEnum   = MfGuids.IID_IMMDeviceEnumerator;
        Guid iidDevice = typeof(IMMDevice).GUID;

        int hr = NativeMethods.CoCreateInstance(
            ref clsidEnum,
            IntPtr.Zero,
            1,       // CLSCTX_INPROC_SERVER
            ref iidEnum,
            out IntPtr enumeratorPtr);
        Marshal.ThrowExceptionForHR(hr);

        var enumerator = (IMMDeviceEnumerator)Marshal.GetObjectForIUnknown(enumeratorPtr);
        try
        {
            // Get the default multimedia audio render device
            hr = enumerator.GetDefaultAudioEndpoint(
                (int)EDataFlow.eRender,
                (int)ERole.eMultimedia,
                out _device);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }

        // ── 2. Activate IAudioClient on the device ──
        Guid iidAudioClient = typeof(IAudioClient).GUID;
        hr = ((IMMDevice)Marshal.GetObjectForIUnknown(_device))
                .Activate(ref iidAudioClient, 1, IntPtr.Zero, out IntPtr acPtr);
        Marshal.ThrowExceptionForHR(hr);
        _audioClient = acPtr;

        var audioClient = (IAudioClient)Marshal.GetObjectForIUnknown(_audioClient);

        try
        {
            // ── 3. Check format support ──
            // Pin WAVEFORMATEX to unmanaged memory for IsFormatSupported
            IntPtr fmtPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WAVEFORMATEX>());
            try
            {
                Marshal.StructureToPtr(waveFormat, fmtPtr, fDeleteOld: false);

                IntPtr closestPtr;
                hr = audioClient.IsFormatSupported(
                    AUDCLNT_SHAREMODE_SHARED,
                    fmtPtr,
                    out closestPtr);

                if (hr < 0 && closestPtr != IntPtr.Zero)
                {
                    // Exact format not supported — use the closest match
                    WAVEFORMATEX closest = Marshal.PtrToStructure<WAVEFORMATEX>(closestPtr);
                    Marshal.FreeCoTaskMem(closestPtr);

                    _channels      = closest.nChannels;
                    _sampleRate    = closest.nSamplesPerSec;
                    _bitsPerSample = closest.wBitsPerSample;
                    _blockAlign    = closest.nBlockAlign;
                    waveFormat     = closest;

                    // Re-pin with the corrected format
                    Marshal.FreeCoTaskMem(fmtPtr);
                    fmtPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WAVEFORMATEX>());
                    Marshal.StructureToPtr(waveFormat, fmtPtr, fDeleteOld: false);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(fmtPtr);
            }

            // ── 4. Re-pin with final format for Initialize call ──
            IntPtr finalFmtPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WAVEFORMATEX>());
            try
            {
                Marshal.StructureToPtr(waveFormat, finalFmtPtr, fDeleteOld: false);

                // ── 5. Create a manual-reset event for the audio engine ──
                _eventHandle = NativeMethods.CreateEventW(
                    IntPtr.Zero,   // default security
                    true,          // manual reset
                    false,         // initial state = non-signaled
                    IntPtr.Zero);  // no name

                if (_eventHandle == IntPtr.Zero)
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

                // ── 6. Initialize the audio client (shared, event-driven) ──
                hr = audioClient.Initialize(
                    AUDCLNT_SHAREMODE_SHARED,
                    AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST,
                    DEFAULT_BUFFER_DURATION_HNS,  // hnsBufferDuration
                    0,                             // hnsPeriodicity (0 = let system choose)
                    finalFmtPtr,                   // pFormat
                    IntPtr.Zero);                  // pAudioSessionGuid
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(finalFmtPtr);
            }

            // ── 7. Get buffer size and acquire IAudioRenderClient ──
            audioClient.GetBufferSize(out uint bufferFrameCount);

            Guid renderIid = typeof(IAudioRenderClient).GUID;
            hr = audioClient.GetService(ref renderIid, out IntPtr rcPtr);
            Marshal.ThrowExceptionForHR(hr);
            _renderClient = rcPtr;

            // ── 8. Start playback ──
            audioClient.Start();
            _started = true;
        }
        finally
        {
            // We're done with the temporary IAudioClient reference.
            // The underlying COM object stays alive via _audioClient (IntPtr).
            Marshal.ReleaseComObject(audioClient);
        }
    }

    // ======================
    //  WRITING AUDIO DATA
    // ======================

    /// <summary>
    /// Queues PCM audio frames into the WASAPI render buffer.
    /// </summary>
    /// <param name="data">Raw PCM bytes matching the initialized format.</param>
    /// <param name="offset">Byte offset into data.</param>
    /// <param name="byteCount">Number of bytes to write.</param>
    /// <returns>Number of bytes actually queued (0 on error).</returns>
    public unsafe int Write(byte[] data, int offset, int byteCount)
    {
        if (_renderClient == IntPtr.Zero || !_started)
            return 0;

        // Convert byte count → frame count (round down)
        uint frameCount = (uint)(byteCount / _blockAlign);
        if (frameCount == 0)
            return 0;

        var renderClient = (IAudioRenderClient)Marshal.GetObjectForIUnknown(_renderClient);

        int hr = renderClient.GetBuffer(frameCount, out IntPtr dest);
        if (hr < 0)
        {
            // Underrun or device lost — skip this chunk silently
            Marshal.ReleaseComObject(renderClient);
            return 0;
        }

        try
        {
            // Copy PCM samples directly into the WASAPI buffer
            fixed (byte* src = &data[offset])
            {
                Buffer.MemoryCopy(src, (void*)dest, byteCount, byteCount);
            }

            int queuedBytes = (int)(frameCount * _blockAlign);
            return queuedBytes;
        }
        finally
        {
            renderClient.ReleaseBuffer(frameCount, dwFlags: 0);
            Marshal.ReleaseComObject(renderClient);
        }
    }

    // ======================
    //  PLAYBACK CONTROL
    // ======================

    public void Start()
    {
        if (_audioClient == IntPtr.Zero || _started) return;

        var audioClient = (IAudioClient)Marshal.GetObjectForIUnknown(_audioClient);
        try
        {
            audioClient.Start();
            _started = true;
        }
        finally
        {
            Marshal.ReleaseComObject(audioClient);
        }
    }

    public void Pause()
    {
        if (_audioClient == IntPtr.Zero || !_started) return;

        var audioClient = (IAudioClient)Marshal.GetObjectForIUnknown(_audioClient);
        try
        {
            audioClient.Stop();
            _started = false;
        }
        finally
        {
            Marshal.ReleaseComObject(audioClient);
        }
    }

    // ======================
    //  DISPOSE
    // ======================

    public void Dispose()
    {
        if (!_disposed)
        {
            // Stop audio client first
            if (_audioClient != IntPtr.Zero)
            {
                try
                {
                    var audioClient = (IAudioClient)Marshal.GetObjectForIUnknown(_audioClient);
                    try
                    {
                        audioClient.Stop();
                        audioClient.Reset();
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(audioClient);
                    }
                }
                catch { }
            }

            // Release render client
            if (_renderClient != IntPtr.Zero)
            {
                Marshal.Release(_renderClient);
                _renderClient = IntPtr.Zero;
            }

            // Close kernel event handle
            if (_eventHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_eventHandle);
                _eventHandle = IntPtr.Zero;
            }

            // Release device
            if (_device != IntPtr.Zero)
            {
                Marshal.Release(_device);
                _device = IntPtr.Zero;
            }

            // Release audio client
            if (_audioClient != IntPtr.Zero)
            {
                Marshal.Release(_audioClient);
                _audioClient = IntPtr.Zero;
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    ~AudioRenderer() => Dispose();
}