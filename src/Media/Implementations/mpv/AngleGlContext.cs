using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Simba.Media.Implementations;

/// <summary>
/// Creates and manages an OpenGL ES context via ANGLE EGL + an offscreen FBO for mpv rendering.
/// mpv renders into our FBO texture, then we read pixels back for Avalonia display.
/// 
/// Threading: ANGLE contexts are per-thread. MakeCurrent()/ReleaseCurrent() are NOT
/// thread-safe by themselves — caller must ensure serialization. The _fboLock protects
/// FBO lifecycle (EnsureFboSize / Dispose).
/// </summary>
public class AngleGlContext : IDisposable
{
    private IntPtr _eglDisplay = AngleInterop.EGL_NO_DISPLAY;
    private IntPtr _eglContext = AngleInterop.EGL_NO_CONTEXT;
    private IntPtr _eglConfig;
    private readonly object _fboLock = new();
    private bool _disposed;
    private int _width;
    private int _height;

    // GL constants used locally
    private const int GL_RGBA = 0x1908;
    private const int GL_UNSIGNED_BYTE = 0x1401;
    private const int GL_FRAMEBUFFER = 0x8D40;
    private const int GL_COLOR_ATTACHMENT0 = 0x8CE0;
    private const int GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
    private const int GL_TEXTURE_2D = 0x0DE1;
    private const int GL_TEXTURE_MAG_FILTER = 0x2800;
    private const int GL_TEXTURE_MIN_FILTER = 0x2801;
    private const int GL_LINEAR = 0x2601;
    private const int GL_CLAMP_TO_EDGE = 0x812F;
    private const int GL_TEXTURE_WRAP_S = 0x2802;
    private const int GL_TEXTURE_WRAP_T = 0x2803;
    private const int GL_RGBA8 = 0x8058;
    private const int GL_NO_ERROR = 0;

    // GL function delegates
    private d_glReadPixels? _glReadPixels;
    private d_glFinish? _glFinish;
    private d_glGenFramebuffers? _glGenFramebuffers;
    private d_glBindFramebuffer? _glBindFramebuffer;
    private d_glFramebufferTexture2D? _glFramebufferTexture2D;
    private d_glDeleteFramebuffers? _glDeleteFramebuffers;
    private d_glCheckFramebufferStatus? _glCheckFramebufferStatus;
    private d_glGenTextures? _glGenTextures;
    private d_glBindTexture? _glBindTexture;
    private d_glTexImage2D? _glTexImage2D;
    private d_glTexParameteri? _glTexParameteri;
    private d_glDeleteTextures? _glDeleteTextures;
    private d_glViewport? _glViewport;
    private d_glClear? _glClear;
    private d_glClearColor? _glClearColor;
    private d_glGetError? _glGetError;

    // FBO state
    private int _fbo = 0;
    private int _fboTexture = 0;

