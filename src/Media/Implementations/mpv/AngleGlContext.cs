using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;


/// <summary>
/// Creates and manages an OpenGL ES context via ANGLE EGL + an offscreen FBO for mpv rendering.
/// mpv renders into our FBO texture, then we read pixels back for Avalonia display.
/// </summary>
public class AngleGlContext : IDisposable
{
    private IntPtr _eglDisplay = AngleInterop.EGL_NO_DISPLAY;
    private IntPtr _eglContext = AngleInterop.EGL_NO_CONTEXT;
    private IntPtr _eglSurface = AngleInterop.EGL_NO_SURFACE;
    private IntPtr _eglConfig;
    private bool _disposed;
    private int _width;
    private int _height;

    // GL constants used locally
    private const int GL_RGBA = 0x1908;
    private const int GL_UNSIGNED_BYTE = 0x1401;
    private const int GL_FRAMEBUFFER = 0x8D40;
    private const int GL_TEXTURE_2D = 0x0DE1;
    private const int GL_COLOR_ATTACHMENT0 = 0x8CE0;
    private const int GL_TEXTURE_MAG_FILTER = 0x2800;
    private const int GL_TEXTURE_MIN_FILTER = 0x2801;
    private const int GL_LINEAR = 0x2601;
    private const int GL_CLAMP_TO_EDGE = 0x812F;
    private const int GL_TEXTURE_WRAP_S = 0x2802;
    private const int GL_TEXTURE_WRAP_T = 0x2803;
    private const int GL_RGBA8 = 0x8058;
    private const int GL_NONE = 0;

    // GL function delegates
    private d_glReadPixels? _glReadPixels;
    private d_glFlush? _glFlush;
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

    // FBO state
    private int _fbo = 0;
    private int _fboTexture = 0;

    // Delegates
    private delegate void d_glReadPixels(int x, int y, int width, int height, int format, int type, IntPtr pixels);
    private delegate void d_glFlush();
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

    public int Width => _width;
    public int Height => _height;
    public IntPtr Display => _eglDisplay;
    public IntPtr Surface => _eglSurface;
    public IntPtr Context => _eglContext;
    /// <summary>mpv will render into this FBO. Pass it as fbo field in mpv_opengl_fbo.</summary>
    public int FboHandle => _fbo > 0 ? _fbo : 0;
    /// <summary>Internal format of the FBO color attachment texture (GL_RGBA8).</summary>
    public int InternalFormat => GL_RGBA8;
    /// <summary>Internal texture backing the FBO (for readback).</summary>
    public int FboTexture => _fboTexture;

    /// <summary>
    /// Initialize EGL with ANGLE and create an offscreen FBO.
    /// Context is made current after creation; caller should ReleaseCurrent()
    /// before handing off to the render thread.
    /// </summary>
    public AngleGlContext(int width = 1920, int height = 1080)
    {
        Log("=== ANGLE Init ===");
        _width = width;
        _height = height;

        // Step 1-6: EGL setup
        _eglDisplay = AngleInterop.eglGetDisplay(IntPtr.Zero);
        if (_eglDisplay == AngleInterop.EGL_NO_DISPLAY) throw new InvalidOperationException($"eglGetDisplay failed: {AngleInterop.eglGetError()}");
        Log($"eglGetDisplay=0x{_eglDisplay:X}");

        if (AngleInterop.eglInitialize(_eglDisplay, out int maj, out int min) == 0)
            throw new InvalidOperationException($"eglInitialize failed: {AngleInterop.eglGetError()}");
        Log($"eglInitialize: v{maj}.{min}");

        if (AngleInterop.eglBindAPI(AngleInterop.EGL_OPENGL_ES_API) == 0)
            throw new InvalidOperationException($"eglBindAPI failed: {AngleInterop.eglGetError()}");

        _eglConfig = ChooseConfig();
        Log($"config=0x{_eglConfig:X}");

        _eglContext = CreateContext();
        if (_eglContext == AngleInterop.EGL_NO_CONTEXT) throw new InvalidOperationException($"eglCreateContext failed: {AngleInterop.eglGetError()}");
        Log($"context=0x{_eglContext:X}");

        _eglSurface = CreateSurface();
        Log($"surface=0x{_eglSurface:X}");

        // Step 7: Make current
        MakeCurrent();
        Log("Context made current");

        // Step 8: Load GL functions needed for FBO creation
        LoadGlFunctions();

        // Step 9: Create the FBO + texture
        CreateFboInternal(width, height);
        Log($"FBO created: fbo={_fbo}, texture={_fboTexture}");

        Log("=== ANGLE Init Done ===");
    }

