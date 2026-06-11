using System;
using Cine.Media.Implementations;

namespace Cine.Media.Implementations;

/// <summary>
/// Creates and manages an OpenGL ES context via ANGLE EGL.
/// mpv uses this context for rendering.
/// </summary>
public class AngleGlContext : IDisposable
{
    private IntPtr _eglDisplay = AngleInterop.EGL_NO_DISPLAY;
    private IntPtr _eglContext = AngleInterop.EGL_NO_CONTEXT;
    private IntPtr _eglSurface = AngleInterop.EGL_NO_SURFACE;
    private bool _disposed;

    /// <summary>
    /// Initialize EGL with ANGLE (no surface yet).
    /// </summary>
    public AngleGlContext()
    {
        // Get EGL display (EGL_DEFAULT_DISPLAY for ANGLE D3D11)
        _eglDisplay = AngleInterop.eglGetDisplay(AngleInterop.EGL_NO_DISPLAY);
        if (_eglDisplay == AngleInterop.EGL_NO_DISPLAY)
            throw new InvalidOperationException($"eglGetDisplay failed: error={AngleInterop.eglGetError()}");

        // Initialize EGL
        int major = 0, minor = 0;
        int result = AngleInterop.eglInitialize(_eglDisplay, out major, out minor);
        if (result == 0)
            throw new InvalidOperationException($"eglInitialize failed: error={AngleInterop.eglGetError()}");

        // Choose config for OpenGL ES 2
        var attribs = new[]
        {
            AngleInterop.EGL_SURFACE_TYPE, 0, // No surface needed for pbuffer-less context
            AngleInterop.EGL_RENDERABLE_TYPE, AngleInterop.EGL_OPENGL_ES2_BIT,
            AngleInterop.EGL_NONE
        };

        IntPtr[] configs = new IntPtr[1];
        int numConfig = 0;
        result = AngleInterop.eglChooseConfig(_eglDisplay, attribs, configs, 1, out numConfig);
        if (result == 0 || numConfig == 0)
            throw new InvalidOperationException($"eglChooseConfig failed: error={AngleInterop.eglGetError()}");

        var config = configs[0];

        // Create context with OpenGL ES 2
        var ctxAttribs = new[]
        {
            AngleInterop.EGL_CONTEXT_CLIENT_VERSION, 2,
            AngleInterop.EGL_NONE
        };

        _eglContext = AngleInterop.eglCreateContext(_eglDisplay, config, AngleInterop.EGL_NO_CONTEXT, ctxAttribs);
        if (_eglContext == AngleInterop.EGL_NO_CONTEXT)
            throw new InvalidOperationException($"eglCreateContext failed: error={AngleInterop.eglGetError()}");
    }

    /// <summary>
    /// Make the GL context current on the calling thread.
    /// </summary>
    public void MakeCurrent()
    {
        if (_eglDisplay == AngleInterop.EGL_NO_DISPLAY || _eglContext == AngleInterop.EGL_NO_CONTEXT)
            throw new ObjectDisposedException(nameof(AngleGlContext));

        // Can call with EGL_NO_SURFACE for pbuffer-less context
        int result = AngleInterop.eglMakeCurrent(_eglDisplay, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, _eglContext);
        if (result == 0)
            throw new InvalidOperationException($"eglMakeCurrent failed: error={AngleInterop.eglGetError()}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_eglDisplay != AngleInterop.EGL_NO_DISPLAY)
        {
            AngleInterop.eglMakeCurrent(_eglDisplay, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_SURFACE, AngleInterop.EGL_NO_CONTEXT);

            if (_eglContext != AngleInterop.EGL_NO_CONTEXT)
            {
                AngleInterop.eglDestroyContext(_eglDisplay, _eglContext);
                _eglContext = AngleInterop.EGL_NO_CONTEXT;
            }

            if (_eglSurface != AngleInterop.EGL_NO_SURFACE)
            {
                AngleInterop.eglDestroySurface(_eglDisplay, _eglSurface);
                _eglSurface = AngleInterop.EGL_NO_SURFACE;
            }

            AngleInterop.eglTerminate(_eglDisplay);
            _eglDisplay = AngleInterop.EGL_NO_DISPLAY;
        }
    }
}