    // Delegates
    private delegate void d_glReadPixels(int x, int y, int width, int height, int format, int type, IntPtr pixels);
    private delegate void d_glFinish();
    private delegate void d_glGenFramebuffers(int n, [Out] int[] framebuffers);
    private delegate void d_glBindFramebuffer(int target, int framebuffer);
    private delegate void d_glFramebufferTexture2D(int target, int attachment, int textarget, int texture, int level);
    private delegate void d_glDeleteFramebuffers(int n, [In] ref int framebuffers);
    private delegate int d_glCheckFramebufferStatus(int target);
    private delegate void d_glGenTextures(int n, [Out] int[] textures);
    private delegate void d_glBindTexture(int target, int texture);
    private delegate void d_glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, IntPtr pixels);
    private delegate void d_glTexParameteri(int target, int pname, int param);
    private delegate void d_glDeleteTextures(int n, [In] ref int textures);
    private delegate void d_glViewport(int x, int y, int width, int height);
    private delegate void d_glClear(int mask);
    private delegate void d_glClearColor(float r, float g, float b, float a);
    private delegate int d_glGetError();

    public int Width => _width;
    public int Height => _height;
    public IntPtr Display => _eglDisplay;
    public IntPtr Context => _eglContext;
    /// <summary>mpv will render into this FBO. Pass it as fbo field in mpv_opengl_fbo.</summary>
    public int FboHandle => Volatile.Read(ref _fbo) > 0 ? Volatile.Read(ref _fbo) : 0;
    /// <summary>Internal format of the FBO color attachment texture (GL_RGBA8).</summary>
    public int InternalFormat => GL_RGBA8;
    /// <summary>Internal texture backing the FBO (for readback).</summary>
    public int FboTexture => Volatile.Read(ref _fboTexture);
    /// <summary>Whether the GL context is currently valid (no detected loss).</summary>
    public bool IsContextValid => !_disposed && _eglContext != AngleInterop.EGL_NO_CONTEXT;

    /// <summary>
    /// Initialize EGL with ANGLE and create an offscreen FBO.
    /// No PBuffer surface is created — we render exclusively to FBOs.
    /// Context is made current; caller should ReleaseCurrent() before handing
    /// off to the render thread.
    /// </summary>
    public AngleGlContext(int width = 1920, int height = 1080)
    {
        Log("=== ANGLE Init ===");
        _width = width;
        _height = height;

        _eglDisplay = AngleInterop.eglGetDisplay(IntPtr.Zero);
        if (_eglDisplay == AngleInterop.EGL_NO_DISPLAY)
            throw new InvalidOperationException($"eglGetDisplay failed: {AngleInterop.eglGetError()}");
        Log($"eglGetDisplay=0x{_eglDisplay:X}");

        if (AngleInterop.eglInitialize(_eglDisplay, out int maj, out int min) == 0)
            throw new InvalidOperationException($"eglInitialize failed: {AngleInterop.eglGetError()}");
        Log($"eglInitialize: v{maj}.{min}");

        if (AngleInterop.eglBindAPI(AngleInterop.EGL_OPENGL_ES_API) == 0)
            throw new InvalidOperationException($"eglBindAPI failed: {AngleInterop.eglGetError()}");

        _eglConfig = ChooseConfig();
        Log($"config=0x{_eglConfig:X}");

        _eglContext = CreateContext();
        if (_eglContext == AngleInterop.EGL_NO_CONTEXT)
            throw new InvalidOperationException($"eglCreateContext failed: {AngleInterop.eglGetError()}");
        Log($"context=0x{_eglContext:X}");

        // Make current WITHOUT a surface (FBO-only rendering works without a PBuffer)
        if (AngleInterop.eglMakeCurrent(_eglDisplay, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, _eglContext) == 0)
            throw new InvalidOperationException($"eglMakeCurrent(surfaceless) failed: {AngleInterop.eglGetError()}");
        Log("Context made current (surfaceless)");

        LoadGlFunctions();

        // FBO is NOT created here — it would be on the calling (UI) thread.
        // GL objects are per-context and contexts are per-thread.
        // The FBO will be created lazily on the render thread when EnsureFboSize
        // is first called. This avoids having invalid GL handles cross threads.
        Log("=== ANGLE Init Done ===");
    }

    private static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine("[AngleGlContext] " + msg);
        try
        {
            File.AppendAllText(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Simba", "simba_angle.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private IntPtr ChooseConfig()
    {
        const int max = 64;
        var cfgs = new IntPtr[max];
        int n = 0;
        // Request ES2+ renderable config — no PBUFFER_BIT needed since we're surfaceless
        var attrs = new[]
        {
            AngleInterop.EGL_RENDERABLE_TYPE, AngleInterop.EGL_OPENGL_ES2_BIT,
            AngleInterop.EGL_RED_SIZE, 8,
            AngleInterop.EGL_GREEN_SIZE, 8,
            AngleInterop.EGL_BLUE_SIZE, 8,
            AngleInterop.EGL_ALPHA_SIZE, 8,
            AngleInterop.EGL_NONE
        };
        int r = AngleInterop.eglChooseConfig(_eglDisplay, attrs, cfgs, max, out n);
        if (r != 0 && n > 0) return cfgs[0];

        // Fallback: minimal config
        attrs = new[]
        {
            AngleInterop.EGL_RENDERABLE_TYPE, AngleInterop.EGL_OPENGL_ES2_BIT,
            AngleInterop.EGL_NONE
        };
        r = AngleInterop.eglChooseConfig(_eglDisplay, attrs, cfgs, max, out n);
        if (r != 0 && n > 0) return cfgs[0];

        throw new InvalidOperationException($"eglChooseConfig failed: {AngleInterop.eglGetError()}");
    }

    private IntPtr CreateContext()
    {
        foreach (var ver in new[] { 3, 2 })
        {
            var ctx = AngleInterop.eglCreateContext(
                _eglDisplay, _eglConfig, AngleInterop.EGL_NO_CONTEXT,
                new[] { AngleInterop.EGL_CONTEXT_CLIENT_VERSION, ver, AngleInterop.EGL_NONE });
            if (ctx != AngleInterop.EGL_NO_CONTEXT)
            {
                Log($"Created ES {ver} context");
                return ctx;
            }
        }
        return AngleInterop.EGL_NO_CONTEXT;
    }

    /// <summary>
    /// Make the EGL context current on the calling thread. No surface needed (FBO-only).
    /// Throws if the context has been lost.
    /// </summary>
    public void MakeCurrent()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AngleGlContext));
        if (_eglContext == AngleInterop.EGL_NO_CONTEXT)
            throw new InvalidOperationException("No EGL context");

        var err = AngleInterop.eglMakeCurrent(
            _eglDisplay, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, _eglContext);
        if (err == 0)
        {
            var eglErr = AngleInterop.eglGetError();
            Log($"eglMakeCurrent FAILED: {eglErr} — context may be lost");
            throw new InvalidOperationException($"eglMakeCurrent failed (context lost?): {eglErr}");
        }
    }

    /// <summary>
    /// Release the EGL context from the calling thread.
    /// </summary>
    public void ReleaseCurrent()
    {
        AngleInterop.eglMakeCurrent(
            _eglDisplay, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_CONTEXT);
    }

    /// <summary>
    /// Bind our FBO in the current GL context. Must be called before mpv_render_context_render.
    /// </summary>
    public void BindFbo()
    {
        var fbo = Volatile.Read(ref _fbo);
        if (fbo > 0 && _glBindFramebuffer != null)
        {
            _glBindFramebuffer(GL_FRAMEBUFFER, fbo);
            _glViewport!(0, 0, _width, _height);
        }
    }

    /// <summary>
    /// Unbind our FBO (bind default framebuffer 0).
    /// Default FBO 0 with no surface is a no-op but safe to call.
    /// </summary>
    public void UnbindFbo()
    {
        _glBindFramebuffer?.Invoke(GL_FRAMEBUFFER, 0);
    }

    /// <summary>
    /// Create or resize the FBO to match the given dimensions.
    /// Thread-safe — can be called from any thread.
    /// Context MUST be made current by the caller before calling this.
    /// </summary>
    public void EnsureFboSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        lock (_fboLock)
        {
            if (_width == width && _height == height && _fbo != 0) return;

            if (_fbo == 0)
                Log($"Creating initial FBO {width}x{height} on thread {Environment.CurrentManagedThreadId}");
            else
                Log($"Resizing FBO: {_width}x{_height} -> {width}x{height}");
            if (_fbo != 0)
            {
                int fb = _fbo;
                _glDeleteFramebuffers?.Invoke(1, ref fb);
                int tx = _fboTexture;
                _glDeleteTextures?.Invoke(1, ref tx);
                _fbo = 0;
                _fboTexture = 0;
            }
            _width = width;
            _height = height;
            CreateFboInternal(width, height);
            Log($"Resized FBO to {width}x{height} (fbo={_fbo}, tex={_fboTexture})");
        }
    }

    private void CreateFboInternal(int w, int h)
    {
        // Generate texture
        var textures = new int[1];
        _glGenTextures?.Invoke(1, textures);
        _fboTexture = textures[0];
        GL_CHECK("glGenTextures");
        Log($"Tex gen: {_fboTexture}");

        _glBindTexture?.Invoke(GL_TEXTURE_2D, _fboTexture);
        GL_CHECK("glBindTexture");
        _glTexImage2D?.Invoke(GL_TEXTURE_2D, 0, GL_RGBA8, w, h, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
        GL_CHECK("glTexImage2D");
        _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
        _glBindTexture?.Invoke(GL_TEXTURE_2D, 0);

        // Generate FBO
        var fbos = new int[1];
        _glGenFramebuffers?.Invoke(1, fbos);
        _fbo = fbos[0];
        GL_CHECK("glGenFramebuffers");

        _glBindFramebuffer?.Invoke(GL_FRAMEBUFFER, _fbo);
        _glFramebufferTexture2D?.Invoke(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _fboTexture, 0);
        GL_CHECK("glFramebufferTexture2D");

        var status = _glCheckFramebufferStatus?.Invoke(GL_FRAMEBUFFER) ?? 0;
        if (status != GL_FRAMEBUFFER_COMPLETE)
            Log($"FBO incomplete! status=0x{status:X}");
        else
            Log($"FBO {_fbo} complete with texture {_fboTexture}");

        _glBindFramebuffer?.Invoke(GL_FRAMEBUFFER, 0);
        _glViewport?.Invoke(0, 0, w, h);
        _glClearColor?.Invoke(0, 0, 0, 1);
        _glClear?.Invoke(0x4000); // GL_COLOR_BUFFER_BIT
    }

    /// <summary>
    /// Read rendered frame pixels. Returns BGRA byte array.
    /// Context must be current.
    /// </summary>
    public byte[] ReadPixels(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            _glReadPixels?.Invoke(0, 0, width, height, GL_RGBA, GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
            _glFinish?.Invoke();
            // GL_RGBA → BGRA swap (Avalonia WriteableBitmap expects BGRA format)
            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
        finally { handle.Free(); }
        return pixels;
    }

    private void LoadGlFunctions()
    {
        _glReadPixels = Load<d_glReadPixels>("glReadPixels");
        _glFinish = Load<d_glFinish>("glFinish");
        _glGenFramebuffers = Load<d_glGenFramebuffers>("glGenFramebuffers")
            ?? Load<d_glGenFramebuffers>("glGenFramebuffersOES");
        _glBindFramebuffer = Load<d_glBindFramebuffer>("glBindFramebuffer")
            ?? Load<d_glBindFramebuffer>("glBindFramebufferOES");
        _glFramebufferTexture2D = Load<d_glFramebufferTexture2D>("glFramebufferTexture2D")
            ?? Load<d_glFramebufferTexture2D>("glFramebufferTexture2DOES");
        _glDeleteFramebuffers = Load<d_glDeleteFramebuffers>("glDeleteFramebuffers")
            ?? Load<d_glDeleteFramebuffers>("glDeleteFramebuffersOES");
        _glCheckFramebufferStatus = Load<d_glCheckFramebufferStatus>("glCheckFramebufferStatus")
            ?? Load<d_glCheckFramebufferStatus>("glCheckFramebufferStatusOES");
        _glGenTextures = Load<d_glGenTextures>("glGenTextures");
        _glBindTexture = Load<d_glBindTexture>("glBindTexture");
        _glTexImage2D = Load<d_glTexImage2D>("glTexImage2D");
        _glTexParameteri = Load<d_glTexParameteri>("glTexParameteri");
        _glDeleteTextures = Load<d_glDeleteTextures>("glDeleteTextures");
        _glViewport = Load<d_glViewport>("glViewport");
        _glClear = Load<d_glClear>("glClear");
        _glClearColor = Load<d_glClearColor>("glClearColor");
        _glGetError = Load<d_glGetError>("glGetError");

        Log("GL functions loaded");
    }

    /// <summary>
    /// Check for GL errors and log them. Only active when _glGetError is loaded successfully.
    /// </summary>
    private void GL_CHECK(string caller)
    {
        if (_glGetError == null) return;
        var err = _glGetError();
        if (err != GL_NO_ERROR)
            Log($"GL error after {caller}: 0x{err:X}");
    }

    private static T? Load<T>(string name) where T : class
    {
        var ptr = AngleInterop.eglGetProcAddress(name);
        if (ptr == IntPtr.Zero) return null;
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Log("Disposing...");

        var dpy = _eglDisplay;
        if (dpy != AngleInterop.EGL_NO_DISPLAY)
        {
            lock (_fboLock)
            {
                AngleInterop.eglMakeCurrent(dpy, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_CONTEXT);
                if (_fbo != 0) { try { int f = _fbo; _glDeleteFramebuffers?.Invoke(1, ref f); } catch { } _fbo = 0; }
                if (_fboTexture != 0) { try { int t = _fboTexture; _glDeleteTextures?.Invoke(1, ref t); } catch { } _fboTexture = 0; }
            }
            if (_eglContext != AngleInterop.EGL_NO_CONTEXT)
            {
                AngleInterop.eglDestroyContext(dpy, _eglContext);
                _eglContext = AngleInterop.EGL_NO_CONTEXT;
            }
            // Don't call eglTerminate — the EGL display is shared across all
            // ANGLE contexts in the process (eglGetDisplay returns the same handle).
            // Terminating it would destroy the main window's renderer when PiP exits.
            // The display is cleaned up by the OS on process termination.
            _eglDisplay = AngleInterop.EGL_NO_DISPLAY;
        }
        Log("Disposed");
    }
}
