using System;
using System.IO;
using System.Threading;
using Avalonia.Threading;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Manages the secondary MpvPlayer instance used for PiP (Picture-in-Picture).
    /// Creates an ANGLE GL context, initializes the mpv render API, and runs a
    /// dedicated render thread that reads frames back for display in the PipWindow.
    /// </summary>
    public class PipPlayerService : IDisposable
    {
        private MpvPlayer? _player;
    private AngleGlContext? _angleContext;
    private Thread? _renderThread;
    private CancellationTokenSource? _renderCts;
    private volatile bool _frameReady;
    private bool _disposed;
    private readonly object _initLock = new();

    // Video dimensions — set from event-loop thread, read from render thread.
    // Only mpv command/API calls (not render API) are allowed from non-render threads.
    private volatile int _videoWidth;
    private volatile int _videoHeight;

        /// <summary>The secondary PiP player instance, if initialized.</summary>
        public IMediaPlayer? Player => _player;

        /// <summary>Fired when a new video frame is available (BGRA byte array).</summary>
        public event Action<byte[], int, int>? FrameRendered;

        /// <summary>Fired when an error occurs on the secondary player.</summary>
        public event EventHandler<string>? Error;

        /// <summary>
        /// Creates the ANGLE GL context, initializes the mpv render API, and starts
        /// a dedicated render thread. Frames are delivered through <see cref="FrameRendered"/>.
        /// </summary>
        public bool Initialize()
        {
            if (_disposed)
            {
                Error?.Invoke(this, "PipPlayerService is disposed");
                return false;
            }

            if (_player != null)
                return true;

            lock (_initLock)
            {
                if (_player != null)
                    return true;

                try
                {
                    PipLog("=== PiP Initialize Start ===");

                    // Check ANGLE availability
                    if (!AngleInterop.IsAvailable)
                    {
                        PipLog("ANGLE/EGL not available — PiP cannot start");
                        Error?.Invoke(this, "ANGLE (OpenGL) libraries not found. PiP requires libEGL.dll/libGLESv2.dll.");
                        return false;
                    }

                    // 1. Create ANGLE context (default 1920x1080 — will resize on first frame)
                    _angleContext = new AngleGlContext(1920, 1080);
                    PipLog("ANGLE context created");

                    // 2. Create MpvPlayer
                    _player = new MpvPlayer();
                    _player.Error += OnSecondaryError;
                    _player.Mute(true);
                    PipLog("MpvPlayer created");

                    // 3. Initialize render API — ANGLE provides the GL function pointers
                    _angleContext.MakeCurrent();
                    try
                    {
                        _player.InitializeRenderApi(
                            name =>
                            {
                                var ptr = AngleInterop.eglGetProcAddress(name);
                                return ptr;
                            },
                            () =>
                            {
                                // Called from mpv's internal thread when a new frame is ready.
                                // Signal the render thread to wake up.
                                _frameReady = true;
                            });
                        PipLog("Render API initialized");
                    }
                    finally
                    {
                        _angleContext.ReleaseCurrent();
                    }

                    // 4. Start dedicated render thread
                    _renderCts = new CancellationTokenSource();
                    _renderThread = new Thread(() => RenderLoop(_renderCts.Token))
                    {
                        Name = "PiP-RenderGL",
                        IsBackground = true
                    };
                    _renderThread.Start();
                    PipLog("Render thread started");

                    PipLog("=== PiP Initialize Success ===");
                    return true;
                }
                catch (Exception ex)
                {
                    PipLog($"Initialize FAILED: {ex}");
                    Error?.Invoke(this, $"Failed to create secondary player: {ex.Message}");
                    Cleanup();
                    return false;
                }
            }
        }

        /// <summary>
        /// Dedicated render loop running on its own thread.
        /// Wakes up when mpv signals a new frame is ready.
        /// </summary>
        private void RenderLoop(CancellationToken token)
        {
            // ANGLE contexts are per-thread — must make current on THIS thread
            try
            {
                _angleContext?.MakeCurrent();
            }
            catch (Exception ex)
            {
                PipLog($"Render thread: MakeCurrent failed: {ex}");
                return;
            }

            try
            {
                while (!token.IsCancellationRequested && !_disposed)
                {
                    if (_frameReady && _angleContext != null && _player != null)
                    {
                        _frameReady = false;

                        try
                        {
                            // 1. Ensure FBO is sized correctly for PiP (creates on first call).
                            //    Always call when we have video dimensions.
                            int vw = _videoWidth;
                            int vh = _videoHeight;
                            if (vw > 0 && vh > 0)
                            {
                                int maxDim = 1280;
                                double scale = Math.Min(1.0, (double)maxDim / Math.Max(vw, vh));
                                int targetW = Math.Max(1, (int)(vw * scale));
                                int targetH = Math.Max(1, (int)(vh * scale));
                                _angleContext.EnsureFboSize(targetW, targetH);
                            }

                            // 2. Bind FBO before mpv renders
                            _angleContext.BindFbo();

                            // 3. mpv renders into FBO
                            _player.RenderFrame(
                                _angleContext.FboHandle,
                                _angleContext.Width,
                                _angleContext.Height);

                            // 4. Re-bind before readback (mpv may have unbound)
                            _angleContext.BindFbo();

                            // 5. Read rendered pixels
                            var pixels = _angleContext.ReadPixels(
                                _angleContext.Width,
                                _angleContext.Height);

                            int w = _angleContext.Width;
                            int h = _angleContext.Height;

                            // Deliver to UI thread (background priority — don't block UI)
                            Dispatcher.UIThread.Post(() =>
                            {
                                FrameRendered?.Invoke(pixels, w, h);
                            }, DispatcherPriority.Background);
                        }
                        catch (Exception ex)
                        {
                            PipLog($"Render error: {ex}");
                            _frameReady = false;
                        }
                    }
                    else
                    {
                        Thread.Sleep(8); // ~120Hz ceiling, practical ~60fps with actual frames
                        if (token.IsCancellationRequested) break;
                    }
                }
            }
            catch (Exception ex)
            {
                PipLog($"Render thread crashed: {ex}");
            }
            finally
            {
                _angleContext?.ReleaseCurrent();
            }
        }

        /// <summary>
        /// Opens a file in the secondary player (must be called after <see cref="Initialize"/>).
        /// Captures the video dimensions for FBO sizing on the render thread.
        /// </summary>
        public void Open(string path)
        {
            if (_player == null) return;
            _player.Open(path);
            // Cache video dimensions from the event-loop thread (safe: not the render thread)
            _player.GetVideoSize(out int w, out int h);
            _videoWidth = w;
            _videoHeight = h;
        }

        /// <summary>
        /// Seeks the secondary player to the specified position.
        /// </summary>
        public void Seek(TimeSpan position)
        {
            _player?.Seek(position);
        }

        /// <summary>
        /// Sets the secondary player's mute state (should always be muted for PiP).
        /// </summary>
        public void SetMuted(bool muted)
        {
            _player?.Mute(muted);
        }

        /// <summary>
        /// Stops and disposes the secondary player and render thread.
        /// Safe to call multiple times.
        /// </summary>
        public void Stop()
        {
            Cleanup();
        }

        private void OnSecondaryError(object? sender, string message)
        {
            Error?.Invoke(this, message);
        }

        private void Cleanup()
        {
            lock (_initLock)
            {
                // Stop render thread first
                try
                {
                    _renderCts?.Cancel();
                    _renderCts?.Dispose();
                    _renderCts = null;
                }
                catch { }

                if (_renderThread != null && _renderThread.IsAlive)
                {
                    if (!_renderThread.Join(2000))
                        PipLog("Render thread did not stop in 2s");
                    _renderThread = null;
                }

                // Deinit render API
                if (_player != null)
                {
                    _player.Error -= OnSecondaryError;
                    try { _player.DeinitializeRenderApi(); } catch { }
                    _player.Dispose();
                    _player = null;
                }

                // Dispose ANGLE context
                try { _angleContext?.Dispose(); } catch { }
                _angleContext = null;

                PipLog("Cleanup complete");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Cleanup();
        }

        private static void PipLog(string msg)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Cine", "cine_pip_video.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
