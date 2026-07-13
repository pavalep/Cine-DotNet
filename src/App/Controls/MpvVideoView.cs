using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;
using Cine.Avalonia.Services;
using Cine.Media.Implementations;
using Cine.Media.Models;
using Image = Avalonia.Controls.Image;
using LayoutInformation = Avalonia.Layout.LayoutInformation;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Self-contained video renderer for the main window.
/// Creates its own ANGLE/OpenGL context, initializes mpv render API, runs a dedicated
/// render thread, and displays frames via a WriteableBitmap-backed Image.
/// 
/// This does NOT depend on Avalonia's OpenGlControlBase (which can fail silently in
/// Avalonia 12 when the control is occluded or compositor conditions aren't met).
/// The ANGLE context is completely under our control.
/// </summary>
public class MpvVideoView : Decorator
{
    private MpvPlayer? _player;
    private AngleGlContext? _angleContext;
    private Thread? _renderThread;
    private CancellationTokenSource? _renderCts;
    private volatile bool _frameReady;
    private bool _disposed;
    private readonly object _initLock = new();

    // Video dimensions — set from event-loop thread, read from render thread
    private volatile int _videoWidth;
    private volatile int _videoHeight;

    /// <summary>
    /// Fired on the UI thread when a new video frame is displayed.
    /// Allows PiP window and other subscribers to share the same frame data
    /// without needing a second mpv instance or ANGLE context.
    /// </summary>
    public event Action<byte[], int, int>? FrameRendered;

    // ── Corner radius for clipping video inside rounded corners ──
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MpvVideoView, CornerRadius>(nameof(CornerRadius), new CornerRadius(8));