    private static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine("[AngleGlContext] " + msg);
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cine", "cine_angle.log"), $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}"); } catch { }
    }

    private IntPtr ChooseConfig()
    {
        const int max = 64;
        var cfgs = new IntPtr[max];
        int n = 0;
        var attrs = new[] { AngleInterop.EGL_RENDERABLE_TYPE, AngleInterop.EGL_OPENGL_ES2_BIT, AngleInterop.EGL_SURFACE_TYPE, AngleInterop.EGL_PBUFFER_BIT, AngleInterop.EGL_RED_SIZE, 8, AngleInterop.EGL_GREEN_SIZE, 8, AngleInterop.EGL_BLUE_SIZE, 8, AngleInterop.EGL_ALPHA_SIZE, 8, AngleInterop.EGL_NONE };
        int r = AngleInterop.eglChooseConfig(_eglDisplay, attrs, cfgs, max, out n);
        if (r != 0 && n > 0) return cfgs[0];
        // Fallback: no color size
        attrs = new[] { AngleInterop.EGL_RENDERABLE_TYPE, AngleInterop.EGL_OPENGL_ES2_BIT, AngleInterop.EGL_SURFACE_TYPE, AngleInterop.EGL_PBUFFER_BIT, AngleInterop.EGL_NONE };
        r = AngleInterop.eglChooseConfig(_eglDisplay, attrs, cfgs, max, out n);
        if (r != 0 && n > 0) return cfgs[0];
        throw new InvalidOperationException($"eglChooseConfig failed: {AngleInterop.eglGetError()}");
    }

    private IntPtr CreateContext()
    {
        foreach (var ver in new[] { 3, 2 })
        {
            var ctx = AngleInterop.eglCreateContext(_eglDisplay, _eglConfig, AngleInterop.EGL_NO_CONTEXT, new[] { AngleInterop.EGL_CONTEXT_CLIENT_VERSION, ver, AngleInterop.EGL_NONE });
            if (ctx != AngleInterop.EGL_NO_CONTEXT) { Log($"Created ES {ver} context"); return ctx; }
        }
        return AngleInterop.EGL_NO_CONTEXT;
    }

    private IntPtr CreateSurface()
    {
        var s = AngleInterop.eglCreatePbufferSurface(_eglDisplay, _eglConfig, new[] { AngleInterop.EGL_WIDTH, 1, AngleInterop.EGL_HEIGHT, 1, AngleInterop.EGL_NONE });
        if (s != AngleInterop.EGL_NO_SURFACE) return s;
        Log($"Pbuffer failed: {AngleInterop.eglGetError()}, continuing without surface");
        return AngleInterop.EGL_NO_SURFACE;
    }

    public void MakeCurrent()
    {
        if (AngleInterop.eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext) == 0)
            throw new InvalidOperationException($"eglMakeCurrent failed: {AngleInterop.eglGetError()}");
    }

    public void ReleaseCurrent()
    {
        AngleInterop.eglMakeCurrent(_eglDisplay, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_CONTEXT);
    }

    /// <summary>
    /// Bind our FBO in the current GL context. Must be called before mpv_render_context_render.
    /// </summary>
    public void BindFbo()
    {
        if (_fbo > 0 && _glBindFramebuffer != null)
        {
            _glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
            _glViewport!(0, 0, _width, _height);
        }
    }

    /// <summary>
    /// Unbind our FBO (bind default). Call after ReadPixels.
    /// </summary>
    public void UnbindFbo()
    {
        if (_glBindFramebuffer != null)
            _glBindFramebuffer(GL_FRAMEBUFFER, 0);
    }

    /// <summary>
    /// Create or resize the FBO to match the given dimensions.
    /// Call this when the video size changes.
    /// </summary>
    public void EnsureFboSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (_width == width && _height == height && _fbo != 0) return;

        MakeCurrent();
        if (_fbo != 0)
        {
            int fb = _fbo; _glDeleteFramebuffers!(1, ref fb);
            int tx = _fboTexture; _glDeleteTextures!(1, ref tx);
        }
        _width = width;
        _height = height;
        CreateFboInternal(width, height);
        Log($"Resized FBO to {width}x{height} (fbo={_fbo}, tex={_fboTexture})");
    }

    private void CreateFboInternal(int w, int h)
    {
        // Generate texture
        var textures = new int[1];
        _glGenTextures!(1, textures);
        _fboTexture = textures[0];
        Log($"Tex gen: {_fboTexture}");
        _glBindTexture!(GL_TEXTURE_2D, _fboTexture);
        _glTexImage2D!(GL_TEXTURE_2D, 0, GL_RGBA8, w, h, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
        _glTexParameteri!(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        _glTexParameteri!(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        _glTexParameteri!(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        _glTexParameteri!(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
        _glBindTexture!(GL_TEXTURE_2D, 0);

        // Generate FBO
        var fbos = new int[1];
        _glGenFramebuffers!(1, fbos);
        _fbo = fbos[0];
        _glBindFramebuffer!(GL_FRAMEBUFFER, _fbo);
        _glFramebufferTexture2D!(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _fboTexture, 0);
        Log($"FBO {_fbo} bound with texture {_fboTexture}");

        _glBindFramebuffer!(GL_FRAMEBUFFER, 0);
        _glViewport!(0, 0, w, h);
        _glClearColor!(0, 0, 0, 1);
        _glClear!(0x4000); // GL_COLOR_BUFFER_BIT
        Log("FBO created, viewport set, cleared");
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
            _glReadPixels!(0, 0, width, height, GL_RGBA, GL_UNSIGNED_BYTE, handle.AddrOfPinnedObject());
            _glFinish?.Invoke();
            // RGBA -> BGRA swap
            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
        finally { handle.Free(); }
        return pixels;
    }

    private void LoadGlFunctions()
    {
        _glReadPixels = Load<d_glReadPixels>("glReadPixels");
        _glFlush = Load<d_glFlush>("glFlush");
        _glFinish = Load<d_glFinish>("glFinish");
        // In ES 3+ core profile, use non-OES versions first. OES suffixes are for ES 1.x/2.0.
        _glGenFramebuffers = Load<d_glGenFramebuffers>("glGenFramebuffers") ?? Load<d_glGenFramebuffers>("glGenFramebuffersOES");
        _glBindFramebuffer = Load<d_glBindFramebuffer>("glBindFramebuffer") ?? Load<d_glBindFramebuffer>("glBindFramebufferOES");
        _glFramebufferTexture2D = Load<d_glFramebufferTexture2D>("glFramebufferTexture2D") ?? Load<d_glFramebufferTexture2D>("glFramebufferTexture2DOES");
        _glDeleteFramebuffers = Load<d_glDeleteFramebuffers>("glDeleteFramebuffers") ?? Load<d_glDeleteFramebuffers>("glDeleteFramebuffersOES");
        _glCheckFramebufferStatus = Load<d_glCheckFramebufferStatus>("glCheckFramebufferStatus") ?? Load<d_glCheckFramebufferStatus>("glCheckFramebufferStatusOES");
        _glGenTextures = Load<d_glGenTextures>("glGenTextures");
        _glBindTexture = Load<d_glBindTexture>("glBindTexture");
        _glTexImage2D = Load<d_glTexImage2D>("glTexImage2D");
        _glTexParameteri = Load<d_glTexParameteri>("glTexParameteri");
        _glDeleteTextures = Load<d_glDeleteTextures>("glDeleteTextures");
        _glViewport = Load<d_glViewport>("glViewport");
        _glClear = Load<d_glClear>("glClear");
        _glClearColor = Load<d_glClearColor>("glClearColor");

        Log("GL functions loaded");
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
            AngleInterop.eglMakeCurrent(dpy, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_CONTEXT);
            if (_fbo != 0) { try { int f = _fbo; _glDeleteFramebuffers?.Invoke(1, ref f); } catch { } }
            if (_fboTexture != 0) { try { int t = _fboTexture; _glDeleteTextures?.Invoke(1, ref t); } catch { } }
            if (_eglContext != AngleInterop.EGL_NO_CONTEXT) { AngleInterop.eglDestroyContext(dpy, _eglContext); }
            if (_eglSurface != AngleInterop.EGL_NO_SURFACE) { AngleInterop.eglDestroySurface(dpy, _eglSurface); }
            AngleInterop.eglTerminate(dpy);
        }
        Log("Disposed");
    }
}