    static MpvVideoView()
    {
        CornerRadiusProperty.Changed.AddClassHandler<MpvVideoView>((view, e) =>
        {
            view.InvalidateArrange();
        });
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    // Display
    private readonly Image _videoImage;
    private WriteableBitmap? _writeableBitmap;
    private DateTime _lastFrameTime = DateTime.MinValue;
    private static readonly TimeSpan MinFrameInterval = TimeSpan.FromMilliseconds(16.666); // ~60fps cap

    /// <summary>
    /// When false, the main window doesn't display video frames.
    /// Frames still fire <see cref="FrameRendered"/> so PiP continues to work.
    /// </summary>
    public bool DisplayEnabled { get; set; } = true;

    // Performance monitoring (Phase 2 premium)
    private PerformanceMonitor? _performanceMonitor;
    private RenderThrottleService? _renderThrottle;

    // Debug counters
    private long _frameCount;
    private long _renderCount;
    private long _displayCount;
    private DateTime _lastDebugLog = DateTime.MinValue;
    private static readonly TimeSpan DebugLogInterval = TimeSpan.FromSeconds(2);

    private static readonly string LogPath = CreateLogPath();
    private static string CreateLogPath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cine");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cine_mainwin_gl.log");
        }
        catch
        {
            // Fall back to temp path — debug logging is best-effort
            return Path.Combine(Path.GetTempPath(), "cine_mainwin_gl.log");
        }
    }
    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}"); }
        catch { /* best-effort debug logging — can't use Log.ForContext here (circular) */ }
    }

    public MpvVideoView()
    {
        _videoImage = new Image
        {
            Stretch = Stretch.Uniform,
            IsHitTestVisible = true,
            IsVisible = false              // Hide until first frame renders
        };
        Child = _videoImage;
    }

    /// <summary>
    /// Arrange override — applies a rounded-rect clip to the video Image
    /// so native ANGLE-rendered frames stay inside the rounded corners.
    /// </summary>
    protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (_videoImage != null && finalSize.Width > 0 && finalSize.Height > 0)
        {
            var r = CornerRadius;
            var w = finalSize.Width;
            var h = finalSize.Height;

            // Only apply clip if corner radius is actually rounded
            if (r.TopLeft > 0 || r.TopRight > 0 || r.BottomRight > 0 || r.BottomLeft > 0)
            {
                double tl = r.TopLeft, tr = r.TopRight, br = r.BottomRight, bl = r.BottomLeft;
                var geom = new StreamGeometry();
                using var ctx = geom.Open();
                ctx.BeginFigure(new global::Avalonia.Point(tl, 0), isFilled: true);
                ctx.LineTo(new global::Avalonia.Point(w - tr, 0));
                ctx.ArcTo(new global::Avalonia.Point(w, tr), new global::Avalonia.Size(tr, tr), 0, false, global::Avalonia.Media.SweepDirection.Clockwise);
                ctx.LineTo(new global::Avalonia.Point(w, h - br));
                ctx.ArcTo(new global::Avalonia.Point(w - br, h), new global::Avalonia.Size(br, br), 0, false, global::Avalonia.Media.SweepDirection.Clockwise);
                ctx.LineTo(new global::Avalonia.Point(bl, h));
                ctx.ArcTo(new global::Avalonia.Point(0, h - bl), new global::Avalonia.Size(bl, bl), 0, false, global::Avalonia.Media.SweepDirection.Clockwise);
                ctx.LineTo(new global::Avalonia.Point(0, tl));
                ctx.ArcTo(new global::Avalonia.Point(tl, 0), new global::Avalonia.Size(tl, tl), 0, false, global::Avalonia.Media.SweepDirection.Clockwise);
                ctx.EndFigure(isClosed: true);
                _videoImage.Clip = geom;
            }
            else
            {
                _videoImage.Clip = null;
            }
        }

        return size;
    }

    /// <summary>
    /// Initialize the render API and start rendering.
    /// Must be called once after the player is created and BEFORE opening any file.
    /// </summary>
    public void Initialize(MpvPlayer player)
    {
        if (_player != null || _disposed) return;

        lock (_initLock)
        {
            if (_player != null || _disposed) return;

            _player = player;
            Log("=== MainWin GL Init Start ===");

            // Wire player events for video-size detection
            _player.Opened += OnPlayerOpened;

            // 1. Create ANGLE context on this (UI) thread
            _angleContext = new AngleGlContext(1920, 1080);
            Log("ANGLE context created");

            // 2. Init render API — GL context must be current
            _angleContext.MakeCurrent();
            try
            {
                player.InitializeRenderApi(
                    name => AngleInterop.eglGetProcAddress(name),
                    () =>
                    {
                        // Called from mpv's internal thread when new frame ready
                        _frameReady = true;
                    });
                Log("Render API initialized");
            }
            finally
            {
                _angleContext.ReleaseCurrent();
            }

            // 3. Start dedicated render thread
            _renderCts = new CancellationTokenSource();
            _renderThread = new Thread(() => RenderLoop(_renderCts.Token))
            {
                Name = "MainWin-RenderGL",
                IsBackground = true
            };
            _renderThread.Start();
            Log("Render thread started");

            Log("=== MainWin GL Init Complete ===");
        }
    }

    /// <summary>
    /// Detach the player without disposing it.
    /// </summary>
    public void DetachPlayer()
    {
        _player = null;
    }

    /// <summary>
    /// Wire optional performance services (Phase 2 premium).
    /// Call after Initialize() and before opening media.
    /// </summary>
    public void SetPerformanceServices(
        PerformanceMonitor? monitor,
        RenderThrottleService? throttle)
    {
        _performanceMonitor = monitor;
        _renderThrottle = throttle;
        Log($"Performance services set: monitor={(monitor != null)} throttle={(throttle != null)}");
    }

    private void OnPlayerOpened(object? sender, EventArgs e)
    {
        if (_player == null) return;
        // Cache video dimensions from event-loop thread (safe — not render thread)
        _player.GetVideoSize(out int w, out int h);
        _videoWidth = w;
        _videoHeight = h;
        Log($"Video opened: {w}x{h}");
    }

    /// <summary>
    /// Render loop running on dedicated thread.
    /// Renders mpv frames into our ANGLE FBO, reads pixels back, delivers to UI.
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
            Log($"MakeCurrent failed on render thread: {ex}");
            return;
        }

        try
        {
            Log("RenderLoop started — waiting for frames...");
            while (!token.IsCancellationRequested && !_disposed)
            {
                if (_frameReady && _angleContext != null && _player != null)
                {
                    // Frame throttle check BEFORE consuming _frameReady
                    var now = DateTime.UtcNow;
                    if ((now - _lastFrameTime) < MinFrameInterval)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // Phase 2: RenderThrottleService check (60fps cap)
                    if (_renderThrottle != null && !_renderThrottle.ShouldRender())
                    {
                        // Throttled — consume the flag anyway so we don't
                        // infinitely re-process the same frame-ready signal.
                        _frameReady = false;
                        Thread.Sleep(1);
                        continue;
                    }

                    // Consume
                    _frameReady = false;
                    _lastFrameTime = now;
                    _frameCount++;

                    // Periodic debug: log stats every ~2 seconds
                    if ((now - _lastDebugLog) > DebugLogInterval)
                    {
                        _lastDebugLog = now;
                        var throttleSummary = _renderThrottle?.GetSummary() ?? "throttle=unused";
                        var perfSummary = _performanceMonitor?.GetSummary() ?? "perf=unused";
                        Log($"STATS: frames={_frameCount} renders={_renderCount} displays={_displayCount} vidSize={_videoWidth}x{_videoHeight} fbo={_angleContext?.Width}x{_angleContext?.Height} | {throttleSummary} | {perfSummary}");
                    }

                    try
                    {
                        // 1. Ensure FBO is sized correctly (creates on first call).
                        //    Always call when we have video dimensions — EnsureFboSize
                        //    has its own fast-path early return.
                        int vw = _videoWidth;
                        int vh = _videoHeight;
                        if (vw > 0 && vh > 0)
                        {
                            _angleContext!.EnsureFboSize(vw, vh);
                        }

                        // 2. Bind our FBO so mpv renders into it
                        _angleContext!.BindFbo();

                        // 3. Tell mpv to render into our FBO
                        _player!.RenderFrame(
                            _angleContext.FboHandle,
                            _angleContext.Width,
                            _angleContext.Height);
                        _renderCount++;

                        // 4. Re-bind FBO before readback (mpv may have unbound it)
                        _angleContext!.BindFbo();

                        // 5. Read rendered pixels
                        var pixels = _angleContext.ReadPixels(
                            _angleContext.Width,
                            _angleContext.Height);
                        int w = _angleContext.Width;
                        int h = _angleContext.Height;

                        Dispatcher.UIThread.Post(() =>
                        {
                            UpdateDisplay(pixels, w, h);
                            _displayCount++;
                        }, DispatcherPriority.Background);

                        // Phase 2: Notify performance monitor of a successful render
                        _performanceMonitor?.OnFrameRendered();
                    }
                    catch (Exception ex)
                    {
                        Log($"Render error: {ex}");
                        _frameReady = false;
                    }
                }
                else
                {
                    Thread.Sleep(4);
                    if (token.IsCancellationRequested) break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Render thread crashed: {ex}");
        }
        finally
        {
            _angleContext?.ReleaseCurrent();
            Log("Render thread exiting");
        }
    }

    /// <summary>
    /// Update the WriteableBitmap from UI thread. Called via Dispatcher.
    /// </summary>
    private void UpdateDisplay(byte[] pixels, int width, int height)
    {
        // When DisplayEnabled is false (PiP active), skip display rendering
        // but still fire FrameRendered so the PiP window gets frames.
        if (!DisplayEnabled)
        {
            _videoImage.IsVisible = false;
            FrameRendered?.Invoke(pixels, width, height);
            return;
        }

        try
        {
            _videoImage.IsVisible = true;
            bool isNew = _writeableBitmap == null || 
                         _writeableBitmap.PixelSize.Width != width || 
                         _writeableBitmap.PixelSize.Height != height;

            if (isNew)
            {
                _writeableBitmap?.Dispose();
                _writeableBitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);
                Log($"Bitmap created {width}x{height}");
            }

            using (var fb = _writeableBitmap!.Lock())
            {
                unsafe
                {
                    var byteCount = (uint)(width * height * 4);
                    fixed (byte* src = pixels)
                    {
                        Buffer.MemoryCopy(src, (void*)fb.Address, byteCount, byteCount);
                    }
                }
            }
            // Set Source only once, then invalidate the Image to refresh.
            if (isNew)
            {
                _videoImage.Source = _writeableBitmap;
                _videoImage.IsVisible = true;
                IsVisible = true;                       // Unhide the control itself
                Log($"Source set on Image {width}x{height}");
            }
            _videoImage.InvalidateVisual();

            // Share frame data with PiP and other subscribers
            FrameRendered?.Invoke(pixels, width, height);
        }
        catch (Exception ex)
        {
            Log($"UpdateDisplay error: {ex}");
        }
    }

    /// <summary>
    /// Stop rendering and dispose GL resources.
    /// </summary>
    public void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        Log("Shutdown...");

        try
        {
            _renderCts?.Cancel();
            _renderCts?.Dispose();
            _renderCts = null;
        }
        catch (Exception ex) { Log($"Shutdown: CTS cancel failed: {ex.Message}"); }

        if (_renderThread != null && _renderThread.IsAlive)
        {
            if (!_renderThread.Join(2000))
                Log("Render thread join timeout");
            _renderThread = null;
        }

        if (_player != null)
        {
            _player.Opened -= OnPlayerOpened;
            try { _player.DeinitializeRenderApi(); }
            catch { /* best-effort during shutdown */ }
            _player = null;
        }

        try { _angleContext?.Dispose(); }
        catch (Exception ex) { Log($"Shutdown: ANGLE context dispose failed: {ex.Message}"); }
        _angleContext = null;

        try { _writeableBitmap?.Dispose(); }
        catch (Exception ex) { Log($"Shutdown: WriteableBitmap dispose failed: {ex.Message}"); }
        _writeableBitmap = null;

        Log("Shutdown complete");
    }
}
